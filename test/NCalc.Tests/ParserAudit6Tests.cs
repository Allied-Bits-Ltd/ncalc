using System.Diagnostics;
using System.Text.Json;

using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Factories;

using NCalcVector = NCalc.Domain.Vector;

namespace NCalc.Tests;

/// <summary>
/// Regression tests of the parser items left open by the earlier audits (round 6).
/// </summary>
[Trait("Category", "Parser")]
public class ParserAudit6Tests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private const AdvExpressionOptions ComplexVectorsPercent = AdvExpressionOptions.ParseComplexNumbers | AdvExpressionOptions.ParseVectors | AdvExpressionOptions.CalculatePercent;

    // Evaluates the expression with the four evaluators (sync and async, recursive and non-recursive) and checks that they agree
    private static async Task<object?> EvaluateAllAsync(string text, ExpressionOptions options, CultureInfo? culture = null, AdvancedExpressionOptions? advancedOptions = null)
    {
        culture ??= Invariant;
        object? first = null;
        bool hasFirst = false;
        foreach (var evaluatorOption in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var effectiveOptions = options | ExpressionOptions.NoCache | evaluatorOption;
            var syncResult = new Expression(text, effectiveOptions, culture) { AdvancedOptions = advancedOptions }.Evaluate(TestContext.Current.CancellationToken);
            var asyncResult = await new AsyncExpression(text, effectiveOptions, culture) { AdvancedOptions = advancedOptions }.EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(syncResult, asyncResult);
            if (!hasFirst)
            {
                first = syncResult;
                hasFirst = true;
            }
            else
            {
                Assert.Equal(first, syncResult);
            }
        }

        return first;
    }

    // Runs the action with the given current culture (BigDecimal of ExtendedNumerics formats and parses with the current culture)
    private static T WithCurrentCulture<T>(string cultureName, Func<T> action)
    {
        var saved = CultureInfo.CurrentCulture;
        var savedUI = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
            CultureInfo.CurrentUICulture = savedUI;
        }
    }

    #region Currency separators (NCALC-422 edge)

    private static AdvancedExpressionOptions CurrencyOptions(string currencyDecimalSeparator, string currencyGroupSeparator) => new(Invariant)
    {
        Flags = AdvExpressionOptions.AcceptCurrencySymbol,
        CurrencySymbolsType = AdvancedExpressionOptions.CurrencySymbolType.Custom,
        CurrencySymbol = "$",
        NumberGroupSeparatorType = AdvancedExpressionOptions.GroupSeparatorType.Custom,
        NumberGroupSeparator = "'",
        CurrencyDecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
        CurrencyDecimalSeparator = currencyDecimalSeparator,
        CurrencyNumberGroupSeparatorType = AdvancedExpressionOptions.GroupSeparatorType.Custom,
        CurrencyNumberGroupSeparator = currencyGroupSeparator,
    };

    // A currency group separator equal to the currency decimal separator was accepted silently ("$0.5" was ambiguous)
    [Theory]
    [InlineData(".", "$0.5")]
    [InlineData(",", "$0,5")]
    public void CurrencyGroupSeparatorEqualToTheCurrencyDecimalSeparatorShouldBeRejected(string separator, string text)
    {
        var expression = new Expression(text, ExpressionOptions.NoCache, Invariant) { AdvancedOptions = CurrencyOptions(separator, separator) };
        var exception = Assert.Throws<NCalcParserException>(() => expression.Evaluate(TestContext.Current.CancellationToken));
        Assert.Contains("currency group separator", exception.Message);

        // The culture's own currency separators are taken the same way
        var fromCulture = new AdvancedExpressionOptions(Invariant)
        {
            Flags = AdvExpressionOptions.AcceptCurrencySymbol,
            CurrencySymbolsType = AdvancedExpressionOptions.CurrencySymbolType.Custom,
            CurrencySymbol = "$",
            NumberGroupSeparatorType = AdvancedExpressionOptions.GroupSeparatorType.BuiltIn,
            CurrencyNumberGroupSeparatorType = AdvancedExpressionOptions.GroupSeparatorType.BuiltIn,
            CurrencyDecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
            CurrencyDecimalSeparator = ",",
        };
        Assert.Throws<NCalcParserException>(() => new Expression("$0,5", ExpressionOptions.NoCache, Invariant) { AdvancedOptions = fromCulture }.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DifferentCurrencySeparatorsShouldStillWork()
    {
        Assert.Equal(1000.5m, await EvaluateAllAsync("$1.000,5", ExpressionOptions.None, Invariant, CurrencyOptions(",", ".")));
        Assert.Equal(1000.5m, await EvaluateAllAsync("$1,000.5", ExpressionOptions.None, Invariant, CurrencyOptions(".", ",")));
        Assert.Equal(0.5m, await EvaluateAllAsync("$0.5", ExpressionOptions.None, Invariant, CurrencyOptions(".", ",")));
        Assert.Equal(1000.5, await EvaluateAllAsync("1'000.5", ExpressionOptions.None, Invariant, CurrencyOptions(".", ",")));
    }

    #endregion

    #region 12-hour time literals

    // A time-only 12-hour literal was a parse error ("Invalid TimeSpan format"); it is the time span of the 24-hour literal
    [Theory]
    [InlineData("en-US", "#10:30 PM#", "22:30:00")]
    [InlineData("en-US", "#10:30 pm#", "22:30:00")]
    [InlineData("en-US", "#10:30 P#", "22:30:00")]
    [InlineData("en-US", "#10:30PM#", "22:30:00")]
    [InlineData("en-US", "#10:30:15 AM#", "10:30:15")]
    [InlineData("en-US", "#10:30:15 PM#", "22:30:15")]
    [InlineData("en-US", "#12:00 AM#", "00:00:00")]
    [InlineData("en-US", "#12:00 PM#", "12:00:00")]
    [InlineData("en-US", "#12:30:45 AM#", "00:30:45")]
    [InlineData("en-US", "#1:05 a#", "01:05:00")]
    [InlineData("en-AU", "#10:30 pm#", "22:30:00")]
    [InlineData("en-AU", "#9:15:30 am#", "09:15:30")]
    [InlineData("hi-IN", "#10:30 PM#", "22:30:00")]
    public async Task TimeOnlyTwelveHourLiteralsShouldBeTimeSpans(string cultureName, string text, string expected)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        var result = await EvaluateAllAsync(text, ExpressionOptions.None, culture);
        Assert.Equal(TimeSpan.Parse(expected, Invariant), Assert.IsType<TimeSpan>(result));

        // The same value as the 24-hour literal, also in arithmetic and comparisons
        Assert.Equal(true, await EvaluateAllAsync($"{text} == #{expected}#", ExpressionOptions.None, culture));
        Assert.Equal(TimeSpan.Parse(expected, Invariant) + TimeSpan.FromHours(1), await EvaluateAllAsync($"{text} + #01:00:00#", ExpressionOptions.None, culture));

        // With advanced options taken from the culture (the custom time separator path)
        foreach (var hoursFormat in new[] { AdvancedExpressionOptions.HoursFormatKind.FromCulture, AdvancedExpressionOptions.HoursFormatKind.Always12Hour })
        {
            var advancedOptions = new AdvancedExpressionOptions(culture) { HoursFormat = hoursFormat };
            Assert.Equal(TimeSpan.Parse(expected, Invariant), await EvaluateAllAsync(text, ExpressionOptions.None, culture, advancedOptions));
        }

        // ToString writes the time span in the 24-hour form, which is read back
        var parsed = LogicalExpressionFactory.Create(text, null, culture, ExpressionOptions.NoCache, null, TestContext.Current.CancellationToken);
        Assert.Equal(result, new Expression(parsed.ToString(), ExpressionOptions.NoCache, culture).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("#13:30 PM#")]
    [InlineData("#10:60 PM#")]
    [InlineData("#10:30:60 AM#")]
    public void InvalidTwelveHourLiteralsShouldBeRejected(string text)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        Assert.ThrowsAny<NCalcException>(() => new Expression(text, ExpressionOptions.NoCache, culture).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DateAndTimeTwelveHourLiteralsShouldStillWork()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal(new DateTime(2024, 1, 2, 22, 30, 0), await EvaluateAllAsync("#1/2/2024 10:30 PM#", ExpressionOptions.None, culture));
        Assert.Equal(new DateTime(2024, 1, 2, 10, 30, 15), await EvaluateAllAsync("#1/2/2024 10:30:15 AM#", ExpressionOptions.None, culture));
        Assert.Equal(new DateTime(2024, 1, 2, 0, 5, 0), await EvaluateAllAsync("#1/2/2024 12:05 AM#", ExpressionOptions.None, culture));
        Assert.Equal(new TimeSpan(22, 30, 0), await EvaluateAllAsync("#22:30#", ExpressionOptions.None, culture));
    }

    #endregion

    #region Culture-independent formatting of complex numbers, vectors and BigDecimals

    public static TheoryData<string> Cultures => new() { "sk-SK", "de-DE", "fa-IR", "sv-SE" };

    // ComplexNumber, Vector, Percent and BigDecimal values were formatted by ExtendedNumerics, whose BigDecimal.ToString uses the
    // culture current at its first use; and a BigDecimal made from a double was formatted with the current culture and parsed again,
    // which failed with fa-IR (and other cultures with another minus sign or separator), also when the expression was parsed
    [Theory]
    [MemberData(nameof(Cultures))]
    public void ValuesShouldBeFormattedWithoutTheCurrentCulture(string cultureName)
    {
        WithCurrentCulture(cultureName, () =>
        {
            Assert.Equal("-0.2 - 0.4i", new ComplexNumber(new BigDecimal(-2, -1), new BigDecimal(-4, -1)).ToString());
            Assert.Equal("1.5 + 0.25i", ComplexNumber.FromComplex(new System.Numerics.Complex(1.5, 0.25)).ToString());
            Assert.Equal("-0.000000000000000000000000000002 - 4000000000000000000000000000000i",
                new ComplexNumber(new BigDecimal(-2, -30), new BigDecimal(-4, 30)).ToString());
            Assert.Equal("[-1.5, 2.25]", new NCalcVector(new BigDecimal[] { new(-15, -1), new(225, -2) }).ToString());

            Assert.Equal("-0.0000000000000000000000000000025", new ValueExpression(new BigDecimal(-25, -31)).ToString());
            Assert.Equal("-12500.0", new ValueExpression(new BigDecimal(-125, 2)).ToString());

            var advancedOptions = new AdvancedExpressionOptions(Invariant, ComplexVectorsPercent);
            Assert.Equal("-0.2 - 0.4i", Evaluate("-0.2 - 0.4i", ExpressionOptions.None, advancedOptions)?.ToString());
            Assert.Equal("-0.2 - 0.4i", Evaluate("-0.2 - 0.4i", ExpressionOptions.UseBigNumbers, advancedOptions)?.ToString());
            Assert.Equal("[-3, 5]", Evaluate("[-1.5, 2.5] * 2", ExpressionOptions.None, advancedOptions)?.ToString());
            Assert.Equal("-12.5%", Evaluate("-12.5%", ExpressionOptions.None, advancedOptions)?.ToString());
            Assert.Equal("-1.5 + (0.2 - 0.4i) * [-1.5; 2.5] + 3.5%",
                LogicalExpressionFactory.Create("-1.5 + (0.2 - 0.4i) * [-1.5, 2.5] + 3.5%", null, Invariant, ExpressionOptions.NoCache | ExpressionOptions.UseBigNumbers, advancedOptions, TestContext.Current.CancellationToken).ToString());
            return 0;
        });
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void BigNumbersShouldParseAndEvaluateWithoutTheCurrentCulture(string cultureName)
    {
        const ExpressionOptions Big = ExpressionOptions.UseBigNumbers;
        var expected = WithCurrentCulture("en-US", () => Snapshot());
        var actual = WithCurrentCulture(cultureName, () => Snapshot());
        Assert.Equal(expected, actual);

        static string[] Snapshot()
        {
            string Run(Func<object?> f)
            {
                try
                {
                    var value = f();
                    return value switch
                    {
                        BigDecimal bd => $"BigDecimal {bd.Mantissa.ToString(Invariant)}E{bd.Exponent.ToString(Invariant)}",
                        IFormattable formattable => $"{value.GetType().Name} {formattable.ToString(null, Invariant)}",
                        _ => $"{value?.GetType().Name} {value}",
                    };
                }
                catch (Exception ex)
                {
                    return ex.GetType().Name + ": " + ex.Message;
                }
            }

            return
            [
                Run(() => LogicalExpressionFactory.Create("-1.5e40 + -2.5e-30", null, Invariant, ExpressionOptions.NoCache | Big).ToString()),
                Run(() => Evaluate("-1.5e40 / 3", Big, null)),
                Run(() => Evaluate("-1.5e40 / 7", Big, null)),
                Run(() => Evaluate("-2.5e-30 * 4", Big, null)),
                Run(() => Evaluate("0.1 + 0.2", Big, null)),
                Run(() => Evaluate("-0.1 - 0.2", Big | ExpressionOptions.DecimalAsDefault, null)),
                Run(() => Evaluate("1e400 / 1e399 - 0.5", Big, null)),
                Run(() => Evaluate("Sqrt(-2.25)", Big, null)),
                Run(() => Evaluate("x * 1.5", Big, null, ("x", -2.5))),
                Run(() => Evaluate("x * 1.5", Big, null, ("x", -2.5f))),
                Run(() => Evaluate("x * 2", Big, null, ("x", 3.5e300))),
                Run(() => Evaluate("x == y", Big, null, ("x", -2.5), ("y", -2.5m))),
                Run(() => JsonSerializer.Serialize(LogicalExpressionFactory.Create("-1.5e40 + 2.5", null, Invariant, ExpressionOptions.NoCache | Big))),
            ];
        }
    }

    private static object? Evaluate(string text, ExpressionOptions options, AdvancedExpressionOptions? advancedOptions, params (string Name, object Value)[] parameters)
    {
        var expression = new Expression(text, options | ExpressionOptions.NoCache, Invariant) { AdvancedOptions = advancedOptions };
        foreach (var (name, value) in parameters)
            expression.Parameters[name] = value;
        return expression.Evaluate(TestContext.Current.CancellationToken);
    }

    #endregion

    #region ToString and JSON round trips (NCALC-060 leftovers)

    public static TheoryData<string> RoundTripCultures => new() { "en-US", "sk-SK", "de-DE" };

    // The tree written by ToString() evaluates to the same value, and the tree read back from JSON is written the same way and
    // evaluates to the same value: statement sequences with a trailing separator, bracketed names, the kinds of strings and their
    // escapes, GUIDs, big numbers, function declarations, and complex, vector and percent values
    [Theory]
    [MemberData(nameof(RoundTripCultures))]
    public void ToStringAndJsonShouldRoundTripLiteralsAndDeclarations(string cultureName)
    {
        const ExpressionOptions Statements = ExpressionOptions.UseStatementSequences | ExpressionOptions.UseAssignments;
        var complex = () => new AdvancedExpressionOptions(Invariant, ComplexVectorsPercent);
        var rows = new (string Text, ExpressionOptions Options, Func<AdvancedExpressionOptions?> AdvancedOptions)[]
        {
            ("x := 1; x + 1;", Statements, () => null),
            ("[my value] * 2", ExpressionOptions.None, () => null),
            ("[my value] + [x] + x", ExpressionOptions.None, () => null),
            ("'it\\'s' + \"a \\\"b\\\"\" + 'tab\\t'", ExpressionOptions.None, () => null),
            ("@\"c:\\temp\" + `back`", ExpressionOptions.None, () => null),
            ("'\\u0041\\n'", ExpressionOptions.None, () => null),
            ("Guid('2E6E0E3A-AB4C-4B7D-9B67-0A0C6A2F8B11') == 2E6E0E3A-AB4C-4B7D-9B67-0A0C6A2F8B11", ExpressionOptions.None, () => null),
            ("1.5e400 + 2.5 - 0.1", ExpressionOptions.UseBigNumbers, () => null),
            ("123456789012345678901234567890.123456789 * 3", ExpressionOptions.UseBigNumbers, () => null),
            ("-2.5e-400 * 4", ExpressionOptions.UseBigNumbers, () => null),
            ("fn sq(a, b = 2) => a * a + b; sq(3)", Statements, () => null),
            ("fn twice(a) { a * 2 }; twice(5);", Statements, () => null),
            ("(1.5 - 2.5i) * 2i + 3i", ExpressionOptions.None, complex),
            ("(1.5 - 2.5i) * (0.5 + 1i)", ExpressionOptions.UseBigNumbers, complex),
            ("[1, 2.5] + [3, -4.25]", ExpressionOptions.None, complex),
            ("[1, 2.5] * 2.5", ExpressionOptions.None, complex),
            ("200 + 10%", ExpressionOptions.None, complex),
            ("12.5% * 3", ExpressionOptions.None, complex),
            ("(-0.2 + 0.4i) * [1, 2]", ExpressionOptions.None, complex),
        };

        string Evaluate(Expression expression)
        {
            try
            {
                var result = expression.Evaluate(TestContext.Current.CancellationToken);
                return result is System.Collections.IEnumerable items and not string
                    ? string.Join("; ", items.Cast<object?>().Select(item => $"{Convert.ToString(item, Invariant)} {item?.GetType().Name}"))
                    : $"{(result is IFormattable f ? f.ToString(null, Invariant) : Convert.ToString(result, Invariant))} {result?.GetType().Name}";
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }

        var parameters = new Dictionary<string, object?> { ["my value"] = 21, ["x"] = 2 };
        var failures = WithCurrentCulture(cultureName, () =>
        {
            var failures = new List<string>();
            foreach (var (text, options, advancedOptions) in rows)
            {
                Expression Create(string source) => new(source, options | ExpressionOptions.NoCache, Invariant) { AdvancedOptions = advancedOptions(), Parameters = new Dictionary<string, object?>(parameters) };
                var original = Create(text);
                var tree = original.GetLogicalExpression(TestContext.Current.CancellationToken);
                if (tree is null)
                {
                    failures.Add($"{text}: does not parse ({original.Error?.Message})");
                    continue;
                }

                string expected = Evaluate(original);
                string written = tree.ToString();
                var reparsed = Create(written);
                if (reparsed.HasErrors(TestContext.Current.CancellationToken))
                    failures.Add($"{text} -> {written}: does not parse");
                else
                if (Evaluate(reparsed) != expected)
                    failures.Add($"{text} = {expected}, {written} = {Evaluate(reparsed)}");

                LogicalExpression restored;
                try
                {
                    restored = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(tree))!;
                }
                catch (Exception ex)
                {
                    failures.Add($"{text}: JSON {ex.GetType().Name}: {ex.Message}");
                    continue;
                }

                if (restored.ToString() != written)
                    failures.Add($"{text}: JSON -> {restored}, ToString -> {written}");
                var fromJson = new Expression(restored, options | ExpressionOptions.NoCache, Invariant) { AdvancedOptions = advancedOptions(), Parameters = new Dictionary<string, object?>(parameters) };
                if (Evaluate(fromJson) != expected)
                    failures.Add($"{text} = {expected}, from JSON = {Evaluate(fromJson)}");
            }
            return failures;
        });

        Assert.Empty(failures);
    }

    // A function declaration is written with its name and parameters, and a sequence keeps its trailing separator in JSON
    [Fact]
    public void DeclarationsAndSequencesShouldKeepTheirDetails()
    {
        const ExpressionOptions Statements = ExpressionOptions.UseStatementSequences | ExpressionOptions.UseAssignments;
        var declaration = LogicalExpressionFactory.Create("fn sq(a, b = 2) => a * a + b", null, Invariant, Statements | ExpressionOptions.NoCache, null, TestContext.Current.CancellationToken);
        Assert.StartsWith("fn sq(a; b = 2) => ", declaration.ToString(), StringComparison.Ordinal);

        var sequence = LogicalExpressionFactory.Create("x := 1; x + 1;", null, Invariant, Statements | ExpressionOptions.NoCache, null, TestContext.Current.CancellationToken);
        var restored = Assert.IsType<StatementSequence>(JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(sequence)));
        Assert.True(restored.EndsWithSeparator);
        Assert.EndsWith(";", restored.ToString(), StringComparison.Ordinal);

        var bracketed = LogicalExpressionFactory.Create("[my value] + 1", null, Invariant, ExpressionOptions.NoCache, null, TestContext.Current.CancellationToken);
        var restoredBracketed = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(bracketed))!;
        Assert.Equal(bracketed.ToString(), restoredBracketed.ToString());
        Assert.Contains("[my value]", restoredBracketed.ToString(), StringComparison.Ordinal);
    }

    // ComplexNumber and Vector values (results kept in parameters or set as the value of a node) are kept in JSON exactly
    [Theory]
    [MemberData(nameof(Cultures))]
    public void ComplexAndVectorValuesShouldRoundTripThroughJson(string cultureName)
    {
        WithCurrentCulture(cultureName, () =>
        {
            var complex = new ComplexNumber(new BigDecimal(-2, -1), new BigDecimal(123456789, -40));
            var vector = new NCalcVector(new BigDecimal[] { new(-15, -1), new(1, 400) });
            foreach (object value in new object[] { complex, vector, new BigDecimal(-25, -31) })
            {
                var node = new ValueExpression(1) { Value = value };
                var restored = Assert.IsType<ValueExpression>(JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize<LogicalExpression>(node)));
                Assert.Equal(value, restored.Value);
                Assert.Equal(value.ToString(), restored.Value!.ToString());
            }
            return 0;
        });
    }

    #endregion
}

[CollectionDefinition(nameof(ParserTimingCollection), DisableParallelization = true)]
public sealed class ParserTimingCollection;

/// <summary>
/// The parser tests that measure time. They run alone (not in parallel with other tests), and each one compares times measured in
/// the same run, interleaved and taking the shortest of several runs, so that the load of the machine does not decide the result.
/// </summary>
[Trait("Category", "Parser")]
[Collection(nameof(ParserTimingCollection))]
public class ParserTimingTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // The shortest of a few interleaved runs of each action; the actions are run in turn, so that a change of the load of the machine
    // affects all of them alike
    private static TimeSpan[] MeasureInterleaved(int runs, params Action[] actions)
    {
        var best = Enumerable.Repeat(TimeSpan.MaxValue, actions.Length).ToArray();
        for (int run = 0; run < runs; run++)
        {
            for (int i = 0; i < actions.Length; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                actions[i]();
                stopwatch.Stop();
                if (stopwatch.Elapsed < best[i])
                    best[i] = stopwatch.Elapsed;
            }
        }
        return best;
    }

    // A huge exponent is kept as an exponent (1.0e9999999 took more than a minute when its digits were written out). The literals are
    // compared with literals of a small exponent: a kept exponent costs about the same, digits written out cost thousands of times more
    [Theory]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.UseBigNumbers | ExpressionOptions.DecimalAsDefault)]
    public void HugeExponentLiteralsShouldNotBeExpanded(ExpressionOptions options)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        object? Evaluate(string text) => new Expression(text, options | ExpressionOptions.NoCache, Invariant).Evaluate(cancellationToken);

        // The value keeps the exponent: its mantissa is not a number with thousands of digits. Beyond MathHelper.MaxPowResultDigits
        // digits, the literal is a double (infinity)
        var huge = Assert.IsType<BigDecimal>(Evaluate("1.0e99999"));
        Assert.True(huge.Mantissa < 100 && huge.Exponent >= 99998, $"{huge.Mantissa}E{huge.Exponent}");
        Assert.Equal(double.PositiveInfinity, Evaluate("1.0e9999999"));

        string[] hugeTexts = ["1e9999999", "1.0e9999999", "1e999999999 > 1"];
        string[] smallTexts = ["1e500", "1.0e500", "1e500 > 1"];
        var times = MeasureInterleaved(3,
            () => { foreach (var text in smallTexts) Assert.NotNull(Evaluate(text)); },
            () => { foreach (var text in hugeTexts) Assert.NotNull(Evaluate(text)); });

        Assert.True(times[1] < TimeSpan.FromTicks(times[0].Ticks * 100) + TimeSpan.FromMilliseconds(500),
            $"Small exponents took {times[0]}, huge exponents took {times[1]}");
    }

    // The prefix, the opening part repeated at each nesting level, the innermost operand, the closing part and the nesting depth
    public static TheoryData<string, string, string, string, int> NestedExpressionsWithAssignments => new()
    {
        { "", "(", "84", ")", 1000 },
        { "", "Abs(", "84", ")", 200 },
        { "", "a[", "0", "]", 200 },
        { "b = ", "Abs(", "84", ")", 200 },
    };

    // NCALC-135: the times of depth n and 2n are compared after a warm-up, interleaved and taking the shortest of several runs, so that
    // neither the first parse with the options (JIT, building the parser) nor the load of the machine decides the result
    [Theory]
    [MemberData(nameof(NestedExpressionsWithAssignments))]
    public void NestedExpressionsWithAssignmentsShouldParseInLinearTime(string prefix, string open, string operand, string close, int depth)
    {
        // The assignment target used to be parsed again as an ordinary operand at every nesting level, which took exponential time.
        // A large stack keeps the parser's depth guard out of the way, so only the parsing time is measured.
        var cancellationToken = TestContext.Current.CancellationToken;
        const ExpressionOptions options = ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.NoCache;

        string Build(int n) => prefix + string.Concat(Enumerable.Repeat(open, n)) + operand + string.Concat(Enumerable.Repeat(close, n));

        Action Parse(string expression) => () =>
        {
            Expression e = null!;
            bool hasErrors = true;
            var thread = new Thread(() =>
            {
                e = new Expression(expression, options);
                hasErrors = e.HasErrors(cancellationToken);
            }, 64 * 1024 * 1024);
            thread.Start();
            thread.Join();
            Assert.False(hasErrors, e.Error?.InnerException?.Message ?? e.Error?.Message);
        };

        // Warm-up: builds the parser for these options and compiles the code paths
        MeasureInterleaved(1, Parse(Build(10)), Parse(Build(depth)));

        var times = MeasureInterleaved(5, Parse(Build(depth)), Parse(Build(2 * depth)));

        // Linear parsing doubles the time; exponential parsing multiplied it by far more. The bound is generous on purpose
        Assert.True(times[1] < TimeSpan.FromTicks(times[0].Ticks * 8) + TimeSpan.FromMilliseconds(100),
            $"Parsing depth {depth} took {times[0]}, depth {2 * depth} took {times[1]}");
    }
}

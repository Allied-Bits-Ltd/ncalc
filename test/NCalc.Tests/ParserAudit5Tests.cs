using System.Collections;
using System.Diagnostics;
using System.Text.Json;

using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Factories;

namespace NCalc.Tests;

/// <summary>
/// Regression tests of the parser findings of the fifth audit.
/// </summary>
[Trait("Category", "Parser")]
public class ParserAudit5Tests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // Evaluates the expression with the four evaluators (sync and async, recursive and non-recursive) and checks that they agree
    private static async Task<object?> EvaluateAllAsync(string text, ExpressionOptions options, CultureInfo? culture = null,
        AdvancedExpressionOptions? advancedOptions = null, IDictionary<string, object?>? parameters = null)
    {
        culture ??= Invariant;
        object? first = null;
        bool hasFirst = false;
        foreach (var evaluatorOption in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var effectiveOptions = options | ExpressionOptions.NoCache | evaluatorOption;
            var expression = new Expression(text, effectiveOptions, culture) { AdvancedOptions = advancedOptions };
            var asyncExpression = new AsyncExpression(text, effectiveOptions, culture) { AdvancedOptions = advancedOptions };
            if (parameters is not null)
            {
                foreach (var pair in parameters)
                {
                    expression.Parameters[pair.Key] = pair.Value;
                    asyncExpression.Parameters[pair.Key] = pair.Value;
                }
            }

            var syncResult = expression.Evaluate(TestContext.Current.CancellationToken);
            var asyncResult = await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken);
            AssertSame(syncResult, asyncResult);
            if (!hasFirst)
            {
                first = syncResult;
                hasFirst = true;
            }
            else
            {
                AssertSame(first, syncResult);
            }
        }

        return first;
    }

    private static void AssertSame(object? expected, object? actual)
    {
        if (expected is IList expectedList && actual is IList actualList)
        {
            Assert.Equal(expectedList.Count, actualList.Count);
            for (int i = 0; i < expectedList.Count; i++)
                AssertSame(expectedList[i], actualList[i]);
            return;
        }

        Assert.Equal(expected?.GetType(), actual?.GetType());
        Assert.Equal(expected, actual);
    }

    private static LogicalExpression Parse(string text, ExpressionOptions options, CultureInfo? culture = null, AdvancedExpressionOptions? advancedOptions = null)
        => LogicalExpressionFactory.Create(text, null, culture ?? Invariant, options | ExpressionOptions.NoCache, advancedOptions, TestContext.Current.CancellationToken);

    #region NCALC-404

    // NCALC-404: with DecimalAsDefault, a literal below the resolution of decimal became 0 or lost its significant digits;
    // it is a double now, as a literal beyond the range of decimal is
    [Theory]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    [InlineData(ExpressionOptions.DecimalAsDefault | ExpressionOptions.UseBigNumbers)]
    public async Task DecimalAsDefaultLiteralsBelowTheDecimalResolutionShouldKeepTheirDigits(ExpressionOptions options)
    {
        Assert.Equal(true, await EvaluateAllAsync("1e-30 > 0", options));
        Assert.Equal(false, await EvaluateAllAsync("1e-30 = 0", options));
        Assert.Equal(1e-30, await EvaluateAllAsync("1e-30", options));
        Assert.Equal(1.234e-27, await EvaluateAllAsync("1.234e-27", options));
        Assert.Equal(1.5e-29, await EvaluateAllAsync("1.5e-29", options));
        Assert.Equal(1.23456e-28, await EvaluateAllAsync("0.000000000000000000000000000123456", options));
        Assert.Equal(1.0, await EvaluateAllAsync("1e-30 * 1e30", options));
        Assert.Equal(1.5, (double)(await EvaluateAllAsync("1.5e-29 * 1e29", options))!, 12);

        // Literals that decimal holds exactly stay decimal
        Assert.Equal(0.1m, await EvaluateAllAsync("0.1", options));
        Assert.Equal(1e-28m, await EvaluateAllAsync("1e-28", options));
        Assert.Equal(0.0000000000000000000000000001m, await EvaluateAllAsync("0.0000000000000000000000000001", options));
        Assert.Equal(1.5e-10m, await EvaluateAllAsync("1.5e-10", options));
        Assert.Equal(123456789.123456789m, await EvaluateAllAsync("123456789.123456789", options));
    }

    // NCALC-404: a literal with more digits than decimal holds, but not below its resolution, keeps the 28 digits of decimal
    [Fact]
    public async Task DecimalAsDefaultLiteralsWithManyDigitsShouldStayDecimal()
    {
        var result = await EvaluateAllAsync("1.00000000000000000000000000001", ExpressionOptions.DecimalAsDefault);
        Assert.IsType<decimal>(result);
        Assert.Equal(1m, result);

        // About 16 or more significant digits above the resolution are kept as decimal
        Assert.IsType<decimal>(await EvaluateAllAsync("0.0000000000001234567890123456789", ExpressionOptions.DecimalAsDefault));
    }

    #endregion

    #region NCALC-405

    // NCALC-405: with UseBigNumbers, an exponent literal without a decimal separator was a double (1e400 was infinity)
    [Theory]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.UseBigNumbers | ExpressionOptions.DecimalAsDefault)]
    public async Task BigNumberExponentLiteralsWithoutAFractionShouldBeBigDecimals(ExpressionOptions options)
    {
        Assert.Equal(BigDecimal.Parse("1E+400", Invariant), await EvaluateAllAsync("1e400", options));
        Assert.Equal(BigDecimal.Parse("1E+1000", Invariant), await EvaluateAllAsync("1E+1000", options));
        Assert.Equal(true, await EvaluateAllAsync("1e400 > 1e399", options));
        Assert.Equal(0, MathHelperCompare(10, await EvaluateAllAsync("1E+1000 / 1E+999", options)));
        Assert.Equal(await EvaluateAllAsync("1.0e400", options), await EvaluateAllAsync("1e400", options));

        // Literals that a double or decimal holds keep their type
        Assert.Equal(options.HasFlag(ExpressionOptions.DecimalAsDefault) ? 100000m : 100000.0, await EvaluateAllAsync("1e5", options));
        Assert.Equal(options.HasFlag(ExpressionOptions.DecimalAsDefault) ? 1.5m : 1.5, await EvaluateAllAsync("1.5", options));
        Assert.Equal(12, await EvaluateAllAsync("12", options));

        // A huge exponent is kept as an exponent (1.0e9999999 took more than a minute) and an exponent beyond int is infinity
        var stopwatch = Stopwatch.StartNew();
        foreach (var text in new[] { "1e9999999", "1.0e9999999", "1e999999999 > 1" })
            Assert.NotNull(await EvaluateAllAsync(text, options));
        Assert.True(stopwatch.ElapsedMilliseconds < 10_000, $"{stopwatch.ElapsedMilliseconds} ms");
        Assert.Equal(double.PositiveInfinity, await EvaluateAllAsync("1e99999999999999999999", options));
    }

    private static int MathHelperCompare(object a, object? b)
        => NCalc.Helpers.MathHelper.Compare(a, b, default, default);

    #endregion

    #region NCALC-407

    private static AdvancedExpressionOptions CommaDecimalOptions(CultureInfo culture)
        => new(culture) { DecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.FromCulture };

    // NCALC-407: with a comma decimal separator and a comma list separator, "N, M" made N a double, or with UseBigNumbers the number N.M
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    public async Task CommaFollowedByWhitespaceShouldSeparateItems(ExpressionOptions options)
    {
        var sk = CultureInfo.GetCultureInfo("sk-SK");
        var advancedOptions = CommaDecimalOptions(sk);

        var list = Assert.IsAssignableFrom<IList>(await EvaluateAllAsync("(1, 2)", options, sk, advancedOptions));
        Assert.Equal(2, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal(2, list[1]);

        Assert.Equal(9007199254740993L, await EvaluateAllAsync("Max(9007199254740993, 1)", options, sk, advancedOptions));
        Assert.Equal(9007199254740993L, await EvaluateAllAsync("Max(9007199254740993 , 1)", options, sk, advancedOptions));
        Assert.Equal(9007199254740993L, await EvaluateAllAsync("Max(9007199254740993,\t1)", options, sk, advancedOptions));
        Assert.Equal(2, await EvaluateAllAsync("Max(1 ,2)", options, sk, advancedOptions));

        // A comma followed by a digit is still the decimal separator
        object oneAndAHalf = options.HasFlag(ExpressionOptions.DecimalAsDefault) ? 1.5m : 1.5;
        Assert.Equal(oneAndAHalf, await EvaluateAllAsync("1,5", options, sk, advancedOptions));
        Assert.Equal(options.HasFlag(ExpressionOptions.DecimalAsDefault) ? 2.5m : 2.5, await EvaluateAllAsync("Max(1,5; 2,5)", options, sk, advancedOptions));

        // The parsed expression is written and read back with the same values
        foreach (var text in new[] { "Max(9007199254740993, 1)", "(1, 2)", "1,5 + 2", "Max(1,5; 2,5)" })
        {
            var tree = Parse(text, options, sk, advancedOptions);
            var expected = await EvaluateAllAsync(text, options, sk, advancedOptions);
            Assert.Equal(expected, await EvaluateAllAsync(tree.ToString(), options, sk, advancedOptions), new ListComparer());

            var deserialized = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(tree));
            var fromJson = new Expression(deserialized!, options | ExpressionOptions.NoCache, sk) { AdvancedOptions = advancedOptions };
            Assert.Equal(expected, fromJson.Evaluate(TestContext.Current.CancellationToken), new ListComparer());
        }
    }

    // NCALC-407: a number does not contain whitespace; with UseBigNumbers "1 .5" and "1. 5" were the number 1.5
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.UseBigNumbers | ExpressionOptions.DecimalAsDefault)]
    public void NumbersShouldNotContainWhitespace(ExpressionOptions options)
    {
        foreach (var text in new[] { "1 .5", "1. 5", "1.5 e3", "1.5e 3" })
            Assert.True(new Expression(text, options | ExpressionOptions.NoCache, Invariant).HasErrors(TestContext.Current.CancellationToken), text);

        var sk = CultureInfo.GetCultureInfo("sk-SK");
        Assert.IsAssignableFrom<IList>(new Expression("(1, 2)", options | ExpressionOptions.NoCache, sk) { AdvancedOptions = CommaDecimalOptions(sk) }.Evaluate(TestContext.Current.CancellationToken));
    }

    private sealed class ListComparer : IEqualityComparer<object?>
    {
        public new bool Equals(object? x, object? y)
        {
            if (x is IList xl && y is IList yl)
                return xl.Count == yl.Count && Enumerable.Range(0, xl.Count).All(i => Equals(xl[i], yl[i]));
            return object.Equals(x, y) && x?.GetType() == y?.GetType();
        }

        public int GetHashCode(object? obj) => 0;
    }

    #endregion

    #region NCALC-415

    // NCALC-415: a custom time separator '-' or '+' made the last part a UTC offset
    [Theory]
    [InlineData("-")]
    [InlineData("+")]
    [InlineData("_")]
    [InlineData(".")]
    [InlineData("h")]
    public async Task CustomTimeSeparatorsShouldNotBeReadAsUtcOffsets(string separator)
    {
        var advancedOptions = new AdvancedExpressionOptions(Invariant)
        {
            TimeSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
            TimeSeparator = separator,
        };

        foreach (var seconds in new[] { "00", "05", "15" })
        {
            var dateTime = Assert.IsType<DateTime>(await EvaluateAllAsync($"#2024/01/02 10{separator}30{separator}{seconds}#", ExpressionOptions.None, Invariant, advancedOptions));
            Assert.Equal(new DateTime(2024, 1, 2, 10, 30, int.Parse(seconds, Invariant)), dateTime);
            Assert.Equal(DateTimeKind.Unspecified, dateTime.Kind);

            Assert.Equal(new TimeSpan(10, 30, int.Parse(seconds, Invariant)), await EvaluateAllAsync($"#10{separator}30{separator}{seconds}#", ExpressionOptions.None, Invariant, advancedOptions));
        }

        var shortDateTime = Assert.IsType<DateTime>(await EvaluateAllAsync($"#2024/01/02 10{separator}05#", ExpressionOptions.None, Invariant, advancedOptions));
        Assert.Equal(new DateTime(2024, 1, 2, 10, 5, 0), shortDateTime);
        Assert.Equal(DateTimeKind.Unspecified, shortDateTime.Kind);
    }

    #endregion

    #region NCALC-422

    // NCALC-422: a group separator equal to the decimal separator was accepted, and 0.5 was read as 5
    [Fact]
    public void GroupSeparatorEqualToTheDecimalSeparatorShouldBeRejected()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var options = new[]
        {
            new AdvancedExpressionOptions(de) { NumberGroupSeparatorType = AdvancedExpressionOptions.GroupSeparatorType.FromCulture },
            new AdvancedExpressionOptions(Invariant) { NumberGroupSeparatorType = AdvancedExpressionOptions.GroupSeparatorType.Custom, NumberGroupSeparator = "." },
            new AdvancedExpressionOptions(de) { DecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.FromCulture, NumberGroupSeparatorType = AdvancedExpressionOptions.GroupSeparatorType.BuiltIn },
        };

        foreach (var advancedOptions in options)
        {
            var exception = Assert.Throws<NCalcParserException>(() =>
                new Expression("0.5", ExpressionOptions.NoCache, de) { AdvancedOptions = advancedOptions }.Evaluate(TestContext.Current.CancellationToken));
            Assert.Contains("separator", exception.Message);
        }

        // Different separators still work
        var valid = new AdvancedExpressionOptions(de)
        {
            DecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.FromCulture,
            NumberGroupSeparatorType = AdvancedExpressionOptions.GroupSeparatorType.FromCulture,
        };
        Assert.Equal(1000.5, new Expression("1.000,5", ExpressionOptions.NoCache, de) { AdvancedOptions = valid }.Evaluate(TestContext.Current.CancellationToken));
    }

    #endregion

    #region NCALC-423

    // NCALC-423: with AcceptUnderscoresInNumbers, "_1" and "__2" were numbers and "1_" was 1; an underscore separates digits only
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    public async Task UnderscoresShouldOnlySeparateDigits(ExpressionOptions options)
    {
        var advancedOptions = new AdvancedExpressionOptions(Invariant) { Flags = AdvExpressionOptions.AcceptUnderscoresInNumbers };
        var parameters = new Dictionary<string, object?> { ["_1"] = 100, ["__2"] = 500 };

        Assert.Equal(100, await EvaluateAllAsync("_1", options, Invariant, advancedOptions, parameters));
        Assert.Equal(500, await EvaluateAllAsync("__2", options, Invariant, advancedOptions, parameters));
        Assert.Equal(0, MathHelperCompare(200, await EvaluateAllAsync("2 * _1", options, Invariant, advancedOptions, parameters)));
        Assert.Equal(["_1"], new Expression("_1", ExpressionOptions.NoCache, Invariant) { AdvancedOptions = advancedOptions }.GetParameterNames(TestContext.Current.CancellationToken));

        Assert.Equal(1000, await EvaluateAllAsync("1_000", options, Invariant, advancedOptions));
        Assert.Equal(1000, await EvaluateAllAsync("1__000", options, Invariant, advancedOptions));
        Assert.Equal(0, MathHelperCompare(1000.25, await EvaluateAllAsync("1_000.2_5", options, Invariant, advancedOptions)));
        Assert.Equal(0, MathHelperCompare(1e10, await EvaluateAllAsync("1e1_0", options, Invariant, advancedOptions)));

        foreach (var text in new[] { "1_", "1_ + 1", "1_.5", "1._5", "1.5_", "1e_5", "1_e5" })
        {
            var expression = new Expression(text, options | ExpressionOptions.NoCache, Invariant) { AdvancedOptions = advancedOptions };
            Assert.True(expression.HasErrors(TestContext.Current.CancellationToken), text);
        }
    }

    #endregion

    #region NCALC-425

    // NCALC-425: with AllowCharValues, a character written with an escape sequence stayed a string
    [Fact]
    public async Task EscapedOneCharacterLiteralsShouldBeChars()
    {
        const ExpressionOptions options = ExpressionOptions.AllowCharValues;
        // The escape sequence of 'A' (backslash, u, 0041), and of 'b', built so that no tool turns it into the character
        string escapedA = "'" + '\\' + "u0041'";
        string escapedAb = "'a" + '\\' + "u0062'";
        Assert.Equal('A', await EvaluateAllAsync("'A'", options));
        Assert.Equal('A', await EvaluateAllAsync(escapedA, options));
        Assert.Equal('\n', await EvaluateAllAsync(@"'\n'", options));
        Assert.Equal('\'', await EvaluateAllAsync(@"'\''", options));
        Assert.Equal('\\', await EvaluateAllAsync(@"'\\'", options));
        Assert.Equal(66, await EvaluateAllAsync(escapedA + " + 1", options));
        Assert.Equal("ab", await EvaluateAllAsync(escapedAb, options));

        // Without AllowCharValues they are strings
        Assert.Equal("A", await EvaluateAllAsync(escapedA, ExpressionOptions.None));

        // The char is written so that it is read back as the same char
        foreach (var text in new[] { @"'\n'", @"'\''", @"'\\'", escapedA })
        {
            var tree = Parse(text, options);
            Assert.Equal(await EvaluateAllAsync(text, options), await EvaluateAllAsync(tree.ToString(), options));
        }
    }

    #endregion

    #region NCALC-431

    // NCALC-431: with the custom date separator '-' in the DMY or MDY order, date-only literals were rejected as invalid ISO dates
    [Theory]
    [InlineData(AdvancedExpressionOptions.DateOrderKind.DMY, 2024, 2, 1)]
    [InlineData(AdvancedExpressionOptions.DateOrderKind.MDY, 2024, 1, 2)]
    public async Task CustomHyphenDateSeparatorShouldReadDateOnlyLiterals(AdvancedExpressionOptions.DateOrderKind order, int year, int month, int day)
    {
        var advancedOptions = new AdvancedExpressionOptions(Invariant)
        {
            DateSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
            DateSeparator = "-",
            DateOrder = order,
        };

        Assert.Equal(new DateTime(year, month, day), await EvaluateAllAsync("#01-02-2024#", ExpressionOptions.None, Invariant, advancedOptions));
        Assert.Equal(new DateTime(year, month, day, 10, 30, 0), await EvaluateAllAsync("#01-02-2024 10:30:00#", ExpressionOptions.None, Invariant, advancedOptions));

        // ISO dates are still read
        Assert.Equal(new DateTime(2024, 2, 1), await EvaluateAllAsync("#2024-02-01#", ExpressionOptions.None, Invariant, advancedOptions));
    }

    // NCALC-431: an invalid ISO date is still an error
    [Fact]
    public void InvalidIsoDatesShouldStillBeRejected()
    {
        Assert.True(new Expression("#2024-13-45#", ExpressionOptions.NoCache, Invariant).HasErrors(TestContext.Current.CancellationToken));
        Assert.True(new Expression("#01-02-2024#", ExpressionOptions.NoCache, Invariant).HasErrors(TestContext.Current.CancellationToken));
    }

    // NCALC-431 (review): with the custom date separator '-', a date is read only in the date order of the options, as with '/'
    [Theory]
    [InlineData("/")]
    [InlineData("-")]
    public void CustomDateSeparatorShouldFollowTheDateOrder(string separator)
    {
        AdvancedExpressionOptions Options(AdvancedExpressionOptions.DateOrderKind order) => new(Invariant)
        {
            DateSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
            DateSeparator = separator,
            DateOrder = order,
        };
        bool HasErrors(string text, AdvancedExpressionOptions options)
            => new Expression(text, ExpressionOptions.NoCache, Invariant) { AdvancedOptions = options }.HasErrors(TestContext.Current.CancellationToken);
        object? Evaluate(string text, AdvancedExpressionOptions options)
            => new Expression(text, ExpressionOptions.NoCache, Invariant) { AdvancedOptions = options }.Evaluate(TestContext.Current.CancellationToken);

        var dmy = Options(AdvancedExpressionOptions.DateOrderKind.DMY);
        Assert.True(HasErrors($"#12{separator}31{separator}2024#", dmy));
        Assert.True(HasErrors($"#10{separator}30{separator}05#", dmy));
        Assert.Equal(new DateTime(2024, 12, 31), Evaluate($"#31{separator}12{separator}2024#", dmy));

        var ymd = Options(AdvancedExpressionOptions.DateOrderKind.YMD);
        Assert.Equal(new DateTime(2024, 1, 2), Evaluate($"#2024{separator}01{separator}02#", ymd));
        // As '/', '-' accepts the lenient forms of DateTime.TryParse in the order of the options' format (no longer the culture's order)
        var slashYmd = new AdvancedExpressionOptions(Invariant)
        {
            DateSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
            DateSeparator = "/",
            DateOrder = AdvancedExpressionOptions.DateOrderKind.YMD,
        };
        Assert.Equal(HasErrors("#01/02/2024#", slashYmd), HasErrors($"#01{separator}02{separator}2024#", ymd));

        var mdy = Options(AdvancedExpressionOptions.DateOrderKind.MDY);
        Assert.True(HasErrors($"#31{separator}12{separator}2024#", mdy));
        Assert.Equal(new DateTime(2024, 12, 31), Evaluate($"#12{separator}31{separator}2024#", mdy));
    }

    // NCALC-431 (review): where '-' is both the date and the time separator, three numbers are a date only when they make a valid date
    // in the date order, otherwise a time span (as where '.' is both)
    [Fact]
    public async Task HyphenAsDateAndTimeSeparatorShouldTellDatesFromTimes()
    {
        AdvancedExpressionOptions Options(AdvancedExpressionOptions.DateOrderKind order) => new(Invariant)
        {
            DateSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
            DateSeparator = "-",
            TimeSeparatorType = AdvancedExpressionOptions.SeparatorType.Custom,
            TimeSeparator = "-",
            DateOrder = order,
        };

        var dmy = Options(AdvancedExpressionOptions.DateOrderKind.DMY);
        Assert.Equal(new TimeSpan(10, 30, 5), await EvaluateAllAsync("#10-30-05#", ExpressionOptions.None, Invariant, dmy));
        Assert.Equal(new DateTime(2024, 2, 1), await EvaluateAllAsync("#01-02-2024#", ExpressionOptions.None, Invariant, dmy));
        Assert.Equal(new DateTime(2024, 2, 1), await EvaluateAllAsync("#2024-02-01#", ExpressionOptions.None, Invariant, dmy));

        var mdy = Options(AdvancedExpressionOptions.DateOrderKind.MDY);
        Assert.Equal(new DateTime(2005, 10, 30), await EvaluateAllAsync("#10-30-05#", ExpressionOptions.None, Invariant, mdy));
        Assert.Equal(new TimeSpan(10, 40, 5), await EvaluateAllAsync("#10-40-05#", ExpressionOptions.None, Invariant, mdy));
    }

    #endregion

    #region Review fixes

    // NCALC-407 (review): a secondary decimal separator ',' wins over the equal built-in group separator ',' also in the integer
    // parsers, so "1,5" followed by a list separator is 1.5, not the integer 15
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    public async Task SecondaryDecimalSeparatorShouldWinOverTheGroupSeparator(ExpressionOptions options)
    {
        var advancedOptions = new AdvancedExpressionOptions(Invariant)
        {
            SecondaryDecimalSeparator = ",",
            NumberGroupSeparatorType = AdvancedExpressionOptions.GroupSeparatorType.BuiltIn,
        };

        Assert.Equal(0, MathHelperCompare(2, await EvaluateAllAsync("Round(1,5, 0)", options, Invariant, advancedOptions)));
        Assert.Equal(0, MathHelperCompare(1.5, await EvaluateAllAsync("if(true, 1,5, 2)", options, Invariant, advancedOptions)));
        Assert.Equal(0, MathHelperCompare(2, await EvaluateAllAsync("Max(1,000, 2)", options, Invariant, advancedOptions)));
        Assert.Equal(0, MathHelperCompare(1.5, await EvaluateAllAsync("1,5", options, Invariant, advancedOptions)));
        Assert.Equal(0, MathHelperCompare(3, await EvaluateAllAsync("1,5 * 2", options, Invariant, advancedOptions)));

        var list = Assert.IsAssignableFrom<IList>(await EvaluateAllAsync("(1,5, 2)", options, Invariant, advancedOptions));
        Assert.Equal(2, list.Count);
        Assert.Equal(0, MathHelperCompare(1.5, list[0]));
        Assert.Equal(0, MathHelperCompare(2, list[1]));
    }

    // NCALC-405 (review): a big-number literal with more digits (or leading zeros) than MathHelper.MaxPowResultDigits stays the double
    // infinity or 0, as before; within the limit it is a BigDecimal that ToString() writes in bounded size and reads back
    [Theory]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.UseBigNumbers | ExpressionOptions.DecimalAsDefault)]
    public async Task BigNumberLiteralsBeyondTheDigitLimitShouldStayDoubles(ExpressionOptions options)
    {
        Assert.Equal(true, await EvaluateAllAsync("1e400000 + 1 > 0", options));
        Assert.Equal(true, await EvaluateAllAsync("1e-400000 + 1 > 0", options));
        Assert.Equal(true, await EvaluateAllAsync("1e9999999 * 2 > 0", options));
        Assert.Equal(double.PositiveInfinity, await EvaluateAllAsync("1e9999999", options));
        Assert.Equal(double.PositiveInfinity, await EvaluateAllAsync("1.0e999999999", options));
        Assert.Equal(0, MathHelperCompare(0, Assert.IsType<ValueExpression>(Parse("1e-400000", options)).Value));

        var withinLimit = Parse("1e20000", options);
        Assert.IsType<BigDecimal>(Assert.IsType<ValueExpression>(withinLimit).Value);
        Assert.True(withinLimit.ToString().Length <= NCalc.Helpers.MathHelper.MaxPowResultDigits + 20);

        foreach (var text in new[] { "1e5000", "1.5e3000" })
        {
            var tree = Parse(text, options);
            var value = Assert.IsType<ValueExpression>(tree).Value;
            Assert.IsType<BigDecimal>(value);
            Assert.Equal(0, MathHelperCompare(value!, Assert.IsType<ValueExpression>(Parse(tree.ToString(), options)).Value));
            var deserialized = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(tree));
            Assert.Equal(0, MathHelperCompare(value!, Assert.IsType<ValueExpression>(deserialized).Value));
        }
    }

    // NCALC-404 (review): with DecimalAsDefault, a literal below the range of double too stays the decimal 0 (a double 0 was written
    // as 0.0 and read back as a decimal)
    [Fact]
    public async Task DecimalAsDefaultLiteralsBelowTheDoubleRangeShouldStayDecimal()
    {
        const ExpressionOptions options = ExpressionOptions.DecimalAsDefault;
        foreach (var text in new[] { "1e-400", "2e-324", "0.1e-323" })
        {
            var value = await EvaluateAllAsync(text, options);
            Assert.IsType<decimal>(value);
            Assert.Equal(0m, value);
            Assert.Equal(value, await EvaluateAllAsync(Parse(text, options).ToString(), options));
        }

        Assert.Equal(5e-324, await EvaluateAllAsync("5e-324", options));
        Assert.Equal(3e-324, await EvaluateAllAsync("3e-324", options));
    }

    #endregion
}

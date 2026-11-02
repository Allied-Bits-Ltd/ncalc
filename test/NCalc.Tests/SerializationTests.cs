using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Factories;
using NCalc.Helpers;
using NCalc.Tests.TestData;

using Newtonsoft.Json;

using JsonSerializer = System.Text.Json.JsonSerializer;
using NCalcVector = NCalc.Domain.Vector;

namespace NCalc.Tests;

[Trait("Category", "Serialization")]
public class SerializationTests
{
    [Theory]
    [ClassData(typeof(WaterLevelCheckTestData))]
    public void SerializeAndDeserializeShouldWork(string expression, bool expected, double inputValue)
    {
        var compiled = LogicalExpressionFactory.Create(expression, cancellationToken: TestContext.Current.CancellationToken);
        var serialized = JsonConvert.SerializeObject(compiled, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All // We need this to allow serializing abstract classes
        });

        var deserialized = JsonConvert.DeserializeObject<LogicalExpression>(serialized, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

        var exp = new Expression(deserialized, ExpressionOptions.NoCache)
        {
            Parameters =
            {
                { "waterlevel", inputValue }
            }
        };

        object evaluated;
        try
        {
            evaluated = exp.Evaluate(TestContext.Current.CancellationToken);
        }
        catch
        {
            evaluated = false;
        }

        // Assert
        Assert.Equal(expected, evaluated);
    }

#if NET
    [Fact]
    public void SystemTextJsonPolymorphicSerializeAndDeserializeShouldWork()
    {
        var expression = LogicalExpressionFactory.Create("1 == 1", cancellationToken: TestContext.Current.CancellationToken);
        var expressionJson = JsonSerializer.Serialize(expression);
        Assert.True(JsonSerializer.Deserialize<LogicalExpression>(expressionJson) is BinaryExpression);
    }

    [Fact]
    public void DeserializationShouldWork_Issue_552()
    {
        const string expressionString = "waterLevel > 4.0";

        var logicalExpression = LogicalExpressionFactory.Create(expressionString, options: ExpressionOptions.NoCache | ExpressionOptions.UseBigNumbers, cancellationToken: TestContext.Current.CancellationToken); //Created a BinaryExpression object.

        var jsonExpression = JsonSerializer.Serialize(logicalExpression);

        var deserializedLogicalExpression = JsonSerializer.Deserialize<LogicalExpression>(jsonExpression); //The object is still a BinaryExpression.
        Assert.NotNull(deserializedLogicalExpression);

        var expression = new Expression(deserializedLogicalExpression);

        expression.Parameters = new Dictionary<string, object?> { {"waterLevel", 4.0}, };

        var result = expression.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(false, result);
    }
#endif

    [Fact]
    public void Binary_Expression_Serialization_Test()
    {
        Assert.Equal("True and False",
    new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false))
        .ToString());
        Assert.Equal("1 / 2",
            new BinaryExpression(BinaryExpressionType.Div, new ValueExpression(1), new ValueExpression(2)).ToString());
        Assert.Equal("1 = 2",
            new BinaryExpression(BinaryExpressionType.Equal, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 > 2",
            new BinaryExpression(BinaryExpressionType.Greater, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 >= 2",
            new BinaryExpression(BinaryExpressionType.GreaterOrEqual, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 < 2",
            new BinaryExpression(BinaryExpressionType.Less, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 <= 2",
            new BinaryExpression(BinaryExpressionType.LessOrEqual, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 - 2",
            new BinaryExpression(BinaryExpressionType.Minus, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 % 2",
            new BinaryExpression(BinaryExpressionType.Modulo, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 != 2",
            new BinaryExpression(BinaryExpressionType.NotEqual, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("True or False",
            new BinaryExpression(BinaryExpressionType.Or, new ValueExpression(true), new ValueExpression(false))
                .ToString());
        Assert.Equal("1 + 2",
            new BinaryExpression(BinaryExpressionType.Plus, new ValueExpression(1), new ValueExpression(2)).ToString());
        Assert.Equal("1 * 2",
            new BinaryExpression(BinaryExpressionType.Times, new ValueExpression(1), new ValueExpression(2))
                .ToString());
    }

    [Fact]
    public void Unary_Expression_Serialization_Test()
    {
        Assert.Equal("-(True and False)",
            new UnaryExpression(UnaryExpressionType.Negate,
                    new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true),
                        new ValueExpression(false)))
                .ToString());
        Assert.Equal("!(True and False)",
            new UnaryExpression(UnaryExpressionType.Not,
                    new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true),
                        new ValueExpression(false)))
                .ToString());
    }

    [Theory]
    [InlineData("(1 + 2) * 3", "(1 + 2) * 3")]
    [InlineData("1 - (2 - 3)", "1 - (2 - 3)")]
    [InlineData("1 - 2 - 3", "1 - 2 - 3")]
    [InlineData("(-2) ** 2", "(-2) ** 2")]
    [InlineData("(2 ** 3) ** 2", "(2 ** 3) ** 2")]
    [InlineData("2 ** (3 ** 2)", "2 ** 3 ** 2")]
    [InlineData("2 ** -1", "2 ** (-1)")]
    [InlineData("(a ? 1 : 2) ? 3 : 4", "(a ? 1 : 2) ? 3 : 4")]
    [InlineData("a ? (b ? 1 : 2) : 3", "a ? (b ? 1 : 2) : 3")]
    [InlineData("(a or b) and c", "(a or b) and c")]
    [InlineData("Max(abc, 1)", "Max(abc; 1)")]
    [InlineData("Max(a, b)", "Max(a; b)")]
    [InlineData("Abs([a]) + Max([a], abc)", "Abs([a]) + Max([a]; abc)")]
    public void ToStringShouldKeepTheMeaningOfTheExpression(string expression, string expected)
    {
        var parsed = new Expression(expression, ExpressionOptions.NoCache);
        Assert.False(parsed.HasErrors());
        Assert.Equal(expected, parsed.LogicalExpression!.ToString());
    }

    [Fact]
    public void ToStringShouldRoundTripRandomExpressions()
    {
        // Random expressions with random parentheses: the text produced by ToString must parse back to an expression with the same result
        var random = new Random(20261031);
        string[] binaryOperators = ["+", "-", "*", "/", "%", "**", "&", "|", "^", "<<", ">>", "=", "!=", "<", ">=", "and", "or", "xor", "div"];

        string Atom() => random.Next(7) switch
        {
            0 => "a", 1 => "abc", 2 => $"Max({Generate(1)}, {Generate(1)})", 3 => $"Abs({Generate(1)})", 4 => "true", _ => random.Next(10).ToString(CultureInfo.InvariantCulture)
        };
        string Wrap(string text) => random.Next(2) == 0 ? $"({text})" : text;
        string Generate(int depth)
        {
            if (depth <= 0 || random.Next(10) < 2)
                return Atom();
            return random.Next(10) switch
            {
                0 => "-" + Wrap(Generate(depth - 1)),
                1 => "not " + Wrap(Generate(depth - 1)),
                2 => $"{Wrap(Generate(depth - 1))} ? {Wrap(Generate(depth - 1))} : {Wrap(Generate(depth - 1))}",
                _ => $"{Wrap(Generate(depth - 1))} {binaryOperators[random.Next(binaryOperators.Length)]} {Wrap(Generate(depth - 1))}"
            };
        }
        string Evaluate(string text)
        {
            try
            {
                var expression = new Expression(text, ExpressionOptions.NoCache);
                expression.Parameters["a"] = 3;
                expression.Parameters["abc"] = 7;
                var result = expression.Evaluate(TestContext.Current.CancellationToken);
                return $"{Convert.ToString(result, CultureInfo.InvariantCulture)} {result?.GetType().Name}";
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }

        var failures = new List<string>();
        for (int i = 0; i < 1000 && failures.Count < 10; i++)
        {
            string source = Generate(4);
            var original = new Expression(source, ExpressionOptions.NoCache);
            if (original.HasErrors())
                continue;

            string text = original.LogicalExpression!.ToString();
            var reparsed = new Expression(text, ExpressionOptions.NoCache);
            if (reparsed.HasErrors())
                failures.Add($"{source} -> {text}: does not parse");
            else
            if (reparsed.LogicalExpression!.ToString() != text)
                failures.Add($"{source} -> {text} -> {reparsed.LogicalExpression}");
            else
            if (Evaluate(source) != Evaluate(text))
                failures.Add($"{source} = {Evaluate(source)}, {text} = {Evaluate(text)}");
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void ComplexNumbersAndVectorsShouldBeFormattedInvariantly()
    {
        // The real part and vector components were formatted with the culture current at the first use of BigDecimal
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "de-DE", "en-US" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var complex = new ComplexNumber(new BigDecimal(new System.Numerics.BigInteger(-2), -1), new BigDecimal(new System.Numerics.BigInteger(4), -1));
                Assert.Equal("-0.2 + 0.4i", complex.ToString());
                Assert.Equal("[1, 2.5]", new NCalcVector([new BigDecimal(1), new BigDecimal(new System.Numerics.BigInteger(25), -1)]).ToString());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Fact]
    public void Function_Serialization_Test()
    {
        Assert.Equal("test(True and False; -(True and False))", new FunctionCall(new Identifier("test"), [
            new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false)),
            new UnaryExpression(UnaryExpressionType.Negate,
                new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false)))
        ]).ToString());

        Assert.Equal("Sum(1 + 2)", new FunctionCall(new Identifier("Sum"), [
            new BinaryExpression(BinaryExpressionType.Plus, new ValueExpression(1), new ValueExpression(2))
        ]).ToString());
    }

    [Fact]
    public void Value_Serialization_Test()
    {
        Assert.Equal("True", new ValueExpression(true).ToString());
        Assert.Equal("False", new ValueExpression(false).ToString());
        Assert.Equal("1", new ValueExpression(1).ToString());
        // Numbers and dates are written in the syntax of the parser, not in the format of the current culture
        Assert.Equal("1.234", new ValueExpression(1.234).ToString());
        Assert.Equal("'hello'", new ValueExpression("hello").ToString());
        Assert.Equal("'c'", new ValueExpression('c').ToString());
        Assert.Equal("#2009-01-01#", new ValueExpression(new DateTime(2009, 1, 1)).ToString());
    }

    [Fact]
    public void ArraySerializationTest()
    {
        var trueArrayExpression = new LogicalExpressionList([new ValueExpression(true)]);
        var helloWorldArrayExpression = new LogicalExpressionList([new ValueExpression("Hello"), new ValueExpression("World")]);
        Assert.Equal("(True)", trueArrayExpression.ToString());
        Assert.Equal("('Hello'; 'World')", helloWorldArrayExpression.ToString());
        Assert.Equal("()", new LogicalExpressionList([]).ToString());
        Assert.Equal("((True); ('Hello'; 'World'))", new LogicalExpressionList([trueArrayExpression, helloWorldArrayExpression]).ToString());
    }

#if NET
    [Fact]
    public void ShouldSerializeImaginaryNumberExpression()
    {
        // "2i" parses to an ImaginaryNumberExpression wrapping ValueExpression(2)
        var advancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseComplexNumbers);
        var parsed = LogicalExpressionFactory.Create("2i", advancedOptions: advancedOptions,
            cancellationToken: TestContext.Current.CancellationToken);

        var json = JsonSerializer.Serialize(parsed);
        var restored = JsonSerializer.Deserialize<LogicalExpression>(json);

        Assert.NotNull(restored);
        Assert.IsType<ImaginaryNumberExpression>(restored);
    }

    private const ExpressionOptions StatementOptions = ExpressionOptions.NoCache | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseAssignments | ExpressionOptions.UseIfStatement;

    [Theory]
    [InlineData("a := 2; a + 1", 3, "")]
    [InlineData("if (1 > 0) { 2 } else { 3 }", 2, "")]
    [InlineData("fn sq(a, b = 2) => a * a + b; sq(3)", 11, "sq")]
    [InlineData("fn outer(x) { fn inner(y) => y + 1; inner(x) * 2 }; outer(4)", 10, "inner,outer")]
    public async Task StatementsAndFunctionDeclarationsShouldSurviveJsonRoundTrip(string text, int expected, string expectedFunctions)
    {
        var parsed = LogicalExpressionFactory.Create(text, options: StatementOptions, cancellationToken: TestContext.Current.CancellationToken);
        var restored = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(parsed))!;

        var expression = new Expression(restored, StatementOptions);
        Assert.Equal(expected, expression.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(expectedFunctions, string.Join(",", expression.UserFunctions.Keys.OrderBy(k => k, StringComparer.Ordinal)));

        Assert.Equal(expected, await new AsyncExpression(restored, StatementOptions).EvaluateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void FunctionDeclarationShouldKeepItsDetailsInJsonRoundTrip()
    {
        var parsed = LogicalExpressionFactory.Create("/// Adds b to the square of a\nfn sq(a, b = 2) => a * a + b", options: StatementOptions, cancellationToken: TestContext.Current.CancellationToken);
        var restored = Assert.IsType<FunctionExpression>(JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(parsed)));

        Assert.Equal("sq", restored.Function.Name);
        Assert.Equal("Adds b to the square of a", restored.Function.Description);
        Assert.Equal(2, restored.Function.Parameters.Count);
        Assert.Equal("a", restored.Function.Parameters[0].Name);
        Assert.False(restored.Function.Parameters[0].IsOptional);
        Assert.Equal("b", restored.Function.Parameters[1].Name);
        Assert.True(restored.Function.Parameters[1].IsOptional);
        Assert.Equal(2, Assert.IsType<ValueExpression>(restored.Function.Parameters[1].DefaultValue).Value);
    }

    [Fact]
    public void ShouldSerializeVectorExpression()
    {
        // "[1; 2; 3]" parses to a VectorExpression
        var advancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseVectors);
        var parsed = LogicalExpressionFactory.Create("[1; 2; 3]", advancedOptions: advancedOptions,
            cancellationToken: TestContext.Current.CancellationToken);

        var json = JsonSerializer.Serialize(parsed);
        var restored = JsonSerializer.Deserialize<LogicalExpression>(json);

        Assert.NotNull(restored);
        Assert.IsType<VectorExpression>(restored);
        var restoredVec = (VectorExpression)restored;
        Assert.Equal(3, restoredVec.Expressions.Count);
    }

    [Fact]
    public void ShouldSerializeComplexNumberValue()
    {
        // A ValueExpression whose Value is a ComplexNumber should round-trip through JSON
        var complexNumber = new ComplexNumber(new BigDecimal(3), new BigDecimal(4));
        var ve = new ValueExpression { Value = complexNumber };

        var json = JsonSerializer.Serialize<LogicalExpression>(ve);
        var restored = (ValueExpression)JsonSerializer.Deserialize<LogicalExpression>(json)!;

        Assert.NotNull(restored.Value);
        Assert.IsType<ComplexNumber>(restored.Value);
        var cn = (ComplexNumber)restored.Value;
        Assert.Equal(complexNumber.Real, cn.Real);
        Assert.Equal(complexNumber.Imaginary, cn.Imaginary);
    }

    [Fact]
    public void ShouldSerializeVectorValue()
    {
        // A ValueExpression whose Value is a Vector should round-trip through JSON
        var vector = new NCalcVector(new BigDecimal[] { 1, 2, 3 });
        var ve = new ValueExpression { Value = vector };

        var json = JsonSerializer.Serialize<LogicalExpression>(ve);
        var restored = (ValueExpression)JsonSerializer.Deserialize<LogicalExpression>(json)!;

        Assert.NotNull(restored.Value);
        Assert.IsType<NCalcVector>(restored.Value);
        var v = (NCalcVector)restored.Value;
        Assert.Equal(3, v.Dimensions);
        Assert.Equal((BigDecimal)1, v.Components[0]);
        Assert.Equal((BigDecimal)2, v.Components[1]);
        Assert.Equal((BigDecimal)3, v.Components[2]);
    }
#endif

    [Fact]
    public void ShouldSerializeDouble()
    {
        var ve = new ValueExpression { Value = 0.00001d };

        var json = JsonSerializer.Serialize<LogicalExpression>(ve);
        var restored = (ValueExpression)JsonSerializer.Deserialize<LogicalExpression>(json)!;

        Assert.NotNull(restored.Value);
        Assert.Equal(0.00001d, restored.Value);

        var expr = new Expression("0.00001", ExpressionOptions.DecimalAsDefault);
        string s = expr.GetLogicalExpression().ToString();
        Assert.Equal("0.00001", s);

        expr = new Expression("0.00001", ExpressionOptions.None);
        s = expr.GetLogicalExpression().ToString();
        Assert.Equal("0.00001", s);
    }

    [Theory]
    [InlineData("2.5 + 1e-30 + 0.30000000000000004 + 1.5e10", ExpressionOptions.None, AdvExpressionOptions.None)]
    [InlineData("#2024-01-02# + #2024-01-02T10:20:30.5#", ExpressionOptions.None, AdvExpressionOptions.None)]
    [InlineData("x == 5", ExpressionOptions.UseCStyleAssignments, AdvExpressionOptions.None)]
    [InlineData("fn sq(a) => a * a; sq(3)", ExpressionOptions.UseStatementSequences, AdvExpressionOptions.None)]
    [InlineData("6 bit_and 3 + bit_not 3", ExpressionOptions.SkipLogicalAndBitwiseOpChars, AdvExpressionOptions.None)]
    [InlineData("7 mod 3", ExpressionOptions.None, AdvExpressionOptions.CalculatePercent)]
    public void ToStringShouldRoundTripUnderAnyCulture(string expression, ExpressionOptions options, AdvExpressionOptions advancedOptions)
    {
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "sk-SK", "sv-SE", "en-US" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Expression Create(string text) => new(text, options | ExpressionOptions.NoCache) { AdvancedOptions = advancedOptions == AdvExpressionOptions.None ? null : new AdvancedExpressionOptions(advancedOptions) };
                string text = Create(expression).GetLogicalExpression().ToString();
                var reparsed = Create(text);
                Assert.False(reparsed.HasErrors(), $"{text} does not parse under {culture}");
                Assert.Equal(text, reparsed.GetLogicalExpression().ToString());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Theory]
    [InlineData("[my var] + 1")]
    [InlineData("'it\\'s'")]
    [InlineData("deadbeef-cafe-babe-face-0123456789ab")]
    public void JsonShouldKeepIdentifiersStringsAndGuids(string expression)
    {
        var logicalExpression = new Expression(expression, ExpressionOptions.NoCache).GetLogicalExpression();
        var restored = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(logicalExpression))!;
        Assert.Equal(logicalExpression.ToString(), restored.ToString());
    }

    [Fact]
    public void ParsingAndSerializingShouldNotChangeOtherExpressions()
    {
        // NCALC-082: the nodes of true, false and null were shared, and serialization wrote options into the tree
        var first = new Expression("true", ExpressionOptions.NoCache | ExpressionOptions.UseCStyleAssignments).GetLogicalExpression(TestContext.Current.CancellationToken)!;
        var second = new Expression("true", ExpressionOptions.NoCache).GetLogicalExpression(TestContext.Current.CancellationToken)!;
        Assert.NotSame(first, second);
        Assert.True(first.Options.HasFlag(ExpressionOptions.UseCStyleAssignments));
        Assert.False(second.Options.HasFlag(ExpressionOptions.UseCStyleAssignments));

        var tree = new Expression("a == 1", ExpressionOptions.NoCache).GetLogicalExpression(TestContext.Current.CancellationToken)!;
        var child = ((BinaryExpression)tree).RightExpression;
        var before = child.Options;
        _ = tree.ToString();
        Assert.Equal(before, child.Options);
    }

    [Fact]
    public void SerializerShouldHandleUnusualValues()
    {
        // NCALC-086
        Assert.Equal("5!!", new BinaryExpression(BinaryExpressionType.Factorial, new ValueExpression(5), new ValueExpression(2L)).ToString().Trim());

        var guid = Guid.Parse("8a4e5d1c-1b2c-4d5e-9f00-123456789abc");
        var guidText = new ValueExpression(guid).ToString();
        Assert.Equal(guid, new Expression(guidText).Evaluate(TestContext.Current.CancellationToken));

        var quoted = new ValueExpression("it's a \\ test").ToString();
        Assert.Equal("it's a \\ test", new Expression(quoted).Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-117
    [Fact]
    public void ToStringAndJsonShouldRoundTripUnderTheOptionsOfTheExpression()
    {
        static AdvancedExpressionOptions Advanced(AdvExpressionOptions flags = AdvExpressionOptions.None,
            AdvancedExpressionOptions.ListItemSeparatorKind separator = AdvancedExpressionOptions.ListItemSeparatorKind.CommaOrSemicolon,
            bool decimalSeparatorFromCulture = false)
            => new(flags)
            {
                ListItemSeparator = separator,
                DecimalSeparatorType = decimalSeparatorFromCulture ? AdvancedExpressionOptions.SeparatorType.FromCulture : AdvancedExpressionOptions.SeparatorType.BuiltIn
            };

        var parameters = new Dictionary<string, object?> { ["s"] = "abcdefgh", ["x"] = 2, ["a"] = 1, ["b"] = 2 };
        var rows = new (string Text, ExpressionOptions Options, Func<AdvancedExpressionOptions?> AdvancedOptions)[]
        {
            ("not true and false", ExpressionOptions.SkipLogicalAndBitwiseOpChars, () => null),
            ("not (a = b) and true", ExpressionOptions.SkipLogicalAndBitwiseOpChars, () => null),
            ("(s[1..4])[0]", ExpressionOptions.None, () => null),
            ("(3!)[0]", ExpressionOptions.None, () => null),
            ("x i", ExpressionOptions.None, () => Advanced(AdvExpressionOptions.ParseComplexNumbers)),
            ("x i + (1 + 2) i", ExpressionOptions.None, () => Advanced(AdvExpressionOptions.ParseComplexNumbers)),
            ("0xFFFFFFFFFFFFFFFF", ExpressionOptions.None, () => null),
            ("0x8000000000000000 + 0", ExpressionOptions.UseBigNumbers, () => null),
            ("1e400", ExpressionOptions.None, () => null),
            ("-1e400 < 5", ExpressionOptions.None, () => null),
            ("(1e400 - 1e400) * 2", ExpressionOptions.None, () => null),
            ("Max(1; 2)", ExpressionOptions.None, () => Advanced(decimalSeparatorFromCulture: true)),
            ("Max(1,5; 2)", ExpressionOptions.None, () => Advanced(decimalSeparatorFromCulture: true)),
            ("Max(1 , 2) + Max(2,5 , 1)", ExpressionOptions.None, () => Advanced(separator: AdvancedExpressionOptions.ListItemSeparatorKind.Comma, decimalSeparatorFromCulture: true)),
            ("Max(1 (-2))", ExpressionOptions.None, () => Advanced(separator: AdvancedExpressionOptions.ListItemSeparatorKind.Space)),
            ("Max((a) (-b)) + Max((b) [a])", ExpressionOptions.None, () => Advanced(separator: AdvancedExpressionOptions.ListItemSeparatorKind.Space)),
            ("#1 second 500 ms#", ExpressionOptions.None, () => Advanced(AdvExpressionOptions.ParseHumanePeriods)),
            ("#-1.02:03:04.25#", ExpressionOptions.None, () => null),
            ("x == 5", ExpressionOptions.UseCStyleAssignments, () => null),
            ("6 bit_and 3 + bit_not 3", ExpressionOptions.SkipLogicalAndBitwiseOpChars, () => null),
            ("7 mod 3", ExpressionOptions.None, () => Advanced(AdvExpressionOptions.CalculatePercent)),
            ("a := 2; a + 1", ExpressionOptions.UseStatementSequences | ExpressionOptions.UseAssignments, () => null),
        };

        var culture = CultureInfo.GetCultureInfo("sk-SK");
        Expression Create(string text, ExpressionOptions options, Func<AdvancedExpressionOptions?> advancedOptions)
            => new(text, options | ExpressionOptions.NoCache, culture) { AdvancedOptions = advancedOptions(), Parameters = new Dictionary<string, object?>(parameters) };
        string Evaluate(Expression expression)
        {
            try
            {
                var result = expression.Evaluate(TestContext.Current.CancellationToken);
                return $"{Convert.ToString(result, CultureInfo.InvariantCulture)} {result?.GetType().Name}";
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }

        var failures = new List<string>();
        foreach (var (text, options, advancedOptions) in rows)
        {
            var original = Create(text, options, advancedOptions);
            var tree = original.GetLogicalExpression(TestContext.Current.CancellationToken);
            if (tree is null)
            {
                failures.Add($"{text}: does not parse");
                continue;
            }

            string expected = Evaluate(original);
            string written = tree.ToString();
            var reparsed = Create(written, options, advancedOptions);
            if (reparsed.HasErrors(TestContext.Current.CancellationToken))
                failures.Add($"{text} -> {written}: does not parse");
            else
            if (Evaluate(reparsed) != expected)
                failures.Add($"{text} = {expected}, {written} = {Evaluate(reparsed)}");

            var restored = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(tree))!;
            if (restored.ToString() != written)
                failures.Add($"{text}: JSON -> {restored}");
            var fromJson = new Expression(restored, options | ExpressionOptions.NoCache, culture) { AdvancedOptions = advancedOptions(), Parameters = new Dictionary<string, object?>(parameters) };
            if (Evaluate(fromJson) != expected)
                failures.Add($"{text} = {expected}, from JSON = {Evaluate(fromJson)}");
        }

        Assert.Empty(failures);
    }

    // NCALC-117
    [Fact]
    public void JsonShouldKeepTheTypesOfValues()
    {
        static object? RoundTrip(object value)
        {
            var json = JsonSerializer.Serialize<LogicalExpression>(new ValueExpression(1) { Value = value });
            return ((ValueExpression)JsonSerializer.Deserialize<LogicalExpression>(json)!).Value;
        }

        Assert.Equal(new[] { 1, 2 }, Assert.IsType<int[]>(RoundTrip(new[] { 1, 2 })));
        Assert.Equal(new[] { "a", null }, Assert.IsType<string[]>(RoundTrip(new[] { "a", null })));
        Assert.Equal(new object[] { 1L, "x" }, Assert.IsType<object[]>(RoundTrip(new object[] { 1L, "x" })));

        // Dictionaries are read back as dictionaries, also when their keys look like a BigDecimal or a tagged value
        var bigDecimalLike = Assert.IsType<Dictionary<string, object?>>(RoundTrip(new Dictionary<string, object?> { ["Mantissa"] = 5, ["Exponent"] = 1 }));
        Assert.Equal(5, bigDecimalLike["Mantissa"]);
        Assert.Equal(1, bigDecimalLike["Exponent"]);
        var taggedLike = Assert.IsType<Dictionary<string, object?>>(RoundTrip(new Dictionary<string, object?> { ["$type"] = "Int32", ["$value"] = "5" }));
        Assert.Equal("Int32", taggedLike["$type"]);
        Assert.Equal("5", taggedLike["$value"]);

        var offset = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));
        Assert.Equal(offset, Assert.IsType<DateTimeOffset>(RoundTrip(offset)));
        Assert.Equal(offset.Offset, ((DateTimeOffset)RoundTrip(offset)!).Offset);

        // JSON written before dictionaries were tagged is still read
        var untagged = JsonSerializer.Deserialize<LogicalExpression>("""{"$type":"value","Value":{"a":{"$type":"Int32","$value":"1"}},"Type":1}""");
        Assert.Equal(1, Assert.IsType<Dictionary<string, object?>>(((ValueExpression)untagged!).Value)["a"]);
    }

    // NCALC-132
    [Fact]
    public void AdvancedOptionsShouldBeCopiedConsistently()
    {
        // The expression always keeps its own copy, also when the culture of the options is already that of the expression
        var culture = CultureInfo.GetCultureInfo("en-US");
        var shared = new AdvancedExpressionOptions(culture);
        var expression = new Expression("#1 day#", ExpressionOptions.NoCache, culture) { AdvancedOptions = shared };
        Assert.NotSame(shared, expression.AdvancedOptions);
        Assert.True(expression.HasErrors(TestContext.Current.CancellationToken));

        // The copy is changed through the property; the assigned instance is not changed
        expression = new Expression("#1 day#", ExpressionOptions.NoCache, culture) { AdvancedOptions = shared };
        expression.AdvancedOptions!.Flags |= AdvExpressionOptions.ParseHumanePeriods;
        Assert.Equal(TimeSpan.FromDays(1), expression.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(AdvExpressionOptions.None, shared.Flags);

        // Assigning the own copy again keeps it
        var own = expression.AdvancedOptions;
        expression.AdvancedOptions = own;
        Assert.Same(own, expression.AdvancedOptions);

        // A parsed tree keeps a snapshot: changing the options later does not change its text (it is cached and may be shared)
        var options = new AdvancedExpressionOptions(culture) { ListItemSeparator = AdvancedExpressionOptions.ListItemSeparatorKind.Semicolon };
        var cached = new Expression("Max(1; 2) + 0", ExpressionOptions.None, culture) { AdvancedOptions = options };
        var tree = cached.GetLogicalExpression(TestContext.Current.CancellationToken)!;
        cached.AdvancedOptions!.ListItemSeparator = AdvancedExpressionOptions.ListItemSeparatorKind.Comma;
        Assert.Equal("Max(1; 2) + 0", tree.ToString());

        var direct = LogicalExpressionFactory.Create("Max(1; 2)", advancedOptions: options, cancellationToken: TestContext.Current.CancellationToken);
        options.ListItemSeparator = AdvancedExpressionOptions.ListItemSeparatorKind.Comma;
        Assert.Equal("Max(1; 2)", direct.ToString());
    }

    // NCALC-205 (list and sequence roots in JSON), NCALC-226 (unsigned literals), NCALC-227 (fractions in index boundaries),
    // NCALC-228 (Space-separated items that start with an operator), NCALC-230 (fn declarations)
    [Fact]
    public void ToStringAndJsonShouldRoundTripListsSequencesLiteralsAndFunctionDeclarations()
    {
        var culture = CultureInfo.GetCultureInfo("sk-SK");
        AdvancedExpressionOptions Advanced(AdvExpressionOptions flags = AdvExpressionOptions.None,
            AdvancedExpressionOptions.ListItemSeparatorKind separator = AdvancedExpressionOptions.ListItemSeparatorKind.CommaOrSemicolon,
            bool decimalSeparatorFromCulture = false)
            => new(culture, flags)
            {
                ListItemSeparator = separator,
                DecimalSeparatorType = decimalSeparatorFromCulture ? AdvancedExpressionOptions.SeparatorType.FromCulture : AdvancedExpressionOptions.SeparatorType.BuiltIn
            };

        const ExpressionOptions Statements = ExpressionOptions.UseStatementSequences | ExpressionOptions.UseAssignments;
        const ExpressionOptions CStyle = Statements | ExpressionOptions.UseCStyleAssignments;
        var parameters = new Dictionary<string, object?> { ["s"] = "abcdefgh", ["x"] = 2 };
        var rows = new (string Text, ExpressionOptions Options, Func<AdvancedExpressionOptions?> AdvancedOptions)[]
        {
            // NCALC-205: the options of a list or sequence root were lost in JSON, so "==" came back as the assignment "="
            ("x == 1; 2", CStyle, () => null),
            ("(x == 1, 2)", CStyle, () => null),
            ("(7 mod 3, 1)", ExpressionOptions.None, () => Advanced(AdvExpressionOptions.CalculatePercent)),
            ("(6 bit_and 3, 1)", ExpressionOptions.SkipLogicalAndBitwiseOpChars, () => null),
            ("(Max(1,5; 2), 1)", ExpressionOptions.None, () => Advanced(decimalSeparatorFromCulture: true)),
            ("(3 (-1))", ExpressionOptions.None, () => Advanced(separator: AdvancedExpressionOptions.ListItemSeparatorKind.Space)),
            ("x := 1; x + 1;", Statements, () => null),
            // NCALC-226
            ("0xFF - 0x100", ExpressionOptions.HexBinOctAreUnsigned, () => null),
            ("0xFFFFFFFF + 0xFFFFFFFF", ExpressionOptions.HexBinOctAreUnsigned, () => null),
            ("0x7FFFFFFFFFFFFFFF + 0x7FFFFFFFFFFFFFFF", ExpressionOptions.HexBinOctAreUnsigned, () => null),
            // NCALC-227
            ("s[^((2147483647.0 + 1) / 268435456)]", ExpressionOptions.None, () => null),
            ("s[1..((2147483647.0 + 1) / 536870912)]", ExpressionOptions.None, () => null),
            ("s[1..3] + s[^2..]", ExpressionOptions.None, () => null),
            // NCALC-228
            ("if(true (!false) 2)", ExpressionOptions.None, () => Advanced(separator: AdvancedExpressionOptions.ListItemSeparatorKind.Space)),
            ("if(true (not false) 2)", ExpressionOptions.SkipLogicalAndBitwiseOpChars, () => Advanced(separator: AdvancedExpressionOptions.ListItemSeparatorKind.Space)),
            ("Max(1 (~2))", ExpressionOptions.None, () => Advanced(separator: AdvancedExpressionOptions.ListItemSeparatorKind.Space)),
            // NCALC-230
            ("fn f(a, b) => a + b; f(1: 2)", Statements, () => Advanced(separator: AdvancedExpressionOptions.ListItemSeparatorKind.Colon)),
            ("fn f(a, b) => a + b; f(1 2)", Statements, () => Advanced(separator: AdvancedExpressionOptions.ListItemSeparatorKind.Space)),
            ("fn f(a; b = 1,5) => a + b; f(1)", Statements, () => Advanced(decimalSeparatorFromCulture: true)),
            ("fn f(a = null) => a; f()", Statements | ExpressionOptions.AllowNullParameter, () => null),
        };

        Expression Create(string text, ExpressionOptions options, Func<AdvancedExpressionOptions?> advancedOptions)
            => new(text, options | ExpressionOptions.NoCache, culture) { AdvancedOptions = advancedOptions(), Parameters = new Dictionary<string, object?>(parameters) };
        string Evaluate(Expression expression)
        {
            try
            {
                var result = expression.Evaluate(TestContext.Current.CancellationToken);
                return result is System.Collections.IEnumerable items and not string
                    ? string.Join("; ", items.Cast<object?>().Select(item => $"{Convert.ToString(item, CultureInfo.InvariantCulture)} {item?.GetType().Name}"))
                    : $"{Convert.ToString(result, CultureInfo.InvariantCulture)} {result?.GetType().Name}";
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }

        var failures = new List<string>();
        foreach (var (text, options, advancedOptions) in rows)
        {
            var original = Create(text, options, advancedOptions);
            var tree = original.GetLogicalExpression(TestContext.Current.CancellationToken);
            if (tree is null)
            {
                failures.Add($"{text}: does not parse");
                continue;
            }

            string expected = Evaluate(original);
            string written = tree.ToString();
            var reparsed = Create(written, options, advancedOptions);
            if (reparsed.HasErrors(TestContext.Current.CancellationToken))
                failures.Add($"{text} -> {written}: does not parse");
            else
            if (Evaluate(reparsed) != expected)
                failures.Add($"{text} = {expected}, {written} = {Evaluate(reparsed)}");

            var restored = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(tree))!;
            if (restored.ToString() != written)
                failures.Add($"{text}: JSON -> {restored}");
            var fromJson = new Expression(restored, options | ExpressionOptions.NoCache, culture) { AdvancedOptions = advancedOptions(), Parameters = new Dictionary<string, object?>(parameters) };
            if (Evaluate(fromJson) != expected)
                failures.Add($"{text} = {expected}, from JSON = {Evaluate(fromJson)}");
        }

        Assert.Empty(failures);
    }

    // NCALC-205: a list or a sequence keeps its options and its trailing separator in JSON, also as a function argument list,
    // and JSON written before (plain arrays) is still read
    [Fact]
    public void JsonShouldKeepTheOptionsOfListsAndSequences()
    {
        var sequence = LogicalExpressionFactory.Create("x == 1; 2;", null, CultureInfo.InvariantCulture,
            ExpressionOptions.UseStatementSequences | ExpressionOptions.UseAssignments | ExpressionOptions.UseCStyleAssignments);
        var restored = Assert.IsType<StatementSequence>(JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(sequence)));
        Assert.True(restored.EndsWithSeparator);
        Assert.True(restored.Options.HasFlag(ExpressionOptions.UseCStyleAssignments));
        Assert.Equal("x == 1; 2;", restored.ToString());

        var list = LogicalExpressionFactory.Create("(7 mod 3, 1)", null, CultureInfo.InvariantCulture, ExpressionOptions.None,
            new AdvancedExpressionOptions(CultureInfo.InvariantCulture, AdvExpressionOptions.CalculatePercent));
        Assert.Equal("(7 mod 3; 1)", JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(list))!.ToString());

        // A list without options is written as before
        var call = LogicalExpressionFactory.Create("Max(1, 2)", null, CultureInfo.InvariantCulture);
        Assert.Contains("\"Parameters\":[", JsonSerializer.Serialize(call), StringComparison.Ordinal);

        var oldSequence = JsonSerializer.Deserialize<LogicalExpression>("""{"$type":"statements","$values":[{"$type":"value","Value":1,"Type":1},{"$type":"value","Value":2,"Type":1}]}""");
        Assert.Equal("1; 2", Assert.IsType<StatementSequence>(oldSequence).ToString());
        var oldCall = JsonSerializer.Deserialize<LogicalExpression>("""{"$type":"function","Identifier":{"Name":"Max"},"Parameters":[{"$type":"value","Value":1,"Type":1},{"$type":"value","Value":2,"Type":1}]}""");
        Assert.Equal("Max(1; 2)", Assert.IsType<FunctionCall>(oldCall).ToString());
    }

    // NCALC-301: each node was serialized by a nested SerializeToElement call, which did not count the depth, so a deep tree (from
    // about 250 levels, e.g. a flat sum) crashed the process with a stack overflow, and each level copied its whole subtree again
    [Fact]
    public void DeepExpressionsShouldFailWithJsonExceptionInsteadOfCrashing()
    {
        var culture = CultureInfo.InvariantCulture;
        var sum = LogicalExpressionFactory.Create(string.Join(" + ", Enumerable.Range(0, 1000)), null, culture);
        var calls = LogicalExpressionFactory.Create(string.Concat(Enumerable.Repeat("Max(1, ", 400)) + "1" + new string(')', 400), null, culture);
        var sequence = LogicalExpressionFactory.Create("x := " + string.Join(" + ", Enumerable.Range(0, 400)) + "; x", null, culture,
            ExpressionOptions.UseStatementSequences | ExpressionOptions.UseAssignments);
        foreach (var tree in new[] { sum, calls, sequence })
            Assert.Throws<System.Text.Json.JsonException>(() => JsonSerializer.Serialize(tree));

        // Too deep a document is rejected on read as well
        string deepJson = string.Concat(Enumerable.Repeat("""{"$type":"unary","Type":0,"Expression":""", 1000)) + """{"$type":"value","Value":1,"Type":1}""" + new string('}', 1000);
        Assert.Throws<System.Text.Json.JsonException>(() => JsonSerializer.Deserialize<LogicalExpression>(deepJson));

        // With a larger MaxDepth, the trees are written and read back in linear time on a thread with enough stack
        var options = new System.Text.Json.JsonSerializerOptions { MaxDepth = 5000 };
        List<string> expected = [], results = [];
        Exception? error = null;
        long elapsed = 0;
        var thread = new Thread(() =>
        {
            try
            {
                foreach (var tree in new[] { sum, calls, sequence })
                {
                    expected.Add(tree.ToString());
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var restored = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(tree, options), options)!;
                    elapsed += stopwatch.ElapsedMilliseconds;
                    results.Add(restored.ToString());
                }
            }
            catch (Exception e)
            {
                error = e;
            }
        }, 256 * 1024 * 1024);
        thread.Start();
        thread.Join();
        Assert.Null(error);
        Assert.Equal(expected, results);
        Assert.True(elapsed < 10_000, $"Serialization took {elapsed} ms");

        // On a small stack, a tree within MaxDepth but too deep for the stack fails with a JsonException, not a stack overflow
        // (and so does an invalid node deep in a document)
        Exception? writeError = null, readError = null, invalidError = null;
        string sumJson = JsonSerializer.Serialize(LogicalExpressionFactory.Create(string.Join(" + ", Enumerable.Range(0, 50)), null, culture));
        string invalidJson = string.Concat(Enumerable.Repeat("""{"$type":"unary","Type":0,"Expression":""", 150)) + """{"$type":"unknown"}""" + new string('}', 150);
        var smallThread = new Thread(() =>
        {
            try { JsonSerializer.Serialize(sum, options); }
            catch (Exception e) { writeError = e; }
            try { JsonSerializer.Deserialize<LogicalExpression>(deepJson, options); }
            catch (Exception e) { readError = e; }
            try { JsonSerializer.Deserialize<LogicalExpression>(invalidJson, options); }
            catch (Exception e) { invalidError = e; }
        }, 1024 * 1024);
        smallThread.Start();
        smallThread.Join();
        Assert.IsType<System.Text.Json.JsonException>(writeError);
        Assert.IsType<System.Text.Json.JsonException>(readError);
        Assert.Contains("unknown", Assert.IsType<System.Text.Json.JsonException>(invalidError).Message, StringComparison.Ordinal);

        // A tree within the default depth is still written and read back
        Assert.Equal(string.Join(" + ", Enumerable.Range(0, 50)), JsonSerializer.Deserialize<LogicalExpression>(sumJson)!.ToString());
    }

    // NCALC-336: a hostile document was read, and then written by ToString() as another expression (the text of a relative literal
    // and the original text of a string are written verbatim), or failed later with a .NET exception; it is now rejected on read
    [Theory]
    [InlineData("""{"$type":"value","Relative":{"Text":"2 days ago# + Sqrt(-1) + #today","Kind":5,"Days":2}}""")]
    [InlineData("""{"$type":"value","Relative":{}}""")]
    [InlineData("""{"$type":"value","Relative":{"Text":"2 days ago","Kind":5,"Days":3}}""")]
    [InlineData("""{"$type":"value","Relative":{"Text":"2 days","Kind":5,"Days":2}}""")]
    [InlineData("""{"$type":"value","Relative":{"Text":"2 days ago","Kind":99,"Days":2}}""")]
    [InlineData("""{"$type":"value","Relative":{"Text":"2  days ago","Kind":5,"Days":2}}""")]
    [InlineData("""{"$type":"value","Value":"a' + Sqrt(-1) + 'b","Type":2,"StringKind":1,"OriginalString":"a' + Sqrt(-1) + 'b"}""")]
    [InlineData("""{"$type":"value","Value":"a","Type":2,"StringKind":1,"OriginalString":"b"}""")]
    [InlineData("""{"$type":"value","Value":"a\\","Type":2,"StringKind":2,"OriginalString":"a\\"}""")]
    [InlineData("""{"$type":"value","Value":"a\" + x + @\"b","Type":2,"StringKind":4}""")]
    [InlineData("""{"$type":"value","Value":"a` + x + `b","Type":2,"StringKind":3}""")]
    [InlineData("""{"$type":"value","Value":"a","Type":2,"StringKind":99}""")]
    [InlineData("""{"$type":"value","Value":1,"Type":9999}""")]
    [InlineData("""{"$type":"value","Value":{"$type":"Int32","$value":"abc"},"Type":1}""")]
    [InlineData("""{"$type":"value","Value":{"$type":"Int32","$value":"99999999999"},"Type":1}""")]
    [InlineData("""{"$type":"binary"}""")]
    [InlineData("""{"$type":"binary","LeftExpression":{"$type":"value","Value":1,"Type":1},"RightExpression":null,"Type":0}""")]
    [InlineData("""{"$type":"binary","LeftExpression":{"$type":"value","Value":1,"Type":1},"RightExpression":{"$type":"value","Value":1,"Type":1},"Type":9999}""")]
    [InlineData("""{"$type":"unary","Type":0}""")]
    [InlineData("""{"$type":"unary","Type":9999,"Expression":{"$type":"value","Value":1,"Type":1}}""")]
    [InlineData("""{"$type":"ternary","LeftExpression":{"$type":"value","Value":1,"Type":1}}""")]
    [InlineData("""{"$type":"function","Identifier":{"Name":"Max"},"Parameters":[null]}""")]
    [InlineData("""{"$type":"function","Parameters":[]}""")]
    [InlineData("""{"$type":"function","Identifier":{},"Parameters":[]}""")]
    [InlineData("""{"$type":"identifier"}""")]
    [InlineData("""{"$type":"statements","$values":[null]}""")]
    public void HostileJsonShouldBeRejectedOnRead(string json)
    {
        Assert.Throws<System.Text.Json.JsonException>(() => JsonSerializer.Deserialize<LogicalExpression>(json));
    }

    // NCALC-336: valid relative literals and strings are still read, and a relative literal out of the range of dates is an NCalc error
    [Fact]
    public void ValidRelativeLiteralsAndStringsShouldStillBeReadFromJson()
    {
        var advancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, AdvExpressionOptions.ParseHumanePeriods);
        foreach (var text in new[] { "#today#", "#in 3 hours#", "#2 days ago#", "#1 month 2 days#", "#0 days 1 hour#", "'it\\'s' + \"a\\\"b\"", "@\"raw\" + `back`" })
        {
            var tree = LogicalExpressionFactory.Create(text, null, CultureInfo.InvariantCulture, ExpressionOptions.None, advancedOptions);
            Assert.Equal(tree.ToString(), JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(tree))!.ToString());
        }

        var farAway = JsonSerializer.Deserialize<LogicalExpression>("""{"$type":"value","Relative":{"Text":"2147483647 months ago","Kind":5,"Months":2147483647}}""")!;
        Assert.Equal("#2147483647 months ago#", farAway.ToString());
        Assert.Throws<NCalc.Exceptions.NCalcEvaluationException>(() => new Expression(farAway).Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-206: relative date and time literals were frozen at the moment of writing by ToString() and JSON
    [Theory]
    [InlineData("#today#")]
    [InlineData("#2 days ago#")]
    [InlineData("#yesterday# < #today#")]
    [InlineData("#in 3 hours# > #now#")]
    [InlineData("#1 month 2 days#")]
    public void RelativeDateLiteralsShouldStayRelative(string expression)
    {
        var advancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, AdvExpressionOptions.ParseHumanePeriods);
        var tree = LogicalExpressionFactory.Create(expression, null, CultureInfo.InvariantCulture, ExpressionOptions.None, advancedOptions);
        Assert.Equal(expression, tree.ToString());

        var restored = JsonSerializer.Deserialize<LogicalExpression>(JsonSerializer.Serialize(tree))!;
        Assert.Equal(expression, restored.ToString());
    }

    // NCALC-206: a deserialized relative literal is computed when it is evaluated, not taken from the value stored in the JSON
    [Fact]
    public void DeserializedRelativeDateLiteralShouldBeComputedWhenEvaluated()
    {
        var advancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, AdvExpressionOptions.ParseHumanePeriods);
        var tree = LogicalExpressionFactory.Create("#today#", null, CultureInfo.InvariantCulture, ExpressionOptions.None, advancedOptions);
        var json = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(tree))!;
        json["Value"] = System.Text.Json.Nodes.JsonNode.Parse("""{"$type":"DateTime","$value":"2000-01-01T00:00:00.0000000"}""");

        var restored = Assert.IsType<ValueExpression>(JsonSerializer.Deserialize<LogicalExpression>(json.ToJsonString()));
        Assert.Equal(DateTime.Today, restored.Value);

        // A constant assigned later is not written as the relative literal
        restored.Value = new DateTime(2000, 1, 1);
        Assert.Equal("#2000-01-01#", restored.ToString());
    }

    // NCALC-226: a currency literal is a decimal, and is written with the currency symbol so that it is read back as a decimal
    [Fact]
    public void CurrencyLiteralsShouldBeWrittenAsCurrency()
    {
        var enUS = CultureInfo.GetCultureInfo("en-US");
        var advancedOptions = new AdvancedExpressionOptions(enUS, AdvExpressionOptions.AcceptCurrencySymbol) { CurrencySymbolsType = AdvancedExpressionOptions.CurrencySymbolType.FromCulture };
        var tree = LogicalExpressionFactory.Create("$0.1 + $0.2 = $0.3", null, enUS, ExpressionOptions.None, advancedOptions);
        string written = tree.ToString();
        Assert.Equal("$0.1 + $0.2 = $0.3", written);
        Assert.Equal(true, new Expression(written, ExpressionOptions.NoCache, enUS) { AdvancedOptions = advancedOptions }.Evaluate(TestContext.Current.CancellationToken));

        tree = LogicalExpressionFactory.Create("-$5 + $1.25", null, enUS, ExpressionOptions.None, advancedOptions);
        Assert.Equal(-3.75m, new Expression(tree.ToString(), ExpressionOptions.NoCache, enUS) { AdvancedOptions = advancedOptions }.Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-335: after JSON, a currency literal was written with the currency symbol of the thread culture ("€0.1" under sk-SK)
    [Theory]
    [InlineData("en-US")]
    [InlineData("ja-JP")]
    [InlineData("sk-SK")]
    public void CurrencyLiteralsShouldKeepTheirSymbolInJson(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        string symbol = culture.NumberFormat.CurrencySymbol, separator = culture.NumberFormat.CurrencyDecimalSeparator;
        string expression = $"{symbol}0{separator}1 + {symbol}0{separator}2 = {symbol}0{separator}3";
        var advancedOptions = new AdvancedExpressionOptions(culture, AdvExpressionOptions.AcceptCurrencySymbol)
        {
            CurrencySymbolsType = AdvancedExpressionOptions.CurrencySymbolType.FromCulture,
            CurrencyDecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.FromCulture,
        };
        var tree = LogicalExpressionFactory.Create(expression, null, culture, ExpressionOptions.None, advancedOptions);
        string json = JsonSerializer.Serialize(tree);

        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var threadCulture in new[] { "sk-SK", "en-US", "ja-JP" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(threadCulture);
                Assert.Equal(tree.ToString(), JsonSerializer.Deserialize<LogicalExpression>(json)!.ToString());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }

        // The written text is read back with the original options
        Assert.Equal(new Expression(expression, ExpressionOptions.NoCache, culture) { AdvancedOptions = advancedOptions }.Evaluate(TestContext.Current.CancellationToken),
            new Expression(tree.ToString(), ExpressionOptions.NoCache, culture) { AdvancedOptions = advancedOptions }.Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-230: the description of a function is written as "///" lines, from which the parser reads it back
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.SupportCStyleComments)]
    [InlineData(ExpressionOptions.SupportPythonComments)]
    public void FunctionDescriptionShouldSurviveToString(ExpressionOptions commentOptions)
    {
        var options = commentOptions | ExpressionOptions.UseStatementSequences;
        foreach (var text in new[] { "/// Doubles a\nfn f(a) => a * 2; f(3)", "/// line 1\n///  line 2\nfn f(a) => a * 2; f(3)" })
        {
            var tree = (StatementSequence)LogicalExpressionFactory.Create(text, null, CultureInfo.InvariantCulture, options);
            string description = ((FunctionExpression)tree[0]).Function.Description!;
            var reparsed = (StatementSequence)LogicalExpressionFactory.Create(tree.ToString(), null, CultureInfo.InvariantCulture, options);
            Assert.Equal(description, ((FunctionExpression)reparsed[0]).Function.Description);
            Assert.Equal(tree.ToString(), reparsed.ToString());
        }
    }
}

using System.Globalization;
#nullable enable
using NCalc.Exceptions;
using NCalc.Tests.TestData;

namespace NCalc.Tests;

[Trait("Category", "Evaluations")]
public class EvaluationTests : TestBase
{
    [Theory]
    [ClassData(typeof(EvaluationTestData))]
    public void Expression_Should_Evaluate(string expression, object expected)
    {
        Assert.Equal(expected, new Expression(expression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [ClassData(typeof(ValuesTestData))]
    public void ShouldParseValues(string input, object expectedValue)
    {
        var expression = new Expression(input);

        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        if (expectedValue is double expectedDouble)
        {
            Assert.Equal(expectedDouble, (double)result!, precision: 15);
        }
        else
        {
            Assert.Equal(expectedValue, result);
        }
    }

    [Fact]
    public void ShouldEvaluateInFunction()
    {
        // The last argument should not be evaluated
        var ein = new Expression("in((2 + 2), [1], [2], 1 + 2, 4, 1 / 0)");
        ein.Parameters["1"] = 2;
        ein.Parameters["2"] = 5;

        Assert.Equal(true, ein.Evaluate(TestContext.Current.CancellationToken));

        var eout = new Expression("in((2 + 2), [1], [2], 1 + 2, 3)");
        eout.Parameters["1"] = 2;
        eout.Parameters["2"] = 5;

        Assert.Equal(false, eout.Evaluate(TestContext.Current.CancellationToken));

        // Should work with strings
        var estring = new Expression("in('to' + 'to', 'titi', 'toto')");

        Assert.Equal(true, estring.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateTernaryExpression()
    {
        Assert.Equal(1, new Expression("1+2<3 ? 3+4 : 1").Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Not_Throw_Function_Not_Found_Issue_110()
    {
        const string expressionStr = "IN([acp_associated_person_transactions], 'T', 'Z', 'A')";
        var expression = new Expression(expressionStr)
        {
            Options = ExpressionOptions.RoundAwayFromZero | ExpressionOptions.IgnoreCaseAtBuiltInFunctions,
            Parameters =
            {
                ["acp_associated_person_transactions"] = 'T'
            }
        };

        Assert.Equal(true, expression.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Evaluate_Ifs()
    {
        // Test first case true, return next value
        var eifs = new Expression("ifs([divider] != 0, [divided] / [divider], -1)");
        eifs.Parameters["divider"] = 5;
        eifs.Parameters["divided"] = 5;

        CheckResult(1d, eifs.Evaluate(TestContext.Current.CancellationToken));

        // Test first case false, no next case, return default value
        eifs = new Expression("ifs([divider] != 0, [divided] / [divider], -1)");
        eifs.Parameters["divider"] = 0;
        eifs.Parameters["divided"] = 5;

        CheckResult(-1, eifs.Evaluate(TestContext.Current.CancellationToken));

        // Test first case false, next case true, return next value (eg 4th expr)

        eifs = new Expression("ifs([number] == 3, 5, [number] == 5, 3, 8)");
        eifs.Parameters["number"] = 5;
        CheckResult(3, eifs.Evaluate(TestContext.Current.CancellationToken));

        // Test first case false, next case false, return default value (eg 5th expr)

        eifs = new Expression("ifs([number] == 3, 5, [number] == 5, 3, 8)");
        eifs.Parameters["number"] = 1337;

        CheckResult(8, eifs.Evaluate(TestContext.Current.CancellationToken));

        eifs = new Expression("ifs([number] == 3, 5, [number] == 5, 3)");
        eifs.Parameters["number"] = 1337;

        Assert.Null(eifs.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateConditional()
    {
        var eif = new Expression("if([divider] <> 0, [divided] / [divider], 0)");
        eif.Parameters["divider"] = 5;
        eif.Parameters["divided"] = 5;

        CheckResult(1d, eif.Evaluate(TestContext.Current.CancellationToken));

        eif = new Expression("if([divider] <> 0, [divided] / [divider], 0)");
        eif.Parameters["divider"] = 0;
        eif.Parameters["divided"] = 5;
        CheckResult(0, eif.Evaluate(TestContext.Current.CancellationToken));

        eif = new Expression("if([divider] <> 0, [divided] / [divider])");
        eif.Parameters["divider"] = 0;
        eif.Parameters["divided"] = 5;
        Assert.Null(eif.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldHandleCaseSensitiveness()
    {
        Assert.Equal(1M, new Expression("aBs(-1)", ExpressionOptions.DecimalAsDefault | ExpressionOptions.IgnoreCaseAtBuiltInFunctions).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(1M, new Expression("Abs(-1)", ExpressionOptions.DecimalAsDefault).Evaluate(TestContext.Current.CancellationToken));

        Assert.Throws<NCalcFunctionNotFoundException>(() => new Expression("aBs(-1)").Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldCompareDates()
    {
        var dateSeparator = CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator;
        Assert.Equal(true, new Expression($"#1{dateSeparator}1{dateSeparator}2009#==#1{dateSeparator}1{dateSeparator}2009#").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(false, new Expression($"#2{dateSeparator}1{dateSeparator}2009#==#1{dateSeparator}1{dateSeparator}2009#").Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldRoundAwayFromZero()
    {
        Assert.Equal(22d, new Expression("Round(22.5, 0)").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(23d, new Expression("Round(22.5, 0)", ExpressionOptions.RoundAwayFromZero).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateSubExpressions()
    {
        var volume = new Expression("[surface] * h");
        var surface = new Expression("[l] * [L]");
        volume.Parameters["surface"] = surface;
        volume.Parameters["h"] = 3;
        surface.Parameters["l"] = 1;
        surface.Parameters["L"] = 2;

        Assert.Equal(6, volume.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Round(1.412; 2)", 1.41)]
    [InlineData("Max(5.1; 10.2)", 10.2)]
    [InlineData("Min(1.3; 2)", 1.3)]
    [InlineData("Pow(5;2)", 25d)]
    public void ShouldAllowSemicolonAsArgumentSeparator(string expression, object expected)
    {
        Assert.Equal(expected, new Expression(expression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldHandleCurlyBraces()
    {
        var volume = new Expression("{h}");
        volume.Parameters["h"] = 3;

        Assert.Equal(3, volume.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldAllowToUseCurlyBraces()
    {
        var volume = new Expression("{surface} * h");
        var surface = new Expression("{l} * {L}");
        volume.Parameters["surface"] = surface;
        volume.Parameters["h"] = 3;
        surface.Parameters["l"] = 1;
        surface.Parameters["L"] = 2;

        Assert.Equal(6, volume.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [ClassData(typeof(NullCheckTestData))]
    public void ShouldAllowOperatorsWithNulls(string expression, object? expected)
    {
        var e = new Expression(expression, ExpressionOptions.AllowNullParameter);
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldEvaluateArrayParameters()
    {
        var e = new Expression("x * x", ExpressionOptions.IterateParameters)
        {
            Parameters =
            {
                ["x"] = new [] { 0, 1, 2, 3, 4 }
            }
        };

        var result = (IList<object>?)e.Evaluate(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(0, result[0]);
        Assert.Equal(1, result[1]);
        Assert.Equal(4, result[2]);
        Assert.Equal(9, result[3]);
        Assert.Equal(16, result[4]);
    }

    [Fact]
    public void ShouldEvaluateArrayParametersWithFunctions()
    {
        var e = new Expression("Round(x, 2)", ExpressionOptions.IterateParameters)
        {
            Parameters =
            {
                ["x"] = new [] { 0.51, 1.671, 2.237, 3.568, 4.11 }
            }
        };

        var result = (IList<object>?)e.Evaluate(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(0.51, result[0]);
        Assert.Equal(1.67, result[1]);
        Assert.Equal(2.24, result[2]);
        Assert.Equal(3.57, result[3]);
        Assert.Equal(4.11, result[4]);
    }

    [Fact]
    public void AllowNullOrEmptyExpressions()
    {
        Assert.Equal("", new Expression("", ExpressionOptions.AllowNullOrEmptyExpressions).Evaluate(TestContext.Current.CancellationToken));
        Assert.Null(new Expression((string?)null, ExpressionOptions.AllowNullOrEmptyExpressions).Evaluate(TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("01 == ''")]
    [InlineData("' ' == 01")]
    [InlineData("\" \" == 01")]
    [InlineData("\"dog\" == 01")]
    public void ShouldUseStrictTypeMatching(string expression)
    {
        Assert.False(new Expression(expression, ExpressionOptions.StrictTypeMatching).Evaluate(TestContext.Current.CancellationToken) as bool?);
    }

    [Theory]
    [InlineData("MakeStr(2, l)")]
    [InlineData("MakeStr(2, 1.5)")]
    [InlineData("MakeStr(1500000000, 'ab')")]
    [InlineData("MakeList(3000000000)")]
    public void MakeStrAndMakeListShouldValidateTheirArguments(string expression)
    {
        var e = new Expression(expression, ExpressionOptions.NoCache);
        e.Parameters["l"] = new List<object?> { 1, 2 };
        Assert.Throws<NCalcEvaluationException>(() => e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void MakeStrShouldAcceptBigIntegerCharacterCodes()
    {
        var e = new Expression("MakeStr(3, x)", ExpressionOptions.NoCache);
        e.Parameters["x"] = new System.Numerics.BigInteger(65);
        Assert.Equal("AAA", e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Round(1.2345 + 2.5678i, 2)", AdvExpressionOptions.ParseComplexNumbers, "1.23 + 2.57i")]
    [InlineData("Round([1.2345; 2.5678], 2)", AdvExpressionOptions.ParseVectors, "[1.23, 2.57]")]
    [InlineData("-[1;2]", AdvExpressionOptions.ParseVectors, "[-1, -2]")]
    [InlineData("-(1+2i)", AdvExpressionOptions.ParseComplexNumbers, "-1 - 2i")]
    [InlineData("-10%", AdvExpressionOptions.CalculatePercent, "-10%")]
    [InlineData("Abs(-10%)", AdvExpressionOptions.CalculatePercent, "10%")]
    [InlineData("Pow(i, 0.5)", AdvExpressionOptions.ParseComplexNumbers, "0.7071067811865476 + 0.7071067811865476i")]
    public void ComplexVectorAndPercentOperationsShouldWork(string expression, AdvExpressionOptions advancedOptions, string expected)
    {
        var e = new Expression(expression, ExpressionOptions.NoCache) { AdvancedOptions = new AdvancedExpressionOptions(advancedOptions) };
        Assert.Equal(expected, Convert.ToString(e.Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void LogWithFractionalBaseShouldNotTruncateTheBase()
    {
        var e = new Expression("Log(x, 2.5)", ExpressionOptions.UseBigNumbers | ExpressionOptions.NoCache);
        e.Parameters["x"] = new ExtendedNumerics.BigDecimal(8);
        Assert.Equal(Math.Log(8, 2.5), Convert.ToDouble(e.Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture), 12);
    }

    [Theory]
    [InlineData("20*5%")]
    [InlineData("100+5%")]
    [InlineData("100-5%")]
    [InlineData("20/5%")]
    public void PercentResultTypeShouldNotDependOnDynamicCode(string expression)
    {
        // The non-dynamic path (used by NCalc.AOT) returned integers where the dynamic one returns a decimal or a double
        object? Evaluate(ExpressionOptions extra)
            => new Expression(expression, ExpressionOptions.DecimalAsDefault | ExpressionOptions.NoCache | extra) { AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.CalculatePercent) }.Evaluate(TestContext.Current.CancellationToken);
        var dynamicResult = Evaluate(ExpressionOptions.None);
        var staticResult = Evaluate(ExpressionOptions.AvoidDynamicFunctions);
        Assert.Equal(dynamicResult, staticResult);
        Assert.Equal(dynamicResult?.GetType(), staticResult?.GetType());
    }

    [Theory]
    [InlineData("not 1", false)]
    [InlineData("'a' in 'abc'", true)]
    [InlineData("Ln(1.0) == 0", true)]
    [InlineData("Log2(8) == 3", true)]
    [InlineData("Trunc(1.7) == 1", true)]
    [InlineData("2147483647 * 2 == 4294967294", true)]
    [InlineData("if(false, 1) == null", true)]
    public void LambdaShouldMatchEvaluate(string expression, bool expected)
    {
        Assert.Equal(expected, new Expression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(expected, new Expression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache).ToLambda<bool>()());
    }

    [Fact]
    public void LambdaShouldHandleConditionsWithDifferentBranchTypes()
    {
        Assert.Equal(1.0, new Expression("true ? 1 : 2.5", ExpressionOptions.NoCache).ToLambda<double>()());
        Assert.Equal(2.35m, new Expression("Round(2.345, 2)", ExpressionOptions.DecimalAsDefault | ExpressionOptions.RoundAwayFromZero | ExpressionOptions.NoCache).ToLambda<decimal>()());
    }

    [Fact]
    public void CacheInsertsShouldNotScanTheWholeCache()
    {
        // NCALC-063: every insert used to scan all entries, so many distinct expressions took quadratic time
        var cache = new NCalc.Cache.LogicalExpressionCache(null);
        var alive = new List<NCalc.Domain.LogicalExpression>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 30_000; i++)
        {
            var expression = new NCalc.Domain.ValueExpression(i);
            alive.Add(expression);
            cache.Set("e" + i.ToString(CultureInfo.InvariantCulture), expression);
        }
        stopwatch.Stop();

        Assert.True(cache.TryGetValue("e29999", out var last));
        Assert.Same(alive[^1], last);
        // Milliseconds alone, a few seconds when the other tests load the machine; the quadratic version took about 2 minutes
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"30000 inserts took {stopwatch.Elapsed}");
    }
}

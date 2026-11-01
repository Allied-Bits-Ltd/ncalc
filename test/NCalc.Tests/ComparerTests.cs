using System.Numerics;

using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Helpers;

namespace NCalc.Tests;

[Trait("Category", "Comparer")]
public class ComparerTests
{
    [Fact]
    public void Should_Use_Case_Insensitive_Comparer_Issue_85()
    {
        var eif = new Expression("PageState == 'LIST'", ExpressionOptions.CaseInsensitiveStringComparer);
        eif.Parameters["PageState"] = "List";

        Assert.True((bool)eif.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null, 2, false)]
    [InlineData(2, 2L, true)]
    [InlineData("Hello", "World", false)]
    public void Compare_Using_Most_Precise_Type_Issue_102(object? a, object b, bool expectedResult)
    {
        var issueExp = new Expression("a == b")
        {
            Parameters =
            {
                ["a"] = a,
                ["b"] = b
            }
        };

        Assert.Equal(expectedResult, (bool)issueExp.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("'abc' == 'abc'")]
    [InlineData("'abc' < 'abd'")]
    [InlineData("'abd' > 'abc'")]
    [InlineData("'abd' != 'abc'")]
    public void ExpressionShouldHandleStringComparison(string expression)
    {
        var e = new Expression(expression, ExpressionOptions.NoStringTypeCoercion);
        Assert.True((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("'abc' == 'abc'")]
    [InlineData("'abc' < 'abd'")]
    [InlineData("'abd' > 'abc'")]
    [InlineData("'abd' != 'abc'")]
    public void ExpressionShouldHandleStringComparison2(string expression)
    {
        var e = new Expression(expression, ExpressionOptions.None);
        Assert.True((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExpressionShouldHandleNullLeftParameters()
    {
        var e = new Expression("null == 'a string'", ExpressionOptions.AllowNullParameter);
        Assert.False((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExpressionShouldHandleNullRightParameters()
    {
        var e = new Expression("'a string' == null", ExpressionOptions.AllowNullParameter);
        Assert.False((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExpressionShouldHandleNullBothParameters()
    {
        var e = new Expression("null == null", ExpressionOptions.AllowNullParameter);
        Assert.True((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldCompareNullToNull()
    {
        var e = new Expression("[x] = null", ExpressionOptions.AllowNullParameter);
        e.Parameters["x"] = null;

        Assert.True((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldCompareNullableToNonNullable()
    {
        var e = new Expression("[x] = 5", ExpressionOptions.AllowNullParameter);

        e.Parameters["x"] = (int?)5;
        Assert.True((bool)e.Evaluate(TestContext.Current.CancellationToken));

        e.Parameters["x"] = (int?)6;
        Assert.False((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldCompareNullableNullToNonNullable()
    {
        var e = new Expression("[x] = 5", ExpressionOptions.AllowNullParameter);

        e.Parameters["x"] = null;
        Assert.False((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldCompareNullToString()
    {
        var e = new Expression("[x] = 'foo'", ExpressionOptions.AllowNullParameter);
        e.Parameters["x"] = null;

        Assert.False((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DifferentTypesShouldEvaluateToFalse()
    {
        var e = new Expression("'' == 1", ExpressionOptions.CompareIncompatibleTypes);

        var result = e.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(false, result);
    }

    [Fact]
    public void ExpressionDoesNotDefineNullParameterWithoutNullOption()
    {
        var e = new Expression("'a string' == null");

        var ex = Assert.Throws<NCalcParameterNotDefinedException>(() => e.Evaluate(TestContext.Current.CancellationToken));
        Assert.Contains("not defined", ex.Message);
    }

    [Theory]
    [InlineData("(10/null)<0")]
    [InlineData("(10/null)>0")]
    [InlineData("(10/x)<0")]
    [InlineData("(10/x)>0")]
    public void CompareWithNullShouldReturnFalse(string expression)
    {
        var e = new Expression(expression, ExpressionOptions.AllowNullParameter);
        e.Parameters["x"] = null;

        Assert.False((bool)e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("(10/null)<0", true)]
    [InlineData("(10/null)>0", false)]
    [InlineData("(10/x)<0", true)]
    [InlineData("(10/x)>0", false)]
    public void CompareWithNullShouldEvaluate(string expression, bool expected)
    {
        var e = new Expression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.CompareNullValues);
        e.Parameters["x"] = null;

        Assert.Equal((bool)e.Evaluate(TestContext.Current.CancellationToken), expected);
    }

    [Theory]
    [InlineData("1.7", "x < 2.0", true)]
    [InlineData("5", "x == 5", true)]
    [InlineData("-5", "x < 0", true)]
    [InlineData("+5", "x == 5", true)]
    [InlineData(" 5 ", "x == 5", true)]
    [InlineData("0x10", "x == 16", true)]
    [InlineData("true", "x == true", true)]
    [InlineData("#2024-01-01#", "x == #2024-01-01#", true)]
    [InlineData("(8)", "x == 8", false)]
    [InlineData("--5", "x == 5", false)]
    [InlineData("5", "in(x, 1, 5)", true)]
    [InlineData("5", "x in (1, 5)", true)]
    [InlineData("2020-01-02", "x == 2017", false)]
    [InlineData("2020-01-02", "in(x, 2017, 1)", false)]
    [InlineData("Pow(2, 3)", "x == 8", false)]
    [InlineData("1 == 1", "x == true", false)]
    public async Task StringComparedWithNumberShouldBeConvertedOnlyWhenItIsLiteral(string value, string expression, bool expected)
    {
        var e = new Expression(expression);
        e.Parameters["x"] = value;
        Assert.Equal(expected, e.Evaluate(TestContext.Current.CancellationToken));

        var asyncExpression = new AsyncExpression(expression);
        asyncExpression.Parameters["x"] = value;
        Assert.Equal(expected, await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<object?[]> EvaluateInAllEvaluators(string expression, ExpressionOptions options, IDictionary<string, object?>? parameters = null, CultureInfo? cultureInfo = null, AdvExpressionOptions advancedOptions = AdvExpressionOptions.None)
    {
        var results = new List<object?>();
        foreach (var nonRecursive in new[] { false, true })
        {
            var o = options | ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
            var syncExpression = new Expression(expression, o, cultureInfo ?? CultureInfo.InvariantCulture);
            var asyncExpression = new AsyncExpression(expression, o, cultureInfo ?? CultureInfo.InvariantCulture);
            if (advancedOptions != AdvExpressionOptions.None)
            {
                syncExpression.AdvancedOptions = new AdvancedExpressionOptions(syncExpression.CultureInfo, advancedOptions);
                asyncExpression.AdvancedOptions = new AdvancedExpressionOptions(asyncExpression.CultureInfo, advancedOptions);
            }
            foreach (var parameter in parameters ?? new Dictionary<string, object?>())
            {
                syncExpression.Parameters[parameter.Key] = parameter.Value;
                asyncExpression.Parameters[parameter.Key] = parameter.Value;
            }
            results.Add(syncExpression.Evaluate(TestContext.Current.CancellationToken));
            results.Add(await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
        }
        return results.ToArray();
    }

    [Theory]
    [InlineData(ExpressionOptions.NoStringTypeCoercion, "'5'", 1)]
    [InlineData(ExpressionOptions.NoStringTypeCoercion, "'5'", 5)]
    [InlineData(ExpressionOptions.CompareIncompatibleTypes, "#2020-01-01#", 5)]
    public async Task ValuesThatCannotBeComparedShouldOnlyBeUnequal(ExpressionOptions options, string left, int right)
    {
        // Previously "'5' < 1" and "1 < '5'" were both true, and CompareIncompatibleTypes ordered values by their hash codes
        foreach (var (a, b) in new[] { (left, right.ToString(CultureInfo.InvariantCulture)), (right.ToString(CultureInfo.InvariantCulture), left) })
        {
            foreach (var op in new[] { "<", "<=", ">", ">=", "==" })
                Assert.All(await EvaluateInAllEvaluators($"{a} {op} {b}", options), r => Assert.Equal(false, r));
            Assert.All(await EvaluateInAllEvaluators($"{a} != {b}", options), r => Assert.Equal(true, r));
        }
    }

    public static TheoryData<string, object, object, bool> NumbersOfDifferentKindsData => new()
    {
        { "a < b", 1m, 1e300, true },
        { "a < b", decimal.MaxValue, 1e30, true },
        { "a < b", 1m, double.PositiveInfinity, true },
        { "a > b", 1m, double.NegativeInfinity, true },
        { "a > b", ulong.MaxValue, 1L, true },
        { "a > b", 9223372036854775808UL, -1, true },
        { "a == b", ulong.MaxValue, long.MaxValue, false },
        { "a < b", -1L, 0UL, true },
        { "a == b", new BigInteger(10), 1.5, false },
        { "a > b", new BigInteger(10), 1.5, true },
        { "a == b", new BigInteger(10), 10m, true },
        { "a == b", new BigInteger(10), "10", true },
        { "a == b", new BigDecimal(10), "abc", false },
        { "a < b", new BigDecimal(10), double.PositiveInfinity, true },
    };

    [Theory]
    [MemberData(nameof(NumbersOfDifferentKindsData))]
    public async Task NumbersOfDifferentKindsShouldBeComparedByValue(string expression, object a, object b, bool expected)
    {
        var results = await EvaluateInAllEvaluators(expression, ExpressionOptions.None, new Dictionary<string, object?> { ["a"] = a, ["b"] = b });
        Assert.All(results, r => Assert.Equal(expected, r));
    }

    [Fact]
    public void MathHelperCompareShouldOrderMixedNumbers()
    {
        var comparisonOptions = new ComparisonOptions(CultureInfo.InvariantCulture, ExpressionOptions.None);
        var mathHelperOptions = new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.None);

        Assert.True(MathHelper.Compare(1, new BigDecimal(2), comparisonOptions, mathHelperOptions) < 0);
        Assert.True(MathHelper.Compare(1, new BigInteger(2), comparisonOptions, mathHelperOptions) < 0);
        Assert.True(MathHelper.Compare(new BigDecimal(2), 1, comparisonOptions, mathHelperOptions) > 0);
        Assert.Equal(0, MathHelper.Compare(double.NaN, double.NaN, comparisonOptions, mathHelperOptions));

        var values = new List<object?> { 3, new BigDecimal(1), 2, new BigDecimal(5), 0 };
        values.Sort(new BoxedNumberComparer(comparisonOptions, mathHelperOptions));
        Assert.Equal([0.0, 1.0, 2.0, 3.0, 5.0], values.Select(v => MathHelper.ConvertToDouble(v, mathHelperOptions)));
    }

    [Theory]
    [InlineData("de-DE", "'1,5' == 1.5", true)]
    [InlineData("de-DE", "1.5 in ('1,5', 2)", true)]
    [InlineData("de-DE", "x LIKE '1,5'", true)]
    [InlineData("en-US", "'1,5' == 1.5", false)]
    [InlineData("en-US", "1.5 in ('1.5', 2)", true)]
    [InlineData("en-US", "x LIKE '1.5'", true)]
    [InlineData("en-US", "x in ('1.5', 2)", true)]
    public async Task ComparisonsShouldUseTheCultureOfTheExpression(string expressionCulture, string expression, bool expected)
    {
        // The result must not depend on the culture of the thread, only on the culture of the expression
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var threadCulture in new[] { "en-US", "de-DE", "sk-SK" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(threadCulture);
                var results = await EvaluateInAllEvaluators(expression, ExpressionOptions.None, new Dictionary<string, object?> { ["x"] = 1.5 }, CultureInfo.GetCultureInfo(expressionCulture));
                Assert.All(results, r => Assert.Equal(expected, r));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Theory]
    [InlineData("tr-TR", false)]
    [InlineData("en-US", true)]
    public async Task CaseInsensitiveComparisonShouldUseTheCultureOfTheExpression(string expressionCulture, bool expected)
    {
        var results = await EvaluateInAllEvaluators("'i' == 'I'", ExpressionOptions.CaseInsensitiveStringComparer, cultureInfo: CultureInfo.GetCultureInfo(expressionCulture));
        Assert.All(results, r => Assert.Equal(expected, r));
    }

    [Theory]
    [InlineData("(1+2i) == (1+2i)", AdvExpressionOptions.ParseComplexNumbers, true)]
    [InlineData("(1+2i) != (1+3i)", AdvExpressionOptions.ParseComplexNumbers, true)]
    [InlineData("i*i == -1", AdvExpressionOptions.ParseComplexNumbers, true)]
    [InlineData("10% == 10%", AdvExpressionOptions.CalculatePercent, true)]
    [InlineData("10% < 20%", AdvExpressionOptions.CalculatePercent, true)]
    [InlineData("[1;2] == [1;2]", AdvExpressionOptions.ParseVectors, true)]
    [InlineData("[1;2] == [1;3]", AdvExpressionOptions.ParseVectors, false)]
    [InlineData("[1;2] != 1", AdvExpressionOptions.ParseVectors, true)]
    public async Task ComplexNumbersVectorsAndPercentsShouldBeComparable(string expression, AdvExpressionOptions advancedOptions, bool expected)
    {
        var results = await EvaluateInAllEvaluators(expression, ExpressionOptions.None, advancedOptions: advancedOptions);
        Assert.All(results, r => Assert.Equal(expected, r));
    }

    [Fact]
    public void OrderingComplexNumbersOrVectorsShouldFailClearly()
    {
        var complex = new Expression("(1+2i) < (1+3i)", ExpressionOptions.NoCache) { AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseComplexNumbers) };
        Assert.Throws<NCalcEvaluationException>(() => complex.Evaluate(TestContext.Current.CancellationToken));

        var vector = new Expression("[1;2] < [1;3]", ExpressionOptions.NoCache) { AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseVectors) };
        Assert.Throws<NCalcEvaluationException>(() => vector.Evaluate(TestContext.Current.CancellationToken));

        var percent = new Expression("Max(10%, 20%)", ExpressionOptions.NoCache) { AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.CalculatePercent) };
        Assert.Equal(20, Convert.ToInt32(((Percent)percent.Evaluate(TestContext.Current.CancellationToken)!).Value, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BoxedNumberComparerHashShouldMatchEquality()
    {
        // NCALC-064: equal numbers of different types had different hash codes
        var options = new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.None);
        var comparer = new BoxedNumberComparer(options, options);

        object?[] values = [1, 1.0, new BigInteger(1), 1m, 1L, (byte)1, 1.0f, new BigDecimal(1), "1", true, 2, 2.5, "abc", "ABC", -0.0, 0];
        Assert.Single(new object?[] { 1, 1.0, new BigInteger(1), 1m }.Distinct(comparer));
        foreach (var x in values)
        {
            foreach (var y in values)
            {
                if (comparer.Equals(x, y))
                    Assert.True(comparer.GetHashCode(x) == comparer.GetHashCode(y), $"{x} ({x?.GetType().Name}) and {y} ({y?.GetType().Name}) are equal but have different hash codes");
            }
        }
    }

    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseNonRecursiveEvaluator)]
    public async Task StrictTypeMatchingShouldMakeDifferentTypesUnequal(ExpressionOptions evaluator)
    {
        // NCALC-067: "!=" was false for operands of different types
        var options = ExpressionOptions.StrictTypeMatching | evaluator;
        foreach (var (text, expected) in new[] { ("1 != 1.0", true), ("'1' != 1", true), ("1 == 1.0", false), ("1 < 2.0", false), ("1 != 2", true) })
        {
            Assert.Equal(expected, new Expression(text, options).Evaluate(TestContext.Current.CancellationToken));
            Assert.Equal(expected, await new AsyncExpression(text, options).EvaluateAsync(TestContext.Current.CancellationToken));
        }
    }
}
using System.Globalization;
using System.Numerics;
using ExtendedNumerics;
using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Helpers;

namespace NCalc.Tests;

/// <summary>
/// Regression tests of the sixth audit, arithmetic group: the edges left open by NCALC-401, 402, 403 and 406 (integer divided by an
/// integer percent, a numeric-string divisor under DecimalAsDefault, the hybrid conversion in percents, division by zero in "//", "\"
/// and "%", the rounding of "\" quotients beyond the range of long), NaN in BoxedNumberComparer and the remainder of big numbers.
/// Every expression is evaluated with the four evaluators (recursive and non-recursive, sync and async).
/// </summary>
[Trait("Category", "Math")]
public class ArithmeticAudit6Tests
{
    private static async Task<List<object?>> EvaluateAllAsync(string text, ExpressionOptions options, IDictionary<string, object?>? parameters = null,
        bool calculatePercent = false)
    {
        var results = new List<object?>();
        foreach (var evaluator in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var expression = new Expression(text, options | evaluator | ExpressionOptions.NoCache, CultureInfo.InvariantCulture);
            var asyncExpression = new AsyncExpression(text, options | evaluator | ExpressionOptions.NoCache, CultureInfo.InvariantCulture);
            if (calculatePercent)
            {
                expression.AdvancedOptions = new AdvancedExpressionOptions { Flags = AdvExpressionOptions.CalculatePercent };
                asyncExpression.AdvancedOptions = new AdvancedExpressionOptions { Flags = AdvExpressionOptions.CalculatePercent };
            }
            if (parameters is not null)
            {
                foreach (var parameter in parameters)
                {
                    expression.Parameters[parameter.Key] = parameter.Value;
                    asyncExpression.Parameters[parameter.Key] = parameter.Value;
                }
            }

            try
            {
                results.Add(expression.Evaluate(TestContext.Current.CancellationToken));
            }
            catch (Exception ex)
            {
                results.Add(ex);
            }

            try
            {
                results.Add(await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
            }
            catch (Exception ex)
            {
                results.Add(ex);
            }
        }
        return results;
    }

    private static async Task AssertAllAsync(object expected, string text, ExpressionOptions options, IDictionary<string, object?>? parameters = null,
        bool calculatePercent = false)
    {
        foreach (var result in await EvaluateAllAsync(text, options, parameters, calculatePercent))
        {
            Assert.False(result is Exception, $"{text} [{options}]: {result}");
            Assert.Equal(expected.GetType(), result!.GetType());
            Assert.Equal(expected, result);
        }
    }

    private static Dictionary<string, object?> Parameters(params (string Name, object? Value)[] values)
        => values.ToDictionary(v => v.Name, v => v.Value);

    public static TheoryData<ExpressionOptions> DoubleQuotientOptions =>
    [
        ExpressionOptions.None,
        ExpressionOptions.DecimalAsDefault,
        ExpressionOptions.ReduceArithmeticResultType,
        ExpressionOptions.ReduceDivResultToInteger,
        ExpressionOptions.OverflowProtection,
        ExpressionOptions.AvoidDynamicFunctions,
    ];

    // NCALC-402 edge: an integer divided by an integer percent converted the dividend to double first; the result is still a double,
    // but the nearest one to the exact quotient 100 * a / b
    [Theory]
    [MemberData(nameof(DoubleQuotientOptions))]
    public async Task IntegerDividedByIntegerPercentShouldBeTheNearestDouble(ExpressionOptions options)
    {
        await AssertAllAsync(3.0023997515803315E+17, "a / p%", options, Parameters(("a", 9007199254740995L), ("p", 3)), calculatePercent: true);
        await AssertAllAsync(1.2867427506772853E+17, "a / p%", options, Parameters(("a", 9007199254740997L), ("p", 7)), calculatePercent: true);
        await AssertAllAsync(102399.99999999999, "a / p%", options, Parameters(("a", long.MaxValue), ("p", 9007199254740993L)), calculatePercent: true);
        await AssertAllAsync(204799.99999999997, "a / p%", options, Parameters(("a", ulong.MaxValue), ("p", 9007199254740993L)), calculatePercent: true);
        await AssertAllAsync(7.771561172376095E-14, "a / p%", options, Parameters(("a", 7), ("p", 9007199254740993L)), calculatePercent: true);
        await AssertAllAsync(-3.0023997515803315E+17, "a / p%", options, Parameters(("a", 9007199254740995L), ("p", -3)), calculatePercent: true);
        await AssertAllAsync(3.0023997515803315E+17, "a /= p%; a", options | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences,
            Parameters(("a", 9007199254740995L), ("p", 3)), calculatePercent: true);
        foreach (var result in await EvaluateAllAsync("a% / p%", options, Parameters(("a", 9007199254740995L), ("p", 3)), calculatePercent: true))
            Assert.Equal(3.0023997515803315E+17, Assert.IsType<Percent>(result).Value);

        // Guards: integers that a double holds, and a zero percent, are unchanged
        await AssertAllAsync(233.33333333333334, "7 / 3%", options, calculatePercent: true);
        if (options == ExpressionOptions.None)
            await AssertAllAsync(double.PositiveInfinity, "a / 0%", options, Parameters(("a", long.MaxValue)), calculatePercent: true);
    }

    // NCALC-406 edge: with DecimalAsDefault, an integer divided by a numeric string went through a double dividend
    [Fact]
    public async Task IntegerDividedByNumericStringShouldBeExactWithDecimalAsDefault()
    {
        const ExpressionOptions options = ExpressionOptions.DecimalAsDefault;
        await AssertAllAsync(6148914691236517204.6666666667m, "9223372036854775807 / '1.5'", options);
        await AssertAllAsync(6148914691236517204.6666666667m, "a / '1.5'", options, Parameters(("a", long.MaxValue)));
        await AssertAllAsync(-6148914691236517205.3333333333m, "a / '1.5'", options, Parameters(("a", long.MinValue)));
        await AssertAllAsync(1317624576693539401m, "a / '7'", options, Parameters(("a", long.MaxValue)));
        await AssertAllAsync(12297829382473034410m, "a / '1.5'", options, Parameters(("a", ulong.MaxValue)));
        await AssertAllAsync(6004799503160662m, "a / '1.5'", options, Parameters(("a", 9007199254740993L)));
        await AssertAllAsync(6148914691236517204.6666666667m, "a /= '1.5'; a", options | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences,
            Parameters(("a", long.MaxValue)));

        // Guards: result types are unchanged
        await AssertAllAsync(3.5m, "7 / '2'", options);
        await AssertAllAsync(1m, "7 / '7'", options);
        await AssertAllAsync(3.5, "'7' / 2", options);
        await AssertAllAsync(6.148914691236517E+18, "a / '1.5'", ExpressionOptions.None, Parameters(("a", long.MaxValue)));
    }

    // NCALC-403 edge: a percent of a float and an integer under DecimalAsDefault converted the float with Convert.ToDecimal (7 digits,
    // 16777216f became 16777220, 1e-30f became 0); it now uses the hybrid conversion of arithmetic
    [Fact]
    public async Task PercentShouldConvertFloatingPointValuesAsArithmeticDoes()
    {
        const ExpressionOptions options = ExpressionOptions.DecimalAsDefault;
        await AssertAllAsync(17951621.12m, "a + 7%", options, Parameters(("a", 16777216f)), calculatePercent: true);
        await AssertAllAsync(15602810.88m, "a - 7%", options, Parameters(("a", 16777216f)), calculatePercent: true);
        await AssertAllAsync(1174412.12m, "7 + a%", options, Parameters(("a", 16777216f)), calculatePercent: true);
        await AssertAllAsync(16777216m, "a + 0%", options, Parameters(("a", 16777216f)), calculatePercent: true);

        // A value below the resolution of decimal is computed in double instead of becoming 0
        float tiny = 1e-30f;
        await AssertAllAsync((double)tiny * (100.0 + 7) / 100.0, "a + 7%", options, Parameters(("a", tiny)), calculatePercent: true);
        await AssertAllAsync((double)tiny * 7 / 100.0, "a * 7%", options, Parameters(("a", tiny)), calculatePercent: true);

        // Guards: fractional values keep their 7 (float) significant digits, so 3.7f is 3.7
        await AssertAllAsync(3.959m, "a + 7%", options, Parameters(("a", 3.7f)), calculatePercent: true);
        await AssertAllAsync(0.0075m, "a * b%", ExpressionOptions.None, Parameters(("a", 7.5m), ("b", 0.1)), calculatePercent: true);
    }

    public static TheoryData<string, ExpressionOptions, object?, object?> DivisionsByZero => new()
    {
        { "1 // 0", ExpressionOptions.None, null, null },
        { "1 \\ 0", ExpressionOptions.None, null, null },
        { "5 // 0", ExpressionOptions.UseBigNumbers, null, null },
        { "5 \\ 0", ExpressionOptions.UseBigNumbers, null, null },
        { "5.5 // 0", ExpressionOptions.DecimalAsDefault, null, null },
        { "5.5 \\ 0", ExpressionOptions.DecimalAsDefault, null, null },
        { "5 % 0", ExpressionOptions.None, null, null },
        { "5 % 0", ExpressionOptions.UseBigNumbers, null, null },
        { "5 % 0", ExpressionOptions.DecimalAsDefault, null, null },
        { "5 / 0", ExpressionOptions.UseBigNumbers, null, null },
        { "5.5 / 0", ExpressionOptions.DecimalAsDefault, null, null },
        { "a / b", ExpressionOptions.None, 5m, 0m },
        { "a % b", ExpressionOptions.None, 5m, 0m },
        { "a // b", ExpressionOptions.None, 5m, 0m },
        { "a // b", ExpressionOptions.None, new BigInteger(2), BigInteger.Zero },
        { "a \\ b", ExpressionOptions.None, new BigDecimal(75, -1), new BigDecimal(0, 0) },
        { "a % b", ExpressionOptions.None, new BigDecimal(75, -1), new BigDecimal(0, 0) },
        { "a / b", ExpressionOptions.None, new BigInteger(2), BigInteger.Zero },
        { "a \\ b", ExpressionOptions.None, 1e30, new BigDecimal(1, -40) },
        // "\" truncates the divisor to 0: also a division by zero for a dividend beyond the range of long (it gave ∞, 8 or OverflowException)
        { "1e30 \\ 1e-30", ExpressionOptions.None, null, null },
        { "1e30 \\ 1e-30", ExpressionOptions.UseBigNumbers, null, null },
        { "1e30 \\ 1e-30", ExpressionOptions.DecimalAsDefault, null, null },
        { "1e30 \\ 1e-30", ExpressionOptions.DecimalAsDefault | ExpressionOptions.UseBigNumbers, null, null },
        { "1e30 \\ 0.9", ExpressionOptions.None, null, null },
        { "a \\ b", ExpressionOptions.None, long.MaxValue, 0.5 },
        { "a \\ b", ExpressionOptions.OverflowProtection, 1e20, -0.5 },
        { "a \\ b", ExpressionOptions.None, decimal.MaxValue, 0.5 },
    };

    // "//", "\" and "%" by zero threw the .NET DivideByZeroException (as did "/" of decimals and big numbers); they throw
    // NCalcEvaluationException, with the .NET exception as the inner one, in all option sets
    [Theory]
    [MemberData(nameof(DivisionsByZero))]
    public async Task DivisionByZeroShouldThrowNCalcEvaluationException(string text, ExpressionOptions options, object? a, object? b)
    {
        foreach (var result in await EvaluateAllAsync(text, options, Parameters(("a", a), ("b", b))))
        {
            var exception = Assert.IsType<NCalcEvaluationException>(result);
            Assert.IsType<DivideByZeroException>(exception.InnerException);
        }
    }

    [Fact]
    public async Task DivisionByZeroOfDecimalPercentShouldThrowNCalcEvaluationException()
    {
        foreach (var result in await EvaluateAllAsync("a / 0%", ExpressionOptions.None, Parameters(("a", 7.5m)), calculatePercent: true))
            Assert.IsType<NCalcEvaluationException>(result);
    }

    // Guards: IEEE results of floating-point divisions are unchanged
    [Fact]
    public async Task FloatingPointDivisionByZeroShouldKeepItsIeeeResult()
    {
        await AssertAllAsync(double.PositiveInfinity, "5 / 0", ExpressionOptions.None);
        await AssertAllAsync(double.PositiveInfinity, "a // b", ExpressionOptions.None, Parameters(("a", 7.5), ("b", 0.0)));
        await AssertAllAsync(double.NaN, "a % b", ExpressionOptions.None, Parameters(("a", 7.5), ("b", 0.0)));
        await AssertAllAsync(double.NaN, "a \\ b", ExpressionOptions.None, Parameters(("a", double.NaN), ("b", 0.0)));
        await AssertAllAsync(double.PositiveInfinity, "a \\ b", ExpressionOptions.None, Parameters(("a", double.PositiveInfinity), ("b", 0.5)));
    }

    // A whole quotient beyond the range of long with a double operand is the nearest double: the cast of the BigInteger truncated it
    [Theory]
    [MemberData(nameof(DoubleQuotientOptions))]
    public async Task IntegerDivisionBeyondLongShouldBeCorrectlyRounded(ExpressionOptions options)
    {
        // trunc(1e20) / 7 = 14285714285714285714, whose nearest double is 14285714285714286592 (the cast gave 14285714285714284544)
        await AssertAllAsync(1.4285714285714287E+19, "a \\ 7", options, Parameters(("a", 1e20)));
        await AssertAllAsync(-1.4285714285714287E+19, "a \\ -7", options, Parameters(("a", 1e20)));
        await AssertAllAsync(1.4285714285714287E+19, "a \\ b", options, Parameters(("a", 1e20), ("b", 7.5)));
        await AssertAllAsync(-2.0000000000000002E+29, "a \\ b", options, Parameters(("a", 1e30), ("b", -5.5m)));
        // A decimal and a tiny double: the exact floor 7499999999999999738113410... is 7.5E+30 as the nearest double
        await AssertAllAsync(7.5E+30, "a // b", options, Parameters(("a", 7.5m), ("b", 1e-30)));
    }

    // Round-4 concern: Compare(NaN, NaN) was 0 but Equals(NaN, NaN) false. Equals and GetHashCode follow Compare (and double.Equals):
    // NaN equals NaN, so it can be found in a set or a dictionary; the "=" operator keeps the IEEE rule
    [Fact]
    public async Task BoxedNumberComparerShouldTreatNaNAsEqualToItself()
    {
        var options = new ComparisonOptions(CultureInfo.InvariantCulture, ExpressionOptions.None);
        var comparer = new BoxedNumberComparer(options, new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.None));

        Assert.Equal(0, comparer.Compare(double.NaN, double.NaN));
        Assert.True(comparer.Equals(double.NaN, double.NaN));
        Assert.True(comparer.Equals(float.NaN, double.NaN));
        Assert.True(comparer.Equals(new Percent(double.NaN), double.NaN));
        Assert.Equal(comparer.GetHashCode(double.NaN), comparer.GetHashCode(float.NaN));
        Assert.Equal(comparer.GetHashCode(double.NaN), comparer.GetHashCode(BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000001UL))));
        Assert.True(comparer.Equals(double.NaN, BitConverter.Int64BitsToDouble(unchecked((long)0xFFF8000000000001UL))));
        Assert.False(comparer.Equals(double.NaN, 1.0));
        Assert.False(comparer.Equals(double.NaN, double.PositiveInfinity));
        Assert.False(comparer.Equals(double.NaN, null));

        var set = new HashSet<object?>(comparer) { double.NaN, 1 };
        Assert.Contains(float.NaN, set);
        Assert.Equal(2, set.Count);
        set.Add(double.NaN);
        Assert.Equal(2, set.Count);

        // The operators keep IEEE 754
        await AssertAllAsync(false, "a = b", ExpressionOptions.None, Parameters(("a", double.NaN), ("b", double.NaN)));
        await AssertAllAsync(true, "a != b", ExpressionOptions.None, Parameters(("a", double.NaN), ("b", double.NaN)));
        await AssertAllAsync(false, "a in (b, 1)", ExpressionOptions.None, Parameters(("a", double.NaN), ("b", double.NaN)));
    }

    public static TheoryData<object, object, BigDecimal> BigRemainders => new()
    {
        { 7.5, new BigInteger(2), new BigDecimal(15, -1) },
        { new BigDecimal(75, -1), 2.25, new BigDecimal(75, -2) },
        { new BigDecimal(75, -1), 2.25m, new BigDecimal(75, -2) },
        { new BigDecimal(75, -1), new BigDecimal(225, -2), new BigDecimal(75, -2) },
        { 7.5m, new BigInteger(2), new BigDecimal(15, -1) },
        { new BigInteger(2), new BigDecimal(15, -1), new BigDecimal(5, -1) },
        { BigInteger.Pow(10, 30) + 7, 7.5, new BigDecimal(2, 0) },
        { BigInteger.Pow(10, 30) + 7, 0.1, new BigDecimal(0, 0) },
        { new BigDecimal(-15, -1), new BigInteger(2), new BigDecimal(-15, -1) },
        { new BigDecimal(75, -1), new BigInteger(-3), new BigDecimal(15, -1) },
        { 3.7f, new BigInteger(2), new BigDecimal(17, -1) },
        { new BigDecimal(BigInteger.Pow(10, 40) + 5, -1), new BigInteger(3), new BigDecimal(15, -1) },
        { 1e-28m, new BigDecimal(3, -29), new BigDecimal(1, -29) },
    };

    // Round-1 question (NCALC-003): a fractional "%" with a BigInteger or BigDecimal operand truncated the operands to integers; it is exact
    [Theory]
    [MemberData(nameof(BigRemainders))]
    public async Task FractionalRemainderWithBigNumbersShouldBeExact(object a, object b, BigDecimal expected)
    {
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.UseBigNumbers, ExpressionOptions.DecimalAsDefault })
        {
            foreach (var result in await EvaluateAllAsync("a % b", options, Parameters(("a", a), ("b", b))))
            {
                Assert.False(result is Exception, $"{a} % {b} [{options}]: {result}");
                Assert.True((MathHelper.ConvertToBigDecimal(result!) - expected).IsZero(), $"{a} % {b} [{options}]: {result} instead of {expected}");
            }
        }
    }
}

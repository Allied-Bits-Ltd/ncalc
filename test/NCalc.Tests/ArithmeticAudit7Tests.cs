using System.Globalization;
using System.Numerics;
using ExtendedNumerics;
using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Helpers;
using NCalcVector = NCalc.Domain.Vector;

namespace NCalc.Tests;

/// <summary>
/// Regression tests of the seventh audit round, arithmetic group: "%" of a 64-bit integer and a double with UseBigNumbers, floats read
/// with their own value when they become BigDecimals, the division of a percent by a number, and the conversions of doubles and floats
/// to BigDecimal that depended on the current culture. Every expression is evaluated with the four evaluators.
/// </summary>
[Trait("Category", "Math")]
public class ArithmeticAudit7Tests
{
    private const AdvExpressionOptions ComplexVectorsPercent = AdvExpressionOptions.ParseComplexNumbers | AdvExpressionOptions.ParseVectors | AdvExpressionOptions.CalculatePercent;

    private static async Task<List<object?>> EvaluateAllAsync(string text, ExpressionOptions options, IDictionary<string, object?>? parameters = null,
        AdvExpressionOptions advancedFlags = AdvExpressionOptions.None)
    {
        var results = new List<object?>();
        foreach (var evaluator in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var expression = new Expression(text, options | evaluator | ExpressionOptions.NoCache, CultureInfo.InvariantCulture);
            var asyncExpression = new AsyncExpression(text, options | evaluator | ExpressionOptions.NoCache, CultureInfo.InvariantCulture);
            if (advancedFlags != AdvExpressionOptions.None)
            {
                expression.AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, advancedFlags);
                asyncExpression.AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, advancedFlags);
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
        AdvExpressionOptions advancedFlags = AdvExpressionOptions.None)
    {
        foreach (var result in await EvaluateAllAsync(text, options, parameters, advancedFlags))
        {
            Assert.False(result is Exception, $"{text} [{options}]: {result}");
            Assert.Equal(expected.GetType(), result!.GetType());
            Assert.Equal(expected, result);
        }
    }

    private static Dictionary<string, object?> Parameters(params (string Name, object? Value)[] values)
        => values.ToDictionary(v => v.Name, v => v.Value);

    public static TheoryData<ExpressionOptions> BigNumberOptions =>
    [
        ExpressionOptions.UseBigNumbers,
        ExpressionOptions.UseBigNumbers | ExpressionOptions.DecimalAsDefault,
    ];

    // With UseBigNumbers, "%" of a long or ulong beyond 2^53 and a double or float converted the integer to double (9007199254740993
    // became ...992), while "/", "//" and "\" compute such pairs exactly as BigDecimals
    [Theory]
    [MemberData(nameof(BigNumberOptions))]
    public async Task RemainderOfLargeIntegerAndDoubleShouldBeExactWithBigNumbers(ExpressionOptions options)
    {
        await AssertAllAsync(3, "a % b", options, Parameters(("a", 9007199254740993L), ("b", 7.5)));
        // A fractional remainder has the type of "/" of the same pair: Double, or Decimal with DecimalAsDefault
        bool decimalResult = options.HasFlag(ExpressionOptions.DecimalAsDefault);
        await AssertAllAsync(decimalResult ? 0.25m : 0.25, "a % b", options, Parameters(("a", long.MaxValue), ("b", 2.25)));
        await AssertAllAsync(decimalResult ? -0.5m : -0.5, "a % b", options, Parameters(("a", long.MinValue), ("b", 7.5)));
        await AssertAllAsync(decimalResult ? 3.6m : 3.6, "a % b", options, Parameters(("a", ulong.MaxValue), ("b", 3.7f)));
        await AssertAllAsync(5076944378725480864L, "a % b", options, Parameters(("a", 1e30), ("b", long.MaxValue)));
        await AssertAllAsync(0, "a % b", options, Parameters(("a", long.MaxValue), ("b", 1e-30)));

        // A zero double divisor is a division by zero, as for "/", "//" and "\" of the same pair
        foreach (var result in await EvaluateAllAsync("a % b", options, Parameters(("a", 9007199254740993L), ("b", 0.0))))
            Assert.IsType<NCalcEvaluationException>(result);

        // Guards: integers that a double holds keep the double semantics, and so does the pair without UseBigNumbers
        await AssertAllAsync(2, "a % b", options, Parameters(("a", 7), ("b", 2.5)));
        await AssertAllAsync(7 % 0.1, "a % b", options, Parameters(("a", 7L), ("b", 0.1)));
        await AssertAllAsync(2, "a % b", ExpressionOptions.None, Parameters(("a", 9007199254740993L), ("b", 7.5)));
    }

    // With UseBigNumbers, a float paired with a double was widened to double before it became a BigDecimal, so 1e-30f was read as
    // 1.0000000031710769E-30; it is read with its own shortest value (1E-30), as ConvertToBigDecimal reads a float
    [Theory]
    [MemberData(nameof(BigNumberOptions))]
    public async Task FloatShouldKeepItsOwnValueAsBigDecimal(ExpressionOptions options)
    {
        await AssertAllAsync(BigInteger.Parse("7500000000000000000000000000000", CultureInfo.InvariantCulture), "a // b", options, Parameters(("a", 7.5), ("b", 1e-30f)));
        await AssertAllAsync(BigInteger.Parse("3700000000000000000000000000000", CultureInfo.InvariantCulture), "a // b", options, Parameters(("a", 3.7f), ("b", 1e-30)));
        await AssertAllAsync(BigInteger.Parse("-2500000000000000000000000000000", CultureInfo.InvariantCulture), "a // b", options, Parameters(("a", -2.5), ("b", 1e-30f)));
        await AssertAllAsync(BigInteger.Parse("42857142857142857142857142857142857142", CultureInfo.InvariantCulture), "a \\ b", options, Parameters(("a", 3e38f), ("b", 7.0)));

        // A percent of a float and a big number reads the float the same way
        foreach (var result in await EvaluateAllAsync("a + b%", options, Parameters(("a", 3.7f), ("b", new BigInteger(2))), ComplexVectorsPercent))
            Assert.True((MathHelper.ConvertToBigDecimal(result!) - new BigDecimal(3774, -3)).IsZero(), $"{result}");
    }

    [Fact]
    public void FloatVectorShouldKeepItsOwnValues()
    {
        var vector = NCalcVector.FromVector2(new System.Numerics.Vector2(3.7f, -1e-30f));
        Assert.True(vector[0] == new BigDecimal(37, -1), vector[0].ToString());
        Assert.True(vector[1] == new BigDecimal(-1, -30), vector[1].ToString());
        var vector3 = NCalcVector.FromVector3(new System.Numerics.Vector3(0.1f, 2.5f, -3f));
        Assert.True(vector3[0] == new BigDecimal(1, -1));
        Assert.True(vector3[2] == new BigDecimal(-3, 0));
    }

    // NCALC-402 edge: a% / b converted an integer percent to double before dividing (unlike a / b%); the value now comes from the exact
    // integers when one of them is beyond 2^53, with the same result type
    [Fact]
    public async Task IntegerPercentDividedByIntegerShouldNotLoseDigits()
    {
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.ReduceArithmeticResultType, ExpressionOptions.OverflowProtection, ExpressionOptions.AvoidDynamicFunctions })
        {
            await AssertPercentAsync(3002399751580331.5, "9007199254740995% / 3", options);
            await AssertPercentAsync(3002399751580331.0, "9007199254740993% / 3", options);
            await AssertPercentAsync(1286742750677284.8, "a% / 7", options, Parameters(("a", 9007199254740993L)));
            await AssertPercentAsync(3002399751580331.5, "a /= 3; a", options | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences,
                Parameters(("a", new Percent(9007199254740995L))));
            // Guards: small integers and a zero divisor are unchanged
            await AssertPercentAsync(3.5, "7% / 2", options);
            await AssertPercentAsync(double.PositiveInfinity, "9007199254740995% / 0", ExpressionOptions.None);
        }
        await AssertPercentAsync(1286742750677285.2857142857143m, "9007199254740997% / 7", ExpressionOptions.DecimalAsDefault);
        // With ReduceDivResultToInteger, a whole exact quotient is an integer, as for "/" of two integers
        await AssertPercentAsync(3002399751580331L, "9007199254740993% / 3", ExpressionOptions.ReduceDivResultToInteger);

        static async Task AssertPercentAsync(object expected, string text, ExpressionOptions options, IDictionary<string, object?>? parameters = null)
        {
            foreach (var result in await EvaluateAllAsync(text, options, parameters, ComplexVectorsPercent))
            {
                var percent = Assert.IsType<Percent>(result);
                Assert.Equal(expected.GetType(), percent.Value!.GetType());
                Assert.Equal(expected, percent.Value);
            }
        }
    }

    public static TheoryData<string> Cultures => new() { "fa-IR", "sv-SE", "de-DE" };

    private static readonly string[] CultureTexts =
    [
        "bd * d", "bd + t", "bd - d", "bd / t", "bd * f", "bd + ft", "bd / ft", "bi * d", "bi + t", "bi / f", "bd % d", "bd // t", "bd \\ f",
        "Max(bd, t)", "Min(bd, t)", "bd < t", "bd = d", "d in (bd, t)", "Abs(bd * t)", "Round(bd * d, 1)", "Sqrt(-bd * t)", "Sin(t)", "Ln(-t)",
        "Pow(bd, d)", "t * 4", "f // t", "d // ft", "l % d", "l % t", "h % l", "PercentOf(bd, t)", "PercentDiff(t, bd)", "m * t", "m / ft",
        "c * d", "c + t", "c - t", "c / t", "t / c", "c * f", "Sqrt(c)", "Abs(c)", "Ln(c)", "Log(c, 1.5)", "Pow(c, d)", "c = c", "c * 2i",
        "(1.5 - 2.5i) * -0.5", "(1 + 2i) / -2.5e-30", "-2.5e-30 + (1 + 2i)", "Sqrt(-2.25 + 0i)", "Sqrt(-3 - 4i)", "Abs(3 + 4i)", "Abs(0 + 4e-30i)",
        "[1.5, -2.5] * t", "[1.5, -2.5] + t", "[1.5, -2.5] / f", "[1.5, -2.5] * bd", "Abs([3e-30, -4e-30])", "[1, 2] = [1, 2]", "[d, t] + [f, ft]",
        "t + 10%", "bd + t%", "bd * t%", "l / d%", "f + bi%", "a% / 3",
    ];

    // The conversion of a double or float to BigDecimal by ExtendedNumerics (implicit operator, constructor) formats the number with the
    // current culture and parses it with the culture current at its first use: with fa-IR (another minus sign) or a comma culture, it
    // threw FormatException or read another number. All conversions go through MathHelper.DoubleToBigDecimal/SingleToBigDecimal.
    [Theory]
    [MemberData(nameof(Cultures))]
    public void BigNumberArithmeticShouldNotDependOnTheCurrentCulture(string cultureName)
    {
        var expected = WithCurrentCulture("en-US", Snapshot);
        var actual = WithCurrentCulture(cultureName, Snapshot);
        for (int i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i], actual[i]);

        // The public operators and comparers of complex numbers and vectors with a double operand or tolerance
        var complexExpected = WithCurrentCulture("en-US", ComplexSnapshot);
        var complexActual = WithCurrentCulture(cultureName, ComplexSnapshot);
        Assert.Equal(complexExpected, complexActual);
    }

    private static List<string> Snapshot()
    {
        var parameters = Parameters(("d", -2.5), ("t", -1e-30), ("h", -1.5e300), ("f", -3.7f), ("ft", -1e-30f), ("bd", new BigDecimal(-15, -1)),
            ("bi", new BigInteger(-7)), ("m", -1.25m), ("l", 9007199254740993L), ("a", 9007199254740995L),
            ("c", new ComplexNumber(new BigDecimal(-15, -1), new BigDecimal(25, -31))));
        var results = new List<string>();
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.UseBigNumbers, ExpressionOptions.UseBigNumbers | ExpressionOptions.DecimalAsDefault })
        {
            foreach (var text in CultureTexts)
            {
                string result;
                try
                {
                    var expression = new Expression(text, options | ExpressionOptions.NoCache, CultureInfo.InvariantCulture)
                    {
                        AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, ComplexVectorsPercent)
                    };
                    foreach (var parameter in parameters)
                        expression.Parameters[parameter.Key] = parameter.Value;
                    result = Format(expression.Evaluate(TestContext.Current.CancellationToken));
                }
                catch (Exception ex)
                {
                    result = ex.GetType().Name + ": " + ex.Message;
                }
                results.Add($"{options} {text} => {result}");
            }
        }
        return results;
    }

    private static string ComplexSnapshot()
    {
        var c = new ComplexNumber(new BigDecimal(-15, -1), new BigDecimal(25, -1));
        var tiny = new ComplexNumber(new BigDecimal(-15, -1), new BigDecimal(-25, -31));
        var v = new NCalcVector(new[] { new BigDecimal(-15, -1), new BigDecimal(1, -30) });
        string Run(Func<object> f)
        {
            try
            {
                return Format(f());
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }
        return string.Join(" | ",
            Run(() => c + -1e-30), Run(() => -1e-30 + c), Run(() => c - -2.5), Run(() => -2.5 - c), Run(() => c * -1e-30), Run(() => -2.5 * c),
            Run(() => c / -1e-30), Run(() => -2.5 / c), Run(() => (ComplexNumber)(-1e-30)), Run(() => tiny.IsReal), Run(() => tiny.CheckIsReal()),
            Run(() => new ComplexNumberToleranceComparer().Equals(c, c)), Run(() => new VectorToleranceComparer().Equals(v, v)),
            Run(() => NCalcVector.FromVector2(new System.Numerics.Vector2(-3.7f, -1e-30f))),
            Run(() => MathHelper.Multiply(new BigDecimal(-15, -1), (object)(-1e-30))), Run(() => MathHelper.Add(new BigDecimal(-15, -1), (object)(-1e-30f))),
            Run(() => MathHelper.Subtract(new BigDecimal(-15, -1), (object)(-1e-30))), Run(() => MathHelper.Divide(new BigDecimal(-15, -1), (object)(-1e-30))),
            Run(() => MathHelper.Hypot(BigDecimal.Zero, BigDecimal.Zero, MathHelperOptions.Empty)));
    }

    // A culture-independent text of a result; a BigDecimal is written from its normalized mantissa and exponent (ExtendedNumerics
    // itself may leave trailing zeros in a negative mantissa under some cultures, which does not change the value)
    private static string Format(object? value)
    {
        switch (value)
        {
            case BigDecimal bd:
                BigInteger mantissa = bd.Mantissa;
                int exponent = bd.Exponent;
                while (!mantissa.IsZero && (mantissa % 10).IsZero)
                {
                    mantissa /= 10;
                    exponent++;
                }
                return $"BigDecimal {mantissa.ToString(CultureInfo.InvariantCulture)}E{exponent.ToString(CultureInfo.InvariantCulture)}";
            case Percent percent:
                return "Percent " + Format(percent.Value);
            case IFormattable formattable:
                return $"{value.GetType().Name} {formattable.ToString(null, CultureInfo.InvariantCulture)}";
            default:
                return $"{value?.GetType().Name} {value}";
        }
    }

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
}

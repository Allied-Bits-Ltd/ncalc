using System.Numerics;

using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Helpers;

using Assert = Xunit.Assert;
using NCalcVector = NCalc.Domain.Vector;

namespace NCalc.Tests;

// Regression tests for the complex number, vector, percent and date/time findings of the fifth audit
[Trait("Category", "Math")]
public class ComplexVectorPercentAudit5Tests : TestBase
{
    private const AdvExpressionOptions Complex = AdvExpressionOptions.ParseComplexNumbers;
    private const AdvExpressionOptions Vectors = AdvExpressionOptions.ParseVectors;
    private const AdvExpressionOptions Percent = AdvExpressionOptions.CalculatePercent;

    // Evaluates in the recursive and the non-recursive evaluator, sync and async
    private static async Task<object?[]> EvaluateInAllEvaluators(string text, ExpressionOptions options, AdvExpressionOptions advancedOptions, params (string Name, object Value)[] parameters)
    {
        var results = new List<object?>();
        foreach (var evaluator in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var o = options | evaluator | ExpressionOptions.NoCache;
            var sync = new Expression(text, o, CultureInfo.InvariantCulture);
            var async = new AsyncExpression(text, o, CultureInfo.InvariantCulture);
            if (advancedOptions != AdvExpressionOptions.None)
            {
                sync.AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, advancedOptions);
                async.AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, advancedOptions);
            }
            foreach (var (name, value) in parameters)
            {
                sync.Parameters[name] = value;
                async.Parameters[name] = value;
            }
            results.Add(sync.Evaluate(TestContext.Current.CancellationToken));
            results.Add(await async.EvaluateAsync(TestContext.Current.CancellationToken));
        }
        return results.ToArray();
    }

    private static async Task AssertThrowsInAllEvaluators<T>(string text, ExpressionOptions options, AdvExpressionOptions advancedOptions, params (string Name, object Value)[] parameters) where T : Exception
    {
        foreach (var evaluator in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var o = options | evaluator | ExpressionOptions.NoCache;
            var sync = new Expression(text, o, CultureInfo.InvariantCulture);
            var async = new AsyncExpression(text, o, CultureInfo.InvariantCulture);
            if (advancedOptions != AdvExpressionOptions.None)
            {
                sync.AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, advancedOptions);
                async.AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, advancedOptions);
            }
            foreach (var (name, value) in parameters)
            {
                sync.Parameters[name] = value;
                async.Parameters[name] = value;
            }
            Assert.ThrowsAny<T>(() => sync.Evaluate(TestContext.Current.CancellationToken));
            await Assert.ThrowsAnyAsync<T>(async () => await async.EvaluateAsync(TestContext.Current.CancellationToken));
        }
    }

    // The real and imaginary parts of a result that is a complex number or a real number
    private static (BigDecimal Real, BigDecimal Imaginary) ComplexParts(object? value)
        => value is ComplexNumber cn ? (cn.Real, cn.Imaginary) : (MathHelper.ConvertToBigDecimal(value!), BigDecimal.Zero);

    private static void AssertComplex(string expectedReal, string expectedImaginary, object? actual)
    {
        var (real, imaginary) = ComplexParts(actual);
        static BigDecimal Parse(string text) => MathHelper.ConvertToBigDecimal(decimal.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture));
        Assert.True(real == Parse(expectedReal) && imaginary == Parse(expectedImaginary),
            $"{expectedReal} + {expectedImaginary}i expected, {real} + {imaginary}i ({actual?.GetType().Name}) obtained");
    }

    private static double ToDouble(object value) => MathHelper.ConvertToDouble(value, MathHelperOptions.Empty);

    // A real (not complex) result close to the expected value
    private static void AssertReal(double expected, object? actual)
    {
        Assert.False(actual is ComplexNumber, $"A real number expected, {actual} obtained");
        Assert.Equal(expected, ToDouble(actual!), 1e-12);
    }

    private static void AssertVector(object? actual, params double[] expected)
    {
        var vector = Assert.IsType<NCalcVector>(actual);
        Assert.Equal(expected.Length, vector.Dimensions);
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(expected[i], ToDouble(vector[i]), 12);
    }

    // NCALC-412: a percent of a time span worked only with an integer percent; a fractional, decimal, float or big-number percent
    // threw a conversion or "not implemented" exception
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.SupportTimeOperations)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    [InlineData(ExpressionOptions.AvoidDynamicFunctions)]
    public async Task PercentOfATimeSpanShouldAcceptAnyNumber(ExpressionOptions options)
    {
        var t = TimeSpan.FromHours(1);
        Assert.All(await EvaluateInAllEvaluators("t * 12.5%", options, Percent, ("t", t)), r => Assert.Equal(new TimeSpan(0, 7, 30), r));
        Assert.All(await EvaluateInAllEvaluators("t + 12.5%", options, Percent, ("t", t)), r => Assert.Equal(new TimeSpan(1, 7, 30), r));
        Assert.All(await EvaluateInAllEvaluators("t - 12.5%", options, Percent, ("t", t)), r => Assert.Equal(new TimeSpan(0, 52, 30), r));
        Assert.All(await EvaluateInAllEvaluators("t / 12.5%", options, Percent, ("t", t)), r => Assert.Equal(TimeSpan.FromHours(8), r));
        Assert.All(await EvaluateInAllEvaluators("t * 0.5%", options, Percent, ("t", t)), r => Assert.Equal(TimeSpan.FromSeconds(18), r));
        Assert.All(await EvaluateInAllEvaluators("t * 12%", options, Percent, ("t", t)), r => Assert.Equal(new TimeSpan(0, 7, 12), r));

        foreach (object percent in new object[] { 12.5, 12.5m, 12.5f, new BigDecimal(12.5m) })
            Assert.All(await EvaluateInAllEvaluators("t * x%", options, Percent, ("t", t), ("x", percent)), r => Assert.Equal(new TimeSpan(0, 7, 30), r));
        Assert.All(await EvaluateInAllEvaluators("t + x%", options, Percent, ("t", t), ("x", new BigInteger(15))), r => Assert.Equal(new TimeSpan(1, 9, 0), r));
        Assert.All(await EvaluateInAllEvaluators("t / x%", options, Percent, ("t", t), ("x", new BigInteger(15))), r => Assert.Equal(new TimeSpan(6, 40, 0), r));

        // A zero divisor and a result beyond the range of TimeSpan have no time span
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("t / x%", options, Percent, ("t", t), ("x", 0m));
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("t * x%", options, Percent, ("t", TimeSpan.MaxValue), ("x", 200m));
    }

    // NCALC-413: with UseBigNumbers, a percent of a double was computed through decimal and kept only about 15 digits
    [Fact]
    public async Task PercentOfADoubleWithBigNumbersShouldKeepTheDoublePrecision()
    {
        (string, object)[] parameters = [("d", 1543209862654312.5), ("d2", 0.1234567890123456789), ("f", 1234567.5f), ("l", 9007199254740993L), ("big", 1e308)];
        Assert.All(await EvaluateInAllEvaluators("d * 10%", ExpressionOptions.None, Percent, parameters), r => Assert.Equal(154320986265431.25, r));

        // With UseBigNumbers, the percent is computed exactly from the shortest text of the double (and the exact integer); the
        // result is a double when the double holds it exactly and a BigDecimal otherwise
        foreach (var options in new[] { ExpressionOptions.UseBigNumbers, ExpressionOptions.UseBigNumbers | ExpressionOptions.OverflowProtection })
        {
            foreach (var (text, expected) in new[]
            {
                ("d * 10%", 154320986265431.25m), ("d + 10%", 1697530848919743.75m), ("d - 10%", 1388888876388881.25m), ("d / 10%", 15432098626543125m),
                ("d2 + 10%", 0.135802467913580248m), ("d2 * 12.5%", 0.01543209862654321m), ("f * 10%", 123456.75m), ("l + 0.5%", 9052235251014697.965m)
            })
            {
                Assert.All(await EvaluateInAllEvaluators(text, options, Percent, parameters), r =>
                {
                    if (r is double doubleResult)
                        Assert.Equal((double)expected, doubleResult);
                    else
                        Assert.True(Assert.IsType<BigDecimal>(r) == new BigDecimal(expected), $"{text}: {expected} expected, {r} obtained");
                });
            }
            Assert.All(await EvaluateInAllEvaluators("big + 100%", options, Percent, parameters), r => Assert.True(MathHelper.ConvertToBigDecimal(r!) == new BigDecimal(2) * new BigDecimal(BigInteger.Pow(10, 308)), $"{r} obtained"));
            Assert.All(await EvaluateInAllEvaluators("big * 200%", options, Percent, parameters), r => Assert.True(MathHelper.ConvertToBigDecimal(r!) == new BigDecimal(2) * new BigDecimal(BigInteger.Pow(10, 308)), $"{r} obtained"));
        }

        // A double with a big-number percent is not rounded to 15 digits either
        (string, object)[] bigParameters = [("d2", 0.1234567890123456789), ("bd", new BigDecimal(12.5m))];
        Assert.All(await EvaluateInAllEvaluators("d2 * bd%", ExpressionOptions.None, Percent, bigParameters),
            r => Assert.Equal(0.1234567890123456789 * 0.125, ToDouble(r!)));
    }

    // NCALC-414: DateTimeOffset ± TimeSpan, DateTimeOffset - DateTimeOffset and TimeOnly - TimeOnly needed dynamic operators,
    // so they failed with AvoidDynamicFunctions and in NCalc.AOT, and DateTimeOffset.MaxValue + t was reported as "not implemented"
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.SupportTimeOperations)]
    [InlineData(ExpressionOptions.AvoidDynamicFunctions)]
    [InlineData(ExpressionOptions.AvoidDynamicFunctions | ExpressionOptions.SupportTimeOperations)]
    public async Task DateTimeOffsetAndTimeOnlyArithmeticShouldNotDependOnDynamicOperators(ExpressionOptions options)
    {
        var dto = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        (string, object)[] parameters =
        [
            ("dto", dto), ("dto2", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)), ("t", TimeSpan.FromHours(1)),
            ("ton", new TimeOnly(23, 30)), ("ton2", new TimeOnly(1, 0)), ("dtomax", DateTimeOffset.MaxValue), ("dtomin", DateTimeOffset.MinValue),
            ("tmax", TimeSpan.MaxValue)
        ];
        Assert.All(await EvaluateInAllEvaluators("dto + t", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(dto.AddHours(1), r));
        Assert.All(await EvaluateInAllEvaluators("dto - t", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(dto.AddHours(-1), r));
        Assert.All(await EvaluateInAllEvaluators("dto - dto2", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(TimeSpan.FromHours(-2), r));
        Assert.All(await EvaluateInAllEvaluators("ton - ton2", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(new TimeSpan(22, 30, 0), r));

        // A result beyond the range of the type is reported as such (NCALC-434 for TimeSpan.MaxValue + t)
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("dtomax + t", options, AdvExpressionOptions.None, parameters);
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("dtomin - t", options, AdvExpressionOptions.None, parameters);
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("tmax + t", options, AdvExpressionOptions.None, parameters);
    }

    // NCALC-416: a complex number whose imaginary part was below ComplexNumber.Tolerance was turned into a real number before
    // the arithmetic, so 1e-11i * 1e11 was 0 and (1e-11i) / (1e-11i) was NaN
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    public async Task TinyImaginaryPartsShouldTakePartInTheArithmetic(ExpressionOptions options)
    {
        Assert.All(await EvaluateInAllEvaluators("(1e-11i) / (1e-11i)", options, Complex), r => AssertComplex("1", "0", r));
        Assert.All(await EvaluateInAllEvaluators("1e-11i * 1e11", options, Complex), r => AssertComplex("0", "1", r));
        Assert.All(await EvaluateInAllEvaluators("(1e-11i) * (1e-11i)", options, Complex), r => AssertComplex("-1e-22", "0", r));
        Assert.All(await EvaluateInAllEvaluators("(2e-11+1e-11i) * 1e11", options, Complex), r => AssertComplex("2", "1", r));

        // An imaginary part that is negligible next to the real part (rounding noise) still makes a real number
        Assert.All(await EvaluateInAllEvaluators("Exp(3.141592653589793 * i) + 1", options, Complex), r => AssertReal(0, r));
        Assert.All(await EvaluateInAllEvaluators("Exp(3.141592653589793 * i) * 2", options, Complex), r => AssertReal(-2, r));
        Assert.All(await EvaluateInAllEvaluators("Exp(3.141592653589793 * i) // 1", options, Complex), r => AssertReal(-1, r));
        Assert.All(await EvaluateInAllEvaluators("Exp(3.141592653589793 * i) % 2", options, Complex), r => AssertReal(-1, r));
        Assert.All(await EvaluateInAllEvaluators("(1e-11i) // 1", options, Complex), r => AssertComplex("0", "0", r));
        Assert.All(await EvaluateInAllEvaluators("Abs(1e-11i * 1)", options, Complex), r => Assert.Equal(1e-11, ToDouble(r!), 1e-24));
    }

    // NCALC-417: Magnitude, Normalize, AngleBetween, Distance and Min/MaxByMagnitude threw InvalidCastException for a vector
    // made without UseBigNumbers
    [Fact]
    public void VectorMagnitudeShouldWorkWithoutBigNumbers()
    {
        var v = new NCalcVector(new BigDecimal[] { 3, 4 });
        Assert.Equal(5.0, ToDouble(v.Magnitude), 12);
        AssertVector(v.Normalize(), 0.6, 0.8);
        Assert.Equal(Math.PI / 2, ToDouble(NCalcVector.AngleBetween(new NCalcVector(new BigDecimal[] { 1, 0 }), new NCalcVector(new BigDecimal[] { 0, 1 }), MathHelperOptions.Empty)), 12);
        Assert.Equal(5.0, ToDouble(NCalcVector.Distance(new NCalcVector(new BigDecimal[] { 1, 1 }), new NCalcVector(new BigDecimal[] { 4, 5 }))), 12);
        Assert.Equal(v, NCalcVector.MaxByMagnitude(v, new NCalcVector(new BigDecimal[] { 1, 1 })));
        Assert.Equal(v, NCalcVector.MinByMagnitude(v, new NCalcVector(new BigDecimal[] { 6, 8 })));
        Assert.True(NCalcVector.Zero(2).Magnitude.IsZero());

        var evaluated = new Expression("[3;4]", ExpressionOptions.NoCache, CultureInfo.InvariantCulture) { AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, Vectors) }
            .Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(5.0, ToDouble(Assert.IsType<NCalcVector>(evaluated).Magnitude), 12);
    }

    // NCALC-432: the bitwise and shift operators of vectors truncated fractional components, accepted negative and fractional shift
    // counts and had no size limit, unlike the scalar operators
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    public async Task VectorBitwiseOperatorsShouldCheckTheirOperandsAsScalarsDo(ExpressionOptions options)
    {
        Assert.All(await EvaluateInAllEvaluators("[5;6] & [3;3]", options, Vectors), r => AssertVector(r, 1, 2));
        Assert.All(await EvaluateInAllEvaluators("[5;6] | [3;3]", options, Vectors), r => AssertVector(r, 7, 7));
        Assert.All(await EvaluateInAllEvaluators("[5;6] ^ [3;3]", options, Vectors), r => AssertVector(r, 6, 5));
        Assert.All(await EvaluateInAllEvaluators("~[5;-1]", options, Vectors), r => AssertVector(r, -6, 0));
        Assert.All(await EvaluateInAllEvaluators("[8;16] >> 2", options, Vectors), r => AssertVector(r, 2, 4));
        Assert.All(await EvaluateInAllEvaluators("[1;2] << 2.0", options, Vectors), r => AssertVector(r, 4, 8));

        foreach (var text in new[] { "[1.5;2] & [3;3]", "[3;3] | [1;2.5]", "[1.5;2] ^ [3;3]", "~[1.5;0]", "[8;16] << -1", "[8;16] >> -1", "[1;2] << 1.5", "[1.5;2] << 1", "[1;1] << 400000" })
            await AssertThrowsInAllEvaluators<NCalcEvaluationException>(text, options, Vectors);
    }

    // NCALC-433: Pow of a complex number with a whole exponent was computed as exp(w * log z) in double, so (1+2i) ** 2 != -3+4i
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    public async Task ComplexPowerWithAWholeExponentShouldBeExact(ExpressionOptions options)
    {
        Assert.All(await EvaluateInAllEvaluators("(1+2i) ** 2", options, Complex), r => AssertComplex("-3", "4", r));
        Assert.All(await EvaluateInAllEvaluators("(1+2i) ** 2 == -3+4i", options, Complex), r => Assert.Equal(true, r));
        Assert.All(await EvaluateInAllEvaluators("(1+i) ** 2", options, Complex), r => AssertComplex("0", "2", r));
        Assert.All(await EvaluateInAllEvaluators("(1+i) ** 8", options, Complex), r => AssertComplex("16", "0", r));
        Assert.All(await EvaluateInAllEvaluators("(1+2i) ** -1", options, Complex), r => AssertComplex("0.2", "-0.4", r));
        Assert.All(await EvaluateInAllEvaluators("Pow(1+2i, 3)", options, Complex), r => AssertComplex("-11", "-2", r));
        Assert.All(await EvaluateInAllEvaluators("(1+2i) ** 0", options, Complex), r => AssertComplex("1", "0", r));
    }

    // NCALC-434: complex, vector and time operations threw DivideByZeroException, ArgumentOutOfRangeException,
    // InvalidOperationException, NotFiniteNumberException or OverflowException instead of NCalc exceptions
    [Theory]
    [InlineData("(1+2i) / 0", Complex)]
    [InlineData("(1+2i) / (0+0i)", Complex)]
    [InlineData("Ln(0i)", Complex)]
    [InlineData("Atan(i)", Complex)]
    [InlineData("Log(1+i, 1)", Complex)]
    [InlineData("(1+2i) % 2", Complex)]
    [InlineData("(1+2i) // 2", Complex)]
    [InlineData("Exp(z)", Complex)]
    [InlineData("[1;2] / 0", Vectors)]
    [InlineData("[1;2] % 0", Vectors)]
    [InlineData("[1;2] % [1;0]", Vectors)]
    [InlineData("[1;2] * (0/0)", Vectors)]
    [InlineData("[1; 0/0]", Vectors)]
    [InlineData("[1;2] * (1/0)", Vectors)]
    [InlineData("[1;2] // 2", Vectors)]
    public async Task ComplexAndVectorOperationsShouldThrowNCalcExceptions(string text, AdvExpressionOptions advancedOptions)
    {
        (string, object)[] parameters = [("z", new ComplexNumber(new BigDecimal(800), BigDecimal.One))];
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>(text, ExpressionOptions.None, advancedOptions, parameters);
    }

    // NCALC-434: the integer division of a vector reported a failed conversion of the vector to a number
    [Fact]
    public void IntegerDivisionOfAVectorShouldSayThatItIsNotSupported()
    {
        var expression = new Expression("[1;2] // 2", ExpressionOptions.NoCache, CultureInfo.InvariantCulture) { AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, Vectors) };
        var exception = Assert.ThrowsAny<NCalcEvaluationException>(() => expression.Evaluate(TestContext.Current.CancellationToken));
        Assert.Contains("vector", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // NCALC-434: a real part beyond the range of double threw OverflowException when the complex number was used as a real number
    [Fact]
    public async Task ComplexNumberBeyondTheRangeOfDoubleShouldKeepItsValue()
    {
        Assert.All(await EvaluateInAllEvaluators("Abs(1e200i * 1e200i + 1i)", ExpressionOptions.None, Complex),
            r => Assert.True(MathHelper.ConvertToBigDecimal(r!) == new BigDecimal(BigInteger.Pow(10, 400)), $"{r} obtained"));
    }

    // NCALC-434: a time span divided by an integer or decimal zero threw DivideByZeroException
    [Fact]
    public async Task TimeSpanDividedByZeroShouldThrowNCalcException()
    {
        (string, object)[] parameters = [("t", TimeSpan.FromHours(1))];
        foreach (var text in new[] { "t / 0", "t / 0.0", "t / x" })
            await AssertThrowsInAllEvaluators<NCalcEvaluationException>(text, ExpressionOptions.SupportTimeOperations, AdvExpressionOptions.None, [.. parameters, ("x", 0m)]);
    }

    // NCALC-435: vector functions used the options stored in the vector, not those of the evaluation, so Sqrt([2;3]) had double
    // precision with UseBigNumbers, and a vector made by the host was computed with its own (default) options
    [Fact]
    public async Task VectorFunctionsShouldUseTheOptionsOfTheEvaluation()
    {
        var hostVector = new NCalcVector(new BigDecimal[] { 2, 3 });
        foreach (var text in new[] { "Sqrt([2;3])", "Sqrt(v)", "Sqrt(v * 1)" })
        {
            Assert.All(await EvaluateInAllEvaluators(text, ExpressionOptions.UseBigNumbers, Vectors, ("v", hostVector)), r =>
            {
                var vector = Assert.IsType<NCalcVector>(r);
                // The square root of 2 with more digits than a double holds
                BigDecimal error = BigDecimal.Abs(vector[0] * vector[0] - 2);
                Assert.True(error < new BigDecimal(BigInteger.One, -40), $"{text}: {vector[0]} has double precision");
            });
        }

        // Without UseBigNumbers, the components are computed in double
        Assert.All(await EvaluateInAllEvaluators("Sqrt(v)", ExpressionOptions.None, Vectors, ("v", hostVector)), r =>
            Assert.Equal(Math.Sqrt(2), ToDouble(Assert.IsType<NCalcVector>(r)[0]), 15));
    }
}

using System.Numerics;

using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Helpers;

using Assert = Xunit.Assert;

namespace NCalc.Tests;

[Trait("Category", "Math")]
public class MathFunctionAudit5Tests : TestBase
{
    private static readonly MathHelperOptions BigOptions = new(CultureInfo.InvariantCulture, ExpressionOptions.UseBigNumbers);

    private static readonly BigDecimal Ten400 = new(BigInteger.One, 400);

    // Evaluates in the recursive and the non-recursive evaluator, sync and async
    private static async Task<object?[]> EvaluateInAllEvaluators(string text, ExpressionOptions options, params (string Name, object Value)[] parameters)
    {
        var results = new List<object?>();
        foreach (var nonRecursive in new[] { false, true })
        {
            var o = options | ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
            var expression = new Expression(text, o, CultureInfo.InvariantCulture);
            var asyncExpression = new AsyncExpression(text, o, CultureInfo.InvariantCulture);
            foreach (var (name, value) in parameters)
            {
                expression.Parameters[name] = value;
                asyncExpression.Parameters[name] = value;
            }
            results.Add(expression.Evaluate(TestContext.Current.CancellationToken));
            results.Add(await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
        }
        return results.ToArray();
    }

    private static async Task AssertThrowsInAllEvaluators<T>(string text, ExpressionOptions options, string messagePart) where T : Exception
    {
        foreach (var nonRecursive in new[] { false, true })
        {
            var o = options | ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
            var sync = Assert.Throws<T>(() => new Expression(text, o, CultureInfo.InvariantCulture).Evaluate(TestContext.Current.CancellationToken));
            Assert.Contains(messagePart, sync.Message);
            var async = await Assert.ThrowsAsync<T>(async () => await new AsyncExpression(text, o, CultureInfo.InvariantCulture).EvaluateAsync(TestContext.Current.CancellationToken));
            Assert.Contains(messagePart, async.Message);
        }
    }

    // Asserts that a BigDecimal agrees with the reference to the given number of significant digits
    private static void AssertDigits(BigDecimal reference, object? actual, int digits = 95)
    {
        var value = Assert.IsType<BigDecimal>(actual);
        BigDecimal difference = BigDecimal.Abs(value - reference);
        Assert.True(difference * new BigDecimal(BigInteger.Pow(10, digits), 0) < BigDecimal.Abs(reference),
            $"{value} differs from {reference} in the first {digits} digits");
    }

    private static BigInteger IntegerSquareRoot(BigInteger n)
    {
        BigInteger x = BigInteger.One << (int)((n.GetBitLength() + 1) / 2);
        while (true)
        {
            BigInteger y = (x + n / x) / 2;
            if (y >= x)
                return x;
            x = y;
        }
    }

    // NCALC-409: √, ∛ and ∜ passed only the culture to the root functions, so UseBigNumbers was dropped: √2 was a double and
    // √(10^400) was infinite, while Sqrt gave the big-number result
    [Theory]
    [InlineData("√x", "Sqrt(x)")]
    [InlineData("√h", "Sqrt(h)")]
    public async Task RootOperatorsShouldUseTheExpressionOptions(string rootOperator, string function)
    {
        var options = ExpressionOptions.UseBigNumbers | ExpressionOptions.UseUnicodeCharsForOperations;
        (string, object)[] parameters = [("x", new BigDecimal(2)), ("h", BigInteger.Pow(10, 400))];
        var expected = (await EvaluateInAllEvaluators(function, options, parameters))[0];
        Assert.IsType<BigDecimal>(expected);
        foreach (var result in await EvaluateInAllEvaluators(rootOperator, options, parameters))
            Assert.Equal(expected, result);
    }

    // NCALC-409: the cube and fourth roots give the big-number results of Cbrt and Fthrt; a BigDecimal beyond the range of double
    // threw OverflowException
    [Fact]
    public async Task CubeAndFourthRootOperatorsShouldUseTheExpressionOptions()
    {
        var options = ExpressionOptions.UseBigNumbers | ExpressionOptions.UseUnicodeCharsForOperations;
        var h = BigInteger.Pow(10, 400);
        foreach (var result in await EvaluateInAllEvaluators("∛n", options, ("n", -h)))
            Assert.Equal(MathHelper.Cbrt(-h, BigOptions), result);
        foreach (var result in await EvaluateInAllEvaluators("∜h", options, ("h", h)))
            Assert.Equal(MathHelper.Fthrt(h, BigOptions), result);
        foreach (var result in await EvaluateInAllEvaluators("√hb", options, ("hb", Ten400)))
            AssertDigits(new BigDecimal(BigInteger.One, 200), result);
    }

    // NCALC-410: the big-number square root kept 100 decimal places, so Sqrt of a number below 1E-200 was 0
    [Theory]
    [InlineData(2, -201)]
    [InlineData(4, -300)]
    [InlineData(1, -250)]
    [InlineData(2, -150)]
    [InlineData(5, -1)]
    public async Task BigNumberSqrtOfSmallNumbersShouldKeepItsDigits(int mantissa, int exponent)
    {
        // sqrt(m * 10^e) with an even exponent, computed with 110 digits
        int evenExponent = exponent % 2 == 0 ? exponent : exponent - 1;
        BigInteger scaledMantissa = mantissa * (evenExponent == exponent ? 1 : 10);
        var reference = new BigDecimal(IntegerSquareRoot(scaledMantissa * BigInteger.Pow(10, 220)), -110 + evenExponent / 2);

        var x = new BigDecimal(mantissa, exponent);
        foreach (var result in await EvaluateInAllEvaluators("Sqrt(x)", ExpressionOptions.UseBigNumbers, ("x", x)))
            AssertDigits(reference, result);
    }

    // NCALC-410: the public Cbrt, Fthrt, hyperbolic functions and Arccot gave 0 (or failed with "divisor") for small results
    [Fact]
    public void PublicBigNumberFunctionsShouldKeepTheDigitsOfSmallResults()
    {
        AssertDigits(new BigDecimal(2, -102), MathHelper.Cbrt(new BigDecimal(8, -306), BigOptions));
        AssertDigits(new BigDecimal(-2, -102), MathHelper.Cbrt(new BigDecimal(-8, -306), BigOptions));
        AssertDigits(new BigDecimal(2, -100), MathHelper.Fthrt(new BigDecimal(16, -400), BigOptions));

        var tiny = new BigDecimal(1, -120);
        AssertDigits(tiny, MathHelper.Sinh(tiny, BigOptions));
        AssertDigits(-tiny, MathHelper.Sinh(-tiny, BigOptions));
        AssertDigits(tiny, MathHelper.Tanh(tiny, BigOptions));
        AssertDigits(new BigDecimal(1, 120), MathHelper.Coth(tiny, BigOptions));
        AssertDigits(new BigDecimal(1, 120), MathHelper.Csch(tiny, BigOptions));
        AssertDigits(tiny, MathHelper.Arccot(new BigDecimal(1, 120), BigOptions));

        // Sech(300) * Cosh(300) = 1 and Csch(-300) * Sinh(-300) = 1
        var large = new BigDecimal(300);
        AssertDigits(BigDecimal.One, (BigDecimal)MathHelper.Sech(large, BigOptions) * (BigDecimal)MathHelper.Cosh(large, BigOptions));
        AssertDigits(BigDecimal.One, (BigDecimal)MathHelper.Csch(-large, BigOptions) * (BigDecimal)MathHelper.Sinh(-large, BigOptions));
    }

    // NCALC-410: the series for small arguments agrees with the definitions; Arccot keeps the (0, π) convention
    [Fact]
    public void BigNumberHyperbolicFunctionsShouldAgreeWithTheirDefinitions()
    {
        var half = BigDecimal.OneHalf;
        var exp = (BigDecimal)MathHelper.Exp(half, BigOptions);
        var expNegative = (BigDecimal)MathHelper.Exp(-half, BigOptions);
        var sinh = (BigDecimal)MathHelper.Sinh(half, BigOptions);
        var cosh = (BigDecimal)MathHelper.Cosh(half, BigOptions);
        AssertDigits((exp - expNegative) * BigDecimal.OneHalf, sinh);
        AssertDigits(sinh, (BigDecimal)MathHelper.Tanh(half, BigOptions) * cosh);
        AssertDigits(BigDecimal.One, (BigDecimal)MathHelper.Coth(half, BigOptions) * (BigDecimal)MathHelper.Tanh(half, BigOptions));
        AssertDigits(BigDecimal.One, (BigDecimal)MathHelper.Csch(half, BigOptions) * sinh);
        AssertDigits(BigDecimal.One, (BigDecimal)MathHelper.Sech(half, BigOptions) * cosh);

        var pi = (BigDecimal)MathHelper.Acos(new BigDecimal(-1), BigOptions);
        AssertDigits((BigDecimal)MathHelper.Atan(half, BigOptions), MathHelper.Arccot(new BigDecimal(2), BigOptions));
        AssertDigits(pi - (BigDecimal)MathHelper.Atan(half, BigOptions), MathHelper.Arccot(new BigDecimal(-2), BigOptions));
        AssertDigits(pi, MathHelper.Arccot(new BigDecimal(-1, 120), BigOptions));
        AssertDigits(pi * BigDecimal.OneHalf, MathHelper.Arccot(BigDecimal.Zero, BigOptions));
    }

    // NCALC-411: a BigDecimal beyond the range of double threw OverflowException in the functions computed in double; it is now an
    // infinity of its sign, as a BigInteger of the same size is
    [Theory]
    [InlineData("Atan2(x, 1)", ExpressionOptions.UseBigNumbers)]
    [InlineData("IEEERemainder(x, 3)", ExpressionOptions.UseBigNumbers)]
    [InlineData("Pow(x, 0.5)", ExpressionOptions.UseBigNumbers)]
    [InlineData("Pow(-x, 0.5)", ExpressionOptions.UseBigNumbers)]
    [InlineData("Sin(x)", ExpressionOptions.None)]
    [InlineData("Exp(x)", ExpressionOptions.None)]
    [InlineData("Exp(-x)", ExpressionOptions.None)]
    [InlineData("Ln(x)", ExpressionOptions.None)]
    [InlineData("Log10(x)", ExpressionOptions.None)]
    [InlineData("Log(x, 10)", ExpressionOptions.None)]
    [InlineData("Atan(x)", ExpressionOptions.None)]
    [InlineData("Asin(x)", ExpressionOptions.None)]
    [InlineData("Sqrt(x)", ExpressionOptions.None)]
    [InlineData("√x", ExpressionOptions.UseUnicodeCharsForOperations)]
    public async Task BigDecimalBeyondTheRangeOfDoubleShouldBehaveAsBigInteger(string expression, ExpressionOptions options)
    {
        var expected = await EvaluateInAllEvaluators(expression, options, ("x", BigInteger.Pow(10, 400)));
        var results = await EvaluateInAllEvaluators(expression, options, ("x", Ten400));
        Assert.IsType<double>(expected[0]);
        Assert.Equal(expected, results);
    }

    // NCALC-411: the public functions with a BigDecimal argument beyond the range of double
    [Fact]
    public void PublicDoubleFunctionsShouldSaturateBigDecimals()
    {
        var options = MathHelperOptions.Empty;
        Assert.Equal(Math.PI / 2, MathHelper.Atan2(Ten400, 1, options));
        Assert.Equal(-Math.PI / 2, MathHelper.Atan2(-Ten400, 1, options));
        Assert.Equal(double.PositiveInfinity, MathHelper.Cosh(Ten400, options));
        Assert.Equal(0.0, MathHelper.Sech(Ten400, options));
        Assert.Equal(double.NaN, MathHelper.Arcsec(new BigDecimal(1, -400), options));
    }

    // NCALC-428: the double reciprocal functions threw ArgumentOutOfRangeException; they follow IEEE division as System.Math does
    [Fact]
    public void PublicReciprocalFunctionsShouldNotThrowBclExceptions()
    {
        Assert.Equal(double.PositiveInfinity, MathHelper.Cot(0.0));
        Assert.Equal(double.PositiveInfinity, MathHelper.Csc(0.0));
        Assert.Equal(double.NegativeInfinity, MathHelper.Csc(-0.0));
        Assert.Equal(double.PositiveInfinity, MathHelper.Coth(0.0));
        Assert.Equal(double.PositiveInfinity, MathHelper.Csch(0.0));
        Assert.Equal(1.0, MathHelper.Sec(0.0));
        Assert.Equal(double.NaN, MathHelper.Arcsec(0.5));
        Assert.Equal(double.NaN, MathHelper.Arccsc(0.5));
        Assert.Equal(double.NaN, MathHelper.Arcsec(0.0));
        Assert.Equal(double.NaN, MathHelper.Arccsc(0.0));
        Assert.Equal(Math.PI / 2, MathHelper.Arccsc(1.0));
        Assert.Equal(Math.PI, MathHelper.Arcsec(-1.0));
        Assert.Equal(double.PositiveInfinity, MathHelper.Cot(0.0, MathHelperOptions.Empty));
        Assert.Equal(double.NaN, MathHelper.Arcsec(0.5, MathHelperOptions.Empty));
    }

    // NCALC-428: Arcsec and Arccsc reported conversion errors as the 'Arccot' operation
    [Fact]
    public void ArcsecAndArccscShouldNameThemselvesInErrors()
    {
        var arcsec = Assert.Throws<NCalcConversionException>(() => MathHelper.Arcsec("x", MathHelperOptions.Empty));
        Assert.Contains("'Arcsec'", arcsec.Message);
        var arccsc = Assert.Throws<NCalcConversionException>(() => MathHelper.Arccsc("x", MathHelperOptions.Empty));
        Assert.Contains("'Arccsc'", arccsc.Message);
    }

    // NCALC-428: the big-number Arccsc(±1) divided by zero in ExtendedNumerics
    [Fact]
    public void BigNumberArccscAndArcsecShouldHandleTheirWholeDomain()
    {
        var halfPi = (BigDecimal)MathHelper.Acos(BigDecimal.Zero, BigOptions);
        AssertDigits(halfPi, MathHelper.Arccsc(BigDecimal.One, BigOptions));
        AssertDigits(-halfPi, MathHelper.Arccsc(new BigDecimal(-1), BigOptions));
        AssertDigits(halfPi, MathHelper.Arccsc(BigInteger.One, BigOptions));
        Assert.Equal(BigDecimal.Zero, MathHelper.Arcsec(BigDecimal.One, BigOptions));
        AssertDigits(halfPi * 2, MathHelper.Arcsec(new BigDecimal(-1), BigOptions));
        AssertDigits((BigDecimal)MathHelper.Asin(BigDecimal.OneHalf, BigOptions), MathHelper.Arccsc(new BigDecimal(2), BigOptions));
        AssertDigits((BigDecimal)MathHelper.Acos(BigDecimal.OneHalf, BigOptions), MathHelper.Arcsec(new BigDecimal(2), BigOptions));
        Assert.Equal(double.NaN, MathHelper.Arccsc(BigDecimal.OneHalf, BigOptions));
        Assert.Equal(double.NaN, MathHelper.Arcsec(BigDecimal.Zero, BigOptions));
    }

    // NCALC-429: Max and Min with a BigInteger or BigDecimal second argument failed for fractions, NaN and infinities
    [Fact]
    public void PublicMaxAndMinWithBigNumbersShouldOrderAnyNumber()
    {
        var two = new BigInteger(2);
        CheckResult(2.5, MathHelper.Max(2.5, two));
        CheckResult(2, MathHelper.Min(2.5, two));
        CheckResult(2.5m, MathHelper.Max(2.5m, two));
        CheckResult(1.5m, MathHelper.Min(1.5m, two));
        Assert.Equal(double.PositiveInfinity, MathHelper.Max(double.PositiveInfinity, two));
        Assert.Equal(double.NaN, MathHelper.Min(double.NaN, two));

        var bigTwo = new BigDecimal(2);
        Assert.Equal(double.PositiveInfinity, MathHelper.Max(double.PositiveInfinity, bigTwo));
        Assert.Equal(double.NegativeInfinity, MathHelper.Min(double.NegativeInfinity, bigTwo));
        Assert.Equal(bigTwo, MathHelper.Min(double.PositiveInfinity, bigTwo));
        Assert.Equal(double.NaN, MathHelper.Max(double.NaN, bigTwo));
        Assert.Equal(double.NaN, MathHelper.Min(float.NaN, bigTwo));

        // Integers and finite numbers are returned unchanged, as before
        Assert.Equal(5, MathHelper.Max(5, two));
        Assert.Equal(two, MathHelper.Min(5, two));
        Assert.Equal(2.5, MathHelper.Max(2.5, bigTwo));
    }

    // NCALC-430: 2 ** 332190 has exactly MaxPowResultDigits digits; the power check accepted it, then the conversion to an integer
    // rejected it, while 10 ** 99999 (also 100000 digits) was accepted
    [Theory]
    [InlineData("Pow(2, 332190)", ExpressionOptions.UseBigNumbers)]
    [InlineData("Pow(10, 99999)", ExpressionOptions.UseBigNumbers)]
    [InlineData("Pow(2, 332190)", ExpressionOptions.DecimalAsDefault)]
    public async Task PowerWithExactlyTheDigitLimitShouldBeAccepted(string expression, ExpressionOptions options)
    {
        Assert.Equal(100_000, MathHelper.MaxPowResultDigits);
        foreach (var result in await EvaluateInAllEvaluators(expression, options))
            Assert.Equal(100_000, BigInteger.Abs(Assert.IsType<BigInteger>(result)).ToString(CultureInfo.InvariantCulture).Length);
    }

    // NCALC-430: a power with one digit more than the limit is rejected by the power check, before it is computed
    [Theory]
    [InlineData("Pow(10, 100000)")]
    [InlineData("Pow(2, 332193)")]
    public async Task PowerBeyondTheDigitLimitShouldBeRejectedBeforeItIsComputed(string expression)
    {
        Assert.Equal(100_000, MathHelper.MaxPowResultDigits);
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>(expression, ExpressionOptions.UseBigNumbers, "power operation");
    }

    // e^n * 10^scale (truncated), from e computed in fixed point with guard digits
    private static BigInteger ScaledExp(int n, int scale)
    {
        const int digits = 300;
        BigInteger unity = BigInteger.Pow(10, digits);
        BigInteger e = BigInteger.Zero;
        BigInteger term = unity;
        for (int k = 1; !term.IsZero; k++)
        {
            e += term;
            term /= k;
        }
        return BigInteger.Pow(e, n) / BigInteger.Pow(10, digits * n - scale);
    }

    // NCALC-436: Sinh and Cosh of ExtendedNumerics were wrong from about the 8th digit for large arguments (Sinh(300) was
    // 9.7121318758E+129 instead of 9.7121319762E+129), and so were Tanh, Coth, Sech and Csch built on them
    [Theory]
    [InlineData(300)]
    [InlineData(20)]
    public void BigNumberHyperbolicFunctionsOfLargeArgumentsShouldKeepTheirDigits(int n)
    {
        const int scale = 200;
        BigInteger exp = ScaledExp(n, scale);
        BigInteger inverse = BigInteger.Pow(10, 2 * scale) / exp;
        var sinh = new BigDecimal(exp - inverse, -scale) * BigDecimal.OneHalf;
        var cosh = new BigDecimal(exp + inverse, -scale) * BigDecimal.OneHalf;
        var x = new BigDecimal(n);

        AssertDigits(sinh, MathHelper.Sinh(x, BigOptions));
        AssertDigits(cosh, MathHelper.Cosh(x, BigOptions));
        AssertDigits(-sinh, MathHelper.Sinh(-x, BigOptions));
        AssertDigits(new BigDecimal((exp - inverse) * BigInteger.Pow(10, scale) / (exp + inverse), -scale), MathHelper.Tanh(x, BigOptions));
        AssertDigits(new BigDecimal((exp + inverse) * BigInteger.Pow(10, scale) / (exp - inverse), -scale), MathHelper.Coth(x, BigOptions));
        // sech = 2 / (e^n + e^-n) and csch = 2 / (e^n - e^-n), with 400 decimal places
        AssertDigits(new BigDecimal(2 * BigInteger.Pow(10, scale + 400) / (exp + inverse), -400), MathHelper.Sech(x, BigOptions));
        AssertDigits(new BigDecimal(-2 * BigInteger.Pow(10, scale + 400) / (exp - inverse), -400), MathHelper.Csch(-x, BigOptions));

        // The complex functions use them: sinh(n + 0i) = sinh n, cosh(n + 0i) = cosh n
        AssertDigits(sinh, ComplexNumber.Sinh(new ComplexNumber(x, BigDecimal.Zero, BigOptions), BigOptions).Real);
        AssertDigits(cosh, ComplexNumber.Cosh(new ComplexNumber(x, BigDecimal.Zero, BigOptions), BigOptions).Real);
    }
}

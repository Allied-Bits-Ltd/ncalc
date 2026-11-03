using System.Numerics;

using ExtendedNumerics;

using NCalc.Helpers;

using Assert = Xunit.Assert;

namespace NCalc.Tests;

[Trait("Category", "Math")]
public class MathFunctionAudit6Tests : TestBase
{
    private static readonly MathHelperOptions BigOptions = new(CultureInfo.InvariantCulture, ExpressionOptions.UseBigNumbers);

    private static readonly BigDecimal OnePlus1E30 = new(BigInteger.Pow(10, 30) + 1, -30);

    private static readonly BigDecimal OnePlus1E151 = new(BigInteger.Pow(10, 151) + 1, -151);

    private static readonly BigDecimal HalfPi = Reference("1570796326794896619231321691639751442098584699687552910487472296153908203143104499314017412671058534", -99);

    private static BigDecimal Reference(string mantissa, int exponent) => new(BigInteger.Parse(mantissa, CultureInfo.InvariantCulture), exponent);

    // The result agrees with the 100-digit reference (from mpmath) to 98 significant digits
    private static void AssertDigits(BigDecimal expected, object? actual)
    {
        var value = Assert.IsType<BigDecimal>(actual);
        BigDecimal error = BigDecimal.Abs(value - expected) * new BigDecimal(BigInteger.One, 98);
        Assert.True(error < BigDecimal.Abs(expected), $"{value.Mantissa}E{value.Exponent} differs from {expected.Mantissa}E{expected.Exponent}");
    }

    public static TheoryData<string, BigDecimal> SmallResults => new()
    {
        { "Ln(1 + 1E-30)", Reference("9999999999999999999999999999995000000000000000000000000000003333333333333333333333333333330833333333", -130) },
        { "Log10(1 - 1E-40)", Reference("-4342944819032518276511289189166050822944187205277617287058363396118104794629854960271634056775768965", -140) },
        { "Log(1 + 1E-30, 7)", Reference("5138983423697506930446493893699324374801542895856391007108174561838451551622102876437990752722087396", -130) },
        { "Ln(1 + 1E-151)", Reference("1000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000", -250) },
        { "Atan(1E-30)", Reference("9999999999999999999999999999999999999999999999999999999999996666666666666666666666666666666666666667", -130) },
        { "Arccot(1E+44)", Reference("9999999999999999999999999999999999999999999999999999999999999999999999999999999999999999666666666667", -144) },
        { "Asin(3E-20)", Reference("3000000000000000000000000000000000000000450000000000000000000000000000000000000182250000000000000000", -119) },
        { "Acos(1 - 1E-40)", Reference("1414213562373095048801688724209698078569683660489967848968753085396767559279428453047847385173206700", -119) },
        { "Arcsec(1 + 1E-151)", Reference("4472135954999579392818347337462552470881236719223051448541794490821041851275609798828828816757564550", -175) },
        { "Arccsc(-1 - 1E-30)", Reference("-1570796326794895205017759318544702640409860490578729991804386522873205328217027304757347796010049260", -99) },
        { "Arcsec(-1 - 1E-30)", Reference("3141592653589791824249081010184454082508445190266282902291858819027113531360131804071365208681107794", -99) },
    };

    [Theory]
    [MemberData(nameof(SmallResults))]
    public void BigNumberFunctionsShouldKeepTheDigitsOfSmallResults(string function, BigDecimal expected)
    {
        // Results near zero (logarithms near 1, Atan and Asin of small arguments, Acos and Arcsec near ±1) had the working
        // precision in decimal places, so 1E-30 kept 70 significant digits, and Arcsec and Arccsc near ±1 lost the digits of x - 1
        // beyond the precision of 1/x
        object? actual = function switch
        {
            "Ln(1 + 1E-30)" => MathHelper.Ln(OnePlus1E30, BigOptions),
            "Log10(1 - 1E-40)" => MathHelper.Log10(new BigDecimal(BigInteger.Pow(10, 40) - 1, -40), BigOptions),
            "Log(1 + 1E-30, 7)" => MathHelper.Log(OnePlus1E30, new BigDecimal(7), BigOptions),
            "Ln(1 + 1E-151)" => MathHelper.Ln(OnePlus1E151, BigOptions),
            "Atan(1E-30)" => MathHelper.Atan(new BigDecimal(1, -30), BigOptions),
            "Arccot(1E+44)" => MathHelper.Arccot(new BigDecimal(1, 44), BigOptions),
            "Asin(3E-20)" => MathHelper.Asin(new BigDecimal(3, -20), BigOptions),
            "Acos(1 - 1E-40)" => MathHelper.Acos(new BigDecimal(BigInteger.Pow(10, 40) - 1, -40), BigOptions),
            "Arcsec(1 + 1E-151)" => MathHelper.Arcsec(OnePlus1E151, BigOptions),
            "Arccsc(-1 - 1E-30)" => MathHelper.Arccsc(-OnePlus1E30, BigOptions),
            "Arcsec(-1 - 1E-30)" => MathHelper.Arcsec(-OnePlus1E30, BigOptions),
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        };
        AssertDigits(expected, actual);
    }

    [Fact]
    public void BigNumberFunctionsOfHugeAndTinyExponentsShouldFinish()
    {
        // Comparisons and subtractions of ExtendedNumerics align the exponents, which multiplied out 10^2000000000 (OutOfMemoryException)
        var tiny = new BigDecimal(3, -2000000000);
        Assert.Equal(BigDecimal.One, MathHelper.Exp(tiny, BigOptions));
        Assert.Equal(BigDecimal.One, MathHelper.Exp(-tiny, BigOptions));
        Assert.Equal(tiny, MathHelper.Asin(tiny, BigOptions));
        Assert.Equal(-tiny, MathHelper.Atan(-tiny, BigOptions));
        AssertDigits(HalfPi, MathHelper.Acos(tiny, BigOptions));
        AssertDigits(HalfPi, MathHelper.Arcsec(new BigDecimal(1, 2000000000), BigOptions));
        Assert.Equal(double.NaN, MathHelper.Asin(new BigDecimal(1, 2000000000), BigOptions));
        Assert.Equal(double.NaN, MathHelper.Log(-tiny, new BigDecimal(10), BigOptions));
        AssertDigits(Reference("-9999999998494850021680094023931305526377534866159050592689457293447862694364459053627877452565363739", -100),
            MathHelper.Log(new BigDecimal(2, -2000000000), new BigDecimal(1, 2000000000), BigOptions));
    }

    [Fact]
    public void BigNumberCotAndCscOfZeroShouldBeInfinity()
    {
        // As double Cot/Csc and big-number Coth/Csch of zero (they threw NCalcEvaluationException)
        Assert.Equal(double.PositiveInfinity, MathHelper.Cot(BigDecimal.Zero, BigOptions));
        Assert.Equal(double.PositiveInfinity, MathHelper.Csc(BigInteger.Zero, BigOptions));
        Assert.Equal(double.PositiveInfinity, MathHelper.Coth(BigDecimal.Zero, BigOptions));
        Assert.Equal(double.PositiveInfinity, MathHelper.Cot(0.0, BigOptions));
    }
}

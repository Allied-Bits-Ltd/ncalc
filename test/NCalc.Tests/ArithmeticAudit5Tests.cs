using System.Globalization;
using System.Numerics;
using ExtendedNumerics;
using NCalc.Exceptions;

namespace NCalc.Tests;

/// <summary>
/// Regression tests of the fifth audit, arithmetic group (NCALC-401, 402, 403, 406, 418, 419, 421, 424). Every expression is evaluated
/// with the four evaluators (recursive and non-recursive, sync and async).
/// </summary>
[Trait("Category", "Math")]
public class ArithmeticAudit5Tests
{
    private static async Task<List<object?>> EvaluateAllAsync(string text, ExpressionOptions options, IDictionary<string, object?>? parameters = null,
        bool calculatePercent = false, CultureInfo? culture = null)
    {
        var results = new List<object?>();
        foreach (var evaluator in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var expression = new Expression(text, options | evaluator | ExpressionOptions.NoCache, culture ?? CultureInfo.InvariantCulture);
            var asyncExpression = new AsyncExpression(text, options | evaluator | ExpressionOptions.NoCache, culture ?? CultureInfo.InvariantCulture);
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
        bool calculatePercent = false, CultureInfo? culture = null)
    {
        foreach (var result in await EvaluateAllAsync(text, options, parameters, calculatePercent, culture))
        {
            Assert.False(result is Exception, $"{text} [{options}]: {result}");
            Assert.Equal(expected.GetType(), result!.GetType());
            Assert.Equal(expected, result);
        }
    }

    // The exact integer value of a whole result: a double is compared with the nearest double of the expected value
    private static bool IsWholeValue(object? result, BigInteger expected)
    {
        switch (result)
        {
            case double d:
                return d == (double)expected;
            case float f:
                return f == (float)expected;
            case decimal m:
                return decimal.Truncate(m) == m && new BigInteger(m) == expected;
            case BigInteger bi:
                return bi == expected;
            case BigDecimal bd:
                if (bd.Exponent >= 0)
                    return bd.Mantissa * BigInteger.Pow(10, bd.Exponent) == expected;
                BigInteger quotient = BigInteger.DivRem(bd.Mantissa, BigInteger.Pow(10, -bd.Exponent), out BigInteger remainder);
                return remainder.IsZero && quotient == expected;
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                return BigInteger.Parse(Convert.ToString(result, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture) == expected;
            default:
                return false;
        }
    }

    // An exact rational number
    private readonly record struct Rational(BigInteger Numerator, BigInteger Denominator)
    {
        public static Rational Of(BigInteger value) => new(value, BigInteger.One);

        public static Rational Of(decimal value)
        {
            int scale = (decimal.GetBits(value)[3] >> 16) & 0xFF;
            BigInteger denominator = BigInteger.Pow(10, scale);
            return new(Scaled(value, scale), denominator);
        }

        public Rational DividedBy(Rational divisor)
        {
            BigInteger numerator = Numerator * divisor.Denominator, denominator = Denominator * divisor.Numerator;
            return denominator.Sign < 0 ? new(-numerator, -denominator) : new(numerator, denominator);
        }

        private static BigInteger Scaled(decimal value, int scale)
        {
            int[] bits = decimal.GetBits(value);
            BigInteger mantissa = new BigInteger((uint)bits[0]) | (new BigInteger((uint)bits[1]) << 32) | (new BigInteger((uint)bits[2]) << 64);
            return value < 0 ? -mantissa : mantissa;
        }

        public static Rational Of(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int exponent = (int)((bits >> 52) & 0x7FF);
            long fraction = bits & 0xFFFFFFFFFFFFFL;
            if (exponent == 0)
                exponent++;
            else
                fraction |= 1L << 52;
            exponent -= 1075;
            BigInteger numerator = fraction;
            BigInteger denominator = BigInteger.One;
            if (exponent > 0)
                numerator <<= exponent;
            else
                denominator <<= -exponent;
            return new(negative ? -numerator : numerator, denominator);
        }

        public static Rational Of(BigDecimal value)
            => value.Exponent >= 0
                ? new(value.Mantissa * BigInteger.Pow(10, value.Exponent), BigInteger.One)
                : new(value.Mantissa, BigInteger.Pow(10, -value.Exponent));

        public BigInteger Floor()
        {
            BigInteger quotient = BigInteger.DivRem(Numerator, Denominator, out BigInteger remainder);
            return !remainder.IsZero && (remainder.Sign < 0) != (Denominator.Sign < 0) ? quotient - 1 : quotient;
        }

        public BigInteger Truncate() => BigInteger.Divide(Numerator, Denominator);
    }

    private static Rational ExactValue(object value, object partner)
        => value switch
        {
            decimal m => Rational.Of(m),
            double d when partner is decimal && Math.Floor(d) != d => Rational.Of((decimal)d),
            double d => Rational.Of(d),
            BigInteger bi => Rational.Of(bi),
            BigDecimal bd => Rational.Of(bd),
            _ => Rational.Of(BigInteger.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture))
        };

    public static readonly object[] DivisionOperands =
    [
        7, -7, int.MaxValue,
        9007199254740993L, long.MinValue, -9223372036854775807L,
        ulong.MaxValue, 18446744073709551614UL,
        decimal.MaxValue, 79228162514264337593543950334m, 6.9999999999999999999999999999m, -0.0000000000000000000000000001m, 1m,
        9007199254740992.0, 2251799813685248.5, -7.5, 0.75,
        BigInteger.Pow(10, 30) + 7, -BigInteger.Pow(2, 70),
        new BigDecimal(BigInteger.Parse("12345678901234567890123456789", CultureInfo.InvariantCulture), -9), new BigDecimal(-25, -1)
    ];

    public static TheoryData<ExpressionOptions> DivisionOptions =>
    [
        ExpressionOptions.None,
        ExpressionOptions.DecimalAsDefault,
        ExpressionOptions.UseBigNumbers,
        ExpressionOptions.DecimalAsDefault | ExpressionOptions.UseBigNumbers,
        ExpressionOptions.ReduceArithmeticResultType
    ];

    private static bool IsIntegerOrDouble(object value) => value is not (decimal or BigInteger or BigDecimal);

    [Theory]
    [MemberData(nameof(DivisionOptions))]
    public async Task IntegerDivisionsShouldBeExact(ExpressionOptions options)
    {
        // NCALC-401: a // b is floor(a / b) and a \ b is trunc(trunc(a) / trunc(b)), computed exactly, for every pair of operand types.
        // Without UseBigNumbers, a pair of doubles (or an integer and a double) is divided in double, as "/" divides it; with a decimal,
        // a fractional double has its 15 significant digits
        bool big = options.HasFlag(ExpressionOptions.UseBigNumbers);
        var failures = new List<string>();
        foreach (var a in DivisionOperands)
        {
            foreach (var b in DivisionOperands)
            {
                bool inDouble = !big && (a is double || b is double) && IsIntegerOrDouble(a) && IsIntegerOrDouble(b);
                Rational exactA = inDouble ? Rational.Of(Convert.ToDouble(a, CultureInfo.InvariantCulture)) : ExactValue(a, b);
                Rational exactB = inDouble ? Rational.Of(Convert.ToDouble(b, CultureInfo.InvariantCulture)) : ExactValue(b, a);

                // //
                if (!exactB.Numerator.IsZero)
                {
                    BigInteger expected = inDouble
                        ? new BigInteger(Math.Floor(Convert.ToDouble(a, CultureInfo.InvariantCulture) / Convert.ToDouble(b, CultureInfo.InvariantCulture)))
                        : exactA.DividedBy(exactB).Floor();
                    foreach (var result in await EvaluateAllAsync("a // b", options, new Dictionary<string, object?> { ["a"] = a, ["b"] = b }))
                    {
                        if (!IsWholeValue(result, expected))
                            failures.Add($"{a} ({a.GetType().Name}) // {b} ({b.GetType().Name}) = {result} ({result?.GetType().Name}), expected {expected}");
                    }
                }

                // \
                BigInteger truncatedB = exactB.Truncate();
                if (!truncatedB.IsZero)
                {
                    BigInteger expected = BigInteger.Divide(exactA.Truncate(), truncatedB);
                    foreach (var result in await EvaluateAllAsync("a \\ b", options, new Dictionary<string, object?> { ["a"] = a, ["b"] = b }))
                    {
                        if (!IsWholeValue(result, expected))
                            failures.Add($"{a} ({a.GetType().Name}) \\ {b} ({b.GetType().Name}) = {result} ({result?.GetType().Name}), expected {expected}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct().Take(40)) + $"{Environment.NewLine}({failures.Count} failures)");
    }

    [Theory]
    [InlineData("9223372036854775807 // 3", ExpressionOptions.UseBigNumbers, "3074457345618258602")]
    [InlineData("9007199254740993 // -2", ExpressionOptions.UseBigNumbers, "-4503599627370497")]
    [InlineData("-9223372036854775807 // 2147483647", ExpressionOptions.UseBigNumbers, "-4294967299")]
    [InlineData("6.9999999999999999999999999999 // 7", ExpressionOptions.DecimalAsDefault, "0")]
    [InlineData("-0.0000000000000000000000000001 // 3", ExpressionOptions.DecimalAsDefault, "-1")]
    [InlineData("79228162514264337593543950334 \\ 7", ExpressionOptions.DecimalAsDefault, "11318308930609191084791992904")]
    public async Task IntegerDivisionOfLiteralsShouldBeExact(string text, ExpressionOptions options, string expected)
    {
        // NCALC-401
        foreach (var result in await EvaluateAllAsync(text, options))
            Assert.True(IsWholeValue(result, BigInteger.Parse(expected, CultureInfo.InvariantCulture)), $"{text}: {result}");
    }

    [Fact]
    public async Task FloorDivisionOfDoubleShouldNotBeRoundedToDecimal()
    {
        // NCALC-401: with DecimalAsDefault, a double quotient is floored as it is, not after a conversion to 15 decimal digits
        foreach (var result in await EvaluateAllAsync("a // 1", ExpressionOptions.DecimalAsDefault, new Dictionary<string, object?> { ["a"] = 0.99999999999999989 }))
            Assert.True(IsWholeValue(result, 0), $"{result}");
        foreach (var result in await EvaluateAllAsync("a // 200", ExpressionOptions.DecimalAsDefault, new Dictionary<string, object?> { ["a"] = 9007199254740992.0 }))
            Assert.True(IsWholeValue(result, 45035996273704), $"{result}");
    }

    [Theory]
    [InlineData("9223372036854775807 / 1", ExpressionOptions.ReduceDivResultToInteger)]
    [InlineData("9223372036854775807 / 1", ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.ReduceArithmeticResultType)]
    [InlineData("9223372036854775807 / 1", ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.UseBigNumbers)]
    [InlineData("9223372036854775807 / 1", ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.UseBigNumbers | ExpressionOptions.ReduceArithmeticResultType)]
    public async Task WholeQuotientOfIntegersShouldBeExact(string text, ExpressionOptions options)
    {
        // NCALC-402, NCALC-418: the dividend is not rounded to double, and ReduceDivResultToInteger also applies with UseBigNumbers alone
        foreach (var result in await EvaluateAllAsync(text, options))
        {
            Assert.True(result is long or ulong, $"{result} ({result?.GetType().Name})");
            Assert.True(IsWholeValue(result, long.MaxValue), $"{result}");
        }
    }

    [Theory]
    [InlineData(ExpressionOptions.ReduceDivResultToInteger)]
    [InlineData(ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.DecimalAsDefault)]
    public async Task ReducedQuotientOfLongIntegersShouldBeExact(ExpressionOptions options)
    {
        // NCALC-402
        var parameters = new Dictionary<string, object?> { ["a"] = ulong.MaxValue, ["b"] = 255 };
        foreach (var result in await EvaluateAllAsync("a / b", options, parameters))
            Assert.True(result is not double && IsWholeValue(result, 72340172838076673), $"{result} ({result?.GetType().Name})");
        foreach (var result in await EvaluateAllAsync("a /= b; a", options | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences, parameters))
            Assert.True(result is not double && IsWholeValue(result, 72340172838076673), $"{result} ({result?.GetType().Name})");
        foreach (var result in await EvaluateAllAsync("9007199254740993 / 3", options))
            Assert.True(result is not double && IsWholeValue(result, 3002399751580331), $"{result} ({result?.GetType().Name})");
    }

    [Fact]
    public async Task ReduceDivResultToIntegerShouldApplyWithBigNumbers()
    {
        // NCALC-418: 6 / 3 is the integer 2 with ReduceDivResultToInteger, also with UseBigNumbers; a fractional quotient stays a double
        await AssertAllAsync(2, "6 / 3", ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.UseBigNumbers);
        await AssertAllAsync(2, "6 / 3", ExpressionOptions.ReduceDivResultToInteger);
        await AssertAllAsync(3.5, "7 / 2", ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.UseBigNumbers);
        await AssertAllAsync(3.5, "7 / 2", ExpressionOptions.UseBigNumbers);
    }

    [Fact]
    public async Task QuotientOfLongIntegersShouldKeepItsDigits()
    {
        // NCALC-402: the documented result types are kept (a double with UseBigNumbers alone), but the digits come from the exact operands
        await AssertAllAsync(1317624576693539401m, "9223372036854775807 / 7", ExpressionOptions.DecimalAsDefault | ExpressionOptions.UseBigNumbers);
        await AssertAllAsync(9223372036854775807m, "9223372036854775807 / 1", ExpressionOptions.DecimalAsDefault | ExpressionOptions.UseBigNumbers);
        await AssertAllAsync(1431157948607658.1, "180325901524564923 / 126", ExpressionOptions.None);
        await AssertAllAsync(3002399751580331.0, "9007199254740993 / 3", ExpressionOptions.UseBigNumbers);

        foreach (var result in await EvaluateAllAsync("a / b", ExpressionOptions.UseBigNumbers, new Dictionary<string, object?> { ["a"] = ulong.MaxValue, ["b"] = 1.0 }))
            Assert.True(IsWholeValue(result, ulong.MaxValue), $"{result} ({result?.GetType().Name})");
        foreach (var result in await EvaluateAllAsync("a / b", ExpressionOptions.UseBigNumbers, new Dictionary<string, object?> { ["a"] = long.MaxValue, ["b"] = 0.5 }))
            Assert.True(IsWholeValue(result, new BigInteger(long.MaxValue) * 2), $"{result} ({result?.GetType().Name})");
    }

    [Fact]
    public async Task IntegerDividedByDecimalOrBigPercentShouldKeepItsDigits()
    {
        // NCALC-402 (CVD-01): the percent is unwrapped before deciding whether the dividend is converted to double
        await AssertAllAsync(12345678901234560m, "a / m%", ExpressionOptions.None,
            new Dictionary<string, object?> { ["a"] = 1234567890123456L, ["m"] = 10m }, calculatePercent: true);
        foreach (var result in await EvaluateAllAsync("a / b%", ExpressionOptions.None,
            new Dictionary<string, object?> { ["a"] = long.MaxValue, ["b"] = new BigDecimal(125, -1) }, calculatePercent: true))
            Assert.True(IsWholeValue(result, BigInteger.Parse("73786976294838206456", CultureInfo.InvariantCulture)), $"{result} ({result?.GetType().Name})");
    }

    public static TheoryData<string, object, object, ExpressionOptions, object> DecimalWithDoubleData => new()
    {
        { "a / b", 1m, 1e-300, ExpressionOptions.None, 1.0 / 1e-300 },
        { "a = b", 1e-29, 0m, ExpressionOptions.None, false },
        { "a * b > 0", 1m, 1e-29, ExpressionOptions.None, true },
        { "a = b", 16777216f, 16777216m, ExpressionOptions.None, true },
        { "a - b", 16777216f, 16777216m, ExpressionOptions.None, 0m },
        { "a = b", 9007199254740992.0, 9007199254740992m, ExpressionOptions.None, true },
        { "a + b", 0m, 16777216f, ExpressionOptions.None, 16777216m },
        { "Max(a, b)", 16777216f, 0m, ExpressionOptions.None, 16777216m },
        { "a = b", 0.1, 0.1m, ExpressionOptions.None, true },
        { "a = b", 0.30000000000000004, 0.3m, ExpressionOptions.None, true },
        { "a = b", 0.1f, 0.1m, ExpressionOptions.None, true },
        { "a + b", 0.1, 0.2m, ExpressionOptions.None, 0.3m },
    };

    [Theory]
    [MemberData(nameof(DecimalWithDoubleData))]
    public async Task DecimalWithDoubleShouldKeepTheValue(string text, object a, object b, ExpressionOptions options, object expected)
    {
        // NCALC-403: a tiny double is computed in double, a whole double keeps all its digits, a fractional one keeps 15 digits
        await AssertAllAsync(expected, text, options, new Dictionary<string, object?> { ["a"] = a, ["b"] = b });
    }

    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    public async Task TinyDoubleAndDecimalWithTheSameValueShouldBeEqual(ExpressionOptions options)
    {
        // NCALC-403 (review): a double and a decimal written with the same digits are equal, and their difference is 0
        var failures = new List<string>();
        foreach (long mantissa in new long[] { 1, 5, 59, 88, 311, 618555, 8123333, 123456789012345 })
        {
            int digits = mantissa.ToString(CultureInfo.InvariantCulture).Length;
            for (int scale = 13 + digits; scale <= 28; scale++)
            {
                decimal y = new decimal((int)(mantissa & 0xFFFFFFFF), (int)(mantissa >> 32), 0, false, (byte)scale);
                double x = double.Parse(y.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                var parameters = new Dictionary<string, object?> { ["x"] = x, ["y"] = y };
                foreach (var (text, expected) in new (string, object)[] { ("x = y", true), ("x < y or x > y", false), ("x - y = 0", true), ("x in (y)", true) })
                {
                    foreach (var result in await EvaluateAllAsync(text, options, parameters))
                    {
                        if (!expected.Equals(result))
                            failures.Add($"{text} x = {x}, y = {y}: {result}");
                    }
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct().Take(20)) + $" ({failures.Count} failures)");
    }

    [Fact]
    public async Task DecimalWithSmallDoubleThatFitsShouldStayDecimal()
    {
        // NCALC-403 (review): a small double whose digits decimal holds (1e-28, 1.2345678901234e-15) is still a decimal
        var parameters = new Dictionary<string, object?> { ["m"] = 2.5m };
        await AssertAllAsync(2.4999999999999999999999999999m, "m - 1e-28", ExpressionOptions.None, parameters);
        await AssertAllAsync(2.5000000000000000000000000005m, "m + 5e-28", ExpressionOptions.None, parameters);
        await AssertAllAsync(true, "(m + 1e-28) > m", ExpressionOptions.None, parameters);
        await AssertAllAsync(0.0000000000000012345678901234m, "Min(1.2345678901234e-15, m)", ExpressionOptions.None, parameters);
        // A value below the resolution is still computed in double
        await AssertAllAsync(2.5, "m - 1e-29", ExpressionOptions.None, parameters);
    }

    public static TheoryData<string, ExpressionOptions, object> NumericStringTypeData => new()
    {
        { "a + b", ExpressionOptions.UseBigNumbers, 3.0 },
        { "a * b", ExpressionOptions.UseBigNumbers, new BigInteger(2) },
        { "a + b", ExpressionOptions.None, 3.0 },
        { "a * b", ExpressionOptions.DecimalAsDefault, 2m },
        { "a * b", ExpressionOptions.DecimalAsDefault | ExpressionOptions.UseBigNumbers, new BigInteger(2) },
    };

    [Theory]
    [MemberData(nameof(NumericStringTypeData))]
    public async Task NumericStringsShouldKeepTheirResultTypes(string text, ExpressionOptions options, object expected)
    {
        // NCALC-406 (review): '1' and '2' are still converted to double (decimal with DecimalAsDefault) when that keeps their value,
        // so the results have the types they had before the fix
        await AssertAllAsync(expected, text, options, new Dictionary<string, object?> { ["a"] = "1", ["b"] = "2" });
    }

    public static TheoryData<ExpressionOptions> IntegerQuotientOptions =>
    [
        ExpressionOptions.None,
        ExpressionOptions.DecimalAsDefault,
        ExpressionOptions.ReduceDivResultToInteger,
        ExpressionOptions.OverflowProtection,
        ExpressionOptions.AvoidDynamicFunctions,
        ExpressionOptions.UseBigNumbers,
        ExpressionOptions.DecimalAsDefault | ExpressionOptions.UseBigNumbers,
        ExpressionOptions.ReduceArithmeticResultType | ExpressionOptions.DecimalAsDefault,
    ];

    [Theory]
    [MemberData(nameof(IntegerQuotientOptions))]
    public async Task OrdinaryIntegerQuotientsShouldBeUnchanged(ExpressionOptions options)
    {
        // NCALC-402 (differential): integers that a double holds are divided as before, with the dividend converted to double
        var failures = new List<string>();
        foreach (object a in new object[] { 1, 2, 7, 10, 1000, -123456789, 9007199254740992L })
        {
            foreach (object b in new object[] { 3, 7, -6, 9, 1024 })
            {
                var expected = await EvaluateAllAsync("x / b", options, new Dictionary<string, object?> { ["x"] = Convert.ToDouble(a, CultureInfo.InvariantCulture), ["b"] = b });
                var actual = await EvaluateAllAsync("a / b", options, new Dictionary<string, object?> { ["a"] = a, ["b"] = b });
                for (int i = 0; i < expected.Count; i++)
                {
                    if (expected[i]?.GetType() != actual[i]?.GetType() || !Equals(expected[i], actual[i]))
                        failures.Add($"{a} / {b}: {actual[i]} ({actual[i]?.GetType().Name}), expected {expected[i]} ({expected[i]?.GetType().Name})");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Distinct().Take(20)));
    }

    [Theory]
    [MemberData(nameof(IntegerQuotientOptions))]
    public async Task QuotientWithLongOperandShouldBeTheNearestDouble(ExpressionOptions options)
    {
        // NCALC-402 (differential): an integer beyond 2^53 does not cost the quotient its digits; a double result is the nearest double
        if (options.HasFlag(ExpressionOptions.DecimalAsDefault) && options.HasFlag(ExpressionOptions.UseBigNumbers))
            return;
        bool decimalResult = options.HasFlag(ExpressionOptions.ReduceArithmeticResultType);
        foreach (var (a, b, expected) in new (object, object, double)[]
                 {
                     (1, long.MaxValue, 1.0842021724855044E-19),
                     (1, long.MinValue, -1.0842021724855044E-19),
                     (100, ulong.MaxValue, 5.421010862427522E-18),
                     (65535, ulong.MaxValue, 3.5526594686918767E-15),
                     (7L, ulong.MaxValue, 3.7947076036992655E-19),
                 })
        {
            foreach (var result in await EvaluateAllAsync("a / b", options, new Dictionary<string, object?> { ["a"] = a, ["b"] = b }))
            {
                if (decimalResult)
                    Assert.True(result is decimal, $"{a} / {b}: {result} ({result?.GetType().Name})");
                else
                    Assert.True(result is double d && d == expected, $"{a} / {b} [{options}]: {result} ({result?.GetType().Name}), expected {expected:R}");
            }
        }
    }

    [Fact]
    public async Task IntegerDividedByPercentShouldNotLoseDigits()
    {
        // NCALC-402 (differential): an integer divided by a float percent is divided in double as before (not in float), and with
        // UseBigNumbers an integer percent gives the old double or the exact value, never made-up digits
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.OverflowProtection, ExpressionOptions.DecimalAsDefault })
            await AssertAllAsync(58040097819.57634, "a / f%", options, new Dictionary<string, object?> { ["a"] = 2147483647, ["f"] = 3.7f }, calculatePercent: true);

        foreach (var result in await EvaluateAllAsync("a / 10%", ExpressionOptions.UseBigNumbers, new Dictionary<string, object?> { ["a"] = long.MaxValue }, calculatePercent: true))
            Assert.True(result is not double && IsWholeValue(result, BigInteger.Parse("92233720368547758070", CultureInfo.InvariantCulture)), $"{result} ({result?.GetType().Name})");
        foreach (var result in await EvaluateAllAsync("a / 3%", ExpressionOptions.UseBigNumbers, new Dictionary<string, object?> { ["a"] = ulong.MaxValue }, calculatePercent: true))
            Assert.True(result is not double && IsWholeValue(result, BigInteger.Parse("614891469123651720500", CultureInfo.InvariantCulture)), $"{result} ({result?.GetType().Name})");
        foreach (var result in await EvaluateAllAsync("a /= 10%", ExpressionOptions.UseBigNumbers | ExpressionOptions.UseAssignments,
            new Dictionary<string, object?> { ["a"] = long.MaxValue }, calculatePercent: true))
            Assert.True(result is not double && IsWholeValue(result, BigInteger.Parse("92233720368547758070", CultureInfo.InvariantCulture)), $"{result} ({result?.GetType().Name})");

        // A dividend that a double holds keeps its old result type
        await AssertAllAsync(70.0, "a / 10%", ExpressionOptions.UseBigNumbers, new Dictionary<string, object?> { ["a"] = 7 }, calculatePercent: true);
    }

    [Fact]
    public async Task FlooredDivisionOfDecimalAndDoubleShouldOverflowWithOverflowProtection()
    {
        // NCALC-401 (final differential): the exact "//" of a decimal and a tiny or huge double overflows with OverflowProtection, as "/" does
        foreach (var (a, b) in new (object, object)[] { (double.MaxValue, 0.1m), (1m, 5e-324) })
        {
            foreach (var result in await EvaluateAllAsync("a // b", ExpressionOptions.OverflowProtection, new Dictionary<string, object?> { ["a"] = a, ["b"] = b }))
                Assert.IsType<OverflowException>(result);
            await AssertAllAsync(double.PositiveInfinity, "a // b", ExpressionOptions.None, new Dictionary<string, object?> { ["a"] = a, ["b"] = b });
        }
    }

    [Fact]
    public async Task DoubleDividedByLongShouldKeepDoubleSemantics()
    {
        // NCALC-402 (differential): only an integer dividend is divided exactly by a double; (b - a) * 100 / a stays -100
        foreach (var result in await EvaluateAllAsync("(b - a) * 100 / a", ExpressionOptions.DecimalAsDefault | ExpressionOptions.UseBigNumbers,
            new Dictionary<string, object?> { ["a"] = long.MaxValue, ["b"] = 0.0 }))
            Assert.True(IsWholeValue(result, -100), $"{result} ({result?.GetType().Name})");
    }

    [Fact]
    public async Task BigNumericStringsShouldBeBigNumbers()
    {
        // NCALC-406 (differential): with UseBigNumbers, '1e400' is the BigDecimal 1E+400, never a failed conversion or a concatenation,
        // and a big string keeps its exact value in "/"
        var expected = BigInteger.Pow(10, 400) + 1;
        foreach (var result in await EvaluateAllAsync("'1e400' + 1", ExpressionOptions.UseBigNumbers))
            Assert.True(IsWholeValue(result, expected), $"{result} ({result?.GetType().Name})");
        foreach (var result in await EvaluateAllAsync("'1e400' + '1'", ExpressionOptions.UseBigNumbers))
            Assert.True(IsWholeValue(result, expected), $"{result} ({result?.GetType().Name})");
        foreach (var result in await EvaluateAllAsync("a / a", ExpressionOptions.DecimalAsDefault | ExpressionOptions.UseBigNumbers,
            new Dictionary<string, object?> { ["a"] = "79228162514264337593543950336" }))
            Assert.True(IsWholeValue(result, 1), $"{result} ({result?.GetType().Name})");
    }

    [Fact]
    public async Task ReduceDivResultToIntegerShouldNotInventDigits()
    {
        // NCALC-418 (differential): only an exact integer quotient is reduced, and without UseBigNumbers big-number quotients keep their type
        foreach (var options in new[] { ExpressionOptions.ReduceDivResultToInteger, ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.DecimalAsDefault,
                     ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.UseBigNumbers })
        {
            foreach (var result in await EvaluateAllAsync("a / b", options, new Dictionary<string, object?> { ["a"] = double.MaxValue, ["b"] = new BigDecimal(15, -1) }))
                Assert.False(result is BigInteger, $"{options}: {result} ({result?.GetType().Name})");
        }
        foreach (var result in await EvaluateAllAsync("a / b", ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.DecimalAsDefault,
            new Dictionary<string, object?> { ["a"] = 1m, ["b"] = new BigDecimal(1, -29) }))
            Assert.IsType<BigDecimal>(result);
    }

    [Fact]
    public async Task NegativeDecimalZeroShouldKeepItsSign()
    {
        // NCALC-403 (differential): ∞ / -0.0m is -∞, as IEEE division by a negative zero is
        foreach (var result in await EvaluateAllAsync("a / b", ExpressionOptions.None, new Dictionary<string, object?> { ["a"] = double.PositiveInfinity, ["b"] = new decimal(0, 0, 0, true, 1) }))
            Assert.Equal(double.NegativeInfinity, result);
    }

    [Fact]
    public async Task ProductOfDecimalAndTinyDoubleShouldBeBigDecimalWithBigNumbers()
    {
        // NCALC-403 (differential): the product is not narrowed to decimal 0; it is 1E-300 (a BigDecimal, or the double that holds it
        // exactly, as UseBigNumbers narrows a BigDecimal result that a double holds without losing digits). 1e-300 itself is not used:
        // other tests set the process-wide BigDecimal.AlwaysTruncate for a while, which cuts a product of 1E-300 to 0 meanwhile
        foreach (var b in new[] { 1e-40, 1e-60 })
        {
            foreach (var result in await EvaluateAllAsync("a * b", ExpressionOptions.UseBigNumbers, new Dictionary<string, object?> { ["a"] = 1m, ["b"] = b }))
                Assert.True((result is BigDecimal bd && bd == NCalc.Helpers.MathHelper.ConvertToBigDecimal(b)) || (result is double d && d == b), $"{result} ({result?.GetType().Name})");
        }
        foreach (var result in await EvaluateAllAsync("a * b", ExpressionOptions.UseBigNumbers, new Dictionary<string, object?> { ["a"] = 3m, ["b"] = 1e-40 }))
            Assert.True((result is BigDecimal bd && bd == new BigDecimal(3, -40)) || (result is double d && d == 3e-40), $"{result} ({result?.GetType().Name})");
    }

    [Fact]
    public async Task IntegerDivisionOfDecimalAndTinyDoubleShouldBeExact()
    {
        // NCALC-401 (differential): a decimal and a tiny double are divided in double, but // and \ are computed exactly from their values
        var parameters = new Dictionary<string, object?> { ["a"] = 0.0000000000000000000000000001m, ["b"] = 1E-45f };
        Rational quotient = ExactValue(0.0000000000000000000000000001m, 0).DividedBy(Rational.Of((double)1E-45f));
        foreach (var result in await EvaluateAllAsync("a // b", ExpressionOptions.None, parameters))
            Assert.True(IsWholeValue(result, quotient.Floor()), $"{result} ({result?.GetType().Name}), expected {quotient.Floor()}");
        parameters["a"] = -0.0000000000000000000000000001m;
        foreach (var result in await EvaluateAllAsync("a // b", ExpressionOptions.None, parameters))
            Assert.True(IsWholeValue(result, -quotient.Floor() - 1), $"{result} ({result?.GetType().Name})");
    }

    [Fact]
    public async Task DecimalWithTinyDoubleShouldBeBigDecimalWithBigNumbers()
    {
        // NCALC-403 (1e-29 rather than 1e-300: other tests set BigDecimal.AlwaysTruncate for a while, which cuts 1e-300 to 0)
        foreach (var result in await EvaluateAllAsync("a / b", ExpressionOptions.UseBigNumbers, new Dictionary<string, object?> { ["a"] = 1m, ["b"] = 1e-29 }))
            Assert.True(result is BigDecimal bd && bd == new BigDecimal(1, 29), $"{result} ({result?.GetType().Name})");
    }

    [Theory]
    [InlineData("a * 2", "1e3", ExpressionOptions.DecimalAsDefault, "2000")]
    [InlineData("a + 1", "1.5E2", ExpressionOptions.DecimalAsDefault, "151")]
    [InlineData("a - 12345678901234567890", "12345678901234567891", ExpressionOptions.UseBigNumbers, "1")]
    [InlineData("a + 0", "12345678901234567890", ExpressionOptions.UseBigNumbers, "12345678901234567890")]
    [InlineData("a * 2", "1e3", ExpressionOptions.None, "2000")]
    public async Task NumericStringsShouldBeReadAsLiterals(string text, string a, ExpressionOptions options, string expected)
    {
        // NCALC-406
        foreach (var result in await EvaluateAllAsync(text, options, new Dictionary<string, object?> { ["a"] = a }))
            Assert.True(IsWholeValue(result, BigInteger.Parse(expected, CultureInfo.InvariantCulture)), $"{result} ({result?.GetType().Name})");
    }

    [Theory]
    [InlineData("'1,5' + 0", "en-US")]
    [InlineData("'1,000' + 0", "")]
    public async Task NumericStringsWithGroupSeparatorsShouldNotBeNumbers(string text, string cultureName)
    {
        // NCALC-406: a group separator is not dropped silently ("1,5" was 15)
        foreach (var result in await EvaluateAllAsync(text, ExpressionOptions.None, culture: CultureInfo.GetCultureInfo(cultureName)))
            Assert.IsType<NCalcConversionException>(result);
    }

    [Fact]
    public async Task NumericStringsShouldBeParsedAsInComparisons()
    {
        // NCALC-406: in de-DE, '0.1' means what '0.1' = 0.1 assumes; an out-of-range string is an NCalc error, not OverflowException
        var deDe = CultureInfo.GetCultureInfo("de-DE");
        await AssertAllAsync(0.30000000000000004, "'0.1' + '0.2' * 1", ExpressionOptions.None, culture: deDe);
        await AssertAllAsync(true, "'0.1' = 0.1", ExpressionOptions.None, culture: deDe);
        foreach (var result in await EvaluateAllAsync("a + 1", ExpressionOptions.DecimalAsDefault, new Dictionary<string, object?> { ["a"] = "79228162514264337593543950336" }))
            Assert.True(result is double d && d == 79228162514264337593543950337.0, $"{result} ({result?.GetType().Name})");
    }

    [Fact]
    public async Task ULongDifferenceShouldBeExactWithOverflowProtection()
    {
        // NCALC-419: ulong - ulong with a negative result that fits in long is that long, also above long.MaxValue
        var options = ExpressionOptions.OverflowProtection;
        await AssertAllAsync(-1L, "a - b", options, new Dictionary<string, object?> { ["a"] = 18446744073709551614UL, ["b"] = ulong.MaxValue });
        await AssertAllAsync(long.MinValue, "a - b", options, new Dictionary<string, object?> { ["a"] = 0UL, ["b"] = 9223372036854775808UL });
        await AssertAllAsync(-1L, "a -= b; a", options | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences,
            new Dictionary<string, object?> { ["a"] = 18446744073709551614UL, ["b"] = ulong.MaxValue });

        // A difference beyond the range of long, and a sum beyond the range of ulong, remain an arithmetic overflow
        foreach (var result in await EvaluateAllAsync("a - b", options, new Dictionary<string, object?> { ["a"] = 0UL, ["b"] = ulong.MaxValue }))
        {
            Assert.IsType<OverflowException>(result);
            Assert.DoesNotContain("Int64", ((Exception)result!).Message);
        }
        foreach (var result in await EvaluateAllAsync("a + b", options, new Dictionary<string, object?> { ["a"] = ulong.MaxValue, ["b"] = 1UL }))
            Assert.IsType<OverflowException>(result);
    }

    [Fact]
    public async Task OverflowProtectionDocumentationExampleShouldHold()
    {
        // NCALC-420: the example of docs/articles/overflow_protection.md
        await AssertAllAsync(2147483648L, $"{int.MaxValue} + 1", ExpressionOptions.OverflowProtection);
        foreach (var result in await EvaluateAllAsync($"{long.MaxValue} + 1", ExpressionOptions.OverflowProtection))
            Assert.IsType<OverflowException>(result);
    }

    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    public async Task DecimalRemainderShouldNotOverflow(ExpressionOptions options)
    {
        // NCALC-421
        await AssertAllAsync(0.0771837485735662406456049673m, "a % b", options,
            new Dictionary<string, object?> { ["a"] = decimal.MaxValue, ["b"] = 1.0000000000000000000000000001m });
        await AssertAllAsync(-0.0771837485735662406456049673m, "a % b", options,
            new Dictionary<string, object?> { ["a"] = -decimal.MaxValue, ["b"] = 1.0000000000000000000000000001m });
    }

    [Theory]
    [InlineData("1 in (1.0, 2)", false)]
    [InlineData("1 not in (1.0, 2)", true)]
    [InlineData("'1' in (1, 2)", false)]
    [InlineData("1 in ('1', '2')", false)]
    [InlineData("true in (1, 2)", false)]
    [InlineData("1 in (true, 2)", false)]
    [InlineData("1 in p", false)]
    [InlineData("1 in '123'", false)]
    [InlineData("1 in (1, 2)", true)]
    [InlineData("'a' in ('a', 'b')", true)]
    [InlineData("'b' in 'abc'", true)]
    [InlineData("2.0 in p", true)]
    public async Task InShouldApplyStrictTypeMatching(string text, bool expected)
    {
        // NCALC-424: with StrictTypeMatching, "x in (...)" agrees with "x = ..."
        await AssertAllAsync(expected, text, ExpressionOptions.StrictTypeMatching, new Dictionary<string, object?> { ["p"] = new[] { 1.0, 2.0 } });
    }
}

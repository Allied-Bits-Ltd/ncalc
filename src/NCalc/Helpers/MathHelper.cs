using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;

using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Parser;
using NCalcVector = NCalc.Domain.Vector;

namespace NCalc.Helpers;

/// <summary>
/// Utilities for doing mathematical operations between different object types.
/// </summary>
public static class MathHelper
{
    private static readonly object TruncationLock = new();
    private static int _truncationScopes;
    private static bool _truncationOriginalValue;

    /// <summary>
    /// Runs a transcendental function of ExtendedNumerics with <see cref="BigDecimal.AlwaysTruncate"/> set, as suggested by the
    /// author of the library: the intermediate results are then truncated to <see cref="BigDecimal.Precision"/> digits, so the
    /// series used by Ln, Log, Asin, Acos, Atan, Sin, Cos and the others do not grow to tens of thousands of digits.
    /// The setting is process-wide and makes operations on large values much slower, so it is set only for these calls
    /// (nested and concurrent calls are counted) and the original value is restored afterwards.
    /// </summary>
    private static T Truncated<T>(Func<T> calculation)
    {
        lock (TruncationLock)
        {
            if (_truncationScopes++ == 0)
            {
                _truncationOriginalValue = BigDecimal.AlwaysTruncate;
                BigDecimal.AlwaysTruncate = true;
            }
        }

        try
        {
            return calculation();
        }
        catch (Exception ex) when (ex is ArithmeticException or ArgumentException)
        {
            // ExtendedNumerics reports arguments it cannot handle with .NET exceptions (DivideByZeroException, ArgumentException ...)
            throw new NCalcEvaluationException("The big-number function cannot be computed for this argument: " + ex.Message, ex);
        }
        finally
        {
            lock (TruncationLock)
            {
                if (--_truncationScopes == 0)
                    BigDecimal.AlwaysTruncate = _truncationOriginalValue;
            }
        }
    }

    // The big-number transcendental functions below guard the domain and reduce the arguments before calling ExtendedNumerics,
    // whose functions return wrong values for some arguments (Exp(-100) was negative, Atan(1) was 0.7866, Sin(100) had the wrong
    // sign, Ln(-1) was 0), throw .NET exceptions for others (Asin(1), Sqrt(-4), Ln(0), Sin(1E+30)), or do not finish (Exp(1E+400)).
    // Where System.Math returns NaN or an infinity, so do they (as a double). The trigonometric functions are computed here.

    /// <summary>
    /// The maximum number of integer digits of a big-number argument of Sin, Cos, Tan, Sec, Csc and Cot. The argument is reduced
    /// modulo 2π, which needs π to that many digits more than <see cref="BigDecimal.Precision"/>; a larger argument throws
    /// <see cref="NCalcEvaluationException"/>.
    /// </summary>
    private const int MaxBigTrigArgumentDigits = 10_000;

    private static readonly BigDecimal BigQuarter = new(new BigInteger(25), -2);

    private sealed class PiDigits(int digits, BigInteger value)
    {
        public int Digits { get; } = digits;

        public BigInteger Value { get; } = value;
    }

    private static PiDigits? _piDigits;

    // The last few truncations of the cached π, so that after one call with a huge argument (which caches thousands of digits)
    // the usual precisions are not cut from that long number again on every call (NCALC-329)
    private static PiDigits[] _truncatedPiDigits = [];

    private const int TruncatedPiCacheSize = 4;

    /// <summary>Returns π multiplied by 10^<paramref name="digits"/> and truncated, computed with Machin's formula and cached.</summary>
    private static BigInteger GetScaledPi(int digits)
    {
        var cached = Volatile.Read(ref _piDigits);
        if (cached is null || cached.Digits < digits)
        {
            // π = 16 atan(1/5) - 4 atan(1/239), in fixed point with 10 guard digits
            BigInteger unity = BigInteger.Pow(10, digits + 10);
            BigInteger pi = (16 * ArctanOfInverse(5, unity) - 4 * ArctanOfInverse(239, unity)) / BigInteger.Pow(10, 10);
            cached = new PiDigits(digits, pi);
            Volatile.Write(ref _piDigits, cached);
        }

        if (cached.Digits == digits)
            return cached.Value;

        var truncations = Volatile.Read(ref _truncatedPiDigits);
        foreach (var truncation in truncations)
        {
            if (truncation.Digits == digits)
                return truncation.Value;
        }

        BigInteger value = cached.Value / BigInteger.Pow(10, cached.Digits - digits);
        var updated = new PiDigits[Math.Min(truncations.Length + 1, TruncatedPiCacheSize)];
        updated[0] = new PiDigits(digits, value);
        Array.Copy(truncations, 0, updated, 1, updated.Length - 1);
        Volatile.Write(ref _truncatedPiDigits, updated);
        return value;
    }

    // atan(1 / x) * unity in fixed point
    private static BigInteger ArctanOfInverse(int x, BigInteger unity)
    {
        BigInteger xSquared = x * x;
        BigInteger power = unity / x;
        BigInteger sum = power;
        int sign = -1;
        for (int n = 3; !power.IsZero; n += 2)
        {
            power /= xSquared;
            sum += sign * (power / n);
            sign = -sign;
        }
        return sum;
    }

    private static BigDecimal GetBigPi() => new(GetScaledPi(BigDecimal.Precision), -BigDecimal.Precision);

    // The number of digits of the integer part of |value| (zero or less for |value| < 1), possibly one too many
    private static int GetIntegerDigits(BigDecimal value)
        => value.Mantissa.IsZero ? 0 : (int)Math.Floor(BigInteger.Log10(BigInteger.Abs(value.Mantissa))) + 1 + value.Exponent;

    private static double SaturatingToDouble(BigDecimal value)
    {
        try
        {
            return BigDecimalToDouble(value);
        }
        catch (OverflowException)
        {
            return value.Sign * double.MaxValue;
        }
    }

    // Keeps the first digits of the mantissa, so that repeated multiplication does not grow the number
    private static BigDecimal TruncateMantissa(BigDecimal value, int digits)
    {
        int excess = GetIntegerDigits(new BigDecimal(value.Mantissa, 0)) - digits;
        return excess <= 0 ? value : new BigDecimal(value.Mantissa / BigInteger.Pow(10, excess), value.Exponent + excess);
    }

    // value * 10^scale, truncated
    private static BigInteger ToFixedPoint(BigDecimal value, int scale)
        => value.Exponent + scale >= 0
            ? value.Mantissa * BigInteger.Pow(10, value.Exponent + scale)
            : value.Mantissa / BigInteger.Pow(10, -(value.Exponent + scale));

    private static BigInteger FloorDivide(BigInteger dividend, BigInteger divisor)
    {
        BigInteger quotient = BigInteger.DivRem(dividend, divisor, out BigInteger remainder);
        return remainder.Sign != 0 && (remainder.Sign < 0) != (divisor.Sign < 0) ? quotient - 1 : quotient;
    }

    private enum BigTrigFunction
    {
        Sin,
        Cos,
        Tan,
        Sec,
        Csc,
        Cot
    }

    /// <summary>
    /// Computes a trigonometric function of a big number in fixed point with <see cref="BigDecimal.Precision"/> decimal places.
    /// The argument is reduced by the nearest multiple of π/2, with enough digits of π for its integer part, to |t| &lt;= π/4,
    /// where the Taylor series of sin and cos converge quickly. (The functions of ExtendedNumerics give the wrong sign for
    /// many arguments, such as Sin(2), Cos(0.3) and Tan(1), and throw for Tan(0) and arguments beyond the range of int.)
    /// </summary>
    private static object BigTrigonometric(BigDecimal x, BigTrigFunction function)
    {
        // A tiny argument would be zero in the fixed point below: sin x = x and cos x = 1 to the precision of BigDecimal (NCALC-330)
        if (IsTinyBigArgument(x))
        {
            return function switch
            {
                BigTrigFunction.Sin or BigTrigFunction.Tan => x,
                BigTrigFunction.Cos or BigTrigFunction.Sec => BigDecimal.One,
                _ => Reciprocal(x)
            };
        }

        int digits = BigDecimal.Precision + 10;
        int integerDigits = Math.Max(GetIntegerDigits(x), 0);
        if (integerDigits > MaxBigTrigArgumentDigits)
            throw new NCalcEvaluationException($"The argument of {function}() has more than {MaxBigTrigArgumentDigits} integer digits");

        int scale = digits + integerDigits;
        BigInteger scaled = ToFixedPoint(x, scale);
        BigInteger halfPi = GetScaledPi(scale) / 2;
        BigInteger multiple = FloorDivide(scaled + halfPi / 2, halfPi);
        BigInteger t = (scaled - multiple * halfPi) / BigInteger.Pow(10, integerDigits);

        BigInteger unity = BigInteger.Pow(10, digits);
        BigInteger tSquared = t * t / unity;
        BigInteger sinT = t;
        BigInteger cosT = unity;
        BigInteger sinTerm = t;
        BigInteger cosTerm = unity;
        for (int n = 1; !sinTerm.IsZero || !cosTerm.IsZero; n++)
        {
            sinTerm = -sinTerm * tSquared / unity / (2 * n * (2 * n + 1));
            cosTerm = -cosTerm * tSquared / unity / ((2 * n - 1) * 2 * n);
            sinT += sinTerm;
            cosT += cosTerm;
        }

        int quadrant = (int)(((multiple % 4) + 4) % 4);
        BigInteger sin = quadrant switch { 0 => sinT, 1 => cosT, 2 => -sinT, _ => -cosT };
        BigInteger cos = quadrant switch { 0 => cosT, 1 => -sinT, 2 => -cosT, _ => sinT };
        (BigInteger numerator, BigInteger denominator) = function switch
        {
            BigTrigFunction.Sin => (sin, unity),
            BigTrigFunction.Cos => (cos, unity),
            BigTrigFunction.Tan => (sin, cos),
            BigTrigFunction.Sec => (unity, cos),
            BigTrigFunction.Csc => (unity, sin),
            _ => (cos, sin)
        };
        if (denominator.IsZero)
            throw new NCalcEvaluationException($"{function}() is undefined for this argument");

        return new BigDecimal(numerator * BigInteger.Pow(10, BigDecimal.Precision) / denominator, -BigDecimal.Precision);
    }

    private static BigDecimal ToBigDecimalArgument(object a) => a is BigInteger biA ? new BigDecimal(biA) : (BigDecimal)a;

    private static object BigExp(BigDecimal x)
    {
        if (x.IsZero())
            return BigDecimal.One;

        // The result has about |x| * log10(e) digits before (or zeros after) the decimal point
        double digits = Math.Abs(SaturatingToDouble(x)) * 0.43429448190325182;
        if (digits > MaxPowResultDigits)
        {
            // As Math.Exp, a result too small to be represented is zero
            if (x.Sign < 0)
                return BigDecimal.Zero;
            throw new NCalcEvaluationException($"The result of Exp would have more than {MaxPowResultDigits} digits (MathHelper.MaxPowResultDigits).");
        }

        BigDecimal magnitude = Truncated(() =>
        {
            // e^|x| = (e^(|x| / 2^k))^(2^k) with |x| / 2^k <= 1, where the series of ExtendedNumerics is fast and exact
            BigDecimal reduced = x.Sign < 0 ? -x : x;
            int halvings = 0;
            while (reduced > BigDecimal.One)
            {
                reduced *= BigDecimal.OneHalf;
                halvings++;
            }

            BigDecimal result = BigDecimal.Exp(reduced);
            for (int i = 0; i < halvings; i++)
                result = TruncateMantissa(result * result, BigDecimal.Precision + 10);

            return result;
        });

        // e^-|x| = 1 / e^|x|
        return x.Sign < 0 ? Reciprocal(magnitude) : magnitude;
    }

    // 1 / value with about BigDecimal.Precision significant digits: 1 / (m * 10^e) = (10^k / m) * 10^(-k - e). The division of
    // ExtendedNumerics keeps a fixed number of decimal places, so 1 / e^1000 would be zero. (With AlwaysTruncate set, a BigDecimal
    // below 10^-Precision is zero, so a tiny result must be created outside Truncated.)
    private static BigDecimal Reciprocal(BigDecimal value)
    {
        int k = BigDecimal.Precision + GetIntegerDigits(new BigDecimal(value.Mantissa, 0));
        return new BigDecimal(BigInteger.Pow(10, k) / value.Mantissa, -k - value.Exponent);
    }

    /// <summary>
    /// Whether |x| is below 10^-(Precision / 2), where sin x = tan x = asin x = atan x = x and cos x = 1 to the precision of BigDecimal
    /// (the next terms of the series are below x * 10^-Precision). Such an argument would be zero in the fixed point of the
    /// trigonometric functions, and truncated to zero with <see cref="BigDecimal.AlwaysTruncate"/> set.
    /// </summary>
    private static bool IsTinyBigArgument(BigDecimal x)
        => !x.IsZero() && GetIntegerDigits(x) <= -(BigDecimal.Precision / 2);

    // asin(x) = atan(x / sqrt(1 - x²)) for |x| < 1, with the reduced arctangent: the series of ExtendedNumerics for Arcsin is
    // inaccurate for some arguments (Asin(0.7) was wrong in the 7th digit) and slow (NCALC-310)
    private static BigDecimal ArcsinOfFraction(BigDecimal x)
        => IsTinyBigArgument(x) ? x : Truncated(() => ReducedArctan(x / BigDecimal.SquareRoot(BigDecimal.One - x * x, BigDecimal.Precision)));

    // Asin and Acos of ±1 are exact (ExtendedNumerics divided by zero), and of |x| > 1 NaN
    private static object BigArcsin(BigDecimal x)
    {
        BigDecimal magnitude = BigDecimal.Abs(x);
        if (magnitude > BigDecimal.One)
            return double.NaN;
        if (magnitude < BigDecimal.One)
            return ArcsinOfFraction(x);
        BigDecimal halfPi = GetBigPi() * BigDecimal.OneHalf;
        return x.Sign < 0 ? -halfPi : halfPi;
    }

    private static object BigArccos(BigDecimal x)
    {
        BigDecimal magnitude = BigDecimal.Abs(x);
        if (magnitude > BigDecimal.One)
            return double.NaN;
        // acos(x) = π/2 - asin(x); Arccos of ExtendedNumerics divides by zero for 0
        BigDecimal halfPi = GetBigPi() * BigDecimal.OneHalf;
        if (magnitude < BigDecimal.One)
        {
            BigDecimal arcsin = ArcsinOfFraction(x);
            return Truncated(() => halfPi - arcsin);
        }
        return x.Sign > 0 ? BigDecimal.Zero : GetBigPi();
    }

    private static object BigArctan(BigDecimal x)
        => IsTinyBigArgument(x) ? x : Truncated(() => ReducedArctan(x));

    // Called with BigDecimal.AlwaysTruncate set (in Truncated)
    private static BigDecimal ReducedArctan(BigDecimal x)
    {
        // The series of ExtendedNumerics is inaccurate near ±1, so the argument is reduced first: atan(x) = ±π/2 - atan(1/x)
        // for |x| > 1, then atan(x) = 2 atan(x / (1 + sqrt(1 + x²))) until |x| <= 1/4
        BigDecimal offset = BigDecimal.Zero;
        BigDecimal reduced = x;
        if (BigDecimal.Abs(x) > BigDecimal.One)
        {
            offset = GetBigPi() * BigDecimal.OneHalf;
            if (x.Sign < 0)
                offset = -offset;
            reduced = -Reciprocal(x);
        }

        int factor = 1;
        while (BigDecimal.Abs(reduced) > BigQuarter)
        {
            reduced /= BigDecimal.One + BigDecimal.SquareRoot(BigDecimal.One + reduced * reduced, BigDecimal.Precision);
            factor *= 2;
        }
        return offset + BigDecimal.Arctan(reduced) * factor;
    }

    // The logarithm of zero or a negative number is -∞ or NaN, as with Math.Log
    private static object BigLogarithm(BigDecimal x, Func<BigDecimal, BigDecimal> logarithm)
    {
        if (x.Sign < 0)
            return double.NaN;
        if (x.IsZero())
            return double.NegativeInfinity;
        return Truncated(() => logarithm(x));
    }

    // ln(x) as a double, also for x beyond the range of double: ln(m * 10^e) = ln(m) + e * ln(10) with the first 17 digits of m
    private static double NaturalLogarithmInDouble(BigDecimal x)
    {
        // A value within the range of double is used directly, which is more accurate near 1
        double value = SaturatingToDouble(x);
        if (x.Sign <= 0 || (Math.Abs(value) < double.MaxValue && Math.Abs(value) >= 1E-300))
            return Math.Log(value);

        int excess = GetIntegerDigits(new BigDecimal(x.Mantissa, 0)) - 17;
        BigInteger mantissa = excess > 0 ? x.Mantissa / BigInteger.Pow(10, excess) : x.Mantissa;
        return Math.Log((double)mantissa) + ((double)x.Exponent + Math.Max(excess, 0)) * Math.Log(10);
    }

    private static object BigLog(BigDecimal x, BigDecimal newBase)
    {
        // Zero or negative numbers, and a base of one, follow Math.Log (NaN, -∞ or +∞)
        if (x.Sign <= 0 || newBase.Sign <= 0 || (newBase - BigDecimal.One).IsZero())
            return Math.Log(SaturatingToDouble(x), SaturatingToDouble(newBase));

        if (newBase > int.MaxValue)
            return Truncated(() => BigDecimal.Ln(x) / BigDecimal.Ln(newBase));

        int intBase = (int)GetWholeValue(newBase);
        return Truncated(() => BigDecimal.Log(x, intBase));
    }

    enum ArithmeticOperation
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
        AddPercent,
        SubtractPercent,
        MultiplyPercent,
        DividePercent,
    }

    // unchecked
#if !AOT_COMPILATION
    private static object DynamicAddFunc(dynamic a, dynamic b) => unchecked(a + b);
    private static object DynamicSubtractFunc(dynamic a, dynamic b) => unchecked(a - b);
    private static object DynamicMultiplyFunc(dynamic a, dynamic b) => unchecked(a * b);
    private static object DynamicDivideFunc(dynamic a, dynamic b) => unchecked(a / b);
    private static object DynamicModuloFunc(dynamic a, dynamic b) => unchecked(a % b);
#endif

#if AOT_COMPILATION
    public static T AddGeneric<T>(T left, T right) where T : IAdditionOperators<T, T, T>
    {
        return left + right;
    }
#endif

    private static object? AddFunc(object a, object b, MathHelperOptions options)
    {
        // Time spans are computed with their own operators in NCalc, NCalc.AOT and with AvoidDynamicFunctions alike
        if (a is TimeSpan timeSpanA && b is TimeSpan timeSpanB)
            return timeSpanA + timeSpanB;

        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = AddFuncChecked(a, b);
            else
                result = DynamicAddFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca + cb);
            else
                result = unchecked(ca + cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba + bb);
            else
                result = unchecked(ba + bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa + sb);
            else
                result = unchecked(sa + sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha + shb);
            else
                result = unchecked(sha + shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha + ushb);
            else
                result = unchecked(usha + ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia + ib);
            else
                result = unchecked(ia + ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia + uib);
            else
                result = unchecked(uia + uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la + lb);
            else
                result = unchecked(la + lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula + ulb);
            else
                result = unchecked(ula + ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa + fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa + fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da + db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da + db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca + dcb);
            }
            else
            {
                result = unchecked(dca + dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Addition");
            if (method is null)
                throw new InvalidOperationException($"No overloaded addition function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded addition function found for operands of type '{a.GetType()}'");
#endif
        }

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> AddFuncChecked = (a, b) =>
    {
        var res = checked(a + b);
        CheckOverflow(res);

        return res;
    };
#endif

    // The result type of a percent operation: decimal when DecimalAsDefault is set or an operand is a decimal, double otherwise
    // (big numbers, complex numbers and vectors stay as they are)
    private static bool IsDecimalPercentResult(object a, object b, MathHelperOptions options)
        => (options.DecimalAsDefault && !(a is double || b is double)) || (a is decimal || b is decimal);

    private static object ToPercentResultType(object result, object a, object b, MathHelperOptions options)
    {
        if (IsBigNumber(result) || result is ComplexNumber || result is NCalcVector)
            return result;
        if (IsDecimalPercentResult(a, b, options))
            return ConvertToDecimal(result, options);

        // With UseBigNumbers, the exact value is kept when the double would lose digits, whatever the type of the intermediate
        // result was (ulong.MaxValue + 10% and ulong.MaxValue * 10% are both exact)
        double doubleResult = ConvertToDouble(result, options);
        if (options.UseBigNumbers && IsBoxedNumber(result) && result is not double and not float && !double.IsInfinity(doubleResult))
        {
            BigDecimal exact = ConvertToBigDecimal(result);
            if (new BigDecimal(doubleResult) != exact)
                return IsWholeNumber(exact) ? GetWholeValue(exact) : exact;
        }
        return doubleResult;
    }

    // a + b% = a * (100 + b) / 100, a - b% = a * (100 - b) / 100, a * b% = a * b / 100 and a / b% = 100 * a / b.
    // NCalc and NCalc.AOT (or AvoidDynamicFunctions) use this same implementation, so that they give the same results.
    // Ordinary numbers are converted to the result type (double or decimal) before the formula is applied, so that an integer
    // product such as 300000000 * 10 cannot wrap around in the operand type. Big numbers (and every operand with UseBigNumbers),
    // complex numbers and vectors are computed exactly with the arithmetic operations and converted to the result type afterwards.
    private static object PercentFunc(object a, object b, ArithmeticOperation operation, MathHelperOptions options)
    {
        if (a is TimeSpan timeSpan && IsBoxedNumber(b))
            return PercentOfTimeSpan(timeSpan, ConvertToDouble(b, options), operation);

        // NaN and the infinities have no exact value and are computed in double, also with UseBigNumbers or a big-number operand
        if ((IsNaNOrInfinity(a) || IsNaNOrInfinity(b)) && IsBoxedNumberOrBigNumber(a) && IsBoxedNumberOrBigNumber(b))
            return PercentOfDouble(ToDoubleOrInfinity(a), ToDoubleOrInfinity(b), operation);

        if (options.UseBigNumbers || IsBigNumber(a) || IsBigNumber(b) || a is ComplexNumber or NCalcVector || b is ComplexNumber or NCalcVector)
        {
            return operation switch
            {
                ArithmeticOperation.AddPercent => AddPercentFunc(a, b, options),
                ArithmeticOperation.SubtractPercent => SubtractPercentFunc(a, b, options),
                ArithmeticOperation.MultiplyPercent => MultiplyPercentFunc(a, b, options),
                _ => DividePercentFunc(a, b, options)
            };
        }

        if (!IsBoxedNumber(a) || !IsBoxedNumber(b))
            throw new InvalidOperationException($"The percent operation is not implemented for operands of types {a.GetType()} and {b.GetType()}");

        // A double or float beyond the range of decimal (or NaN) cannot be converted to decimal; such a pair is computed in double
        if (IsDecimalPercentResult(a, b, options) && !IsOutsideDecimalRange(a) && !IsOutsideDecimalRange(b))
        {
            decimal decA = Convert.ToDecimal(a, options.CultureInfo);
            decimal decB = Convert.ToDecimal(b, options.CultureInfo);
            return PercentOfDecimal(decA, decB, operation);
        }

        double doubleA = Convert.ToDouble(a, options.CultureInfo);
        double doubleB = Convert.ToDouble(b, options.CultureInfo);
        double result = PercentOfDouble(doubleA, doubleB, operation);
        if (options.OverflowProtection && !double.IsInfinity(doubleA) && !double.IsInfinity(doubleB))
            CheckOverflow(result);
        return result;
    }

    private static decimal PercentOfDecimal(decimal a, decimal b, ArithmeticOperation operation)
    {
        try
        {
            return operation switch
            {
                ArithmeticOperation.AddPercent => a * (100m + b) / 100m,
                ArithmeticOperation.SubtractPercent => a * (100m - b) / 100m,
                ArithmeticOperation.MultiplyPercent => a * b / 100m,
                _ => 100m * a / b
            };
        }
        catch (OverflowException)
        {
            // The product of the formula can overflow although the result is in range (7E+28 * 10% is 7E+27), as in double
        }

        try
        {
            return operation switch
            {
                ArithmeticOperation.AddPercent => a * (1m + b / 100m),
                ArithmeticOperation.SubtractPercent => a * (1m - b / 100m),
                ArithmeticOperation.MultiplyPercent => a * (b / 100m),
                _ => a / b * 100m
            };
        }
        catch (OverflowException ex)
        {
            throw new NCalcEvaluationException("The result of the percent operation is outside the range of decimal.", ex);
        }
    }

    private static double PercentOfDouble(double a, double b, ArithmeticOperation operation)
    {
        double result = operation switch
        {
            ArithmeticOperation.AddPercent => a * (100.0 + b) / 100.0,
            ArithmeticOperation.SubtractPercent => a * (100.0 - b) / 100.0,
            ArithmeticOperation.MultiplyPercent => a * b / 100.0,
            _ => 100.0 * a / b
        };

        // The product of the formula can overflow although the result is in range (1e308 * 10% is 1e307)
        if (double.IsInfinity(result) && !double.IsInfinity(a) && !double.IsInfinity(b) && !double.IsNaN(a) && !double.IsNaN(b))
        {
            result = operation switch
            {
                ArithmeticOperation.AddPercent => a * ((100.0 + b) / 100.0),
                ArithmeticOperation.SubtractPercent => a * ((100.0 - b) / 100.0),
                ArithmeticOperation.MultiplyPercent => a * (b / 100.0),
                _ => a / b * 100.0
            };
        }
        return result;
    }

    // A percent of a time span (01:00:00 + 10% is 01:06:00) is computed on its ticks, rounded to the nearest tick
    private static TimeSpan PercentOfTimeSpan(TimeSpan timeSpan, double percent, ArithmeticOperation operation)
    {
        double factor = operation switch
        {
            ArithmeticOperation.AddPercent => (100.0 + percent) / 100.0,
            ArithmeticOperation.SubtractPercent => (100.0 - percent) / 100.0,
            ArithmeticOperation.MultiplyPercent => percent / 100.0,
            _ => 100.0 / percent
        };
        double ticks = Math.Round(timeSpan.Ticks * factor);
        if (double.IsNaN(ticks) || ticks < long.MinValue || ticks >= long.MaxValue)
            throw new NCalcEvaluationException($"The result of the percent operation on the time span {timeSpan} is not a valid time span");
        return TimeSpan.FromTicks((long)ticks);
    }

    private static object AddPercentFunc(object a, object b, MathHelperOptions options)
    {
        object? im1 = Add(100m, b, false, options);
        if (im1 is null)
            throw new InvalidOperationException($"No addition was possible for a number and an object of type '{b.GetType()}'");

        object? im2 = Multiply(a, im1, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for objects of types '{a.GetType()}' and '{im1.GetType()}'");

        object? result = Divide(im2, 100m, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for an object of type '{im2.GetType()}' and a number");

        return ToPercentResultType(result, a, b, options);
    }

    private static object? SubtractFunc(object a, object b, MathHelperOptions options)
    {
        // Time spans and dates are computed with their own operators in NCalc, NCalc.AOT and with AvoidDynamicFunctions alike
        if (a is TimeSpan timeSpanA && b is TimeSpan timeSpanB)
            return timeSpanA - timeSpanB;
        if (a is DateTime dateA && b is DateTime dateB)
            return dateA - dateB;

        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            result =  options.OverflowProtection ? SubtractFuncChecked(a, b) : DynamicSubtractFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca - cb);
            else
                result = unchecked(ca - cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba - bb);
            else
                result = unchecked(ba - bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa - sb);
            else
                result = unchecked(sa - sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha - shb);
            else
                result = unchecked(sha - shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha - ushb);
            else
                result = unchecked(usha - ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia - ib);
            else
                result = unchecked(ia - ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia - uib);
            else
                result = unchecked(uia - uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la - lb);
            else
                result = unchecked(la - lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula - ulb);
            else
                result = unchecked(ula - ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa - fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa - fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da - db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da - db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca - dcb);
            }
            else
            {
                result = unchecked(dca - dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Subtraction");
            if (method is null)
                throw new InvalidOperationException($"No overloaded subtraction function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded subtraction function found for operands of type '{a.GetType()}'");
#endif
        }

            return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> SubtractFuncChecked = (a, b) =>
    {
        var res = checked(a - b);
        CheckOverflow(res);

        return res;
    };
#endif

    private static object SubtractPercentFunc(object a, object b, MathHelperOptions options)
    {
        object? im1 = Subtract(100m, b, false, options);
        if (im1 is null)
            throw new InvalidOperationException($"No subtraction was possible for a number and an '{b.GetType()}' object");

        object? im2 = Multiply(a, im1, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for objects of types '{a.GetType()}' and '{im1.GetType()}'");

        object? result = Divide(im2, 100m, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for an '{im2.GetType()}' object and a number");

        return ToPercentResultType(result, a, b, options);
    }

    private static object? MultiplyFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = MultiplyFuncChecked(a, b);
            else
                result = DynamicMultiplyFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca * cb);
            else
                result = unchecked(ca * cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba * bb);
            else
                result = unchecked(ba * bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa * sb);
            else
                result = unchecked(sa * sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha * shb);
            else
                result = unchecked(sha * shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha * ushb);
            else
                result = unchecked(usha * ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia * ib);
            else
                result = unchecked(ia * ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia * uib);
            else
                result = unchecked(uia * uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la * lb);
            else
                result = unchecked(la * lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula * ulb);
            else
                result = unchecked(ula * ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa * fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa * fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da * db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da * db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca * dcb);
            }
            else
            {
                result = unchecked(dca * dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Multiply");
            if (method is null)
                throw new InvalidOperationException($"No overloaded multiplication function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded multiplication function found for operands of type '{a.GetType()}'");
#endif
        }

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> MultiplyFuncChecked = (a, b) =>
    {
        var res = checked(a * b);
        CheckOverflow(res);

        return res;
    };
#endif
    private static object MultiplyPercentFunc(object a, object b, MathHelperOptions options)
    {
        object? im2 = Multiply(a, b, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for objects of types '{a.GetType()}' and '{b.GetType()}'");

        object? result = Divide(im2, 100m, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for an '{im2.GetType()}' object and a number");

        return ToPercentResultType(result, a, b, options);
    }

    private static object? DivideFunc(object a, object b, MathHelperOptions options)
    {
        // The ratio of two time spans is a double, as TimeSpan / TimeSpan gives in .NET, in NCalc, NCalc.AOT and with
        // AvoidDynamicFunctions alike (and also on the targets whose TimeSpan has no such operator)
        if (a is TimeSpan timeSpanA && b is TimeSpan timeSpanB)
            return timeSpanA.Ticks / (double)timeSpanB.Ticks;

        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            result = options.OverflowProtection ? DivideFuncChecked(a, b) : DynamicDivideFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca / cb);
            else
                result = unchecked(ca / cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba / bb);
            else
                result = unchecked(ba / bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa / sb);
            else
                result = unchecked(sa / sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha / shb);
            else
                result = unchecked(sha / shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha / ushb);
            else
                result = unchecked(usha / ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia / ib);
            else
                result = unchecked(ia / ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia / uib);
            else
                result = unchecked(uia / uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la / lb);
            else
                result = unchecked(la / lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula / ulb);
            else
                result = unchecked(ula / ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa / fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa / fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da / db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da / db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca / dcb);
            }
            else
            {
                result = unchecked(dca / dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Division");
            if (method is null)
                throw new InvalidOperationException($"No overloaded division function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded division function found for operands of type '{a.GetType()}'");
#endif
        }

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> DivideFuncChecked = (a, b) =>
    {
        var res = checked(a / b);
        CheckOverflow(res);

        return res;
    };
#endif
    private static object DividePercentFunc(object a, object b, MathHelperOptions options)
    {
        object? im2 = Multiply(a, 100m, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for an '{a.GetType()}' object and a number");

        object? result = Divide(im2, b, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for objects of types '{im2.GetType()}' and '{b.GetType()}'");

        return ToPercentResultType(result, a, b, options);
    }

    private static object? ModuloFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = ModuloFuncChecked(a, b);
            else
                result = DynamicModuloFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca % cb);
            else
                result = unchecked(ca % cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba % bb);
            else
                result = unchecked(ba % bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa % sb);
            else
                result = unchecked(sa % sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha % shb);
            else
                result = unchecked(sha % shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha % ushb);
            else
                result = unchecked(usha % ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia % ib);
            else
                result = unchecked(ia % ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia % uib);
            else
                result = unchecked(uia % uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la % lb);
            else
                result = unchecked(la % lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula % ulb);
            else
                result = unchecked(ula % ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa % fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa % fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da % db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da % db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca % dcb);
            }
            else
            {
                result = unchecked(dca % dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Modulus");
            if (method is null)
                throw new InvalidOperationException($"No overloaded modulus function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded modulus function found for operands of type '{a.GetType()}'");
#endif
        }

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> ModuloFuncChecked = (a, b) =>
    {
        var res = checked(a % b);
        CheckOverflow(res);

        return res;
    };
#endif

    public static object? AddPercent(object? a, object? b)
    {
        return AddPercent(a, b, CultureInfo.CurrentCulture);
    }

    public static object? Add(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "+", options);
        b = ConvertIfNeeded(b, "+", options);

        if (TryComputeULongWithSignedInteger(a, b, ArithmeticOperation.Add, out var mixedResult))
            return ReduceNumericType(mixedResult, reduceTypes ? null : t, options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options, out var typesWereExpanded);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Addition is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (typeCode == TypeCode.Object)
        {
            if (a is ComplexNumber) // then b is ComplexNumber too ;)
            {
                return (ComplexNumber)a + (ComplexNumber)b;
            }

            if (a is NCalcVector va)
            {
                if (b is NCalcVector vb)
                    return va + vb;
                throw new InvalidOperationException("Addition of a vector and a scalar is not supported.");
            }

            if (b is NCalcVector)
                throw new InvalidOperationException("Addition of a scalar and a vector is not supported.");

            if (options.UseBigNumbers || IsBigNumber(a) || IsBigNumber(b))
            {
                CheckBigSumSize(a, b, "addition");
                if (a is BigDecimal bdA)
                {
                    return ReduceNumericType(Add(bdA, b), reduceTypes ? null : t, options);
                }
                else
                if (b is BigDecimal bdB)
                {
                    return ReduceNumericType(Add(bdB, a), reduceTypes ? null : t, options);
                }
                else
                if (a is BigInteger biA)
                {
                    return ReduceNumericType(Add(biA, b), reduceTypes ? null : t, options);
                }
                else
                if (b is BigInteger biB)
                {
                    return ReduceNumericType(Add(biB, a), reduceTypes ? null : t, options);
                }
                else
                if (a is long || a is ulong || b is long || b is ulong)
                {
                    BigInteger result;
                    if (a is long || a is ulong)
                    {
                        if (a is long la)
                            result = new BigInteger(la);
                        else
                            result = new BigInteger((ulong)a);

                        result = Add(result, b);
                    }
                    else
                    {
                        if (b is long lb)
                            result = new BigInteger(lb);
                        else
                            result = new BigInteger((ulong)b);

                        result = Add(result, a);
                    }
                    return ReduceNumericType(result, reduceTypes ? null : t, options);
                }
            }
        }

        try
        {
            object result = ExecuteOperation(a, b, '+', ArithmeticOperation.Add, options, typeCode);
            if (TryPromoteWrappedInteger(a, b, result, false, options, out var promoted))
                result = promoted;
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
        catch (OverflowException)
        {
            if (!TryExpandAfterOverflow(ref a, ref b, options))
                throw;
            object? result = Add(a, b, reduceTypes, options);
            if (result is null)
                return null;
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
    }

    public static object? AddPercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, "+", options);
        b = ConvertIfNeeded(b, "+", options);

        //var func = options.OverflowProtection ? AddPercentFuncChecked : AddPercentFunc;
        return ExecuteOperation(a, b, '+', ArithmeticOperation.AddPercent, options);
    }

   /* public static object? Subtract(object? a, object? b)
    {
        return Subtract(a, b, CultureInfo.CurrentCulture);
    }*/

    public static object? SubtractPercent(object? a, object? b)
    {
        return SubtractPercent(a, b, CultureInfo.CurrentCulture);
    }

    public static object? Subtract(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "-", options);
        b = ConvertIfNeeded(b, "-", options);

        if (TryComputeULongWithSignedInteger(a, b, ArithmeticOperation.Subtract, out var mixedResult))
            return ReduceNumericType(mixedResult, reduceTypes ? null : t, options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options, out var typesWereExpanded);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Subtraction is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (typeCode == TypeCode.Object)
        {
            if (a is ComplexNumber) // then b is ComplexNumber too ;)
            {
                return (ComplexNumber)a - (ComplexNumber)b;
            }

            if (a is NCalcVector va)
            {
                if (b is NCalcVector vb)
                    return va - vb;
                throw new InvalidOperationException("Subtraction of a scalar from a vector is not supported.");
            }

            if (b is NCalcVector)
                throw new InvalidOperationException("Subtraction of a vector from a scalar is not supported.");

            if (options.UseBigNumbers || IsBigNumber(a) || IsBigNumber(b))
            {
                CheckBigSumSize(a, b, "subtraction");
                BigInteger? result = null;
                BigDecimal? bdResult = null;

                // If any of the arguments are BigDecimal, calculate the result and either reduce it to minimal size
                switch (a)
                {
                    case BigDecimal bdA:
                        bdResult = Subtract(bdA, b);
                        break;
                    default:
                        if (b is BigDecimal bdB)
                        {
                            bdResult = Subtract(a, bdB);
                        }

                        break;
                }

                if (bdResult is not null)
                {
                    if (IsWholeNumber(bdResult.Value))
                    {
                        result = GetWholeValue(bdResult.Value);
                        return ReduceNumericType(result, reduceTypes ? null : t, options);
                    }
                    else
                    {
                        return ReduceNumericType(bdResult, reduceTypes ? null : t, options);
                    }
                }

                // If there was no BigDecimal calculation performed, proceed with the operation
                if (result is null)
                {
                    if (a is BigInteger biA)
                    {
                        result = Subtract(biA, b);
                    }
                    else
                    if (b is BigInteger biB)
                    {
                        result = Subtract(a, biB);
                    }
                    else
                    if (a is long || a is ulong || b is long || b is ulong)
                    {
                        if (a is long || a is ulong)
                        {
                            if (a is long la)
                                result = new BigInteger(la);
                            else
                                result = new BigInteger((ulong)a);

                            result = Subtract(result.Value, b);
                        }
                        else
                        {
                            if (b is long lb)
                                result = new BigInteger(lb);
                            else
                                result = new BigInteger((ulong)b);

                            result = Subtract(a, result.Value);
                        }
                    }
                }

                if (result is not null)
                    return ReduceNumericType(result, reduceTypes ? null : t, options);
            }
        }
        //var func = options.OverflowProtection ? SubtractFuncChecked : SubtractFunc;
        try
        {
            object result = ExecuteOperation(a, b, '-', ArithmeticOperation.Subtract, options, typeCode);
            if (TryPromoteWrappedInteger(a, b, result, true, options, out var promoted))
                result = promoted;
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
        catch (OverflowException)
        {
            if (!TryExpandAfterOverflow(ref a, ref b, options))
                throw;
            object? result = Subtract(a, b, reduceTypes, options);
            if (result is null)
                return result;
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
    }

    // The number of decimal digits of a big number (0 for other values), counted as for the result of a power: the trailing zeros
    // that a positive exponent of a BigDecimal stands for are digits; the decimal places of a negative exponent are digits only
    // as far as the mantissa is shorter than them (1.5^300 has 353 digits, 300 of them decimals; 1E-600 has 600)
    private static double BigNumberDigits(object value)
    {
        switch (value)
        {
            case BigInteger bi when !bi.IsZero:
                return BigInteger.Log10(BigInteger.Abs(bi)) + 1;
            case BigDecimal bd when !bd.Mantissa.IsZero:
                double mantissaDigits = BigInteger.Log10(BigInteger.Abs(bd.Mantissa)) + 1;
                return bd.Exponent >= 0 ? mantissaDigits + bd.Exponent : Math.Max(mantissaDigits, -(double)bd.Exponent);
            default:
                return 0;
        }
    }

    /// <summary>
    /// Rejects a product of big numbers that would have more than <see cref="MaxPowResultDigits"/> digits: the digits of a product
    /// are the sum of the digits of its factors, so repeated squaring (x = x * x in a loop or in nested function calls) would otherwise
    /// double the size of the value at each step.
    /// </summary>
    private static void CheckBigProductSize(object a, object b)
    {
        double digits = BigNumberDigits(a) + BigNumberDigits(b);
        if (digits > MaxPowResultDigits)
            throw new NCalcEvaluationException($"The result of the multiplication would have about {digits:0} digits, which exceeds the limit of {MaxPowResultDigits} digits (MathHelper.MaxPowResultDigits).");
    }

    /// <summary>
    /// Rejects a sum or difference of two BigDecimals that would have more than <see cref="MaxPowResultDigits"/> digits: the digits of
    /// the result span from the most significant digit of the larger operand down to the smallest exponent (1E+1000000 + 1E-1000000).
    /// </summary>
    private static void CheckBigSumSize(object a, object b, string operation)
    {
        if (a is not BigDecimal bdA || b is not BigDecimal bdB || bdA.Mantissa.IsZero || bdB.Mantissa.IsZero || bdA.Exponent == bdB.Exponent)
            return;
        double digits = Math.Max(DecimalMagnitude(bdA), DecimalMagnitude(bdB)) - Math.Min(bdA.Exponent, bdB.Exponent);
        if (digits > MaxPowResultDigits)
            throw new NCalcEvaluationException($"The result of the {operation} would have about {digits:0} digits, which exceeds the limit of {MaxPowResultDigits} digits (MathHelper.MaxPowResultDigits).");
    }

    /// <summary>
    /// Rejects a quotient of big numbers that would have more than <see cref="MaxPowResultDigits"/> digits: its integer digits (or its
    /// decimal places) span the difference of the magnitudes of the operands, so repeated division by a small fraction (x / (1 / x))
    /// would otherwise double the size of the value at each step, as repeated squaring would.
    /// </summary>
    private static void CheckBigQuotientSize(object a, object b)
    {
        if (NumberMagnitude(a) is not { } magnitudeA || NumberMagnitude(b) is not { } magnitudeB)
            return;
        double digits = Math.Abs(magnitudeA - magnitudeB) + 1;
        if (digits > MaxPowResultDigits)
            throw new NCalcEvaluationException($"The result of the division would have about {digits:0} digits, which exceeds the limit of {MaxPowResultDigits} digits (MathHelper.MaxPowResultDigits).");
    }

    // The decimal magnitude of a non-zero finite number (see DecimalMagnitude), computed without converting a BigInteger to BigDecimal
    private static double? NumberMagnitude(object value)
    {
        switch (value)
        {
            case BigInteger bi:
                return bi.IsZero ? null : Math.Floor(BigInteger.Log10(BigInteger.Abs(bi))) + 1;
            case BigDecimal bd:
                return bd.Mantissa.IsZero ? null : DecimalMagnitude(bd);
            default:
                if (!IsBoxedNumber(value) || IsNaNOrInfinity(value))
                    return null;
                double d = Math.Abs(Convert.ToDouble(value, CultureInfo.InvariantCulture));
                return d == 0 ? null : Math.Floor(Math.Log10(d)) + 1;
        }
    }

    // Tells whether a number can be converted to decimal without leaving its range or becoming 0: decimal has no exponent, so a value
    // below its resolution of 1E-28 would silently become 0 (a quotient such as 1E-40 stays a double or a BigDecimal instead)
    private static bool IsInDecimalResolution(object value) => value switch
    {
        double d => d == 0 || (Math.Abs(d) >= 1e-28 && !IsOutsideDecimalRange(d)),
        float f => f == 0 || (Math.Abs(f) >= 1e-28f && !IsOutsideDecimalRange(f)),
        BigDecimal bd => bd.Mantissa.IsZero || (DecimalMagnitude(bd) is >= -27 and <= 29 && bd >= decimal.MinValue && bd <= decimal.MaxValue),
        _ => true
    };

    // A ulong and a signed integer have no common 64-bit type, so their sum, difference or product is computed exactly as a big integer,
    // whatever the width of the signed operand: 5UL - 7 is -2, as 5UL - 7L and 5UL + (-7) are. The result is a ulong when it is not
    // negative and fits, a long when it is negative and fits, and a BigInteger beyond both ranges, whatever the options: such a pair
    // has no 64-bit type to wrap around in, and so -a, 0 - a and a * -1 give the same value (-18446744073709551615 for ulong.MaxValue),
    // as the negation of a ulong does.
    private static bool TryComputeULongWithSignedInteger(object a, object b, ArithmeticOperation operation, out object result)
    {
        result = null!;
        if (!((a is ulong && b is sbyte or short or int or long) || (b is ulong && a is sbyte or short or int or long)))
            return false;

        BigInteger valueA = ConvertToBigInteger(a);
        BigInteger valueB = ConvertToBigInteger(b);
        BigInteger exact = operation switch
        {
            ArithmeticOperation.Add => valueA + valueB,
            ArithmeticOperation.Subtract => valueA - valueB,
            _ => valueA * valueB
        };

        if (exact.Sign >= 0 && exact <= ulong.MaxValue)
            result = (ulong)exact;
        else
        if (exact.Sign < 0 && exact >= long.MinValue)
            result = (long)exact;
        else
            result = exact;
        return true;
    }

    // With UseBigNumbers, an integer sum or difference that wrapped around in the operand type is replaced with the exact value,
    // as products and powers are promoted to BigInteger with this option
    private static bool TryPromoteWrappedInteger(object a, object b, object result, bool subtract, MathHelperOptions options, out BigInteger exact)
    {
        exact = default;
        if (!options.UseBigNumbers || !IsBoxedIntegerNumber(a) || !IsBoxedIntegerNumber(b) || !IsBoxedIntegerNumber(result))
            return false;

        exact = subtract ? ConvertToBigInteger(a) - ConvertToBigInteger(b) : ConvertToBigInteger(a) + ConvertToBigInteger(b);
        return exact != ConvertToBigInteger(result);
    }

    public static object? SubtractPercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, "-", options);
        b = ConvertIfNeeded(b, "-", options);

        //var func = options.OverflowProtection ? SubtractPercentFuncChecked : SubtractPercentFunc;
        return ExecuteOperation(a, b, '-', ArithmeticOperation.SubtractPercent, options);
    }

    public static object? MultiplyPercent(object? a, object? b)
    {
        return MultiplyPercent(a, b, CultureInfo.CurrentCulture);
    }

    public static object? Multiply(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "*", options);
        b = ConvertIfNeeded(b, "*", options);

        if (TryComputeULongWithSignedInteger(a, b, ArithmeticOperation.Multiply, out var mixedResult))
            return ReduceNumericType(mixedResult, reduceTypes ? null : t, options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options, out var typesWereExpanded);

        // The product of two unsigned values always fits in the next unsigned type, but not in the signed type of that size
        // (255 * 255 > short.MaxValue), so it is computed in that type, also with OverflowProtection (uint.MaxValue * uint.MaxValue fits in ulong)
        TypeCode? unsignedProductTypeCode = typeCode switch { TypeCode.Byte => TypeCode.UInt16, TypeCode.UInt16 => TypeCode.UInt32, TypeCode.UInt32 => TypeCode.UInt64, _ => null };
        if (unsignedProductTypeCode is { } productTypeCode)
        {
            a = Convert.ChangeType(a, productTypeCode, options.CultureInfo);
            b = Convert.ChangeType(b, productTypeCode, options.CultureInfo);
            typeCode = productTypeCode;
            typesWereExpanded = true;
        }
        else
        if (!options.OverflowProtection)
        {
            // For multiplication, we must up the number of bits to avoid a possible overflow.
            // We can't just call ConvertToHighestPrecision(...,...,true,...) because ConvertToHighestPrecision will do unexpected things.
            // There is no wider 64-bit type for a ulong product; converting it to long would fail for values above long.MaxValue,
            // so without big numbers the product is computed in ulong (and wraps around, as in C#)
            if (typeCode != TypeCode.UInt64 || options.UseBigNumbers)
                typeCode = TypeCodeExpandBits(typeCode, ref a, ref b, options);
            typesWereExpanded = true;
        }

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Multiplication is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (typeCode == TypeCode.Object)
        {
            if (a is ComplexNumber) // then b is ComplexNumber too ;)
            {
                return (ComplexNumber)a * (ComplexNumber)b;
            }

            if (a is NCalcVector va)
            {
                if (b is NCalcVector)
                    throw new InvalidOperationException("Element-wise vector multiplication is not supported; use Dot() or Cross() for vector products.");
                return va * (BigDecimal)b;
            }
            if (b is NCalcVector vb)
                return vb * (BigDecimal)a;

            if (options.UseBigNumbers || IsBigNumber(a) || IsBigNumber(b))
            {
                CheckBigProductSize(a, b);
                if (a is BigDecimal bdA)
                {
                    return ReduceNumericType(Multiply(bdA, b), reduceTypes ? null : t, options);
                }
                else
                if (b is BigDecimal bdB)
                {
                    return ReduceNumericType(Multiply(bdB, a), reduceTypes ? null : t, options);
                }
                else
                if (a is BigInteger biA)
                {
                    return ReduceNumericType(Multiply(biA, b), reduceTypes ? null : t, options);
                }
                else
                if (b is BigInteger biB)
                {
                    return ReduceNumericType(Multiply(biB, a), reduceTypes ? null : t, options);
                }
                else
                if (a is long || a is ulong || b is long || b is ulong)
                {
                    BigInteger result;
                    if (a is long || a is ulong)
                    {
                        if (a is long la)
                            result = new BigInteger(la);
                        else
                            result = new BigInteger((ulong)a);

                        result = Multiply(result, b);
                    }
                    else
                    {
                        if (b is long lb)
                            result = new BigInteger(lb);
                        else
                            result = new BigInteger((ulong)b);

                        result = Multiply(result, a);
                    }
                    return ReduceNumericType(result, reduceTypes ? null : t, options);
                }
            }
        }

        //var func = options.OverflowProtection ? MultiplyFuncChecked : MultiplyFunc;
        try
        {
            object result = ExecuteOperation(a, b, '*', ArithmeticOperation.Multiply, options, typeCode);
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
        catch (OverflowException)
        {
            if (!TryExpandAfterOverflow(ref a, ref b, options))
                throw;
            object? result = Multiply(a, b, reduceTypes, options);
            if (result is null)
                return null;
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
    }

    public static object? MultiplyPercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, "*", options);
        b = ConvertIfNeeded(b, "*", options);

        //var func = options.OverflowProtection ? MultiplyPercentFuncChecked : MultiplyPercentFunc;
        return ExecuteOperation(a, b, '*', ArithmeticOperation.MultiplyPercent, options);
    }

   /* public static object? Divide(object? a, object? b)
    {
        return Divide(a, b, CultureInfo.CurrentCulture);
    }
*/
    public static object? Divide(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type typeA = a.GetType(); //Type t = GetBroaderType(a.GetType(), b.GetType());
        bool bigOperand = IsBigNumber(a) || IsBigNumber(b);

        a = ConvertIfNeeded(a, "/", options);
        b = ConvertIfNeeded(b, "/", options);

        bool decimalOperand = a is decimal || b is decimal;
        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        // A decimal and a double beyond the range of decimal are computed in double, and the quotient stays a double, as their
        // product and sum do (2.5m / 1e300 is 2.5E-300, not 0)
        bool computedInDouble = decimalOperand && typeCode == TypeCode.Double;

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Division is not implemented for operands of types {a.GetType()} and {b.GetType()}");

        if ((typeCode == TypeCode.Object) && (a is ComplexNumber)) // then b is ComplexNumber too ;)
        {
            return (ComplexNumber)a / (ComplexNumber)b;
        }

        if (typeCode == TypeCode.Object)
        {
            if (a is NCalcVector va)
            {
                if (b is NCalcVector)
                    throw new InvalidOperationException("Division of a vector by another vector is not supported.");
                return va / (BigDecimal)b;
            }
            if (b is NCalcVector)
                throw new InvalidOperationException("Division of a scalar by a vector is not supported.");
        }

        // Convert types to floating-point ones because otherwise, we get an integer division.
        // NaN and the infinities have no exact value and are divided in double.
        if (options.UseBigNumbers && !IsNaNOrInfinity(a) && !IsNaNOrInfinity(b))
        {
            if (typeCode != TypeCode.Object)
            {
                a = ConvertToBigDecimal(a);
                b = ConvertToBigDecimal(b);
                typeCode = TypeCode.Object;
            }
        }
        else
        {
            if (IsBoxedIntegerNumber(a))
            {
                // Every integer type, including a ulong above long.MaxValue, fits in decimal
                a = GetBoxedNumberAsDecimal(a)!.Value;
                typeCode = TypeCode.Decimal;
            }
            else
            if (a is decimal)
            {
                typeCode = TypeCode.Decimal;
            }
            else
            if (a is float)
            {
                double? da = GetBoxedNumberAsDouble(a);
                if (da is null)
                    return null;
                a = da.Value;
                typeCode = TypeCode.Double;
            }

            if (IsBoxedIntegerNumber(b))
            {
                // Every integer type, including a ulong above long.MaxValue, fits in decimal
                b = GetBoxedNumberAsDecimal(b)!.Value;
                typeCode = TypeCode.Decimal;
            }
            else
            if (b is decimal)
            {
                typeCode = TypeCode.Decimal;
            }
            else
            if (b is float)
            {
                double? db = GetBoxedNumberAsDouble(b);
                if (db is null)
                    return null;
                b = db.Value;
                typeCode = TypeCode.Double;
            }
        }

        if ((options.UseBigNumbers || IsBigNumber(a) || IsBigNumber(b)) && typeCode == TypeCode.Object)
        {
            CheckBigQuotientSize(a, b);
            BigDecimal? bdResult = null;

            if (a is BigDecimal bdA)
            {
                bdResult = Divide(bdA, b);
            }
            else
            if (b is BigDecimal bdB)
            {
                bdResult = Divide(a, bdB);
            }
            else
            if (a is BigInteger biA)
            {
                bdResult = Divide(new BigDecimal(biA), b);
            }
            else
            if (b is BigInteger biB)
            {
                bdResult = Divide(a, new BigDecimal(biB));
            }
            /*else
            if (a is long || a is ulong || b is long || b is ulong)
            {
                if (a is long || a is ulong)
                {
                    if (a is long la)
                        bdResult = new BigDecimal(new BigInteger(la));
                    else
                        bdResult = new BigDecimal(new BigInteger((ulong)a));

                    bdResult = Divide(bdResult.Value, b);
                }
                else
                {
                    if (b is long lb)
                        bdResult = new BigDecimal(new BigInteger(lb));
                    else
                        bdResult = new BigDecimal(new BigInteger((ulong)b));

                    bdResult = Divide(a, bdResult.Value);
                }
            }*/

            if (bdResult is not null)
            {
                // With a big-number operand, a fractional result is narrowed only as far as no digits are lost
                if (bigOperand && !IsWholeNumber(bdResult.Value))
                    return ReduceNumericType(bdResult.Value, null, options);

                if (reduceTypes)
                {
                    if ((IsBoxedIntegerNumberOrBigNumber(typeA) || options.ReduceDivResultToInteger) && IsWholeNumber(bdResult.Value))
                    {
                        BigInteger biResult = GetWholeValue(bdResult.Value);

                        if (biResult >= long.MinValue && biResult <= long.MaxValue)
                            return (long)biResult;
                        else
                        if (biResult >= ulong.MinValue && biResult <= ulong.MaxValue)
                            return (ulong)biResult;

                        return biResult;
                    }

                    if ((options.DecimalAsDefault || typeA == typeof(decimal)) && IsInDecimalResolution(bdResult.Value))
                    {
                        return BigDecimalToDecimal(bdResult.Value);
                    }

                    /*if (bdResult >= float.MinValue && bdResult <= float.MaxValue)
                    {
                        return (float)bdResult;
                    }
                    else*/
                    if (bdResult >= double.MinValue && bdResult <= double.MaxValue)
                    {
                        return BigDecimalToDouble(bdResult.Value);
                    }

                    if (bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue)
                    {
                        return BigDecimalToDecimal(bdResult.Value);
                    }
                }
                else // try to keep the original type of a
                {
                    if (IsBoxedIntegerNumberOrBigNumber(typeA) && IsWholeNumber(bdResult.Value))
                    {
                        BigInteger biResult = GetWholeValue(bdResult.Value);

                        if ((typeA == typeof(short)) && biResult >= short.MinValue && biResult <= short.MaxValue)
                            return (short)biResult;

                        if ((typeA == typeof(ushort)) && biResult >= ushort.MinValue && biResult <= ushort.MaxValue)
                            return (ushort)biResult;

                        if ((typeA == typeof(int)) && biResult >= int.MinValue && biResult <= int.MaxValue)
                            return (int)biResult;

                        if ((typeA == typeof(uint)) && biResult >= uint.MinValue && biResult <= uint.MaxValue)
                            return (uint)biResult;

                        if ((typeA == typeof(long)) && biResult >= long.MinValue && biResult <= long.MaxValue)
                            return (long)biResult;

                        if ((typeA == typeof(ulong)) && biResult >= ulong.MinValue && biResult <= ulong.MaxValue)
                            return (ulong)biResult;

                        return biResult;
                    }

                    if ((options.DecimalAsDefault || typeA == typeof(decimal)) && IsInDecimalResolution(bdResult.Value))
                        return BigDecimalToDecimal(bdResult.Value);

                    // A quotient that would underflow to 0 in float stays a BigDecimal
                    if ((typeA == typeof(float)) && bdResult >= float.MinValue && bdResult <= float.MaxValue && (bdResult.Value.IsZero() || BigDecimalToSingle(bdResult.Value) != 0))
                        return BigDecimalToSingle(bdResult.Value);

                    if ((typeA == typeof(double)) && bdResult >= double.MinValue && bdResult <= double.MaxValue)
                        return BigDecimalToDouble(bdResult.Value);
                }

                return bdResult;
            }

            typeCode = TypeCode.Empty;
        }

        if (a is null || b is null)
            return null;

        //var func = options.OverflowProtection ? DivideFuncChecked : DivideFunc;

        object? result = null;

        try
        {
            result = ExecuteOperation(a, b, '/', ArithmeticOperation.Divide, options, typeCode);
        }
        catch (OverflowException)
        {
            if (!TryExpandAfterOverflow(ref a, ref b, options))
                throw;
            result = Divide(a, b, reduceTypes, options);
        }
        if (result is null)
            return result;

        if (reduceTypes || options.ReduceDivResultToInteger)
        {
            if ((IsBoxedIntegerNumberOrBigNumber(typeA) || options.ReduceDivResultToInteger) && IsBoxedFloatingNumberInteger(result) == true)
            {
                long? iResult = GetBoxedIntegerNumberAsLong(result);
                if (iResult is null)
                    return GetBoxedIntegerNumberAsULong(result) is { } ulResult ? ulResult : result;

                if (iResult >= int.MinValue && iResult <= int.MaxValue)
                    return (int)iResult;

                if (iResult >= uint.MinValue && iResult <= uint.MaxValue)
                    return (uint)iResult;

                return iResult;
            }
            else
            if ((options.DecimalAsDefault || typeA == typeof(decimal)) && (result is not decimal) && !computedInDouble && IsInDecimalResolution(result))
            {
                try
                {
                    if (result is BigDecimal)
                        return BigDecimalToDecimal((BigDecimal)result);

                    return ConvertToDecimal(result, "Divide", options.CultureInfo, null);
                }
                catch
                {
                }
            }
            else
            {
                double? dResult = null;
                if (result is BigDecimal bdResult)
                {
                    if (bdResult >= double.MinValue && bdResult <= double.MaxValue)
                        dResult = BigDecimalToDouble(bdResult);
                }
                else
                if (result is double dd)
                {
                    dResult = dd;
                }

                if (dResult is not null)
                {
                    /*if (dResult >= float.MinValue && dResult <= float.MaxValue)
                      return (float)dResult;*/

                    return dResult;
                }
            }

            return result;
        }
        else // try to keep the original type of a
        {
            if (IsBoxedIntegerNumberOrBigNumber(typeA) && IsBoxedFloatingNumberInteger(result) == true)
            {
                long? iResult = GetBoxedIntegerNumberAsLong(result);
                if (iResult is null)
                    return GetBoxedIntegerNumberAsULong(result) is { } ulResult ? ulResult : result;

                if ((typeA == typeof(short)) && iResult >= short.MinValue && iResult <= short.MaxValue)
                    return (short)iResult;

                if ((typeA == typeof(ushort)) && iResult >= ushort.MinValue && iResult <= ushort.MaxValue)
                    return (ushort)iResult;

                if ((typeA == typeof(int)) && iResult >= int.MinValue && iResult <= int.MaxValue)
                    return (int)iResult;

                if ((typeA == typeof(uint)) && iResult >= uint.MinValue && iResult <= uint.MaxValue)
                    return (uint)iResult;

                if ((typeA == typeof(ulong)) && iResult >= (long) ulong.MinValue)
                    return (ulong)iResult;

                if ((typeA == typeof(long)) && iResult >= long.MinValue && iResult <= long.MaxValue)
                    return (long)iResult;

                return iResult;
            }

            // A float divided by a decimal gives a decimal, which stays as it is; a quotient that would underflow to 0 in float stays a double
            if ((typeA == typeof(float)) && result is double dResult && dResult >= float.MinValue && dResult <= float.MaxValue && (dResult == 0 || (float)dResult != 0))
            {
                return (float)dResult;
            }

            if ((typeA == typeof(decimal)) && (result is not decimal) && !computedInDouble && IsInDecimalResolution(result))
            {
                try
                {
                    return ConvertToDecimal(result, "Div", options.CultureInfo, null);
                }
                catch
                {
                }
            }

            return result;
        }
    }

    public static object? IntegerDivide(object? a, object? b, bool truncateFirst, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "/", options);
        b = ConvertIfNeeded(b, "/", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Integer division is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if ((typeCode == TypeCode.Object) && (a is ComplexNumber)) // then b is ComplexNumber too ;)
        {
            if (a is ComplexNumber cnA)
            {
                if (!cnA.IsReal)
                    throw new InvalidOperationException(
                        $"Integer division is not implemented for complex numbers");
                a = cnA.Real;
                if (!options.UseBigNumbers)
                {
                    a = BigDecimalToDouble(cnA.Real);
                    typeCode = TypeCode.Double;
                }
            }

            if (b is ComplexNumber cnB)
            {
                if (!cnB.IsReal)
                    throw new InvalidOperationException(
                        $"Integer division is not implemented for complex numbers");
                b = cnB.Real;
                if (!options.UseBigNumbers)
                {
                    b = BigDecimalToDouble(cnB.Real);
                    typeCode = TypeCode.Double;
                }
            }
        }

        if ((options.UseBigNumbers || IsBigNumber(a) || IsBigNumber(b)) && typeCode == TypeCode.Object)
        {
            // The quotient is floored exactly (FloorDivide); a division with the limited precision of BigDecimal followed by a floor
            // would lose the last digits of a quotient with more than BigDecimal.Precision digits
            BigInteger? biResult = null;
            if (a is BigDecimal bdA)
            {
                if (truncateFirst)
                {
                    BigInteger biA = GetWholeValue(bdA);
                    if (b is BigDecimal bdDivisor)
                        b = GetWholeValue(bdDivisor);
                    else
                    if (b is not BigInteger)
                        b = ConvertToLong(b, "Integer Div", options.CultureInfo, null);

                    biResult = IntegerDivide(biA, b);
                }
                else
                {
                    biResult = FloorDivide(bdA, ConvertToBigDecimal(b));
                }
            }
            else
            if (b is BigDecimal bdB)
            {
                if (truncateFirst)
                {
                    BigInteger biB = GetWholeValue(bdB);
                    if (a is not BigInteger)
                        a = ConvertToLong(a, "Integer Div", options.CultureInfo, null);

                    biResult = IntegerDivide(a, biB);
                }
                else
                {
                    biResult = FloorDivide(ConvertToBigDecimal(a), bdB);
                }
            }
            else
            if (a is BigInteger biA)
            {
                if (truncateFirst)
                {
                    if (b is not BigInteger)
                        b = ConvertToLong(b, "Integer Div", options.CultureInfo, null);
                    biResult = IntegerDivide(biA, b);
                }
                else
                {
                    biResult = FloorDivide(new BigDecimal(biA), ConvertToBigDecimal(b));
                }
            }
            else
            if (b is BigInteger biB)
            {
                if (truncateFirst)
                {
                    a = ConvertToLong(a, "Integer Div", options.CultureInfo, null);

                    biResult = IntegerDivide(a, biB);
                }
                else
                {
                    biResult = FloorDivide(ConvertToBigDecimal(a), new BigDecimal(biB));
                }
            }
            else
            if (a is long || a is ulong || b is long || b is ulong)
            {
                if (a is long || a is ulong)
                {
                    if (a is long la)
                        biResult = new BigInteger(la);
                    else
                        biResult = new BigInteger((ulong)a);

                    if (truncateFirst)
                        biResult = IntegerDivide(biResult.Value, b);
                    else
                        biResult = FloorDivide(new BigDecimal(biResult.Value), ConvertToBigDecimal(b));
                }
                else
                {
                    if (b is long lb)
                        biResult = new BigInteger(lb);
                    else
                        biResult = new BigInteger((ulong)b);

                    if (truncateFirst)
                        biResult = IntegerDivide(a, biResult.Value);
                    else
                        biResult = FloorDivide(ConvertToBigDecimal(a), new BigDecimal(biResult.Value));
                }
            }

            if (biResult is not null)
            {
                return ReduceNumericType(biResult, reduceTypes ? null : t, options);
            }
        }

        if (truncateFirst)
        {
            a = TruncateForIntegerDivision(a, options);
            b = TruncateForIntegerDivision(b, options);

            // Integer operands are divided exactly, so that a ulong above long.MaxValue and long.MinValue \ -1 do not overflow;
            // a floating-point operand beyond the range of long (or NaN) is divided below and stays a floating-point number
            if (IsBoxedIntegerNumber(a) && IsBoxedIntegerNumber(b))
            {
                BigInteger quotient = BigInteger.Divide(ConvertToBigInteger(a), ConvertToBigInteger(b));
                return ReduceNumericType(quotient >= long.MinValue && quotient <= long.MaxValue ? (object)(long)quotient : quotient, reduceTypes ? null : t, options);
            }
        }

        //var func = options.OverflowProtection ? DivideFuncChecked : DivideFunc;
        object? result = null;

        try
        {
            if (!truncateFirst)
                result = Divide(a, b, reduceTypes, options);
            else
                result = ExecuteOperation(a, b, '/', ArithmeticOperation.Divide, options);
        }
        catch (OverflowException)
        {
            if (!TryExpandAfterOverflow(ref a, ref b, options))
                throw;

            result = Divide(a, b, reduceTypes, options);
        }

        if (result is null)
            return result;

        if (IsBoxedIntegerNumber(result))
            return ReduceNumericType(result, reduceTypes ? null : t, options);

        // A quotient beyond the range of long (or an infinite one) stays a floating-point number instead of being clamped
        switch (result)
        {
            case double dq when !FitsInLong(truncateFirst ? Math.Truncate(dq) : Math.Floor(dq)):
                return truncateFirst ? Math.Truncate(dq) : Math.Floor(dq);
            case float fq when !FitsInLong(truncateFirst ? Math.Truncate(fq) : Math.Floor(fq)):
                return (float)(truncateFirst ? Math.Truncate(fq) : Math.Floor(fq));
            case decimal mq when (truncateFirst ? Math.Truncate(mq) : Math.Floor(mq)) is var fm && (fm < long.MinValue || fm > long.MaxValue):
                return fm;
        }

        if (reduceTypes)
        {
            if (result is decimal decResult)
            {
                long lResult = truncateFirst ? (long)Math.Truncate(decResult) : (long) Math.Floor(decResult);
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;

                return lResult;
            }
            if (result is double dResult)
            {
                long lResult = truncateFirst ? (long)Math.Truncate(dResult) : (long)Math.Floor(dResult);
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;

                return lResult;
            }
            if (result is float fResult)
            {
                long lResult = truncateFirst ? (long)Math.Truncate(fResult) : (long)Math.Floor(fResult);
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;

                return lResult;
            }
        }
        else
        {
            if (result is decimal decResult)
            {
                long lResult = truncateFirst ? (long)Math.Truncate(decResult) : (long)Math.Floor(decResult);
                if (t == typeof(long) || lResult < Int32.MinValue || lResult > Int32.MaxValue)
                    return lResult;

                return (int)lResult;
            }
            if (result is double dResult)
            {
                long lResult = truncateFirst ? (long)Math.Truncate(dResult) : (long)Math.Floor(dResult);
                if (t == typeof(long) || lResult < Int32.MinValue || lResult > Int32.MaxValue)
                    return lResult;

                return (int)lResult;
            }
            if (result is float fResult)
            {
                long lResult = truncateFirst ? (long)Math.Truncate(fResult) : (long)Math.Floor(fResult);
                if (t == typeof(long) || lResult < Int32.MinValue || lResult > Int32.MaxValue)
                    return lResult;

                return (int)lResult;
            }
            else
            if (result is long lResult)
            {
                if (t == typeof(long) || lResult < Int32.MinValue || lResult > Int32.MaxValue)
                    return lResult;

                return (int)lResult;
            }
        }

        return result;
    }

    // Truncates an operand of \ to an integer. Integers stay as they are; a floating-point value that does not fit in long
    // (including NaN and infinity) is only truncated, because casting it to long would clamp it or give 0.
    private static object TruncateForIntegerDivision(object value, MathHelperOptions options)
    {
        switch (value)
        {
            case byte or sbyte or short or ushort or int or uint or long or ulong:
                return value;
            case double d:
                double truncatedDouble = Math.Truncate(d);
                return FitsInLong(truncatedDouble) ? (object)(long)truncatedDouble : truncatedDouble;
            case float f:
                double truncatedSingle = Math.Truncate((double)f);
                return FitsInLong(truncatedSingle) ? (object)(long)truncatedSingle : truncatedSingle;
            case decimal m:
                decimal truncatedDecimal = Math.Truncate(m);
                return truncatedDecimal >= long.MinValue && truncatedDecimal <= long.MaxValue ? (object)(long)truncatedDecimal : truncatedDecimal;
            default:
                return ConvertToLong(value, "Integer Div", options.CultureInfo, null);
        }
    }

    public static object? DividePercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, "/", options);
        b = ConvertIfNeeded(b, "/", options);

        //var func = options.OverflowProtection ? DividePercentFuncChecked : DividePercentFunc;
        return ExecuteOperation(a, b, '/', ArithmeticOperation.DividePercent, options);
    }

    /*public static object? Modulo(object? a, object? b)
    {
        return Modulo(a, b, CultureInfo.CurrentCulture);
    }*/

    public static object? Modulo(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        // Vector dispatch — element-wise modulo (remainder of a truncated division, consistent with scalar BigDecimal modulo)
        if (a is NCalcVector va)
        {
            if (b is NCalcVector vb)
                return va % vb;
            return va % ConvertToBigDecimal(b);
        }
        if (b is NCalcVector)
            throw new InvalidOperationException("Modulo of a scalar by a vector is not supported.");

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "%", options);
        b = ConvertIfNeeded(b, "%", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options, out var typesWereExpanded);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"The Modulo operation is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if ((typeCode == TypeCode.Object) && (a is ComplexNumber)) // then b is ComplexNumber too ;)
        {
            if (a is ComplexNumber cnA)
            {
                if (!cnA.IsReal)
                    throw new InvalidOperationException(
                        $"The Modulo operation is not implemented for complex numbers");
                a = cnA.Real;
                if (!options.UseBigNumbers)
                {
                    a = BigDecimalToDouble(cnA.Real);
                    typeCode = TypeCode.Double;
                }
            }

            if (b is ComplexNumber cnB)
            {
                if (!cnB.IsReal)
                    throw new InvalidOperationException(
                        $"The Modulo operation is not implemented for complex numbers");
                b = cnB.Real;
                if (!options.UseBigNumbers)
                {
                    b = BigDecimalToDouble(cnB.Real);
                    typeCode = TypeCode.Double;
                }
            }
        }

        if ((options.UseBigNumbers || IsBigNumber(a) || IsBigNumber(b)) && typeCode == TypeCode.Object)
        {
            // A BigDecimal operand keeps its fractional part, like the remainder of double and decimal operands
            if (a is BigDecimal || b is BigDecimal)
                return ReduceNumericType(Remainder(ConvertToBigDecimal(a), ConvertToBigDecimal(b)), reduceTypes ? null : t, options);

            BigInteger? biResult = null;
            if (a is BigInteger biA)
            {
                if (!(b is BigInteger))
                    b = ConvertToBigInteger(b);

                biResult = BigInteger.Remainder(biA, (BigInteger)b);
            }
            else
            if (b is BigInteger biB)
            {
                if (!(a is BigInteger))
                    a = ConvertToBigInteger(a);

                biResult = BigInteger.Remainder((BigInteger)a, biB);
            }
            else
            if (a is long || a is ulong || b is long || b is ulong)
            {
                biResult = BigInteger.Remainder(ConvertToBigInteger(a), ConvertToBigInteger(b));
            }
            if (biResult is not null)
                return ReduceNumericType(biResult, reduceTypes ? null : t, options);
        }

        // long.MinValue % -1 overflows in .NET although the remainder is 0; any value % -1 is 0
        if (typeCode == TypeCode.Int64 && b is -1L)
            return ReduceNumericType(0L, reduceTypes ? null : t, options);

        try
        {
            object result = ExecuteOperation(a, b, '%', ArithmeticOperation.Modulo, options, typeCode);
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
        catch (OverflowException)
        {
            if (!TryExpandAfterOverflow(ref a, ref b, options))
                throw;

            object? result = Modulo(a, b, reduceTypes, options);
            if (result is null)
                return null;

            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
    }

    public static object? Max(object? a, object? b, MathHelperOptions options)
    {
        return MaxOrMin(a, b, true, options);

        /*
        if (a is null && b is null)
        {
            return null;
        }

        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        a = ConvertIfNeeded(a, "Max", options);
        b = ConvertIfNeeded(b, "Max", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Maximum is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            if (a is BigInteger biA)
            {
                return Max(b, biA);
            }
            else
            if (b is BigInteger biB)
            {
                return Max(a, biB);
            }
        }

        return typeCode switch
        {
            TypeCode.Byte => Math.Max((byte)a!, (byte)b!),
            TypeCode.SByte => Math.Max((sbyte)a!, (sbyte)b!),
            TypeCode.Int16 => Math.Max((short)a!, (short)b!),
            TypeCode.UInt16 => Math.Max((ushort)a!, (ushort)b!),
            TypeCode.Int32 => Math.Max((int)a!, (int)b!),
            TypeCode.UInt32 => Math.Max((uint)a!, (uint)b!),
            TypeCode.Int64 => Math.Max((long)a!, (long)b!),
            TypeCode.UInt64 => Math.Max((ulong)a!, (ulong)b!),
            TypeCode.Single => Math.Max((float)a!, (float)b!),
            TypeCode.Double => Math.Max((double)a!, (double)b!),
            TypeCode.Decimal => Math.Max((decimal)a!, (decimal)b!),
            _ => null,
        };*/
    }

    public static object? Min(object? a, object? b, MathHelperOptions options)
    {
        return MaxOrMin(a, b, false, options);

        /*if (a is null && b is null)
        {
            return null;
        }

        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        a = ConvertIfNeeded(a, "Min", options);
        b = ConvertIfNeeded(b, "Min", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Minimum is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            if (a is BigInteger biA)
            {
                return Min(b, biA);
            }
            else
            if (b is BigInteger biB)
            {
                return Min(a, biB);
            }
        }

        return typeCode switch
        {
            TypeCode.Byte => Math.Min((byte)a!, (byte)b!),
            TypeCode.SByte => Math.Min((sbyte)a!, (sbyte)b!),
            TypeCode.Int16 => Math.Min((short)a!, (short)b!),
            TypeCode.UInt16 => Math.Min((ushort)a!, (ushort)b!),
            TypeCode.Int32 => Math.Min((int)a!, (int)b!),
            TypeCode.UInt32 => Math.Min((uint)a!, (uint)b!),
            TypeCode.Int64 => Math.Min((long)a!, (long)b!),
            TypeCode.UInt64 => Math.Min((ulong)a!, (ulong)b!),
            TypeCode.Single => Math.Min((float)a!, (float)b!),
            TypeCode.Double => Math.Min((double)a!, (double)b!),
            TypeCode.Decimal => Math.Min((decimal)a!, (decimal)b!),
            _ => null
        };*/
    }

    /// <summary>
    /// Flips the sign of a boxed numeric value.
    /// For float and double, negating +0 produces -0 (IEEE 754 negative zero).
    /// For integer types and decimal, negating 0 produces 0 (no negative zero concept).
    /// Negating the minimum value of a signed integer type produces the next wider type, as the result does not fit the original one.
    /// Returns null if the input is null or an unsupported type.
    /// </summary>
    public static object? Negate(object? value, bool reduceType, MathHelperOptions options)
    {
        if (value is null)
            return null;

        // Every arm is cast to object: otherwise the switch expression gets BigDecimal as its natural type
        // and every result is implicitly converted to BigDecimal before boxing
        object result = value switch
        {
            // Signed integers — negate directly; the minimum values are promoted to the next wider type
            sbyte sb => sb == sbyte.MinValue ? (object)(short)(-sb) : (object)(sbyte)(-sb),
            short s => s == short.MinValue ? (object)(-s) : (object)(short)(-s),
            int i => i == int.MinValue ? (object)(-(long)i) : (object)(-i),
            long l => l == long.MinValue ? (object)unchecked((ulong)l) : (object)(-l),

            // Unsigned integers — negated in the next wider signed type
            byte b => (object)(-b),
            ushort us => (object)(-us),
            uint ui => (object)(-(long)ui),
            // 2^63 is the negation of long.MinValue, so negating it gives long.MinValue back; the negation of a larger ulong
            // is beyond the range of long and becomes a BigInteger
            ulong ul => options.UseBigNumbers || ul > (1UL << 63) ? (object)(-(new BigInteger(ul))) : (ul == (1UL << 63) ? (object)long.MinValue : (object)(-(long)ul)),

            // IEEE 754: -0.0f and -0.0d are distinct bit patterns from +0
            float f => (object)(-f),   // correctly produces -0f when f == +0f
            double d => (object)(-d),   // correctly produces -0d when d == +0d

            // decimal has no negative zero — negating 0m gives 0m
            decimal dc => (object)(-dc),

            // BigInteger has no negative zero either
            BigInteger bi => (object)(-bi),

            BigDecimal bd => (object)(-bd),

            // Vectors and complex numbers are negated by their operators; a percent keeps being a percent
            NCalcVector vector => (object)(-vector),
            ComplexNumber complex => (object)(-complex),
            Percent percent when percent.Value is not null => (object)new Percent(Negate(percent.Value, reduceType, options)!),

            _ => throw new NCalcEvaluationException($"Unsupported type {value.GetType().Name} in the negate operation"),
        };

        if (result is NCalcVector or ComplexNumber or Percent)
            return result;

        if (reduceType)
            return ReduceNumericType(result, null, options);

        return result;
    }

    /// <summary>
    /// The method attempts to raise the number of bits in <paramref name="a"/> and <paramref name="b"/>, update the arguments to have a new type, and return the type code of the resulting type.
    /// </summary>
    /// <param name="a">The first argument to expand.</param>
    /// <param name="b">The second argument to expand.</param>
    /// <param name="forceExpandBits">Specifies that the arguments should be expanded even when they are of the same type. This parameter is used only when handling an overflow exception, where it is known that the original type does not work.</param>
    /// <param name="options">Calculation options.</param>
    /// <returns>The type of the converted variables or <seealso cref="TypeCode.Empty"/> if arguments are not numbers or if there is nowhere more to expand the arguments (e.g., when the original type is <see langword="double"/> and big numbers are disabled). The latter case is used to indicate that attempts to expand the arguments should be stopped.</returns>
    public static TypeCode ConvertToHighestPrecision(ref object a, ref object b, bool forceExpandBits, MathHelperOptions options)
    {
        return ConvertToHighestPrecision(ref a, ref b, forceExpandBits, options, out _);
    }

    /// <summary>
    /// Widens the operands of an operation that has overflowed, so that the operation can be retried with the wider types.
    /// </summary>
    /// <returns><see langword="true"/> if the type of at least one operand has changed; <see langword="false"/> if there is nowhere to widen the operands.
    /// In the latter case, the caller must rethrow the overflow instead of retrying: a retry with the same operand types fails the same way and would recurse infinitely.</returns>
    private static bool TryExpandAfterOverflow(ref object a, ref object b, MathHelperOptions options)
    {
        Type typeA = a.GetType();
        Type typeB = b.GetType();
        if (ConvertToHighestPrecision(ref a, ref b, true, options) == TypeCode.Empty)
            return false;
        return a.GetType() != typeA || b.GetType() != typeB;
    }

    public static TypeCode ConvertToHighestPrecision(ref object a, ref object b, bool forceExpandBits, MathHelperOptions options, out bool typesWereExpanded)
    {
        typesWereExpanded = false;

        if (options.AllowCharValues)
        {
            if (a is char)
                a = Convert.ChangeType(a, TypeCode.UInt16, options.CultureInfo);
            if (b is char)
                b = Convert.ChangeType(b, TypeCode.UInt16, options.CultureInfo);
        }

        if (a is ComplexNumber cnA && b is ComplexNumber cnB && cnA.IsReal && cnB.IsReal)
        {
            a = ReduceComplexToReal(cnA, options);
            b = ReduceComplexToReal(cnB, options);
        }
        else
        if (a is ComplexNumber cnA2 && cnA2.IsReal)
        {
            a = ReduceComplexToReal(cnA2, options);
        }
        else
        if (b is ComplexNumber cnB2 && cnB2.IsReal)
        {
            b = ReduceComplexToReal(cnB2, options);
        }

        var typeA = a.GetType();
        var typeB = b.GetType();
        var typeCodeA = Type.GetTypeCode(typeA);
        var typeCodeB = Type.GetTypeCode(typeB);

        if (typeCodeA == typeCodeB && !forceExpandBits && a is not ComplexNumber && b is not ComplexNumber && a is not NCalcVector && b is not NCalcVector)
            return typeCodeA;

        if (TypeCodeBitSize(typeCodeA, out var floatingPointA) is not { } bitSizeA)
            return TypeCode.Empty;

        if (TypeCodeBitSize(typeCodeB, out var floatingPointB) is not { } bitSizeB)
            return TypeCode.Empty;

        if (a is ComplexNumber)
        {
            if (b is ComplexNumber)
            {
                return TypeCode.Object;
            }
            else
            {
                b = new ComplexNumber(MathHelper.ConvertToBigDecimal(b), BigDecimal.Zero, options);
                typesWereExpanded = true;
                return TypeCode.Object;
            }
        }

        if (b is ComplexNumber) //a is not a complex number here
        {
            a = new ComplexNumber(MathHelper.ConvertToBigDecimal(a), BigDecimal.Zero, options);
            typesWereExpanded = true;
            return TypeCode.Object;
        }

        if (a is NCalcVector)
        {
            if (b is not NCalcVector)
            {
                // Vector op scalar: promote the scalar to BigDecimal so the arithmetic operators can consume it
                b = ConvertToBigDecimal(b);
                typesWereExpanded = true;
            }
            return TypeCode.Object;
        }

        if (b is NCalcVector) // a is not a vector here
        {
            // scalar op Vector: promote the scalar to BigDecimal so the arithmetic operators can consume it
            a = ConvertToBigDecimal(a);
            typesWereExpanded = true;
            return TypeCode.Object;
        }

        // A big-number operand promotes the other operand to a big number even when UseBigNumbers is not set;
        // otherwise, the pair stays as it is and the operation cannot be carried out (or overflows).
        // NaN and the infinities have no exact value: a big number with such a value is computed in double, as + and - with
        // UseBigNumbers are (a big number beyond the range of double becomes an infinity)
        if ((IsBigNumber(a) || IsBigNumber(b)) && (IsNaNOrInfinity(a) || IsNaNOrInfinity(b)))
        {
            a = ToDoubleOrInfinity(a);
            b = ToDoubleOrInfinity(b);
            typesWereExpanded = true;
            return TypeCode.Double;
        }

        if (IsBigNumber(a) || IsBigNumber(b))
        {
            if (a is BigInteger)
            {
                if (b is BigDecimal)
                {
                    a = ConvertToBigDecimal(a);
                }
                else
                if (IsBoxedFloatingNumber(b))
                {
                    a = ConvertToBigDecimal(a);
                    b = ConvertToBigDecimal(b);
                }
                else
                if (b is not BigInteger)
                {
                    b = ConvertToBigInteger(b);
                }
                return TypeCode.Object;
            }
            if (b is BigInteger)
            {
                if (a is BigDecimal)
                {
                    b = ConvertToBigDecimal(b);
                }
                else
                if (IsBoxedFloatingNumber(a))
                {
                    a = ConvertToBigDecimal(a);
                    b = ConvertToBigDecimal(b);
                }
                else
                {
                    a = ConvertToBigInteger(a);
                }

                return TypeCode.Object;
            }
            if (a is BigDecimal)
            {
                if (b is not BigDecimal)
                b = ConvertToBigDecimal(b);
                return TypeCode.Object;
            }
            if (b is BigDecimal)
            {
                a = ConvertToBigDecimal(a);
                return TypeCode.Object;
            }
        }

        // A double or float beyond the range of decimal (or NaN, or an infinity) cannot be converted to decimal, so a decimal and such a value
        // are computed in double, as they are compared; with UseBigNumbers, a finite value is computed exactly as a BigDecimal
        if ((typeCodeA == TypeCode.Decimal && IsOutsideDecimalRange(b)) || (typeCodeB == TypeCode.Decimal && IsOutsideDecimalRange(a)))
        {
            typesWereExpanded = true;
            if (options.UseBigNumbers && !IsNaNOrInfinity(a) && !IsNaNOrInfinity(b))
            {
                a = ConvertToBigDecimal(a);
                b = ConvertToBigDecimal(b);
                return TypeCode.Object;
            }
            a = Convert.ToDouble(a, options.CultureInfo);
            b = Convert.ToDouble(b, options.CultureInfo);
            return TypeCode.Double;
        }

        // When one operand is floating-point and the other is an integer, the integer is converted to the floating-point type, whatever the bit sizes are.
        // A float cannot hold a 64-bit integer with a meaningful precision, so such a pair is computed in double.
        if (floatingPointA != floatingPointB && (floatingPointA ? typeCodeA : typeCodeB) == TypeCode.Single && (floatingPointA ? bitSizeB : bitSizeA) >= 64)
        {
            a = Convert.ToDouble(a, options.CultureInfo);
            b = Convert.ToDouble(b, options.CultureInfo);
            typesWereExpanded = true;
            return TypeCode.Double;
        }

        // A signed integer and an unsigned integer of at least its size (up to 32 bits) are computed in the next wider signed type, as in C#;
        // converting the signed operand to the unsigned type would turn 0 - 1u into 4294967295
        if (!floatingPointA && !floatingPointB && IsUnsignedTypeCode(typeCodeA) != IsUnsignedTypeCode(typeCodeB))
        {
            int unsignedSize = IsUnsignedTypeCode(typeCodeA) ? bitSizeA : bitSizeB;
            int signedSize = IsUnsignedTypeCode(typeCodeA) ? bitSizeB : bitSizeA;
            if (unsignedSize >= signedSize && unsignedSize <= 32)
            {
                TypeCode signedTypeCode = unsignedSize switch { 8 => TypeCode.Int16, 16 => TypeCode.Int32, _ => TypeCode.Int64 };
                a = Convert.ChangeType(a, signedTypeCode, options.CultureInfo);
                b = Convert.ChangeType(b, signedTypeCode, options.CultureInfo);
                typesWereExpanded = true;
                return signedTypeCode;
            }
        }

        if ((floatingPointA && !floatingPointB) || (bitSizeA > bitSizeB && floatingPointA == floatingPointB))
        {
            try
            {
                b = Convert.ChangeType(b, typeCodeA, options.CultureInfo);
                typesWereExpanded = true;
                return typeCodeA;
            }
            catch (Exception icex) when (icex is InvalidCastException or FormatException)
            {
                throw new NCalcConversionException($"Conversion of a value '{b}' of type {typeB.Name} to type '{typeA.Name}' failed.", b.ToString() ?? "null", typeB, typeA, icex);
            }
            catch (OverflowException)
            {
                typesWereExpanded = true;
                // the code below is used to upgrade both variables
                return TypeCodeExpandBits(typeCodeA, ref a, ref b, options);
            }
        }
        else
        if ((!floatingPointA && floatingPointB) || (bitSizeB > bitSizeA && floatingPointA == floatingPointB))
        {
            try
            {
                a = Convert.ChangeType(a, typeCodeB, options.CultureInfo);
                typesWereExpanded = true;
                return typeCodeB;
            }
            catch (Exception icex) when (icex is InvalidCastException or FormatException)
            {
                throw new NCalcConversionException($"Conversion of a value '{a}' of type {typeA.Name} to type '{typeB.Name}' failed.", a.ToString() ?? "null", typeA, typeB, icex);
            }
            catch (OverflowException)
            {
                typesWereExpanded = true;
                // the code below is used to upgrade both variables
                return TypeCodeExpandBits(typeCodeB, ref a, ref b, options);
            }
        }
        else // same size, different types
        {
            // the code below is used to upgrade both variables
            TypeCode resultTypeCode = TypeCodeExpandBits(typeCodeB, ref a, ref b, options);
            if (typeCodeA == typeCodeB && typeCodeA == resultTypeCode)
            {
                return TypeCode.Empty; // nowhere else to expand
            }
            else
            {
                typesWereExpanded = true;
                return resultTypeCode;
            }
        }
    }

    private static TypeCode TypeCodeExpandBits(TypeCode typeCode, ref object a, ref object b, MathHelperOptions options)
    {
        TypeCode result = TypeCode.Empty;
        Type t = typeof(long);
        switch (typeCode)
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
                result = TypeCode.Int16;
                t = typeof(short);
                break;
            case TypeCode.Int16:
            case TypeCode.UInt16:
                result = TypeCode.Int32;
                t = typeof(int);
                break;
            case TypeCode.Int32:
            case TypeCode.UInt32:
                result = TypeCode.Int64;
                t = typeof(long);
                break;
            case TypeCode.Int64:
            case TypeCode.UInt64:
                if (options.UseBigNumbers)
                {
                    a = ConvertToBigInteger(a);
                    b = ConvertToBigInteger(b);
                    return TypeCode.Object;
                }
                else
                if (a.GetType() != b.GetType() && IsBoxedIntegerNumber(a) && IsBoxedIntegerNumber(b) && ((a is ulong ulA && ulA > long.MaxValue) || (b is ulong ulB && ulB > long.MaxValue)))
                {
                    // A ulong beyond the range of long and a signed integer have no common 64-bit type; they are computed exactly as big integers
                    a = ConvertToBigInteger(a);
                    b = ConvertToBigInteger(b);
                    return TypeCode.Object;
                }
                else
                {
                    result = TypeCode.Int64;
                }

                break;
            case TypeCode.Single:
                result = TypeCode.Double;
                t = typeof(double);
                break;
            case TypeCode.Double:
                // NaN and the infinities cannot be converted to BigDecimal and stay doubles
                if (options.UseBigNumbers && !IsNaNOrInfinity(a) && !IsNaNOrInfinity(b))
                {
                    a = ConvertToBigDecimal(a);
                    b = ConvertToBigDecimal(b);
                    return TypeCode.Object;
                }
                else
                {
                    result = typeCode;
                    t = typeof(double);
                }

                break;
            case TypeCode.Decimal:
                if (options.UseBigNumbers)
                {
                    a = ConvertToBigDecimal(a);
                    b = ConvertToBigDecimal(b);
                    return TypeCode.Object;
                }
                else
                {
                    result = typeCode;
                    t = typeof(decimal);
                }
                break;
            case TypeCode.Object:
                return options.UseBigNumbers || IsBigNumber(a) || IsBigNumber(b) || a is ComplexNumber || b is ComplexNumber || a is NCalcVector || b is NCalcVector ? TypeCode.Object : TypeCode.Empty;
            default:
                return TypeCode.Empty;
        }
        if (result != TypeCode.Empty && result != TypeCode.Object)
        {
            var typeA = a.GetType();
            var typeB = b.GetType();

            try
            {
                a = Convert.ChangeType(a, result, options.CultureInfo);
            }
            catch (Exception icex) when (icex is InvalidCastException or FormatException)
            {
                throw new NCalcConversionException($"Conversion of a value '{a}' of type {typeA.Name} to type '{t.Name}' failed.", a.ToString() ?? "null", typeA, t, icex);
            }

            try
            {
                b = Convert.ChangeType(b, result, options.CultureInfo);
            }
            catch (Exception icex) when (icex is InvalidCastException or FormatException)
            {
                throw new NCalcConversionException($"Conversion of a value '{b}' of type {typeB.Name} to type '{t.Name}' failed.", b.ToString() ?? "null", typeB, t, icex);
            }
        }
        return result;
    }

    /// <summary>
    /// Tells whether a value is a double or float that cannot be converted to decimal: NaN, an infinity or a value whose magnitude is at least
    /// 2^96 (the nearest double to <see cref="decimal.MaxValue"/> is 2^96 itself, which is beyond the range of decimal).
    /// </summary>
    internal static bool IsOutsideDecimalRange(object? value) => value switch
    {
        double d => double.IsNaN(d) || double.IsInfinity(d) || d <= (double)decimal.MinValue || d >= (double)decimal.MaxValue,
        float f => float.IsNaN(f) || float.IsInfinity(f) || f <= (float)decimal.MinValue || f >= (float)decimal.MaxValue,
        _ => false
    };

    private static bool IsNaNOrInfinity(object value)
        => value is double d ? double.IsNaN(d) || double.IsInfinity(d) : value is float f && (float.IsNaN(f) || float.IsInfinity(f));

    // Converts a number to double; a big number beyond the range of double becomes an infinity of its sign
    private static double ToDoubleOrInfinity(object value)
    {
        switch (value)
        {
            case BigInteger bi:
                return (double)bi;
            case BigDecimal bd when bd.Mantissa.IsZero || DecimalMagnitude(bd) < -330:
                return 0.0;
            case BigDecimal bd when DecimalMagnitude(bd) > 310:
                return bd.Sign * double.PositiveInfinity;
            case BigDecimal bd:
                try
                {
                    return BigDecimalToDouble(bd);
                }
                catch (OverflowException)
                {
                    return bd.Sign * double.PositiveInfinity;
                }
            default:
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
    }

    private static bool IsUnsignedTypeCode(TypeCode typeCode) => typeCode is TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64;

    private static int? TypeCodeBitSize(TypeCode typeCode, out bool floatingPoint)
    {
        floatingPoint = false;
        switch (typeCode)
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
                return 8;
            case TypeCode.Int16:
            case TypeCode.UInt16:
                return 16;
            case TypeCode.Int32:
            case TypeCode.UInt32:
                return 32;
            case TypeCode.Int64:
            case TypeCode.UInt64:
                return 64;
            case TypeCode.Single:
                floatingPoint = true;
                return 32;
            case TypeCode.Double:
                floatingPoint = true;
                return 64;
            case TypeCode.Decimal:
                floatingPoint = true;
                return 128;
            case TypeCode.Object:
                return int.MaxValue;
            default: return null;
        }
    }

    public static Type GetBroaderType(Type typeA, Type typeB)
    {
        if (typeA == typeB)
            return typeA;

        if (typeA == typeof(ComplexNumber))
            return typeA;

        if (typeB == typeof(ComplexNumber))
            return typeB;

        if (typeA == typeof(BigDecimal))
            return typeA;

        if (typeB == typeof(BigDecimal))
            return typeB;

        if (typeA == typeof(BigInteger))
            return typeA;

        if (typeB == typeof(BigInteger))
            return typeB;

        if (typeA == typeof(decimal))
            return typeA;

        if (typeB == typeof(decimal))
            return typeB;

        if (typeA == typeof(double))
            return typeA;

        if (typeB == typeof(double))
            return typeB;

        if (typeA == typeof(float))
            return typeA;

        if (typeB == typeof(float))
            return typeB;

        if (typeA == typeof(long))
            return typeA;

        if (typeB == typeof(long))
            return typeB;

        if (typeA == typeof(ulong))
            return typeA;

        if (typeB == typeof(ulong))
            return typeB;

        if (typeA == typeof(int))
            return typeA;

        if (typeB == typeof(int))
            return typeB;

        if (typeA == typeof(uint))
            return typeA;

        if (typeB == typeof(uint))
            return typeB;

        if (typeA == typeof(short))
            return typeA;

        if (typeB == typeof(short))
            return typeB;

        if (typeA == typeof(ushort))
            return typeA;

        if (typeB == typeof(ushort))
            return typeB;

        if (typeA == typeof(char))
            return typeA;

        if (typeB == typeof(char))
            return typeB;

        if (typeA == typeof(sbyte))
            return typeA;

        if (typeB == typeof(sbyte))
            return typeB;

        if (typeA == typeof(byte))
            return typeA;

        if (typeB == typeof(byte))
            return typeB;

        return typeA;
    }

    public static object Abs(object? a, MathHelperOptions options)
    {
        if (a is Percent percent && percent.Value is not null)
            return new Percent(Abs(percent.Value, options));

        // A big number keeps its type even when UseBigNumbers is not set
        if (a is BigInteger biA)
            return BigInteger.Abs(biA);
        if (a is BigDecimal bdA)
            return bdA.Sign < 0 ? -bdA : bdA;

        if (a is ComplexNumber cnA)
            return cnA.Abs;

        if (a is NCalcVector vA)
            return NCalcVector.Abs(vA);

        // An integer is made positive exactly, as by negation: the minimum value of a signed type becomes the next wider type
        if (a is not null && IsBoxedIntegerNumber(a))
            return ConvertToBigInteger(a).Sign < 0 ? Negate(a, false, options)! : a;

        if (UseDecimalArithmetic(a, options))
            return Math.Abs(ConvertToDecimal(a, "Abs", options.CultureInfo, null));

        return Math.Abs(ConvertToDouble(a, "Abs", options.CultureInfo, null));
    }

    /// <summary>
    /// Whether a rounding function (Abs, Ceiling, Floor, Round, Sign, Truncate) computes its argument as a decimal: a decimal
    /// argument always, and any other one with DecimalAsDefault, except a double or float that decimal cannot hold
    /// (NaN, infinity, a magnitude of 7.9E+28 or more, or a small value whose digits do not fit in the 28 decimal places of
    /// decimal, such as 1E-30, which would become 0), which is computed as a double, as without the option.
    /// </summary>
    private static bool UseDecimalArithmetic(object? a, MathHelperOptions options)
    {
        if (a is decimal)
            return true;
        if (!options.DecimalAsDefault)
            return false;

        return a switch
        {
            // (double)decimal.MaxValue rounds up to 2^96, which is already outside the range of decimal
            double d => !double.IsNaN(d) && Math.Abs(d) < (double)decimal.MaxValue && KeepsDigitsAsDecimal(d, 1E-13),
            float f => !float.IsNaN(f) && Math.Abs(f) < (float)decimal.MaxValue && KeepsDigitsAsDecimal(f, 1E-21),
            _ => true
        };
    }

    /// <summary>
    /// Whether the conversion of a double (or float) to decimal keeps its significant digits (15 for a double, 7 for a float),
    /// which a value below <paramref name="limit"/> may not: decimal has only 28 decimal places, so 1.234E-27 becomes 1E-27 and
    /// 1E-30 becomes 0 (NCALC-311). A conversion that needs all 28 places is taken as cut.
    /// </summary>
    private static bool KeepsDigitsAsDecimal(double value, double limit)
    {
        if (value == 0 || Math.Abs(value) >= limit)
            return true;

        decimal converted = (decimal)value;
        return converted != 0 && ((decimal.GetBits(converted)[3] >> 16) & 0xFF) < 28;
    }

    public static object Acos(object? a, MathHelperOptions options)
    {
        //BigDecimal.

        if (a is ComplexNumber cnA)
            return ComplexNumber.Acos(cnA, options);

        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigArccos(ToBigDecimalArgument(a));
        }
        return Math.Acos(ConvertToDouble(a, "Acos", options.CultureInfo, null));
    }

    public static object Arccot(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return Truncated(() => BigDecimal.Arccot(new BigDecimal(biA)));
            }
            if (a is BigDecimal bdA)
            {
                return Truncated(() => BigDecimal.Arccot(bdA));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Acot(cnA, options);

        return Arccot(ConvertToDouble(a, "Arccot", options.CultureInfo, null));
    }

    public static double Arccot(double x)
    {
        return Math.PI / 2 - Math.Atan(x);
    }

    public static object Arccsc(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return Truncated(() => BigDecimal.Arccsc(new BigDecimal(biA)));
            }
            if (a is BigDecimal bdA)
            {
                return Truncated(() => BigDecimal.Arccsc(bdA));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Acsc(cnA, options);

        return Arccsc(ConvertToDouble(a, "Arccot", options.CultureInfo, null));
    }

    public static double Arccsc(double x)
    {
        if (x == 0.0 || Math.Abs(x) < 1.0)
            throw new ArgumentOutOfRangeException(nameof(x), "arccsc is defined only for |x| >= 1.");

        return Math.Asin(1.0 / x);
    }

    public static object Arcsec(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return Truncated(() => BigDecimal.Arcsec(new BigDecimal(biA)));
            }
            if (a is BigDecimal bdA)
            {
                return Truncated(() => BigDecimal.Arcsec(bdA));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Asec(cnA, options);

        return Arcsec(ConvertToDouble(a, "Arccot", options.CultureInfo, null));
    }

    public static double Arcsec(double x)
    {
        if (x == 0.0 || Math.Abs(x) < 1.0)
            throw new ArgumentOutOfRangeException(nameof(x), "arcsec is defined only for |x| >= 1.");

        return Math.Acos(1.0 / x);
    }

    public static object Asin(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigArcsin(ToBigDecimalArgument(a));
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Asin(cnA, options);

        return Math.Asin(ConvertToDouble(a, "Asin", options.CultureInfo, null));
    }

    public static object Atan(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigArctan(ToBigDecimalArgument(a));
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Atan(cnA, options);

        return Math.Atan(ConvertToDouble(a, "Atan", options.CultureInfo, null));
    }

    public static object Atan2(object? a, object? b, MathHelperOptions options)
    {
        return Math.Atan2(ConvertToDouble(a, "Atan2", options.CultureInfo, null), ConvertToDouble(b, "Atan2", options.CultureInfo, null));
    }

    public static object Ceiling(object? a, MathHelperOptions options)
    {
        if (a is Percent percent && percent.Value is not null)
            return new Percent(Ceiling(percent.Value, options));

        // An integer is already whole and is returned unchanged, so that it does not lose digits through double
        if (a is not null && IsBoxedIntegerNumber(a))
            return a;

        // A big number is rounded up exactly even when UseBigNumbers is not set
        if (a is BigInteger biA)
            return biA;
        if (a is BigDecimal bdA)
            return CeilingBigDecimal(bdA);

        if (a is ComplexNumber cnA)
            return ComplexNumber.Ceiling(cnA);

        if (a is NCalcVector vA)
            return NCalcVector.Ceiling(vA);

        if (UseDecimalArithmetic(a, options))
            return Math.Ceiling(ConvertToDecimal(a, "Ceiling", options.CultureInfo, null));

        return Math.Ceiling(ConvertToDouble(a, "Ceiling", options.CultureInfo, null));
    }

    public static object Cos(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigTrigonometric(ToBigDecimalArgument(a), BigTrigFunction.Cos);
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Cos(cnA, options);

        return Math.Cos(ConvertToDouble(a, "Cos", options.CultureInfo, null));
    }

    public static object Cosh(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return Truncated(() => BigDecimal.Cosh(new BigDecimal(biA)));
            }
            if (a is BigDecimal bdA)
            {
                return Truncated(() => BigDecimal.Cosh(bdA));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Cosh(cnA, options);

        return Math.Cosh(ConvertToDouble(a, "Cosh", options.CultureInfo, null));
    }

    public static object Cot(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigTrigonometric(ToBigDecimalArgument(a), BigTrigFunction.Cot);
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Cot(cnA, options);

        return Cot(ConvertToDouble(a, "Cot", options.CultureInfo, null));
    }

    public static double Cot(double x)
    {
        double sin = Math.Sin(x);
        double cos = Math.Cos(x);

        if (sin == 0.0)
            throw new ArgumentOutOfRangeException(nameof(x), "cot is undefined when sin(x) = 0.");

        return cos / sin;
    }

    public static object Coth(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return Truncated(() => BigDecimal.Coth(new BigDecimal(biA)));
            }
            if (a is BigDecimal bdA)
            {
                return Truncated(() => BigDecimal.Coth(bdA));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Coth(cnA, options);

        return Coth(ConvertToDouble(a, "Coth", options.CultureInfo, null));
    }

    public static double Coth(double x)
    {
        double sinh = Math.Sinh(x);
        double cosh = Math.Cosh(x);

        if (sinh == 0.0)
            throw new ArgumentOutOfRangeException(nameof(x), "coth is undefined when sinh(x) = 0.");

        return cosh / sinh;
    }

    public static object Csc(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigTrigonometric(ToBigDecimalArgument(a), BigTrigFunction.Csc);
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Csc(cnA, options);

        return Csc(ConvertToDouble(a, "Csc", options.CultureInfo, null));
    }

    public static double Csc(double x)
    {
        double sin = Math.Sin(x);

        if (sin == 0.0)
            throw new ArgumentOutOfRangeException(nameof(x), "csc is undefined when sin(x) = 0.");

        return 1.0 / sin;
    }

    public static object Csch(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return Truncated(() => BigDecimal.Csch(new BigDecimal(biA)));
            }
            if (a is BigDecimal bdA)
            {
                return Truncated(() => BigDecimal.Csch(bdA));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Csch(cnA, options);

        return Csch(ConvertToDouble(a, "Csch", options.CultureInfo, null));
    }

    public static double Csch(double x)
    {
        double sinh = Math.Sinh(x);

        if (sinh == 0.0)
            throw new ArgumentOutOfRangeException(nameof(x), "csch is undefined when sinh(x) = 0.");

        return 1.0 / sinh;
    }

    public static object Exp(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigExp(ToBigDecimalArgument(a));
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Exp(cnA, options);

        return Math.Exp(ConvertToDouble(a, "Exp", options.CultureInfo, null));
    }

    public static object Floor(object? a, MathHelperOptions options)
    {
        if (a is Percent percent && percent.Value is not null)
            return new Percent(Floor(percent.Value, options));

        // An integer is already whole and is returned unchanged, so that it does not lose digits through double
        if (a is not null && IsBoxedIntegerNumber(a))
            return a;

        // A big number is floored exactly even when UseBigNumbers is not set
        if (a is BigInteger biA)
        {
            return biA;
        }
        if (a is BigDecimal bdA)
        {
            return FloorBigDecimal(bdA);
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Floor(cnA);

        if (a is NCalcVector vA)
            return NCalcVector.Floor(vA);

        if (UseDecimalArithmetic(a, options))
            return Math.Floor(ConvertToDecimal(a, "Floor", options.CultureInfo, null));

        return Math.Floor(ConvertToDouble(a, "Floor", options.CultureInfo, null));
    }

    // ReSharper disable once InconsistentNaming
    public static object IEEERemainder(object? a, object? b, MathHelperOptions options)
    {
        if (a is ComplexNumber)
            throw new NCalcEvaluationException("The IEEERemainder function is not defined for complex numbers");

        return Math.IEEERemainder(ConvertToDouble(a, "IEEERemainder", options.CultureInfo, null), ConvertToDouble(b, "IEEEReminder", options.CultureInfo, null));
    }

    public static object Ln(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigLogarithm(ToBigDecimalArgument(a), BigDecimal.Ln);
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Log(cnA, options);

        return Math.Log(ConvertToDouble(a, "Ln", options.CultureInfo, null));
    }

    public static object Log(object? a, object? b, MathHelperOptions options)
    {
        // BigDecimal.Log takes an integer base; a fractional base (Log(x, 2.5)) is computed in double instead of being truncated to 2
        bool integralBase = b is not null && (IsBoxedIntegerNumber(b) || b is BigInteger || (b is BigDecimal bdB && IsWholeNumber(bdB)) || IsBoxedFloatingNumberInteger(b) == true)
            && b is not (double.PositiveInfinity or double.NegativeInfinity or float.PositiveInfinity or float.NegativeInfinity);
        // A big base is also used exactly with an integer or decimal argument (Log(8, 10 ** 400)), as double would make it infinite
        bool bigArgument = a is BigInteger or BigDecimal
            || (b is BigInteger or BigDecimal && a is not null && (IsBoxedIntegerNumber(a) || a is decimal));
        if (options.UseBigNumbers && integralBase && bigArgument)
            return BigLog(ConvertToBigDecimal(a!), ConvertToBigDecimal(b!));
        // With a fractional base, a big argument or base beyond the range of double is not made infinite (Log(10 ** 400, 2.5)
        // was ∞; NCALC-322): its natural logarithm is computed in double from its digits and its exponent
        if (options.UseBigNumbers && bigArgument && (b is BigDecimal or decimal || (b is double dB && !double.IsNaN(dB) && !double.IsInfinity(dB)) || (b is float fB && !float.IsNaN(fB) && !float.IsInfinity(fB))))
            return NaturalLogarithmInDouble(ConvertToBigDecimal(a!)) / NaturalLogarithmInDouble(ConvertToBigDecimal(b!));

        if (a is ComplexNumber cnA && MathHelper.IsBoxedNumberOrBigNumber(b))
            return ComplexNumber.Log(cnA, MathHelper.ConvertToBigDecimal(b!), options);

        return Math.Log(ConvertToDouble(a, "Log", options.CultureInfo, null), ConvertToDouble(b, "Log", options.CultureInfo, null));
    }

#if NET8_0_OR_GREATER
    public static object Log2(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigLogarithm(ToBigDecimalArgument(a), BigDecimal.Log2);
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Log(cnA, 2, options);

        return Math.Log2(ConvertToDouble(a, "Log2", options.CultureInfo, null));
    }
#endif

    public static object Log10(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigLogarithm(ToBigDecimalArgument(a), BigDecimal.Log10);
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Log10(cnA, options);

        return Math.Log10(ConvertToDouble(a, "Log10", options.CultureInfo, null));
    }

    public static object Pow(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (b is null)
            throw new ArgumentNullException(nameof(b));

        // A vector is raised element-wise to a scalar exponent
        if (a is NCalcVector vectorA && b is not NCalcVector)
            return NCalcVector.Pow(vectorA, b);

        // A percent exponent is its fraction: 2 ** 10% is 2 ** 0.1
        if (b is Percent percentB && percentB.Value is not null)
            b = Divide(percentB.Value, 100m, true, options) ?? b;

        // A complex base or exponent is raised through ComplexNumber.Pow, which also handles fractional exponents
        if (a is ComplexNumber cnA)
        {
            if (b is ComplexNumber cnB)
                return ComplexNumber.Pow(cnA, cnB, options);
            else
            if (MathHelper.IsBoxedNumberOrBigNumber(b))
                return ComplexNumber.Pow(cnA, MathHelper.ConvertToBigDecimal(b), options);
        }
        else
        if (b is ComplexNumber complexExponent && MathHelper.IsBoxedNumberOrBigNumber(a))
            return ComplexNumber.Pow(new ComplexNumber(MathHelper.ConvertToBigDecimal(a), BigDecimal.Zero, options), complexExponent, options);

        Type typeA = a.GetType();

        // Use BigDecimal arithmetic when the base is already a BigDecimal or BigInteger (avoids
        // the decimal-range overflow that ConvertToDecimal would cause for large values), or when
        // options request big-number arithmetic.  Non-integer exponents always fall back to
        // Math.Pow — exact fractional-exponent support via BigDecimal is not feasible with the
        // ExtendedNumerics library.
        // A NaN or infinite base has no exact value and is raised in double, as Math.Pow does
        bool tryBigDecimalPath = (a is BigDecimal || a is BigInteger || a is decimal
            || options.DecimalAsDefault || options.UseBigNumbers)
            && a is not (double.NaN or double.PositiveInfinity or double.NegativeInfinity)
            && a is not (float.NaN or float.PositiveInfinity or float.NegativeInfinity);

        BigInteger bigExp = BigInteger.Zero;
        // Zero raised to a negative power is infinite, as 1 / 0 is under the same options; it is computed in double below
        if (tryBigDecimalPath && TryGetBigIntegerExponent(b, out bigExp) && !(bigExp.Sign < 0 && ConvertToBigDecimal(a).IsZero()))
        {
            BigDecimal bdBase = ConvertToBigDecimal(a);

            // Without UseBigNumbers, a decimal base (or a fractional base with DecimalAsDefault) gives a decimal when the result fits
            // in it and a double otherwise, so the exact power is computed only when it is not too long and fits in decimal;
            // otherwise the power is computed in double
            bool fractionalBase = !options.UseBigNumbers && (a is decimal || (options.DecimalAsDefault && a is double or float));
            if (!fractionalBase || PowDigits(bdBase.Mantissa, bdBase.Exponent, BigInteger.Abs(bigExp)) <= MaxPowResultDigits)
            {
                BigDecimal bdResult = BigDecimalPow(bdBase, bigExp);

                // A decimal base gives a decimal (rounded to the precision of decimal) when the result fits in it
                if (a is decimal && !options.UseBigNumbers && bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue)
                    return BigDecimalToDecimal(bdResult);

                if (!fractionalBase || (bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue))
                    return ReduceNumericType(bdResult, reduceTypes ? null : typeA, options)
                        ?? throw new NCalcEvaluationException("Pow result could not be reduced to a smaller type");
            }
        }

        double result = Math.Pow(
            ConvertToDouble(a, "Pow", options.CultureInfo, null),
            ConvertToDouble(b, "Pow", options.CultureInfo, null));
        // A decimal base keeps its type only when the result fits in decimal; NaN, infinity and huge results stay double
        if (typeA == typeof(decimal) && !double.IsNaN(result) && !double.IsInfinity(result) && Math.Abs(result) < (double)decimal.MaxValue)
            return (decimal)result;

        return result;
    }

    // Returns true and sets exponent when b represents an integer value that can serve as a
    // BigDecimal exponent.  Handles all boxed numeric types including BigDecimal and BigInteger.
    private static bool TryGetBigIntegerExponent(object b, out BigInteger exponent)
    {
        exponent = BigInteger.Zero;
        switch (b)
        {
            case BigInteger bi:
                exponent = bi;
                return true;
            case BigDecimal bd:
                if (!IsWholeNumber(bd)) return false;
                exponent = GetWholeValue(bd);
                return true;
            case byte byt: exponent = new BigInteger(byt); return true;
            case sbyte sb: exponent = new BigInteger(sb); return true;
            case short s: exponent = new BigInteger(s); return true;
            case ushort us: exponent = new BigInteger(us); return true;
            case int i: exponent = new BigInteger(i); return true;
            case uint ui: exponent = new BigInteger(ui); return true;
            case long l: exponent = new BigInteger(l); return true;
            case ulong ul: exponent = new BigInteger(ul); return true;
            // An infinite exponent is not a whole number (Math.Truncate returns it unchanged); it is raised in double
            case float f:
                if (float.IsInfinity(f) || f != Math.Truncate(f)) return false;
                exponent = GetWholeValue(new BigDecimal(f));
                return true;
            case double d:
                if (double.IsInfinity(d) || d != Math.Truncate(d)) return false;
                if (d >= long.MinValue && d <= long.MaxValue)
                    exponent = new BigInteger((long)d);
                else
                    exponent = GetWholeValue(new BigDecimal(d));
                return true;
            case decimal m:
                if (m != Math.Truncate(m)) return false;
                exponent = GetWholeValue(new BigDecimal(m));
                return true;
            default:
                return false;
        }
    }

    // BigDecimal.DecimalPlaces and BigDecimal.WholeValue work through the string form of the number, which takes seconds for numbers
    // with tens of thousands of digits; these helpers compute the same from the mantissa and the exponent.
    // A value with more decimal places than mantissa digits (other than 0) is not whole, and its whole value is 0, so 10^-exponent
    // is not computed for it (1E-1000000 would take seconds).
    // The whole value of a value with a large positive exponent has the digits of the mantissa followed by exponent zeros; a value
    // with more than MaxPowResultDigits digits throws NCalcEvaluationException instead of taking seconds to compute (1E+1000000).
    internal static bool IsWholeNumber(BigDecimal value)
        => value.Exponent >= 0 || value.Mantissa.IsZero
        || (-(double)value.Exponent <= BigInteger.Log10(BigInteger.Abs(value.Mantissa)) + 1 && (value.Mantissa % BigInteger.Pow(10, -value.Exponent)).IsZero);

    // BigDecimal.Floor and BigDecimal.Ceiling go through the string form; Ceiling also compares the whole part with the mantissa,
    // so that Ceiling(30) is 31 when 30 is stored as 3E1
    internal static BigDecimal FloorBigDecimal(BigDecimal value)
        => new(GetWholeValue(value) - (value.Sign < 0 && !IsWholeNumber(value) ? 1 : 0));

    internal static BigDecimal CeilingBigDecimal(BigDecimal value)
        => new(GetWholeValue(value) + (value.Sign > 0 && !IsWholeNumber(value) ? 1 : 0));

    internal static BigInteger GetWholeValue(BigDecimal value)
    {
        if (value.Mantissa.IsZero)
            return BigInteger.Zero;
        double mantissaDigits = BigInteger.Log10(BigInteger.Abs(value.Mantissa)) + 1;
        if (value.Exponent >= 0)
        {
            double digits = mantissaDigits + value.Exponent;
            if (digits > MaxPowResultDigits)
                throw new NCalcEvaluationException($"The integer value of the number would have about {digits:0} digits, which exceeds the limit of {MaxPowResultDigits} digits (MathHelper.MaxPowResultDigits).");
            return value.Mantissa * BigInteger.Pow(10, value.Exponent);
        }
        if (-(double)value.Exponent > mantissaDigits)
            return BigInteger.Zero;
        return BigInteger.Divide(value.Mantissa, BigInteger.Pow(10, -value.Exponent));
    }

    // The explicit conversions of BigDecimal to double, float and decimal format the number with the culture that was current when
    // BigDecimal was first used, but parse it with the current culture, so after a culture change 1.5 becomes 15; its conversion to int
    // fails on any fraction. These helpers format the mantissa and the exponent invariantly instead.
    // A value beyond the range of the target type throws OverflowException, as the library's conversions do.
    private static string ToInvariantExponentString(BigDecimal value)
        => value.Mantissa.ToString(CultureInfo.InvariantCulture) + "E" + value.Exponent.ToString(CultureInfo.InvariantCulture);

    internal static double BigDecimalToDouble(BigDecimal value)
    {
        double result = double.Parse(ToInvariantExponentString(value), NumberStyles.Float, CultureInfo.InvariantCulture);
        return double.IsInfinity(result) ? throw new OverflowException("The value is outside the range of double.") : result;
    }

    internal static float BigDecimalToSingle(BigDecimal value)
    {
        float result = float.Parse(ToInvariantExponentString(value), NumberStyles.Float, CultureInfo.InvariantCulture);
        return float.IsInfinity(result) ? throw new OverflowException("The value is outside the range of float.") : result;
    }

    /// <summary>
    /// Formats a BigDecimal with the decimal separator of the given culture (the invariant culture by default). BigDecimal.ToString()
    /// uses the culture current at the first use of BigDecimal, and BigDecimal.ToString(provider) pads the fraction to
    /// NumberDecimalDigits places (1 becomes "1.00"), so the culture's number format is used without the padding.
    /// </summary>
    internal static string FormatBigDecimal(BigDecimal value, IFormatProvider? provider)
    {
        var numberFormat = (NumberFormatInfo)NumberFormatInfo.GetInstance(provider ?? CultureInfo.InvariantCulture).Clone();
        numberFormat.NumberDecimalDigits = 0;
        return value.ToString(numberFormat);
    }

    internal static decimal BigDecimalToDecimal(BigDecimal value)
        => decimal.Parse(ToInvariantExponentString(value), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>
    /// The maximum number of significant digits of an exact (big-number or decimal) integer power or big-number factorial.
    /// A power or factorial whose result would have more digits throws <see cref="NCalcEvaluationException"/> before it is computed,
    /// so that a short expression such as "2 ** 2000000" or "1000000!" cannot occupy the CPU for a long time. The same limit applies to
    /// big-number shifts, products (the digits of the factors are added up, so repeated squaring cannot build unbounded values), sums of
    /// BigDecimals with very different exponents, and to the quotient computed by % and // of big numbers. The digits of a BigDecimal
    /// include the trailing zeros or decimal places that its exponent stands for. The default is 100,000.
    /// </summary>
    public static int MaxPowResultDigits { get; set; } = 100_000;

    // Raises baseValue to an integer power exactly: the digits (mantissa) are raised with BigInteger.Pow and the decimal scale is multiplied.
    // Handles zero, one, negative exponents, and a zero base.
    private static BigDecimal BigDecimalPow(BigDecimal baseValue, BigInteger exponent)
    {
        if (exponent.IsZero)
            return BigDecimal.One;

        if (exponent.IsOne)
            return baseValue;

        if (baseValue.IsZero())
        {
            if (exponent > BigInteger.Zero)
                return BigDecimal.Zero;
            throw new NCalcEvaluationException("Zero cannot be raised to a negative power exactly.");
        }

        BigInteger mantissa = baseValue.Mantissa;
        int scale = baseValue.Exponent;
        BigInteger n = BigInteger.Abs(exponent);

        // 1 and -1 can be raised to any power without computing anything
        if (scale == 0 && BigInteger.Abs(mantissa).IsOne)
            return (mantissa.Sign < 0 && !n.IsEven) ? new BigDecimal(BigInteger.MinusOne, 0) : BigDecimal.One;

        double digits = PowDigits(mantissa, scale, n);
        double resultScale = (double)n * scale;
        if (digits > MaxPowResultDigits)
            throw new NCalcEvaluationException($"The result of the power operation would have about {digits:0} digits, which exceeds the limit of {MaxPowResultDigits} digits (MathHelper.MaxPowResultDigits).");
        if (Math.Abs(resultScale) > int.MaxValue)
            throw new NCalcEvaluationException("The result of the power operation is too large or too small to be represented exactly.");

        var positive = new BigDecimal(BigInteger.Pow(mantissa, (int)n), (int)resultScale);
        return exponent.Sign < 0 ? BigDecimal.One / positive : positive;
    }

    // The number of digits of mantissa * 10^scale raised to the power n: the power of the mantissa has about n * log10|mantissa|
    // significant digits, and the scale of the result is n * scale. A positive scale adds trailing zeros that are stored in the
    // exponent but become digits when the result is converted to an integer (10 is stored as 1E1, and 10 ** 200000 would become
    // a 200,000-digit BigInteger). A negative scale makes n * |scale| of the digits decimal places; they are not additional digits
    // (1.5 ** n has about n * log10(15) digits, n of them decimals), but they are digits when the mantissa is shorter than the
    // scale (0.1 ** 3000000 has 3,000,000 decimal places).
    private static double PowDigits(BigInteger mantissa, int scale, BigInteger n)
    {
        double mantissaDigits = BigInteger.Log10(BigInteger.Abs(mantissa));
        return (double)n * (scale >= 0 ? mantissaDigits + scale : Math.Max(mantissaDigits, -(double)scale));
    }

    public static object Factorial(object a, object b, MathHelperOptions options)
    {
        if (a is ComplexNumber || b is ComplexNumber)
            throw new NCalcEvaluationException("The factorial is not defined for complex numbers");

        // A fraction would be truncated by the conversion below (3.5! would give 6), so only whole numbers are accepted
        if (IsBoxedFloatingNumberInteger(a) == false || IsBoxedFloatingNumberInteger(b) == false)
            throw new NCalcEvaluationException("The factorial is defined only for whole numbers");

        // The operands are whole numbers of any size (a BigInteger, decimal or double beyond the range of long is not an
        // OverflowException; NCALC-323), checked before the product is computed
        BigInteger bigValue = GetFactorialOperand(a, options);
        BigInteger bigStep = GetFactorialOperand(b, options);
        CheckFactorialArguments(bigValue, bigStep);

        if (!options.UseBigNumbers)
        {
            if (bigValue <= long.MaxValue)
            {
                try
                {
                    // A step beyond the range of long leaves only the first factor, as long.MaxValue does
                    return Factorial((long)bigValue, bigStep > long.MaxValue ? long.MaxValue : (long)bigStep, options);
                }
                catch (OverflowException)
                {
                    // The result becomes a double below
                }
            }

            // Like other integer results beyond the range of long without UseBigNumbers, the result becomes a double.
            // Every factor is at least 2, so the product reaches infinity after at most about a thousand steps.
            double result = (double)bigValue;
            for (BigInteger value = bigValue - bigStep; value > 1 && !double.IsInfinity(result); value -= bigStep)
                result *= (double)value;
            return result;
        }

        if (bigValue <= 1)
            return 1;

        // log10(n!) by Stirling's formula; a multifactorial with step k has about 1/k of these digits.
        // A number beyond the range of double gives an infinite (or NaN) estimate, which is beyond the limit as well.
        double n2 = (double)bigValue;
        double digits = (n2 * Math.Log(n2) - n2 + 0.5 * Math.Log(2 * Math.PI * n2)) / Math.Log(10) / (double)bigStep;
        if (double.IsNaN(digits) || digits > MaxPowResultDigits)
            throw new NCalcEvaluationException($"The result of the factorial would have about {digits:0} digits, which exceeds the limit of {MaxPowResultDigits} digits (MathHelper.MaxPowResultDigits).");

        var product = bigValue;
        for (var value = bigValue - bigStep; value > 1; value -= bigStep)
            product *= value;

        if (product <= int.MaxValue)
            return (int)product;
        if (product <= long.MaxValue)
            return (long)product;
        return product;
    }

    // A whole number (checked by the caller) as a BigInteger; another value (a string) is converted to long, as before
    private static BigInteger GetFactorialOperand(object value, MathHelperOptions options) => value switch
    {
        BigInteger bigValue => bigValue,
        BigDecimal or double or float or decimal => GetWholeValue(ConvertToBigDecimal(value)),
        _ when IsBoxedIntegerNumber(value) => ConvertToBigInteger(value),
        _ => ConvertToLong(value, "Factorial", options.CultureInfo, null)
    };

    private static void CheckFactorialArguments(BigInteger value, BigInteger step)
    {
        if (value.Sign < 0)
            throw new NCalcEvaluationException("The factorial is not defined for negative numbers");
        if (step <= 0)
            throw new NCalcEvaluationException("The step of a factorial must be positive");
    }

    /// <summary>
    /// Computes the factorial of <paramref name="a"/>, or the multifactorial with the step <paramref name="b"/> (a * (a - b) * (a - 2b) * ...).
    /// </summary>
    /// <exception cref="NCalcEvaluationException"><paramref name="a"/> is negative or <paramref name="b"/> is not positive.</exception>
    /// <exception cref="OverflowException">The result does not fit in a <see cref="long"/>.</exception>
    public static long Factorial(long a, long b, MathHelperOptions options)
    {
        CheckFactorialArguments(a, b);

        long result = Math.Max(a, 1);
        for (long value = a - b; value > 1; value -= b)
            result = checked(result * value);

        return result;
    }

    public static BigDecimal Hypot(BigDecimal x, BigDecimal y, MathHelperOptions options)
    {
        BigDecimal absX = ConvertToBigDecimal(MathHelper.Abs(x, options));
        BigDecimal absY = ConvertToBigDecimal(MathHelper.Abs(y, options));

        if (absX > absY)
        {
            BigDecimal ratio = absY / absX;
            BigDecimal result = absX * ConvertToBigDecimal(Sqrt(new BigDecimal(1.0) + ratio * ratio, options)!);
            return result;
        }

        if (absY > 0.0)
        {
            BigDecimal ratio = absX / absY;
            BigDecimal result = absY * ConvertToBigDecimal(Sqrt(new BigDecimal(1.0) + ratio * ratio, options)!);
            return result;
        }

        return 0.0;
    }

    /// <summary>
    /// Rounds a value to <paramref name="b"/> decimal places. A negative number of places rounds to tens, hundreds and so on
    /// (Round(1234, -2) is 1200) for all types, and more places than a type can hold leave the value unchanged.
    /// </summary>
    public static object Round(object? a, object? b, MidpointRounding rounding, MathHelperOptions options)
    {
        if (!IsDefinedMidpointMode(rounding))
            throw new NCalcEvaluationException($"The value '{rounding}' is not a defined MidpointRounding value");

        int precision = GetRoundingDigits(b, options);

        if (a is Percent percent && percent.Value is not null)
            return new Percent(Round(percent.Value, b, rounding, options));

        if (a is ComplexNumber cnA)
            return ComplexNumber.Round(cnA, precision, rounding);

        if (a is NCalcVector vA)
            return NCalcVector.Round(vA, precision, rounding);

        if (a is BigDecimal bdA)
            return RoundBigDecimal(bdA, precision, rounding);

        if (IsBoxedIntegerNumberOrBigNumber(a))
        {
            if (precision >= 0)
                return a!;

            // -int.MinValue overflows; rounding to 10^2147483648 gives zero as rounding to 10^2147483647 does
            var rounded = RoundToPowerOfTen(ConvertToBigInteger(a!), precision == int.MinValue ? int.MaxValue : -precision, rounding);
            // The result keeps the type of the argument when it fits
            return NarrowToTypeOf(a!, rounded);
        }

        // A decimal argument is rounded as a decimal, so that it does not lose digits through double
        if (UseDecimalArithmetic(a, options))
        {
            decimal decimal_a = ConvertToDecimal(a, "Round", options.CultureInfo, null);
            if (precision < 0)
            {
                var roundedDecimal = RoundBigDecimal(new BigDecimal(decimal_a), precision, rounding);
                return roundedDecimal >= (BigDecimal)decimal.MinValue && roundedDecimal <= (BigDecimal)decimal.MaxValue
                    ? BigDecimalToDecimal(roundedDecimal)
                    : BigDecimalToDouble(roundedDecimal);
            }
            if (precision > 28)
                return decimal_a;
            // RoundToPrecision applies MidpointRounding only to exact midpoints; the legacy System.Math.Round
            // behavior (which applies it to every value) remains available via the UseSystemMathRound option.
            return options.UseSystemMathRound
                ? Math.Round(decimal_a, precision, rounding)
                : RoundToPrecision(decimal_a, precision, rounding);
        }

        double double_a = ConvertToDouble(a, "Round", options.CultureInfo, null);
        if (precision < 0)
        {
            if (double.IsNaN(double_a) || double.IsInfinity(double_a))
                return double_a;
            // The shortest text of the double is rounded exactly, so that e.g. 1250 is a midpoint
            return BigDecimalToDouble(RoundBigDecimal(ToShortestBigDecimal(double_a), precision, rounding));
        }
        // Math.Round accepts at most 15 places. Otherwise the shortest text of the double is rounded exactly, for any number of
        // places: a place beyond its last digit leaves it unchanged, and more places can still cut digits of a small double
        // (Round(1.23E-17, 16) is 0). (RoundToPrecision reads only 15 significant digits, so Round(123456.78901234567, 11)
        // was 123456.789012346 although the value has 11 places; NCALC-331.)
        if (options.UseSystemMathRound && precision <= 15)
            return Math.Round(double_a, precision, rounding);
        if (double.IsNaN(double_a) || double.IsInfinity(double_a))
            return double_a;
        return BigDecimalToDouble(RoundBigDecimal(ToShortestBigDecimal(double_a), precision, rounding));
    }

    /// <summary>
    /// The number of decimal places of Round. A count beyond the range of int is clamped, the same on every target framework: more
    /// places than any type holds leave the value unchanged, and fewer than int.MinValue round it to zero, as the nearest counts
    /// do (NCALC-323, NCALC-338). NaN is not a number of places.
    /// </summary>
    private static int GetRoundingDigits(object? b, MathHelperOptions options)
    {
        BigInteger digits;
        switch (b)
        {
            case double.NaN or float.NaN:
                throw new NCalcEvaluationException("The number of decimal places of Round() cannot be NaN");
            case double d:
                return d >= int.MaxValue ? int.MaxValue : d <= int.MinValue ? int.MinValue : (int)Math.Truncate(d);
            case float f:
                return f >= int.MaxValue ? int.MaxValue : f <= int.MinValue ? int.MinValue : (int)Math.Truncate(f);
            case decimal m:
                digits = new BigInteger(Math.Truncate(m));
                break;
            case BigDecimal bd:
                digits = GetWholeValue(bd);
                break;
            case BigInteger or long or ulong or uint:
                digits = ConvertToBigInteger(b);
                break;
            default:
                return ConvertToInt(b, "Round", options.CultureInfo, null);
        }

        return digits > int.MaxValue ? int.MaxValue : digits < int.MinValue ? int.MinValue : (int)digits;
    }

    /// <summary>
    /// Rounds a BigDecimal to <paramref name="digits"/> decimal places (a negative number rounds to tens, hundreds and so on),
    /// choosing the nearest value and consulting <paramref name="mode"/> only at an exact midpoint.
    /// </summary>
    public static BigDecimal RoundBigDecimal(BigDecimal value, int digits, MidpointRounding mode)
    {
        // Computed in long, so that extreme digit counts (int.MinValue) do not overflow
        long targetExponent = -(long)digits;
        if (value.Exponent >= targetExponent)
            return value;

        long shift = targetExponent - value.Exponent;
        // A multiple that is larger than twice the value rounds it to zero (as in RoundToPowerOfTen), and the power is not computed
        if (value.Mantissa.IsZero || shift > (long)Math.Ceiling(BigInteger.Log10(BigInteger.Abs(value.Mantissa))) + 1)
            return BigDecimal.Zero;

        var rounded = RoundToPowerOfTen(value.Mantissa, (int)shift, mode);
        return new BigDecimal(rounded / BigInteger.Pow(10, (int)shift), (int)targetExponent);
    }

    private static object NarrowToTypeOf(object original, BigInteger value)
    {
        return original switch
        {
            byte when value >= byte.MinValue && value <= byte.MaxValue => (object)(byte)value,
            sbyte when value >= sbyte.MinValue && value <= sbyte.MaxValue => (object)(sbyte)value,
            short when value >= short.MinValue && value <= short.MaxValue => (object)(short)value,
            ushort when value >= ushort.MinValue && value <= ushort.MaxValue => (object)(ushort)value,
            int when value >= int.MinValue && value <= int.MaxValue => (object)(int)value,
            uint when value >= uint.MinValue && value <= uint.MaxValue => (object)(uint)value,
            long when value >= long.MinValue && value <= long.MaxValue => (object)(long)value,
            ulong when value >= ulong.MinValue && value <= ulong.MaxValue => (object)(ulong)value,
            // A result that does not fit the type of the argument (Round(2147483647, -1)) gets the next integer type it fits in
            BigInteger => (object)value,
            _ when value >= int.MinValue && value <= int.MaxValue => (object)(int)value,
            _ when value >= long.MinValue && value <= long.MaxValue => (object)(long)value,
            _ when value >= ulong.MinValue && value <= ulong.MaxValue => (object)(ulong)value,
            _ => (object)value
        };
    }

    /// <summary>
    /// Converts a finite double to the BigDecimal of its shortest round-trip text, so that e.g. 0.1 is 0.1 and not the binary value.
    /// </summary>
    private static BigDecimal ToShortestBigDecimal(double value)
    {
        string text = value.ToString("R", CultureInfo.InvariantCulture);
        int exponent = 0;
        int exponentIndex = text.IndexOfAny(['E', 'e']);
        if (exponentIndex >= 0)
        {
            exponent = int.Parse(text.Substring(exponentIndex + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            text = text.Substring(0, exponentIndex);
        }
        int pointIndex = text.IndexOf('.');
        if (pointIndex >= 0)
        {
            exponent -= text.Length - pointIndex - 1;
            text = text.Remove(pointIndex, 1);
        }
        return new BigDecimal(BigInteger.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture), exponent);
    }

    /// <summary>
    /// Rounds an integer to a multiple of 10 to the power of <paramref name="exponent"/>, choosing the nearest multiple and
    /// consulting <paramref name="mode"/> only at an exact midpoint.
    /// </summary>
    private static BigInteger RoundToPowerOfTen(BigInteger value, int exponent, MidpointRounding mode)
    {
        if (exponent <= 0 || value.IsZero)
            return value;

        // A multiple that is larger than twice the value rounds it to zero, and the power is not computed
        if (exponent > (int)Math.Ceiling(BigInteger.Log10(BigInteger.Abs(value))) + 1)
            return BigInteger.Zero;

        var factor = BigInteger.Pow(10, exponent);
        var lower = BigInteger.Divide(value, factor);
        var remainder = value - lower * factor;
        if (remainder.Sign < 0)
        {
            // Division truncates toward zero; the lower candidate is the floor
            lower -= 1;
            remainder += factor;
        }

        var twice = remainder * 2;
        BigInteger chosen;
        if (twice < factor)
            chosen = lower;
        else
        if (twice > factor)
            chosen = lower + 1;
        else
        {
            var higher = lower + 1;
            chosen = mode switch
            {
                MidpointRounding.ToEven => lower.IsEven ? lower : higher,
                MidpointRounding.AwayFromZero => value.Sign >= 0 ? higher : lower,
#if NET
                MidpointRounding.ToZero => value.Sign >= 0 ? lower : higher,
                MidpointRounding.ToNegativeInfinity => lower,
                MidpointRounding.ToPositiveInfinity => higher,
#endif
                _ => throw new ArgumentException($"The value '{mode}' is not a defined MidpointRounding value.", nameof(mode)),
            };
        }

        return chosen * factor;
    }

    // Doubles at or above this magnitude have no fractional precision left (and may exceed the decimal range),
    // so rounding them to any number of decimal digits is a no-op.
    private const double DecimalRoundLimit = 1e28;

    /// <summary>
    /// Rounds <paramref name="value"/> to <paramref name="digits"/> fractional decimal digits, always choosing the
    /// nearest representable value. <paramref name="mode"/> is consulted <b>only</b> when <paramref name="value"/>
    /// lies exactly halfway between the two candidates; for every other value the nearest candidate is chosen
    /// regardless of <paramref name="mode"/>.
    /// </summary>
    /// <remarks>
    /// The midpoint test is performed in exact <see cref="decimal"/> arithmetic. A binary <see cref="double"/> such
    /// as <c>1.55</c> cannot be stored exactly, so the value is first reinterpreted as its nearest decimal using the
    /// ~15 significant digits a <see cref="double"/> actually carries, and the decimal rounding is then applied to
    /// that. This is the cleanest practical way to honor decimal midpoints (e.g. <c>1.55</c> rounds to <c>1.5</c> or
    /// <c>1.6</c> depending on the mode) for doubles. The unavoidable limitation is that a <see cref="double"/>
    /// requiring more than ~15 significant digits is rounded according to that reinterpretation rather than its full
    /// binary value. Non-finite values, and magnitudes too large to carry any fractional part, are returned unchanged,
    /// exactly as <see cref="Math.Round(double, int, MidpointRounding)"/> would.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="digits"/> is less than 0 or greater than 15.</exception>
    /// <exception cref="ArgumentException"><paramref name="mode"/> is not a defined <see cref="MidpointRounding"/> value.</exception>
    public static double RoundToPrecision(double value, int digits, MidpointRounding mode)
    {
        if (digits < 0 || digits > 15)
            throw new ArgumentOutOfRangeException(nameof(digits), "Rounding digits must be between 0 and 15, inclusive.");

        if (!IsDefinedMidpointMode(mode))
            throw new ArgumentException($"The value '{mode}' is not a defined MidpointRounding value.", nameof(mode));

        // Non-finite values and magnitudes that cannot be represented as a decimal have no fractional part to round
        // at this precision; return them unchanged.
        if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) >= DecimalRoundLimit)
            return value;

        return (double)RoundToPrecision((decimal)value, digits, mode);
    }

    /// <summary>
    /// Rounds <paramref name="value"/> to <paramref name="digits"/> fractional decimal digits, always choosing the
    /// nearest representable value. <paramref name="mode"/> is consulted <b>only</b> when <paramref name="value"/>
    /// lies exactly halfway between the two candidates; for every other value the nearest candidate is chosen
    /// regardless of <paramref name="mode"/>.
    /// </summary>
    /// <remarks>
    /// This is a deliberate, self-contained reimplementation that does not delegate to
    /// <see cref="Math.Round(decimal, int, MidpointRounding)"/> or <see cref="decimal.Round(decimal, int, MidpointRounding)"/>.
    /// All arithmetic is performed in exact <see cref="decimal"/> precision so that the midpoint detection is exact.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="digits"/> is less than 0 or greater than 28.</exception>
    /// <exception cref="ArgumentException"><paramref name="mode"/> is not a defined <see cref="MidpointRounding"/> value.</exception>
    public static decimal RoundToPrecision(decimal value, int digits, MidpointRounding mode)
    {
        if (digits < 0 || digits > 28)
            throw new ArgumentOutOfRangeException(nameof(digits), "Rounding digits must be between 0 and 28, inclusive.");

        if (!IsDefinedMidpointMode(mode))
            throw new ArgumentException($"The value '{mode}' is not a defined MidpointRounding value.", nameof(mode));

        // If the value already carries no more fractional digits than requested there is nothing to round.
        // Short-circuiting here also keeps the subsequent scaling well within the decimal range.
        int scaleOfValue = (decimal.GetBits(value)[3] >> 16) & 0xFF;
        if (scaleOfValue <= digits)
            return value;

        decimal factor = 1m;
        for (int i = 0; i < digits; i++)
            factor *= 10m;

        decimal scaled = value * factor;        // moves the rounding position onto the integer boundary
        decimal lower = Math.Floor(scaled);     // largest integer <= scaled (toward negative infinity)
        decimal fraction = scaled - lower;      // always in the range [0, 1)

        decimal chosen;
        if (fraction < 0.5m)
        {
            chosen = lower;                     // nearest is the lower candidate
        }
        else
        if (fraction > 0.5m)
        {
            chosen = lower + 1m;                // nearest is the higher candidate
        }
        else
        {
            // Exact midpoint: the only case in which the rounding mode is consulted.
            decimal higher = lower + 1m;
            chosen = mode switch
            {
                MidpointRounding.ToEven => (lower % 2m == 0m) ? lower : higher,
                MidpointRounding.AwayFromZero => (scaled >= 0m) ? higher : lower,
#if NET
                MidpointRounding.ToZero => (scaled >= 0m) ? lower : higher,
                MidpointRounding.ToNegativeInfinity => lower,
                MidpointRounding.ToPositiveInfinity => higher,
#endif
                _ => throw new ArgumentException($"The value '{mode}' is not a defined MidpointRounding value.", nameof(mode)),
            };
        }

        return chosen / factor;
    }

    /// <summary>
    /// Determines whether <paramref name="mode"/> is a <see cref="MidpointRounding"/> value defined by the target
    /// framework. Explicit cases (rather than reflection) keep this AOT friendly and ensure unknown values are
    /// rejected instead of silently ignored.
    /// </summary>
    private static bool IsDefinedMidpointMode(MidpointRounding mode)
    {
        switch (mode)
        {
            case MidpointRounding.ToEven:
            case MidpointRounding.AwayFromZero:
#if NET
            case MidpointRounding.ToZero:
            case MidpointRounding.ToNegativeInfinity:
            case MidpointRounding.ToPositiveInfinity:
#endif
                return true;
            default:
                return false;
        }
    }

    public static object Sign(object? a, MathHelperOptions options)
    {
        // The sign of a percent is the sign of its value
        if (a is Percent percent && percent.Value is not null)
            return Sign(percent.Value, options);

        // NaN has no sign: it gives NaN, as the other functions do, instead of the ArithmeticException of Math.Sign
        if (a is double.NaN)
            return double.NaN;
        if (a is float.NaN)
            return float.NaN;

        if (a is BigInteger biA)
        {
            return biA.Sign;
        }
        else
        if (a is BigDecimal bdA)
        {
            return bdA.Sign;
        }

        if (a is ComplexNumber)
            throw new NCalcEvaluationException("The Sign function is not defined for complex numbers");

        if (a is NCalcVector vA)
            return NCalcVector.Sign(vA);

        if (UseDecimalArithmetic(a, options))
            return Math.Sign(ConvertToDecimal(a, "Sign", options.CultureInfo, null));

        double d = ConvertToDouble(a, "Sign", options.CultureInfo, null);
        // A string such as "NaN" converts to NaN
        if (double.IsNaN(d))
            return double.NaN;
        return Math.Sign(d);
    }

    public static object Sec(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigTrigonometric(ToBigDecimalArgument(a), BigTrigFunction.Sec);
        }
        if (a is ComplexNumber cnA)
            return ComplexNumber.Sec(cnA, options);

        return Sec(ConvertToDouble(a, "Sec", options.CultureInfo, null));
    }

    public static double Sec(double x)
    {
        double cos = Math.Cos(x);

        if (cos == 0.0)
            throw new ArgumentOutOfRangeException(nameof(x), "sec is undefined when cos(x) = 0.");

        return 1.0 / cos;
    }
    public static object Sech(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return Truncated(() => BigDecimal.Sech(new BigDecimal(biA)));
            }
            if (a is BigDecimal bdA)
            {
                return Truncated(() => BigDecimal.Sech(bdA));
            }
        }
        if (a is ComplexNumber cnA)
            return ComplexNumber.Sech(cnA, options);

        return Sech(ConvertToDouble(a, "Sech", options.CultureInfo, null));
    }

    public static double Sech(double x)
    {
        double cosh = Math.Cosh(x);

        return 1.0 / cosh;
    }

    public static object Sin(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigTrigonometric(ToBigDecimalArgument(a), BigTrigFunction.Sin);
        }
        if (a is ComplexNumber cnA)
            return ComplexNumber.Sin(cnA, options);

        return Math.Sin(ConvertToDouble(a, "Sin", options.CultureInfo, null));
    }

    public static object Sinh(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return Truncated(() => BigDecimal.Sinh(new BigDecimal(biA)));
            }
            if (a is BigDecimal bdA)
            {
                return Truncated(() => BigDecimal.Sinh(bdA));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Sinh(cnA, options);

        return Math.Sinh(ConvertToDouble(a, "Sinh", options.CultureInfo, null));
    }

    public static object? Sqrt(object? a, MathHelperOptions options)
    {
        if (a is null)
            return null;

        // The square root of a percent is the square root of its fraction: Sqrt(25%) is 0.5
        if (a is Percent percent && percent.Value is not null)
            return Sqrt(Divide(percent.Value, 100m, true, options), options);

        if (a is NCalcVector vA)
            return NCalcVector.Sqrt(vA);

        if (options.UseBigNumbers)
        {
            // The square root of a negative number is NaN, as with Math.Sqrt
            if (a is BigInteger or BigDecimal)
            {
                BigDecimal bdA = ToBigDecimalArgument(a);
                if (bdA.Sign < 0)
                    return double.NaN;
                return Truncated(() => BigDecimal.SquareRoot(bdA, BigDecimal.Precision));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Sqrt(cnA, options);

        var d = ConvertToDouble(a, "Sqrt", options.CultureInfo, null);

        return Math.Sqrt(d);
    }

    public static object? Fthrt(object? a, MathHelperOptions options)
    {
        if (a is null)
            return null;

        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
            {
                BigDecimal bdA = ToBigDecimalArgument(a);
                if (bdA.Sign < 0)
                    return double.NaN;
                return Truncated(() => BigDecimal.SquareRoot(BigDecimal.SquareRoot(bdA, BigDecimal.Precision), BigDecimal.Precision));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Sqrt(ComplexNumber.Sqrt(cnA, options), options);

        var d = ConvertToDouble(a, "Fthrt", options.CultureInfo, null);

        return Math.Sqrt(Math.Sqrt(d));
    }

#if NET8_0_OR_GREATER
    public static object? Cbrt(object? a, MathHelperOptions options)
    {
        if (a is null)
            return null;

        if (options.UseBigNumbers)
        {
            // The cube root of a negative number is negative, as with Math.Cbrt (ExtendedNumerics accepts only positive numbers)
            if (a is BigInteger or BigDecimal)
            {
                BigDecimal bdA = ToBigDecimalArgument(a);
                if (bdA.IsZero())
                    return BigDecimal.Zero;
                return Truncated(() => bdA.Sign < 0 ? -BigDecimal.NthRoot(-bdA, 3, BigDecimal.Precision) : BigDecimal.NthRoot(bdA, 3, BigDecimal.Precision));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Pow(cnA, (BigDecimal) 1.0 / 3.0, options);

        var d = ConvertToDouble(a, "Cbrt", options.CultureInfo, null);

        return Math.Cbrt(d);
    }
#endif

    public static object Tan(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger or BigDecimal)
                return BigTrigonometric(ToBigDecimalArgument(a), BigTrigFunction.Tan);
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Tan(cnA, options);

        return Math.Tan(ConvertToDouble(a, "Tan", options.CultureInfo, null));
    }

    public static object Tanh(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return Truncated(() => BigDecimal.Tanh(new BigDecimal(biA)));
            }
            if (a is BigDecimal bdA)
            {
                return Truncated(() => BigDecimal.Tanh(bdA));
            }
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Tanh(cnA, options);

        return Math.Tanh(ConvertToDouble(a, "Tanh", options.CultureInfo, null));
    }

    public static object Truncate(object? a, MathHelperOptions options)
    {
        if (a is Percent percent && percent.Value is not null)
            return new Percent(Truncate(percent.Value, options));

        if (options.SupportTimeOperations)
        {
            if (a is TimeSpan ts)
                return new TimeSpan((int) ts.TotalDays, 0, 0, 0);
            else
            if (a is DateTime dt)
                return dt.Date;
        }
        if (a is not null && (IsBoxedIntegerNumber(a) || a is BigInteger))
        {
            return a;
        }
        else
        if (a is BigDecimal bdA)
        {
            // A BigDecimal stays a BigDecimal, as with Floor and Ceiling
            return new BigDecimal(GetWholeValue(bdA));
        }

        if (a is ComplexNumber cnA)
            return ComplexNumber.Truncate(cnA);

        if (a is NCalcVector vA)
            return NCalcVector.Truncate(vA);

        if (UseDecimalArithmetic(a, options))
            return Math.Truncate(ConvertToDecimal(a, "Truncate", options.CultureInfo, null));

        return Math.Truncate(ConvertToDouble(a, "Truncate", options.CultureInfo, null));
    }

    private static object ConvertIfNeeded(object value, string operationName, MathHelperOptions options)
    {
        try
        {
            return value switch
            {
                char ch when options is { DecimalAsDefault: true, AllowCharValues: false } => decimal.Parse(ch.ToString(), options.CultureInfo),
                string s when options is { DecimalAsDefault: true } => decimal.Parse(s, options.CultureInfo),
                char ch when options is { AllowCharValues: false } => double.Parse(ch.ToString(), options.CultureInfo),
                string s => double.Parse(s, options.CultureInfo),
                bool boolean when options.AllowBooleanCalculation => boolean ? 1 : 0,
                _ => value
            };
        }
        catch (FormatException)
        {
            if (value is string s)
            {
                string valueStr = s;
                Type targetType = options.DecimalAsDefault ? typeof(decimal) : typeof(double);
                throw new NCalcConversionException($"Conversion of a string value '{valueStr}' to type '{targetType.Name}' in the '{operationName}' operation failed as a string could not be parsed.", valueStr, typeof(string), targetType);
            }

            if (value is char c)
            {
                string valueStr = c.ToString();
                Type targetType = options.DecimalAsDefault ? typeof(decimal) : typeof(double);
                throw new NCalcConversionException($"Conversion of a string value '{valueStr}' to type '{targetType.Name}' in the '{operationName}' operation failed as a string could not be parsed.", valueStr, typeof(char), targetType);
            }

            throw;
        }
    }

    public static double ConvertToDouble(object? value, MathHelperOptions options)
    {
        return value switch
        {
            ComplexNumber cn => cn.IsReal ? BigDecimalToDouble(cn.Real) : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to double", cn.ToString(), typeof(ComplexNumber), typeof(double)),
            BigInteger bigI => ((double)bigI),
            BigDecimal bigD => BigDecimalToDouble(bigD),
            double @double => @double,
            char ch => Convert.ToDouble(ch.ToString(), options.CultureInfo),
            _ => Convert.ToDouble(value, options.CultureInfo)
        };
    }

    public static double ConvertToDouble(object? value, string operation, CultureInfo cultureInfo, ExpressionLocation? location)
    {
        try
        {
            return value switch
            {
                ComplexNumber cn => cn.IsReal ? BigDecimalToDouble(cn.Real) : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to double", cn.ToString(), typeof(ComplexNumber), typeof(double)),
                BigInteger bigI => ((double)bigI),
                BigDecimal bigD => BigDecimalToDouble(bigD),
                double @double => @double,
                char ch => Convert.ToDouble(ch.ToString(), cultureInfo),
                _ => Convert.ToDouble(value, cultureInfo)
            };
        }
        catch (Exception icex) when (icex is InvalidCastException or FormatException)
        {
            if (value is not null)
                throw new NCalcConversionException($"Conversion of a value '{value}' of type {value.GetType().Name} to type '{typeof(double).Name}' in the '{operation}' operation failed.", value.ToString() ?? "null", value.GetType(), typeof(double), location ?? ExpressionLocation.Empty, icex);
            else
                throw new NCalcConversionException($"Conversion of a null value to type '{typeof(double).Name}' in the '{operation}' operation failed.", typeof(double), location ?? ExpressionLocation.Empty, icex);
        }
    }

    public static decimal ConvertToDecimal(object? value, MathHelperOptions options)
    {
        return value switch
        {
            ComplexNumber cn => cn.IsReal ? BigDecimalToDecimal(cn.Real) : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to decimal", cn.ToString(), typeof(ComplexNumber), typeof(decimal)),
            BigInteger bigI => ((decimal)bigI),
            BigDecimal bigD => BigDecimalToDecimal(bigD),
            decimal @decimal => @decimal,
            char ch => Convert.ToDecimal(ch.ToString(), options.CultureInfo),
            _ => Convert.ToDecimal(value, options.CultureInfo)
        };
    }

    public static decimal ConvertToDecimal(object? value, string operation, CultureInfo cultureInfo, ExpressionLocation? location)
    {
        try
        {
            return value switch
            {
                ComplexNumber cn => cn.IsReal ? BigDecimalToDecimal(cn.Real) : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to decimal", cn.ToString(), typeof(ComplexNumber), typeof(decimal)),
                BigInteger bigI => ((decimal)bigI),
                BigDecimal bigD => BigDecimalToDecimal(bigD),
                decimal @decimal => @decimal,
                char ch => Convert.ToDecimal(ch.ToString(), cultureInfo),
                _ => Convert.ToDecimal(value, cultureInfo)
            };
        }
        catch (Exception icex) when (icex is InvalidCastException or FormatException)
        {
            if (value is not null)
                throw new NCalcConversionException($"Conversion of a value '{value}' of type {value.GetType().Name} to type '{typeof(decimal).Name}' in the '{operation}' operation failed.", value.ToString() ?? "null", value.GetType(), typeof(decimal), location ?? ExpressionLocation.Empty, icex);
            else
                throw new NCalcConversionException($"Conversion of a null value to type '{typeof(decimal).Name}' in the '{operation}' operation failed.", typeof(decimal), location ?? ExpressionLocation.Empty, icex);
        }
    }

    // A cast of NaN or of a double outside the range of the integer type is unspecified in C#: .NET 9 and later saturate, earlier
    // targets give int.MinValue / long.MinValue. Such a value throws OverflowException on every target, as BigInteger and decimal
    // do (NCALC-338).
    private static int TruncateToInt(double value)
    {
        double truncated = Math.Truncate(value);
        if (double.IsNaN(truncated) || truncated < int.MinValue || truncated > int.MaxValue)
            throw new OverflowException($"The value {value.ToString(CultureInfo.InvariantCulture)} is outside the range of Int32.");
        return (int)truncated;
    }

    private static long TruncateToLong(double value)
    {
        double truncated = Math.Truncate(value);
        // (double)long.MaxValue is 2^63, which is already outside the range of long
        if (double.IsNaN(truncated) || truncated < long.MinValue || truncated >= 9223372036854775808.0)
            throw new OverflowException($"The value {value.ToString(CultureInfo.InvariantCulture)} is outside the range of Int64.");
        return (long)truncated;
    }

    public static int ConvertToInt(object? value, MathHelperOptions options)
    {
        return value switch
        {
            ComplexNumber cn => cn.IsReal ? (int)GetWholeValue(cn.Real) : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to an integer", cn.ToString(), typeof(ComplexNumber), typeof(int)),
            BigInteger bigI => ((int)bigI),
            BigDecimal bigD => (int)GetWholeValue(bigD),

            int i => i,
            double dd => TruncateToInt(dd),
            decimal dc => (int)Math.Truncate(dc),
            float df => TruncateToInt(df),
            char ch => Convert.ToInt32(ch.ToString(), options.CultureInfo),
            _ => Convert.ToInt32(value, options.CultureInfo)
        };
    }

    public static int ConvertToInt(object? value, string operation, CultureInfo cultureInfo, ExpressionLocation? location)
    {
        try
        {
            return value switch
            {
                ComplexNumber cn => cn.IsReal ? (int)GetWholeValue(cn.Real) : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to an integer", cn.ToString(), typeof(ComplexNumber), typeof(int)),
                BigInteger bigI => ((int)bigI),
                BigDecimal bigD => (int)GetWholeValue(bigD),

                int i => i,
                double dd => TruncateToInt(dd),
                decimal dc => (int)Math.Truncate(dc),
                float df => TruncateToInt(df),
                char ch => Convert.ToInt32(ch.ToString(), cultureInfo),
                _ => Convert.ToInt32(value, cultureInfo)
            };
        }
        catch (Exception icex) when (icex is InvalidCastException or FormatException or OverflowException)
        {
            if (value is not null)
                throw new NCalcConversionException($"Conversion of a value '{value}' of type {value.GetType().Name} to type '{typeof(int).Name}' in the '{operation}' operation failed.", value.ToString() ?? "null", value.GetType(), typeof(int), location ?? ExpressionLocation.Empty, icex);
            else
                throw new NCalcConversionException($"Conversion of a null value to type '{typeof(int).Name}' in the '{operation}' operation failed.", typeof(int), location ?? ExpressionLocation.Empty, icex);
        }
    }

    public static long ConvertToLong(object? value, MathHelperOptions options)
    {
        return value switch
        {
            ComplexNumber cn => cn.IsReal
                ? (IsWholeNumber(cn.Real)
                    ? (long)GetWholeValue(cn.Real) : throw new NCalcConversionException("The real part of the complex number contains a fractional part, so this complex number cannot be converted to long", cn.ToString(), typeof(ComplexNumber), typeof(long)))
                : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to long", cn.ToString(), typeof(ComplexNumber), typeof(long)),
            BigInteger bigI => ((long)bigI),
            BigDecimal bigD => ((long)GetWholeValue(bigD)),

            long l => l,
            int i => i,
            short s => s,
            sbyte sb => sb,
            double dd => TruncateToLong(dd),
            decimal dc => (long)Math.Truncate(dc),
            float df => TruncateToLong(df),
            char ch => Convert.ToInt64(ch.ToString(), options.CultureInfo),
            _ => Convert.ToInt64(value, options.CultureInfo)
        };
    }

    public static long ConvertToLong(object? value, string operation, CultureInfo cultureInfo, ExpressionLocation? location)
    {
        try
        {
            return value switch
            {
                ComplexNumber cn => cn.IsReal
                    ? (IsWholeNumber(cn.Real)
                        ? (long)GetWholeValue(cn.Real) : throw new NCalcConversionException("The real part of the complex number contains a fractional part, so this complex number cannot be converted to long", cn.ToString(), typeof(ComplexNumber), typeof(long)))
                    : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to long", cn.ToString(), typeof(ComplexNumber), typeof(long)),
                BigInteger bigI => ((long)bigI),
                BigDecimal bigD => ((long)GetWholeValue(bigD)),

                long l => l,
                int i => i,
                short s => s,
                sbyte sb => sb,
                char ch => Convert.ToInt64(ch.ToString(), cultureInfo),
                double dd => TruncateToLong(dd),
                decimal dc => (long) Math.Truncate(dc),
                float df => TruncateToLong(df),
                _ => Convert.ToInt64(value, cultureInfo)
            };
        }
        catch (Exception icex) when (icex is InvalidCastException or FormatException or OverflowException)
        {
            if (value is not null)
                throw new NCalcConversionException($"Conversion of a value '{value}' of type {value.GetType().Name} to type '{typeof(long).Name}' in the '{operation}' operation failed.", value.ToString() ?? "null", value.GetType(), typeof(long), location ?? ExpressionLocation.Empty, icex);
            else
                throw new NCalcConversionException($"Conversion of a null value to type '{typeof(long).Name}' in the '{operation}' operation failed.", typeof(long), location ?? ExpressionLocation.Empty, icex);
        }
    }

    public static ulong ConvertToULong(object? value, MathHelperOptions options)
    {
        return value switch
        {
            ComplexNumber cn => cn.IsReal
                ? (!IsWholeNumber(cn.Real)
                    ? throw new NCalcConversionException("The real part of the complex number contains a fractional part, so this complex number cannot be converted to ulong", cn.ToString(), typeof(ComplexNumber), typeof(ulong))
                    : (cn.Real.Sign >= 0
                        ? (ulong)GetWholeValue(cn.Real)
                        : throw new NCalcConversionException("A negative number cannot be converted to ulong", value.ToString() ?? string.Empty, typeof(BigInteger), typeof(ulong))))
                : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to ulong", cn.ToString(), typeof(ComplexNumber), typeof(ulong)),
            BigInteger bigI => bigI.Sign >= 0 ? ((ulong)bigI) : throw new NCalcConversionException("A negative number cannot be converted to ulong", value.ToString() ?? string.Empty, typeof(BigInteger), typeof(ulong)),
            BigDecimal bigD => bigD.Sign >= 0 ? ((ulong)GetWholeValue(bigD)) : throw new NCalcConversionException("A negative number cannot be converted to ulong", value.ToString() ?? string.Empty, typeof(BigDecimal), typeof(ulong)),

            ulong ul => ul,
            uint ui => ui,
            ushort us => us,
            byte b => b,
            double dd => (ulong)Math.Truncate(dd),
            decimal dc => (ulong)Math.Truncate(dc),
            float df => (ulong)Math.Truncate(df),
            char ch => Convert.ToUInt64(ch.ToString(), options.CultureInfo),
            _ => Convert.ToUInt64(value, options.CultureInfo)
        };
    }

    public static ulong ConvertToULong(object? value, string operation, CultureInfo cultureInfo, ExpressionLocation? location)
    {
        try
        {
            return value switch
            {
                ComplexNumber cn => cn.IsReal
                    ? (!IsWholeNumber(cn.Real)
                        ? throw new NCalcConversionException("The real part of the complex number contains a fractional part, so this complex number cannot be converted to ulong", cn.ToString(), typeof(ComplexNumber), typeof(ulong))
                        : (cn.Real.Sign >= 0
                            ? (ulong)GetWholeValue(cn.Real)
                            : throw new NCalcConversionException("A negative number cannot be converted to ulong", value.ToString() ?? string.Empty, typeof(BigInteger), typeof(ulong))))
                    : throw new NCalcConversionException("A complex number with a non-zero imaginary part cannot be converted to ulong", cn.ToString(), typeof(ComplexNumber), typeof(ulong)),
                BigInteger bigI => bigI.Sign >= 0 ? ((ulong)bigI) : throw new NCalcConversionException("A negative number cannot be converted to ulong", value.ToString() ?? string.Empty, typeof(BigInteger), typeof(ulong)),
                BigDecimal bigD => bigD.Sign >= 0 ? ((ulong)GetWholeValue(bigD)) : throw new NCalcConversionException("A negative number cannot be converted to ulong", value.ToString() ?? string.Empty, typeof(BigDecimal), typeof(ulong)),

                ulong ul => ul,
                uint ui => ui,
                ushort us => us,
                byte b => b,
                double dd => (ulong)Math.Truncate(dd),
                decimal dc => (ulong)Math.Truncate(dc),
                float df => (ulong)Math.Truncate(df),
                char ch => Convert.ToUInt64(ch.ToString(), cultureInfo),
                _ => Convert.ToUInt64(value, cultureInfo)
            };
        }
        catch (Exception icex) when (icex is InvalidCastException or FormatException)
        {
            if (value is not null)
                throw new NCalcConversionException($"Conversion of a value '{value}' of type {value.GetType().Name} to type '{typeof(ulong).Name}' in the '{operation}' operation failed.", value.ToString() ?? "null", value.GetType(), typeof(ulong), location ?? ExpressionLocation.Empty, icex);
            else
                throw new NCalcConversionException($"Conversion of a null value to type '{typeof(ulong).Name}' in the '{operation}' operation failed.", typeof(ulong), location ?? ExpressionLocation.Empty, icex);
        }
    }

    public static bool ConvertToBoolean(object? value, string operation, CultureInfo cultureInfo, ExpressionLocation? location)
    {
        try
        {
            // Big numbers are true when they are not zero, as the other numbers are (Convert.ToBoolean does not know them; NCALC-331)
            return value switch
            {
                bool b => b,
                BigInteger bigInteger => !bigInteger.IsZero,
                BigDecimal bigDecimal => !bigDecimal.IsZero(),
                _ => Convert.ToBoolean(value, cultureInfo)
            };
        }
        catch (Exception icex) when (icex is InvalidCastException or FormatException)
        {
            if (value is not null)
                throw new NCalcConversionException($"Conversion of a value '{value}' of type {value.GetType().Name} to type '{typeof(bool).Name}' in the '{operation}' operation failed.", value.ToString() ?? "null", value.GetType(), typeof(bool), location ?? ExpressionLocation.Empty, icex);
            else
                throw new NCalcConversionException($"Conversion of a null value to type '{typeof(bool).Name}' in the '{operation}' operation failed.", typeof(bool), location ?? ExpressionLocation.Empty, icex);
        }
    }

    private static object ExecuteOperation(object a, object b, char operatorName, ArithmeticOperation operation, MathHelperOptions options, TypeCode typeCode = TypeCode.Empty)
    {
        object origA = a, origB = b;
        if (typeCode == TypeCode.Empty)
            typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        /*
        We permit math operations on any parameters and let the runtime blow up on unsupported types
        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException($"Operator '{operatorName}' is not implemented for operands of types {origA.GetType()} and {origB.GetType()}");

        if (!IsBoxedNumber(a) && !(typeCode is TypeCode.Char))
        {
            throw new InvalidOperationException($"Operator '{operatorName}' is not implemented for operands of type '{typeCode.ToString()}'");
        }
        else
        */
        try
        {
            object? result = null;
            switch (operation)
            {
                case ArithmeticOperation.Add:
                    result = AddFunc(a, b, options);
                    break;
                case ArithmeticOperation.Subtract:
                    result = SubtractFunc(a, b, options);
                    break;
                case ArithmeticOperation.Multiply:
                    result = MultiplyFunc(a, b, options);
                    break;
                case ArithmeticOperation.Divide:
                    result = DivideFunc(a, b, options);
                    break;
                case ArithmeticOperation.Modulo:
                    result = ModuloFunc(a, b, options);
                    break;
                case ArithmeticOperation.AddPercent:
                case ArithmeticOperation.SubtractPercent:
                case ArithmeticOperation.MultiplyPercent:
                case ArithmeticOperation.DividePercent:
                    result = PercentFunc(a, b, operation, options);
                    break;
            }
            if (result is null)
                throw new InvalidOperationException($"Operation '{operatorName}' is not implemented for operands of type '{typeCode.ToString()}'");

            return result;
        }
        catch(Exception ex) when (ex is not OverflowException and not DivideByZeroException && !(ex is NCalcException && operation is ArithmeticOperation.AddPercent or ArithmeticOperation.SubtractPercent or ArithmeticOperation.MultiplyPercent or ArithmeticOperation.DividePercent))
        {
            throw new InvalidOperationException($"Operator '{operatorName}' is not implemented for operands of types {origA.GetType()} and {origB.GetType()}", ex);
        }
    }

    private static void CheckOverflow(dynamic value)
    {
        switch (value)
        {
            case double doubleVal when double.IsInfinity(doubleVal):
            case float floatValue when float.IsInfinity(floatValue):
                throw new OverflowException("Arithmetic operation resulted in an overflow");
        }
    }

    public static BigInteger Multiply(BigInteger a, object b)
    {
        switch (b)
        {
            case byte value: return a * value;
            case sbyte value: return a * value;
            case short value: return a * value;
            case ushort value: return a * value;
            case int value: return a * value;
            case uint value: return a * value;
            case long value: return a * value;
            case ulong value: return a * value;
            case BigInteger value: return a * value;
            default:
                throw new ArgumentException("When multiplying BigInteger, the second parameter must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", nameof(b));
        }
    }

    public static BigDecimal Multiply(BigDecimal a, object b)
    {
        switch (b)
        {
            case byte value: return a * value;
            case sbyte value: return a * value;
            case short value: return a * value;
            case ushort value: return a * value;
            case int value: return a * value;
            case uint value: return a * value;
            case long value: return a * value;
            case ulong value: return a * value;
            case float value: return a * value;
            case double value: return a * value;
            case decimal value: return a * value;
            case BigInteger value: return a * new BigDecimal(value);
            case BigDecimal value: return a * value;
            default:
                throw new ArgumentException("When multiplying BigInteger, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", nameof(b));
        }
    }

    public static BigDecimal Multiply(BigInteger a, BigDecimal b)
    {
        return new BigDecimal(a) * b;
    }

    public static BigInteger Add(BigInteger a, object b)
    {
        switch (b)
        {
            case byte value: return a + value;
            case sbyte value: return a + value;
            case short value: return a + value;
            case ushort value: return a + value;
            case int value: return a + value;
            case uint value: return a + value;
            case long value: return a + value;
            case ulong value: return a + value;
            case BigInteger value: return a + value;
            default:
                throw new ArgumentException("When adding to a BigInteger, the second parameter must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", nameof(b));
        }
    }

    public static BigDecimal Add(BigInteger a, BigDecimal b)
    {
        return new BigDecimal(a) + b;
    }

    public static BigDecimal Add(BigDecimal a, object b)
    {
        switch (b)
        {
            case byte value: return a + value;
            case sbyte value: return a + value;
            case short value: return a + value;
            case ushort value: return a + value;
            case int value: return a + value;
            case uint value: return a + value;
            case long value: return a + value;
            case ulong value: return a + value;
            case float value: return a + value;
            case double value: return a + value;
            case decimal value: return a + value;
            case BigInteger value: return a + new BigDecimal(value);
            case BigDecimal value: return a + value;
            default:
                throw new ArgumentException("When adding to a BigDecimal, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", nameof(b));
        }
    }

    public static BigInteger Subtract(BigInteger a, object b)
    {
        switch (b)
        {
            case byte value: return a - value;
            case sbyte value: return a - value;
            case short value: return a - value;
            case ushort value: return a - value;
            case int value: return a - value;
            case uint value: return a - value;
            case long value: return a - value;
            case ulong value: return a - value;
            case BigInteger value: return a - value;
            default:
                throw new ArgumentException("When subtracting from a BigInteger, the second parameter must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", nameof(b));
        }
    }

    public static BigDecimal Subtract(BigDecimal a, object b)
    {
        switch (b)
        {
            case byte value: return a - value;
            case sbyte value: return a - value;
            case short value: return a - value;
            case ushort value: return a - value;
            case int value: return a - value;
            case uint value: return a - value;
            case long value: return a - value;
            case ulong value: return a - value;
            case float value: return a - value;
            case double value: return a - value;
            case decimal value: return a - value;
            case BigInteger value: return a - new BigDecimal(value);
            case BigDecimal value: return a - value;
            default:
                throw new ArgumentException("When subtracting from a BigDecimal, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", nameof(b));
        }
    }

    /// <summary>
    /// Returns the remainder of a truncated division; the result has the sign of the dividend, as with the % operator of .NET.
    /// </summary>
    public static BigDecimal Remainder(BigDecimal a, BigDecimal b)
    {
        if (b.IsZero())
            throw new DivideByZeroException();

        // A dividend smaller than the divisor is the remainder itself (the truncated quotient is 0)
        if (a.IsZero() || HasSmallerMagnitude(a, b, "remainder"))
            return a;

        // The remainder is computed exactly on the mantissas aligned to a common exponent; BigDecimal.Divide rounds the quotient
        // to BigDecimal.Precision digits, so a - b * Truncate(a / b) is wrong for a quotient with more digits (10^206 % 7)
        int exponent = AlignExponents(a, b, out BigInteger mantissaA, out BigInteger mantissaB);
        return new BigDecimal(BigInteger.Remainder(mantissaA, mantissaB), exponent);
    }

    /// <summary>
    /// Returns the largest integer that is not greater than a / b, computed exactly.
    /// </summary>
    internal static BigInteger FloorDivide(BigDecimal a, BigDecimal b)
    {
        if (b.IsZero())
            throw new DivideByZeroException();

        if (a.IsZero())
            return BigInteger.Zero;
        if (HasSmallerMagnitude(a, b, "integer division"))
            return a.Sign == b.Sign ? BigInteger.Zero : BigInteger.MinusOne;

        AlignExponents(a, b, out BigInteger mantissaA, out BigInteger mantissaB);
        BigInteger quotient = BigInteger.DivRem(mantissaA, mantissaB, out BigInteger remainder);
        if (!remainder.IsZero && (mantissaA.Sign < 0) != (mantissaB.Sign < 0))
            quotient -= BigInteger.One;
        return quotient;
    }

    // The position of the most significant digit of a BigDecimal: |value| is in [10^(m-1), 10^m) for the returned m (give or take one,
    // as the digit count is computed with a logarithm)
    internal static double DecimalMagnitude(BigDecimal value)
        => value.Exponent + Math.Floor(BigInteger.Log10(BigInteger.Abs(value.Mantissa))) + 1;

    /// <summary>
    /// Compares two BigDecimals; values of different signs or of clearly different magnitudes are ordered without aligning their
    /// exponents, which would take seconds for 1E+1000000 and 1.
    /// </summary>
    internal static int CompareBigDecimals(BigDecimal a, BigDecimal b)
    {
        int signA = a.Mantissa.Sign, signB = b.Mantissa.Sign;
        if (signA != signB)
            return signA.CompareTo(signB);
        if (signA == 0)
            return 0;
        double magnitudeA = DecimalMagnitude(a), magnitudeB = DecimalMagnitude(b);
        if (Math.Abs(magnitudeA - magnitudeB) > 1)
            return signA * magnitudeA.CompareTo(magnitudeB);
        return a.CompareTo(b);
    }

    // Tells whether |a| < |b| for certain, from the magnitudes of the values. Throws if the integer quotient of a and b would have more
    // than MaxPowResultDigits digits, which would take a long time to compute.
    private static bool HasSmallerMagnitude(BigDecimal a, BigDecimal b, string operation)
    {
        double difference = DecimalMagnitude(a) - DecimalMagnitude(b);
        if (difference > MaxPowResultDigits)
            throw new NCalcEvaluationException($"The quotient of the {operation} would have about {difference:0} digits, which exceeds the limit of {MaxPowResultDigits} digits (MathHelper.MaxPowResultDigits).");
        return difference < -1;
    }

    // Writes a and b as mantissaA * 10^exponent and mantissaB * 10^exponent, exactly, and returns the common exponent
    private static int AlignExponents(BigDecimal a, BigDecimal b, out BigInteger mantissaA, out BigInteger mantissaB)
    {
        int exponent = Math.Min(a.Exponent, b.Exponent);
        mantissaA = a.Mantissa * BigInteger.Pow(10, a.Exponent - exponent);
        mantissaB = b.Mantissa * BigInteger.Pow(10, b.Exponent - exponent);
        return exponent;
    }

    public static BigInteger IntegerDivide(BigInteger a, object b)
    {
        switch (b)
        {
            case byte value: return a / value;
            case sbyte value: return a / value;
            case short value: return a / value;
            case ushort value: return a / value;
            case int value: return a / value;
            case uint value: return a / value;
            case long value: return a / value;
            case ulong value: return a / value;
            case BigInteger value: return a / value;
            default:
                throw new ArgumentException("When dividing a BigInteger, the second parameter must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", nameof(b));
        }
    }

    public static BigInteger IntegerDivide(BigDecimal a, object b)
    {
        BigDecimal result;
        switch (b)
        {
            case byte value: result = a / value; break;
            case sbyte value: result = a / value; break;
            case short value: result = a / value; break;
            case ushort value: result = a / value; break;
            case int value: result = a / value; break;
            case uint value: result = a / value; break;
            case long value: result = a / value; break;
            case ulong value: result = a / value; break;
            case BigInteger value: result = a / new BigDecimal(value); break;
            case BigDecimal value: result = a / value; break;
            default:
                throw new ArgumentException("When dividing a BigInteger, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger, BigDecimal).", nameof(b));
        }
        return GetWholeValue(result);
    }

    public static BigDecimal Divide(BigDecimal a, object b)
    {
        switch (b)
        {
            case byte value: return a / value;
            case sbyte value: return a / value;
            case short value: return a / value;
            case ushort value: return a / value;
            case int value: return a / value;
            case uint value: return a / value;
            case long value: return a / value;
            case ulong value: return a / value;
            case float value: return a / value;
            case double value: return a / value;
            case decimal value: return a / value;
            case BigInteger value: return a / new BigDecimal(value);
            case BigDecimal value: return a / value;
            default:
                throw new ArgumentException("When dividing a BigDecimal, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", nameof(b));
        }
    }

    public static BigInteger Subtract(object a, BigInteger b)
    {
        var value = ConvertToBigInteger(a);
        return value - b;
    }

    public static BigDecimal Subtract(object a, BigDecimal b)
    {
        var value = ConvertToBigDecimal(a);
        return value - b;
    }

    public static BigInteger Divide(object a, BigInteger b)
    {
        var value = ConvertToBigInteger(a);
        return value / b;
    }

    public static BigInteger IntegerDivide(object a, BigInteger b)
    {
        var value = ConvertToBigInteger(a);
        return value / b;
    }

    public static BigInteger IntegerDivide(object a, BigDecimal b)
    {
        var value = ConvertToBigDecimal(a);
        return GetWholeValue(value / b);
    }

    public static BigDecimal Divide(object a, BigDecimal b)
    {
        var value = ConvertToBigDecimal(a);
        return value / b;
    }

    /// <summary>
    /// Returns the larger (or the smaller) of two values. Numbers are compared exactly and the result has the type of the
    /// arithmetic on the two numbers (Max(3, 2.5) is a double whichever is larger); NaN wins, as with Math.Max and Math.Min.
    /// A string that is a number literal takes part as a number; a number cannot be compared with a value of another kind.
    /// Values of other kinds (strings, dates) are compared as with the comparison operators. A null argument gives null.
    /// Values that cannot be ordered (complex numbers with an imaginary part, vectors, incomparable types) throw <see cref="NCalcEvaluationException"/>.
    /// </summary>
    private static object? MaxOrMin(object? a, object? b, bool max, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        string function = max ? "Max" : "Min";
        a = ConvertNumericString(a, options);
        b = ConvertNumericString(b, options);

        // Percentages are ordered by their values, and the chosen percent is returned
        if (a is Percent percentA && b is Percent percentB && percentA.Value is not null && percentB.Value is not null)
        {
            if (percentA.Value is double.NaN or float.NaN)
                return percentA;
            if (percentB.Value is double.NaN or float.NaN)
                return percentB;
            int percentComparison = Compare(percentA.Value, percentB.Value, options, options);
            return (max ? percentComparison < 0 : percentComparison > 0) ? percentB : percentA;
        }

        // A complex number with no imaginary part is a real number, as with the comparison operators
        if (a is ComplexNumber realA && realA.IsReal)
            a = ReduceComplexToReal(realA, options);
        if (b is ComplexNumber realB && realB.IsReal)
            b = ReduceComplexToReal(realB, options);
        // Two vectors give the element-wise maximum or minimum
        if (a is NCalcVector vectorA && b is NCalcVector vectorB)
            return max ? NCalcVector.Max(vectorA, vectorB) : NCalcVector.Min(vectorA, vectorB);
        if (a is ComplexNumber or NCalcVector or Percent || b is ComplexNumber or NCalcVector or Percent)
            throw new NCalcEvaluationException($"{function}() cannot order values of types {a.GetType().Name} and {b.GetType().Name}");

        // A boolean takes part as 1 or 0 when boolean calculation is allowed, as in true + 5
        if (options.AllowBooleanCalculation)
        {
            if (a is bool boolA && IsBoxedNumberOrBigNumber(b))
                a = boolA ? 1 : 0;
            if (b is bool boolB && IsBoxedNumberOrBigNumber(a))
                b = boolB ? 1 : 0;
        }

        bool aIsNumber = IsBoxedNumberOrBigNumber(a);
        bool bIsNumber = IsBoxedNumberOrBigNumber(b);
        if (aIsNumber != bIsNumber)
            throw new NCalcEvaluationException($"{function}() cannot compare a number with a value of type {(aIsNumber ? b : a).GetType().Name}");

        if (!aIsNumber)
        {
            // The same ordering as the comparison operators; values that they cannot order cannot be compared here either
            if (!TypeHelper.CompareUsingMostPreciseType(a, b, options, options, out int result))
                throw new NCalcEvaluationException($"{function}() cannot compare values of types {a.GetType().Name} and {b.GetType().Name}");
            return (max ? result < 0 : result > 0) ? b : a;
        }

        // NaN wins, as with Math.Max and Math.Min; it is checked before the conversion, which cannot convert NaN to a big number
        bool nanA = a is double.NaN or float.NaN;
        bool nanB = b is double.NaN or float.NaN;
        if (nanA || nanB)
            return a is float && b is float ? (object)float.NaN : (object)double.NaN;

        // An infinity cannot be converted to a big number either; it is larger (or smaller) than any big number
        if (IsBigNumber(a) || IsBigNumber(b))
        {
            object? infinite = IsInfinity(a) ? a : IsInfinity(b) ? b : null;
            if (infinite is not null)
            {
                bool positive = infinite is double.PositiveInfinity or float.PositiveInfinity;
                object other = ReferenceEquals(infinite, a) ? b : a;
                return positive == max ? infinite : other;
            }
        }

        object widenedA = a;
        object widenedB = b;
        ConvertToHighestPrecision(ref widenedA, ref widenedB, false, options);

        // The original values are compared, so that the conversion does not make different values equal
        int comparison = Compare(a, b, options, options);
        bool pickB = max ? comparison < 0 : comparison > 0;

        // A ulong above long.MaxValue and a signed integer are widened to BigInteger only to be compared; the chosen value keeps its type
        if (widenedA is BigInteger && !IsBigNumber(a) && !IsBigNumber(b) && !options.UseBigNumbers)
            return pickB ? b : a;
        return pickB ? widenedB : widenedA;
    }

    private static bool IsInfinity(object value)
        => value is double.PositiveInfinity or double.NegativeInfinity or float.PositiveInfinity or float.NegativeInfinity;

    private static object ConvertNumericString(object value, MathHelperOptions options)
    {
        if (value is string text && !options.AllOptions.HasFlag(ExpressionOptions.NoStringTypeCoercion)
            && TypeHelper.TryConvertLiteral(text, options, out var literal) && IsBoxedNumberOrBigNumber(literal))
            return literal!;
        return value;
    }

    public static object Max(object a, BigInteger b)
    {
        BigInteger aValue = ConvertToBigInteger(a);
        return aValue > b ? a : b;
    }

    public static object Min(object a, BigInteger b)
    {
        BigInteger aValue = ConvertToBigInteger(a);
        return aValue < b ? a: b;
    }

    public static object Max(object a, BigDecimal b)
    {
        BigDecimal aValue = ConvertToBigDecimal(a);
        return aValue > b ? a : b;
    }

    public static object Min(object a, BigDecimal b)
    {
        BigDecimal aValue = ConvertToBigDecimal(a);
        return aValue < b ? a: b;
    }

    // Helper to convert boxed integer types to BigInteger
    public static BigInteger ConvertToBigInteger(object a)
    {
        switch (a)
        {
            case byte value: return value;
            case sbyte value: return value;
            case short value: return value;
            case ushort value: return value;
            case int value: return value;
            case uint value: return value;
            case long value: return value;
            case ulong value: return value;
            case BigInteger value: return value;
            case ComplexNumber cn:
                return cn.IsReal && IsWholeNumber(cn.Real)
                    ? GetWholeValue(cn.Real)
                    : throw new NCalcConversionException("A complex number cannot be converted to BigInteger", cn.ToString() ?? string.Empty, typeof(ComplexNumber), typeof(BigInteger));
            default:
                throw new NCalcConversionException("The source of the conversion to a BigInteger must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", a.ToString() ?? string.Empty, a.GetType(), typeof(BigInteger));
        }
    }

    public static BigDecimal ConvertToBigDecimal(object a)
    {
        switch (a)
        {
            case byte value: return new BigDecimal(value);
            case sbyte value: return new BigDecimal(value);
            case short value: return new BigDecimal(value);
            case ushort value: return new BigDecimal(value);
            case int value: return new BigDecimal(value);
            case uint value: return new BigDecimal(new BigInteger(value));
            case long value: return new BigDecimal(new BigInteger(value));
            case ulong value: return new BigDecimal(new BigInteger(value));
            case float value: return new BigDecimal(value);
            case double value: return new BigDecimal(value);
            case decimal value: return new BigDecimal(value);
            case BigInteger value: return new BigDecimal(value);
            case BigDecimal value: return value;
            default:
                throw new NCalcConversionException("The source of the conversion to a BigDecimal must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", a.ToString() ?? string.Empty, a.GetType(), typeof(BigDecimal));
        }
    }

    // The operands follow the rules of the bitwise operators: a whole floating-point value is accepted (BigInteger 12 & 4.0 is 4),
    // a fraction, NaN or an infinity throws NCalcEvaluationException
    public static BigInteger? BitwiseAnd(object? a, object? b)
    {
        if (a is null || b is null)
            return null;
        return ToBigBitwiseOperand(a, "Bitwise And") & ToBigBitwiseOperand(b, "Bitwise And");
    }

    public static BigInteger? BitwiseOr(object? a, object? b)
    {
        if (a is null || b is null)
            return null;
        return ToBigBitwiseOperand(a, "Bitwise Or") | ToBigBitwiseOperand(b, "Bitwise Or");
    }

    public static BigInteger? BitwiseXOr(object? a, object? b)
    {
        if (a is null || b is null)
            return null;
        return ToBigBitwiseOperand(a, "Bitwise XOr") ^ ToBigBitwiseOperand(b, "Bitwise XOr");
    }

    private static BigInteger ToBigBitwiseOperand(object value, string operation, CultureInfo? cultureInfo = null, ExpressionLocation? location = null)
    {
        object operand = ToBitwiseOperand(value, operation, cultureInfo ?? CultureInfo.InvariantCulture, location);
        return operand is ulong unsignedValue ? new BigInteger(unsignedValue) : (BigInteger)operand;
    }

    public static BigInteger? BitwiseNot(object? a)
    {
        if (a is null)
            return null;
        BigInteger valueA = ConvertToBigInteger(a);
        return ~valueA;
    }

    /// <summary>
    /// Computes a &amp; b as the &amp; operator does. Non-negative integers are combined as 64-bit unsigned integers; a negative integer
    /// is used in two's complement, as by the shift operators and for BigInteger operands (-1 &amp; 3 is 3, -8 | 1 is -7).
    /// A floating-point operand must be a whole number. A BigInteger operand gives a BigInteger result.
    /// </summary>
    public static object? BitwiseAnd(object? a, object? b, string operation, CultureInfo cultureInfo, ExpressionLocation? locationA, ExpressionLocation? locationB)
        => BitwiseOperation(a, b, '&', operation, cultureInfo, locationA, locationB);

    /// <summary>
    /// Computes a | b as the | operator does; see <see cref="BitwiseAnd(object?, object?, string, CultureInfo, ExpressionLocation?, ExpressionLocation?)"/>.
    /// </summary>
    public static object? BitwiseOr(object? a, object? b, string operation, CultureInfo cultureInfo, ExpressionLocation? locationA, ExpressionLocation? locationB)
        => BitwiseOperation(a, b, '|', operation, cultureInfo, locationA, locationB);

    /// <summary>
    /// Computes a ^ b as the ^ operator does; see <see cref="BitwiseAnd(object?, object?, string, CultureInfo, ExpressionLocation?, ExpressionLocation?)"/>.
    /// </summary>
    public static object? BitwiseXOr(object? a, object? b, string operation, CultureInfo cultureInfo, ExpressionLocation? locationA, ExpressionLocation? locationB)
        => BitwiseOperation(a, b, '^', operation, cultureInfo, locationA, locationB);

    /// <summary>
    /// Computes ~a as the ~ operator does: a non-negative integer is complemented as a 64-bit unsigned integer (~5 is 18446744073709551610),
    /// a negative integer in two's complement (~-1 is 0, ~-6 is 5), and a BigInteger as a BigInteger reduced to ulong when it fits.
    /// </summary>
    public static object? BitwiseNot(object? a, string operation, CultureInfo cultureInfo, ExpressionLocation? location)
    {
        if (a is BigInteger bigA)
            return TryReduceToUInt64(~bigA);

        object operand = ToBitwiseOperand(a, operation, cultureInfo, location);
        return operand is ulong unsignedOperand ? ~unsignedOperand : ReduceBitwiseResult(~(BigInteger)operand);
    }

    private static object? BitwiseOperation(object? a, object? b, char operatorChar, string operation, CultureInfo cultureInfo, ExpressionLocation? locationA, ExpressionLocation? locationB)
    {
        // The other operand of a BigInteger follows the same rules as with other integers: a whole floating-point value is accepted,
        // a fraction, NaN or an infinity is rejected; the result is a BigInteger
        if (a is BigInteger || b is BigInteger)
        {
            if (a is null || b is null)
                return null;
            BigInteger bigValueA = ToBigBitwiseOperand(a, operation, cultureInfo, locationA);
            BigInteger bigValueB = ToBigBitwiseOperand(b, operation, cultureInfo, locationB);
            return operatorChar switch
            {
                '&' => bigValueA & bigValueB,
                '|' => bigValueA | bigValueB,
                _ => bigValueA ^ bigValueB
            };
        }

        object operandA = ToBitwiseOperand(a, operation, cultureInfo, locationA);
        object operandB = ToBitwiseOperand(b, operation, cultureInfo, locationB);
        if (operandA is ulong unsignedA && operandB is ulong unsignedB)
        {
            return operatorChar switch
            {
                '&' => unsignedA & unsignedB,
                '|' => unsignedA | unsignedB,
                _ => unsignedA ^ unsignedB
            };
        }

        BigInteger bigA = operandA is ulong ulA ? new BigInteger(ulA) : (BigInteger)operandA;
        BigInteger bigB = operandB is ulong ulB ? new BigInteger(ulB) : (BigInteger)operandB;
        return ReduceBitwiseResult(operatorChar switch
        {
            '&' => bigA & bigB,
            '|' => bigA | bigB,
            _ => bigA ^ bigB
        });
    }

    // A non-negative result is a ulong, as for non-negative operands; a negative result is a long (a BigInteger beyond the range of long)
    private static object ReduceBitwiseResult(BigInteger value)
    {
        if (value.Sign >= 0)
            return value <= ulong.MaxValue ? (object)(ulong)value : value;
        return value >= long.MinValue ? (object)(long)value : value;
    }

    // Converts an operand of a bitwise operator to a ulong (a non-negative integer) or a BigInteger (a negative integer, or a whole
    // number beyond the range of ulong). A fraction, NaN or an infinity has no bits and is rejected.
    private static object ToBitwiseOperand(object? value, string operation, CultureInfo cultureInfo, ExpressionLocation? location)
    {
        BigInteger whole;
        switch (value)
        {
            case BigInteger bigIntegerValue:
                whole = bigIntegerValue;
                break;
            case sbyte or short or int or long:
                long signedValue = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return signedValue >= 0 ? (object)(ulong)signedValue : new BigInteger(signedValue);
            case double or float:
                double doubleValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue) || Math.Floor(doubleValue) != doubleValue)
                    throw NonIntegralBitwiseOperand(value, operation, location);
                whole = new BigInteger(doubleValue);
                break;
            case decimal decimalValue:
                if (decimal.Truncate(decimalValue) != decimalValue)
                    throw NonIntegralBitwiseOperand(value, operation, location);
                whole = new BigInteger(decimalValue);
                break;
            case BigDecimal bigDecimalValue:
                if (!IsWholeNumber(bigDecimalValue))
                    throw NonIntegralBitwiseOperand(value, operation, location);
                whole = GetWholeValue(bigDecimalValue);
                break;
            default:
                try
                {
                    return ConvertToULong(value, operation, cultureInfo, location);
                }
                catch (OverflowException ex)
                {
                    throw new NCalcConversionException($"Conversion of a value '{value}' of type {value?.GetType().Name} to type 'UInt64' in the '{operation}' operation failed.", value?.ToString() ?? "null", value?.GetType() ?? typeof(object), typeof(ulong), location ?? ExpressionLocation.Empty, ex);
                }
        }
        return whole.Sign >= 0 && whole <= ulong.MaxValue ? (object)(ulong)whole : whole;
    }

    private static NCalcEvaluationException NonIntegralBitwiseOperand(object value, string operation, ExpressionLocation? location)
        => new($"The '{operation}' operation requires integer operands, but the value '{Convert.ToString(value, CultureInfo.InvariantCulture)}' of type {value.GetType().Name} is not a whole number.", location ?? ExpressionLocation.Empty);

    public static BigInteger? LeftShift(BigInteger a, object? b, MathHelperOptions options)
    {
        if (b is null)
            return null;
        int intB = GetBigShiftCount(a, b, "Left Shift", options);
        CheckBigShift(a, intB);
        return a << intB;
    }

    /// <summary>
    /// Rejects a left shift of a big integer whose result would exceed the size limit of big results
    /// (<see cref="MaxPowResultDigits"/> decimal digits), so that e.g. 1 &lt;&lt; 2147483647 does not allocate hundreds of megabytes.
    /// </summary>
    private static void CheckBigShift(BigInteger a, long step)
    {
        if (a.IsZero || step <= 0)
            return;
        double digits = BigInteger.Log10(BigInteger.Abs(a)) + step * 0.30102999566398120;
        if (digits > MaxPowResultDigits)
            throw new NCalcEvaluationException($"The result of the shift would have about {digits:0} digits, which exceeds the limit of {MaxPowResultDigits} digits (MathHelper.MaxPowResultDigits).");
    }

    // The number of bits to shift: an integer of any type, including BigInteger, from 0 to int.MaxValue.
    // A negative count is rejected for every type of the left operand (it is not turned into a shift in the other direction).
    private static int GetShiftCount(object a, object? b)
    {
        if (b is not BigInteger && !IsBoxedIntegerNumber(b))
            throw new NCalcEvaluationException($"The right operand {b} does not define the number of bits to shift {a}");

        BigInteger count = ConvertToBigInteger(b!);
        if (count.Sign < 0 || count > int.MaxValue)
            throw new NCalcEvaluationException($"The value {count} cannot be used as a number of bits to shift {a}");

        return (int)count;
    }

    // A big integer can also be shifted by a whole floating-point count, as before; a negative count is rejected as for other types
    private static int GetBigShiftCount(BigInteger a, object b, string operation, MathHelperOptions options)
    {
        if (b is BigInteger || IsBoxedIntegerNumber(b))
            return GetShiftCount(a, b);

        // A fraction, NaN or an infinity does not define a number of bits; it is not truncated
        if (IsBoxedFloatingNumberInteger(b) != true)
            throw new NCalcEvaluationException($"The right operand {b} does not define the number of bits to shift {a}");

        int count = ConvertToInt(b, operation, options.CultureInfo, null);
        if (count < 0)
            throw new NCalcEvaluationException($"The value {count} cannot be used as a number of bits to shift {a}");
        return count;
    }

    public static object? LeftShift(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        // Vector dispatch — element-wise left shift; b must be an integer shift amount
        if (a is NCalcVector va)
        {
            int shift = ConvertToInt(b, "Left Shift", options.CultureInfo, null);
            return va << shift;
        }
        if (b is NCalcVector)
            throw new NCalcEvaluationException("The right operand of a left-shift cannot be a vector.");

        if (a is BigInteger ba)
        {
            return LeftShift(ba, b, options);
        }

        if (!MathHelper.IsBoxedIntegerNumber(a))
            throw new NCalcEvaluationException($"The left operand {a} cannot be bit-shifted");

        int stepInt = GetShiftCount(a!, b);

        // A negative value is shifted arithmetically, as in C# (-1 << 1 is -2), and its result is a long
        BigInteger value = ConvertToBigInteger(a!);
        bool negative = value.Sign < 0;

        // The shift is exact: the count is not masked to the width of the type (1 << 64 is not 1). Bits shifted beyond 64 make
        // a big integer with UseBigNumbers, an overflow with OverflowProtection, and are dropped otherwise (1 << 64 is 0)
        if (options.UseBigNumbers)
            CheckBigShift(value, stepInt);
        BigInteger shifted;
        if (stepInt >= 128 && !options.UseBigNumbers)
        {
            // Only the lowest 64 bits are kept, and they are all zero; the stand-in value just has to be out of range
            shifted = value.IsZero ? BigInteger.Zero : (negative ? BigInteger.MinusOne : BigInteger.One) << 128;
        }
        else
            shifted = value << stepInt;

        if (shifted > ulong.MaxValue || shifted < long.MinValue)
        {
            if (options.UseBigNumbers)
                return reduceTypes ? ReduceNumericType(shifted) : shifted;
            if (options.OverflowProtection)
                throw new OverflowException($"The result of shifting {a} left by {stepInt} bits does not fit in 64 bits");
            shifted &= ulong.MaxValue;
        }

        // A negative result whose high bits were dropped is in the 64-bit pattern of its two's complement and is read back as a long
        object result;
        if (!negative)
            result = (ulong)shifted;
        else
            result = shifted.Sign < 0 ? (long)shifted : unchecked((long)(ulong)shifted);
        if (reduceTypes)
            return ReduceNumericType(result);

        return result;
    }

    public static BigInteger? RightShift(BigInteger a, object? b, MathHelperOptions options)
    {
        if (b is null)
            return null;
        int intB = GetBigShiftCount(a, b, "Right Shift", options);
        return a >> intB;
    }

    public static object? RightShift(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        // Vector dispatch — element-wise right shift; b must be an integer shift amount
        if (a is NCalcVector va)
        {
            int shift = ConvertToInt(b, "Right Shift", options.CultureInfo, null);
            return va >> shift;
        }
        if (b is NCalcVector)
            throw new NCalcEvaluationException("The right operand of a right-shift cannot be a vector.");

        if (a is BigInteger ba)
        {
            return RightShift(ba, b, options);
        }

        if (!MathHelper.IsBoxedIntegerNumber(a))
            throw new NCalcEvaluationException($"The left operand {a} cannot be bit-shifted");

        int step = GetShiftCount(a!, b);

        // The count is not masked to the width of the type: shifting by 64 bits or more leaves nothing (or -1 for a negative value,
        // which is shifted arithmetically as in C#: -8 >> 1 is -4)
        BigInteger value = ConvertToBigInteger(a!);
        object result = value.Sign < 0 ? (long)(value >> step) : (object)(ulong)(value >> step);
        if (reduceTypes)
            return ReduceNumericType(result);

        return result;
    }

    public static int Compare(object? a, object? b, ComparisonOptions comparisonOptions, MathHelperOptions mathHelperOptions)
    {
        int result;

        // Percentages are ordered by their values; vectors have no order
        if (a is Percent percentA && b is Percent percentB)
            return Compare(percentA.Value, percentB.Value, comparisonOptions, mathHelperOptions);
        if (a is NCalcVector || b is NCalcVector)
            throw new NCalcEvaluationException("Vectors can only be compared for equality");

        if (a is ComplexNumber || b is ComplexNumber)
        {
            if (a is ComplexNumber cnA)
            {
                if (!cnA.IsReal)
                    throw new InvalidOperationException($"The comparison operation is not defined for complex numbers");
                a = cnA.Real;
                if (!mathHelperOptions.UseBigNumbers)
                    a = BigDecimalToDouble(cnA.Real);
            }
            if (b is ComplexNumber cnB)
            {
                if (!cnB.IsReal)
                    throw new InvalidOperationException($"The comparison operation is not defined for complex numbers");
                b = cnB.Real;
                if (!mathHelperOptions.UseBigNumbers)
                    b = BigDecimalToDouble(cnB.Real);
            }
        }

        // NaN equals NaN and is smaller than any other number, as double.CompareTo orders it
        bool nanA = (a is double dA && double.IsNaN(dA)) || (a is float fA && float.IsNaN(fA));
        bool nanB = (b is double dB && double.IsNaN(dB)) || (b is float fB && float.IsNaN(fB));
        if (nanA || nanB)
            return nanA == nanB ? 0 : (nanA ? -1 : 1);

        // Handle null
        if (a is null || b is null)
        {
            if (a is null && b is null)
                return 0;
            else
            if (a is null)
                return -1;
            else
                return 1;
        }

        // Values that cannot be compared are ordered by the names of their types, so that sorting stays consistent
        if (!TypeHelper.CompareUsingMostPreciseType(a, b, comparisonOptions, mathHelperOptions, out result))
            return string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName);
        return result;
    }

    // A cast of a double outside the target range does not fail but clamps (or is undefined on older runtimes),
    // so infinity, NaN and values beyond ±2^63 (2^64 for ulong) are checked before casting.
    internal static bool FitsInLong(double value) => value >= -9.2233720368547758E18 && value < 9.2233720368547758E18;

    internal static bool FitsInULong(double value) => value >= 0 && value < 1.8446744073709552E19;

    public static long? GetBoxedIntegerNumberAsLong(object? obj)
    {
        if (obj is null)
            return null;

        try
        {
            switch (obj)
            {
                case byte b: return (long)b;
                case sbyte sb: return (long)sb;
                case short s: return (long)s;
                case ushort us: return (long)us;
                case int i: return (long)i;
                case uint ui: return (long)ui;
                case long l: return l;
                case ulong ul: if (ul <= Int64.MaxValue) return (long)ul; else return null;
                case float fl: if (fl == Math.Truncate(fl) && FitsInLong(fl)) return (long)fl; else return null;
                case double db: if (db == Math.Truncate(db) && FitsInLong(db)) return (long)db; else return null;
                case decimal dd: if (dd == Math.Truncate(dd)) return (long)dd; else return null;
                case BigInteger bi: return (long)bi;
                case BigDecimal bd: if (IsWholeNumber(bd)) return (long)GetWholeValue(bd); else return null;
                default:
                    throw new ArgumentException("Provided object is not a supported numeric type.");
            }
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    public static ulong? GetBoxedIntegerNumberAsULong(object? obj)
    {
        if (obj is null)
            return null;

        try
        {
            switch (obj)
            {
                case byte b: return (ulong)(long)b;
                case sbyte sb: if (sb > 0) return (ulong)sb; else return null;
                case short s: if (s > 0) return (ulong)s; else return null;
                case ushort us: return (ulong)(long)us;
                case int i: if (i > 0) return (ulong)i; else return null;
                case uint ui: return (ulong)ui;
                case long l: if (l > 0) return (ulong)l; else return null;
                case ulong ul: return ul;
                case float fl: if (fl == Math.Truncate(fl) && FitsInULong(fl)) return (ulong)fl; else return null;
                case double db: if (db == Math.Truncate(db) && FitsInULong(db)) return (ulong)db; else return null;
                case decimal dd: if (dd == Math.Truncate(dd)) return (ulong)dd; else return null;
                case BigInteger bi: return (ulong)bi;
                case BigDecimal bd: if (IsWholeNumber(bd)) return (ulong)GetWholeValue(bd); else return null;
                default:
                    throw new ArgumentException("Provided object is not a supported numeric type.");
            }
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    public static double? GetBoxedNumberAsDouble(object? obj)
    {
        if (obj is null)
            return null;

        switch (obj)
        {
            case byte b: return (double)b;
            case sbyte sb: return (double)sb;
            case short s: return (double)s;
            case ushort us: return (double)us;
            case int i: return (double)i;
            case uint ui: return (double)ui;
            case long l: return (double)l;
            case ulong ul: return (double)ul;
            case float fl: return (double)fl;
            case double db: return db;
            case decimal dd: return (double)dd;
            case BigInteger bi: return (double)(long)bi;
            case BigDecimal bd: return BigDecimalToDouble(bd);
            default:
                throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static decimal? GetBoxedNumberAsDecimal(object? obj)
    {
        if (obj is null)
            return null;

        switch (obj)
        {
            case byte b: return (decimal)b;
            case sbyte sb: return (decimal)sb;
            case short s: return (decimal)s;
            case ushort us: return (decimal)us;
            case int i: return (decimal)i;
            case uint ui: return (decimal)ui;
            case long l: return (decimal)l;
            case ulong ul: return (decimal)ul;
            case float fl: return (decimal)fl;
            case double db: return (decimal)db;
            case decimal dd: return dd;
            case BigInteger bi: return (decimal)(long)bi;
            case BigDecimal bd: return BigDecimalToDecimal(bd);
            default:
                throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool IsBoxedIntegerNumber(object? obj)
    {
        if (obj is null)
            return false;

        var t = obj.GetType();
        return t == typeof(byte) || t == typeof(sbyte) ||
               t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) ||
               t == typeof(long) || t == typeof(ulong);
    }

    public static bool IsBoxedIntegerNumberOrBigNumber(object? obj)
    {
        if (obj is null)
            return false;

        var t = obj.GetType();
        if (t == typeof(BigInteger))
        {
            return true;
        }
        else
        if (t == typeof(BigDecimal))
        {
            if (IsWholeNumber((BigDecimal)obj))
            {
                return true;
            }
        }

        return t == typeof(byte) || t == typeof(sbyte) ||
                   t == typeof(short) || t == typeof(ushort) ||
                   t == typeof(int) || t == typeof(uint) ||
                   t == typeof(long) || t == typeof(ulong);
    }

    public static bool IsBoxedIntegerNumberOrBigNumber(Type t)
    {
        if (t == typeof(BigInteger))
        {
            return true;
        }

        return t == typeof(byte) || t == typeof(sbyte) ||
                   t == typeof(short) || t == typeof(ushort) ||
                   t == typeof(int) || t == typeof(uint) ||
                   t == typeof(long) || t == typeof(ulong);
    }

    public static bool IsBoxedNumber(object? obj)
    {
        if (obj is null)
            return false;

        var t = obj.GetType();
        return t == typeof(byte) || t == typeof(sbyte) ||
               t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) ||
               t == typeof(long) || t == typeof(ulong) ||
               t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    public static bool IsBoxedFloatingNumber(object? obj)
    {
        if (obj is null)
            return false;

        var t = obj.GetType();
        return t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    /// <summary>
    /// Checks if the given number is a floating point number and if it is, whether it contains only the whole part
    /// </summary>
    /// <param name="obj">the number to check</param>
    /// <returns><see langword="null"/> if <paramref name="obj"/> is not a floating point number, <see langword="true"/> if <paramref name="obj"/> is a floating point number without a fractional part, and <see langword="false"/> if it has a non-zero fractional part.</returns>
    public static bool? IsBoxedFloatingNumberInteger(object? obj)
    {
        if (obj is null)
            return null;

        // Infinity equals its truncated value, but it is not an integer number
        if (obj is float ft)
        {
            return !float.IsInfinity(ft) && ft == Math.Truncate(ft);
        }

        if (obj is double dt)
        {
            return !double.IsInfinity(dt) && dt == Math.Truncate(dt);
        }

        if (obj is decimal dct)
        {
            return dct == Math.Truncate(dct);
        }

        if (obj is BigDecimal bd)
        {
            return IsWholeNumber(bd);
        }

        return null;
    }

    private static bool IsBigNumber(object? obj) => obj is BigInteger || obj is BigDecimal;

    public static bool IsBoxedNumberOrBigNumber(object? obj)
    {
        if (obj is null)
            return false;

        if (obj is BigInteger || obj is BigDecimal)
            return true;

        var t = obj.GetType();
        return t == typeof(byte) || t == typeof(sbyte) ||
               t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) ||
               t == typeof(long) || t == typeof(ulong) ||
               t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    public static bool IsBoxedFloatingNumberOrBigNumber(object? obj)
    {
        if (obj is null)
            return false;

        if (obj is BigDecimal)
            return true;

        var t = obj.GetType();
        return t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    public static bool? IsBoxedPositiveNumber(object? number)
    {
        if (number is null)
            return null; //throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return b > 0;
            case sbyte sb: return sb > 0;
            case short s: return s > 0;
            case ushort us: return us > 0;
            case int i: return i > 0;
            case uint ui: return ui > 0;
            case long l: return l > 0;
            case ulong ul: return ul > 0;
            case float f: return f > 0f;
            case double d: return d > 0.0;
            case decimal dec: return dec > 0m;
            case BigInteger bi: return bi.Sign > 0;
            case BigDecimal bd: return bd.Sign > 0;
            default:
                return null; //throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool? IsBoxedNegativeNumber(object? number)
    {
        if (number is null)
            return null; //throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return false;
            case sbyte sb: return sb < 0;
            case short s: return s < 0;
            case ushort us: return false;
            case int i: return i < 0;
            case uint ui: return false;
            case long l: return l < 0;
            case ulong ul: return false;
            case float f: return f < 0f;
            case double d: return d < 0.0;
            case decimal dec: return dec < 0m;
            case BigInteger bi: return bi.Sign < 0;
            case BigDecimal bd: return bd.Sign < 0;
            default:
                return null; //throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool? IsBoxedNumberZero(object? number)
    {
        if (number is null)
            return null; // throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return b == 0;
            case sbyte sb: return sb == 0;
            case short s: return s == 0;
            case ushort us: return us == 0;
            case int i: return i == 0;
            case uint ui: return ui == 0;
            case long l: return l == 0;
            case ulong ul: return ul == 0;
            case float f: return f == 0f;
            case double d: return d == 0.0;
            case decimal dec: return dec == 0m;
            case BigInteger bi: return bi.IsZero;
            case BigDecimal bd: return bd.IsZero();
            default:
                return null; //throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool? IsBoxedNumberOne(object? number)
    {
        if (number is null)
            return null; // throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return b == 1;
            case sbyte sb: return sb == 1;
            case short s: return s == 1;
            case ushort us: return us == 1;
            case int i: return i == 1;
            case uint ui: return ui == 1;
            case long l: return l == 1;
            case ulong ul: return ul == 1;
            case float f: return f == 1f;
            case double d: return d == 1d;
            case decimal dec: return dec == 1m;
            case BigInteger bi: return bi.IsOne;
            case BigDecimal bd: return bd.CompareTo(1) == 0;
            default:
                return null; // throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool? IsBoxedNumberZeroOrOne(object? number)
    {
        if (number is null)
            return null; // throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return b == 1 || b == 0;
            case sbyte sb: return sb == 1 || sb == 0;
            case short s: return s == 1 || s == 0;
            case ushort us: return us == 1 || us == 0;
            case int i: return i == 1 || i == 0;
            case uint ui: return ui == 1 || ui == 0;
            case long l: return l == 1 || l == 0;
            case ulong ul: return ul == 1 || ul == 0;
            case float f: return f == 1f || f == 0f;
            case double d: return d == 1d || d == 0d;
            case decimal dec: return dec == 1m || dec == 0m;
            case BigInteger bi: return bi.IsOne || bi.IsZero;
            case BigDecimal bd: return bd.IsZero() || (bd.CompareTo(1) == 0);
            default:
                return null; // throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    internal static object ReduceComplexToReal(ComplexNumber value, MathHelperOptions? options = default)
    {
        if (!value.IsReal)
            return value;

        if (options?.UseBigNumbers == true)
            return value.Real;

        return BigDecimalToDouble(value.Real);
    }

    public static object? ReduceNumericType(object value, Type? restrictToType = null, MathHelperOptions options = default)
    {
        object? lValue = value;
        if (value is ComplexNumber cn && cn.IsReal)
        {
            value = MathHelper.ReduceComplexToReal(cn);
        }

        if (value is BigDecimal bdValue)
        {
            lValue = MathHelper.ReduceBigDecimal(bdValue, restrictToType, false, options);
        }
        else
        if (value is BigInteger biValue)
        {
            lValue = MathHelper.ReduceBigInteger(biValue, restrictToType, options);
        }
        if (MathHelper.IsBoxedIntegerNumber(lValue))
        {
            // The value decides what it can be reduced to: an unsigned value beyond the range of long stays ulong,
            // and a negative value is reduced to the signed type that C# would use instead of the unsigned type
            if (lValue is ulong bigUnsigned && bigUnsigned > long.MaxValue)
                return bigUnsigned;

            bool isNegative = lValue is sbyte or short or int or long && Convert.ToInt64(lValue, CultureInfo.InvariantCulture) < 0;
            if (isNegative)
            {
                if (restrictToType == typeof(byte) || restrictToType == typeof(ushort) || restrictToType == typeof(char))
                    restrictToType = typeof(int);
                else
                if (restrictToType == typeof(uint) || restrictToType == typeof(ulong))
                    restrictToType = typeof(long);
            }

            if (restrictToType == typeof(ulong) || restrictToType == typeof(uint) || restrictToType == typeof(ushort) || restrictToType == typeof(byte) || restrictToType == typeof(char))
            {
                ulong candidate = MathHelper.ConvertToULong(lValue, string.Empty, options.CultureInfo, null);

                if ((restrictToType is null || restrictToType == typeof(uint) || restrictToType == typeof(ushort)) && candidate <= uint.MaxValue)
                    return (uint)candidate;
                else
                if ((restrictToType is null || restrictToType == typeof(int) || restrictToType == typeof(short)) && candidate <= int.MaxValue)
                    return (int)candidate;

                return candidate;
            }

            if ((restrictToType is null || restrictToType == typeof(long) || restrictToType == typeof(int) || restrictToType == typeof(short) || restrictToType == typeof(sbyte)))
            {
                long candidate = MathHelper.ConvertToLong(lValue, string.Empty, options.CultureInfo, null);
                if ((restrictToType is null || restrictToType == typeof(int) || restrictToType == typeof(short)) && candidate >= int.MinValue && candidate <= int.MaxValue)
                    return (int)candidate;
                else
                if ((restrictToType is null || restrictToType == typeof(uint) || restrictToType == typeof(ushort)) && candidate >= uint.MinValue && candidate <= uint.MaxValue)
                    return (uint)(ulong)candidate;

                return candidate;
            }
        }
        else
        if (IsBoxedFloatingNumberInteger(lValue) == true)
        {
            if ((restrictToType == typeof(ulong) || restrictToType == typeof(uint) || restrictToType == typeof(ushort) || restrictToType == typeof(byte) || restrictToType == typeof(char)))
            {
                ulong? candidate = GetBoxedIntegerNumberAsULong(lValue);
                if (candidate is not null)
                {
                    if (restrictToType != typeof(ulong) && candidate >= uint.MinValue && candidate <= uint.MaxValue)
                        return (uint)candidate;
                    return candidate;
                }
            }
            if ((restrictToType is null || restrictToType == typeof(long) || restrictToType == typeof(int) || restrictToType == typeof(short) || restrictToType == typeof(sbyte)))
            {
                long? candidate = GetBoxedIntegerNumberAsLong(lValue);
                if (candidate is not null)
                {
                    if (restrictToType != typeof(long) && candidate >= int.MinValue && candidate <= int.MaxValue)
                        return (int)candidate;
                    return candidate;
                }
            }
        }

        if (lValue is double dValue)
        {
            if ((/*restrictToType is null || */restrictToType == typeof(float)) && dValue >= float.MinValue && dValue <= float.MaxValue)
                return (float)dValue;
            if (restrictToType is null || restrictToType == typeof(double))
                return dValue;
        }

        // here, float and decimal are returned as is
        return lValue;
    }

    public static object? ReduceBigDecimal(BigDecimal value, Type? restrictToType = null, bool forceInteger = false, MathHelperOptions? options = default)
    {
        if (options is null)
            options = MathHelperOptions.Empty;

        // A value far beyond the range of double and decimal (or far below their resolution) cannot be narrowed to them; it is not
        // compared with their limits, which would align the exponents and take seconds for 1E+1000000 or 1E-1000000, and a whole
        // value with more than MaxPowResultDigits digits stays a BigDecimal instead of being expanded to a BigInteger
        if (!value.Mantissa.IsZero && Math.Abs(DecimalMagnitude(value)) > 400)
        {
            if (forceInteger || (DecimalMagnitude(value) <= MaxPowResultDigits && IsWholeNumber(value)))
                return GetWholeValue(value);
            // A tiny value converted to a requested floating-point type is 0, as before
            if (DecimalMagnitude(value) < 0)
            {
                if (restrictToType == typeof(double))
                    return value.Sign < 0 ? -0.0 : 0.0;
                if (restrictToType == typeof(float))
                    return value.Sign < 0 ? -0.0f : 0.0f;
                if (restrictToType == typeof(decimal))
                    return 0m;
            }
            return value;
        }

        BigInteger? biResult = null;

        if (forceInteger || IsWholeNumber(value))
        {
            biResult = GetWholeValue(value);

            if ((restrictToType is null || restrictToType == typeof(long) || restrictToType == typeof(int) || restrictToType == typeof(short)) && biResult >= long.MinValue && biResult <= long.MaxValue)
                return (long)biResult;
            else
            if ((restrictToType is null || restrictToType == typeof(ulong) || restrictToType == typeof(uint) || restrictToType == typeof(ushort)) && biResult >= ulong.MinValue && biResult <= ulong.MaxValue)
                return (ulong)biResult;

            // An integer beyond the 64-bit range stays a BigInteger unless a floating-point type was requested
            if (restrictToType != typeof(float) && restrictToType != typeof(double) && restrictToType != typeof(decimal))
                return biResult;
        }

        if (restrictToType == typeof(decimal) && value >= decimal.MinValue && value <= decimal.MaxValue)
        {
            return BigDecimalToDecimal(value);
        }

        // With DecimalAsDefault, the value is narrowed to decimal only if no digits are lost, as it is narrowed to double otherwise
        if (restrictToType is null && options.Value.DecimalAsDefault && value >= decimal.MinValue && value <= decimal.MaxValue)
        {
            decimal decimalValue = BigDecimalToDecimal(value);
            if (new BigDecimal(decimalValue) == value)
                return decimalValue;
        }

        if (restrictToType == typeof(float) && (value >= float.MinValue && value <= float.MaxValue))
        {
            return BigDecimalToSingle(value);
        }

        if (restrictToType == typeof(double) && value >= double.MinValue && value <= double.MaxValue)
        {
            return BigDecimalToDouble(value);
        }

        // Otherwise, the value is narrowed to double only if no digits are lost
        if (options.Value.DecimalAsDefault != true && value >= double.MinValue && value <= double.MaxValue)
        {
            double dValue = BigDecimalToDouble(value);
            if (new BigDecimal(dValue) == value)
                return dValue;
        }

        if (biResult is not null)
            return biResult;

        return value;
    }

    public static object? ReduceBigInteger(BigInteger value, Type? restrictToType = null, MathHelperOptions? options = default)
    {
        if ((restrictToType is null || restrictToType == typeof(long) || restrictToType == typeof(int) || restrictToType == typeof(short)) && value >= long.MinValue && value <= long.MaxValue)
            return (long)value;
        else
        if ((restrictToType is null || restrictToType == typeof(ulong) || restrictToType == typeof(uint) || restrictToType == typeof(ushort)) && value >= ulong.MinValue && value <= ulong.MaxValue)
            return (ulong)value;

        return value;
    }

    public static object? TryReduceToUInt64(BigInteger? value)
    {
        if (value is null)
            return null;

        if (value >= ulong.MinValue && value <= ulong.MaxValue)
            return (ulong)value;

        return value;
    }

#if !AOT_COMPILATION
    private static MethodInfo? FindOperator(
        Type opType,
        string opName)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        return opType
            .GetMethods(flags)
            .FirstOrDefault(m =>
                m.Name == opName &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType.IsAssignableFrom(opType) &&
                m.GetParameters()[1].ParameterType.IsAssignableFrom(opType)
            );
    }
#endif
}
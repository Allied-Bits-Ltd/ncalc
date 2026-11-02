using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using ExtendedNumerics;

using NCalc.Domain;

namespace NCalc.Helpers
{
    public class BoxedNumberComparer : IComparer<object?>, IEqualityComparer<object?>
    {
        private ComparisonOptions _comparisonOptions;
        private MathHelperOptions _mathHelperOptions;

        public BoxedNumberComparer(ComparisonOptions comparisonOptions, MathHelperOptions mathHelperOptions)
        {
            _comparisonOptions = comparisonOptions;
            _mathHelperOptions = mathHelperOptions;
        }

        public int Compare(object? x, object? y)
        {
            return EvaluationHelper.Compare(x, y, _comparisonOptions, _mathHelperOptions);
        }

        /// <summary>
        /// Determines whether two values are equal in the same way as the "=" operator, with one limitation that keeps this method
        /// consistent with <see cref="GetHashCode(object?)"/>: values that are equal must have equal hash codes, so two values whose
        /// hash codes differ are never equal here.
        /// </summary>
        /// <remarks>
        /// The "=" operator compares numbers of different types after converting them to the more precise type (an int to float,
        /// a long to double, a double to decimal with 15 significant digits, a float to decimal with 7), so it is not transitive:
        /// 16777217 equals 16777216f, which equals 16777216, but 16777217 does not equal 16777216. No hash code can give the same
        /// value to every pair that "=" considers equal (except a constant). The hash code is computed from the value of a number
        /// converted to float, so a pair of an integer and a float or a long and a double that "=" considers equal always has
        /// equal hash codes; so do almost all pairs of a decimal and a double or float. The rare pairs left, such as a decimal and
        /// a double that "=" considers equal only after rounding the double to 15 digits and whose floats differ, are unequal here.
        /// </remarks>
        public new bool Equals(object? x, object? y)
        {
            return EvaluationHelper.Compare(x, y, ComparisonType.Equal, _comparisonOptions, _mathHelperOptions)
                && GetHashCode(x) == GetHashCode(y);
        }

        /// <summary>
        /// Returns the hash code of a value; values that <see cref="Equals(object?, object?)"/> considers equal have the same hash code:
        /// numbers of all types (and booleans) are hashed by their value converted to float, a string that is a literal by the value
        /// of the literal, and other values by their text with the string comparer of the options.
        /// </summary>
        public int GetHashCode(object? obj)
        {
            switch (obj)
            {
                case null:
                    return 0;
                case Percent percent:
                    return GetHashCode(percent.Value);
                case ComplexNumber complex when complex.IsReal:
                    return GetHashCode(MathHelper.ReduceComplexToReal(complex, _mathHelperOptions));
                case NCalc.Domain.Vector vector:
                    var hash = vector.Dimensions;
                    for (var i = 0; i < vector.Dimensions; i++)
                        hash = unchecked(hash * 31 + GetNumberHashCode(vector[i]));
                    return hash;
                case ComplexNumber:
                    return obj.GetHashCode();
                case bool boolean:
                    return GetNumberHashCode(boolean ? 1.0 : 0.0);
                case string text:
                    if (!_comparisonOptions.AllOptions.HasFlag(ExpressionOptions.NoStringTypeCoercion)
                        && TypeHelper.TryConvertLiteral(text, _mathHelperOptions, out var literal) && literal is not string)
                        return GetHashCode(literal);
                    return TypeHelper.GetStringComparer(_comparisonOptions).GetHashCode(text);
            }

            if (MathHelper.IsBoxedNumberOrBigNumber(obj) || obj is char)
                return GetNumberHashCode(obj!);

            return TypeHelper.GetStringComparer(_comparisonOptions).GetHashCode(Convert.ToString(obj, _comparisonOptions.CultureInfo) ?? string.Empty);
        }

        // Numbers of different types are compared after a conversion to the more precise type, which rounds an int to float, a long to
        // double, a double to 15 and a float to 7 significant digits of decimal (0.1f equals 0.1m, and 0.30000000000000004 equals 0.3m).
        // So every number is hashed by its value converted to double and then to float (pairs that are equal after the conversion of an
        // integer to float or double, or of a float to double, are then equal floats), rounded to 7 significant digits (as the conversion
        // of a float to decimal does, so that 0.1f and 0.1m are alike) and kept as a BigDecimal. Values beyond the range of float are
        // rounded to 7 significant digits from their own value, so that values beyond the range of double (1E400) can be hashed too.
        private const int HashedSignificantDigits = 7;

        private static int GetNumberHashCode(object number)
        {
            BigDecimal value;
            switch (number)
            {
                // All NaNs are alike; infinities are equal only to themselves
                case double d when double.IsNaN(d) || double.IsInfinity(d):
                    return d.GetHashCode();
                case float f when float.IsNaN(f) || float.IsInfinity(f):
                    return ((double)f).GetHashCode();
                case BigInteger or BigDecimal when !IsInFloatRange(MathHelper.ConvertToBigDecimal(number)):
                    value = MathHelper.ConvertToBigDecimal(number);
                    break;
                case double d when Math.Abs(d) > float.MaxValue:
                    value = new BigDecimal(d);
                    break;
                default:
                    double asDouble = number switch
                    {
                        double d => d,
                        float f => f,
                        char ch => ch,
                        BigInteger or BigDecimal => MathHelper.ConvertToDouble(number, MathHelperOptions.Empty),
                        _ => Convert.ToDouble(number, CultureInfo.InvariantCulture)
                    };
                    float asFloat = (float)asDouble;
                    if (asFloat == 0)
                        return 0;
                    value = Math.Abs(asFloat) < 7.9e28f ? new BigDecimal((decimal)asFloat) : new BigDecimal((double)asFloat);
                    break;
            }

            // 0.0 and -0.0 are equal
            BigInteger mantissa = value.Mantissa;
            if (mantissa.IsZero)
                return 0;

            int exponent = value.Exponent;
            int digits = BigInteger.Abs(mantissa).ToString(CultureInfo.InvariantCulture).Length;
            if (digits > HashedSignificantDigits)
            {
                int shift = digits - HashedSignificantDigits;
                BigInteger divisor = BigInteger.Pow(10, shift);
                BigInteger quotient = BigInteger.DivRem(mantissa, divisor, out BigInteger remainder);
                if (BigInteger.Abs(remainder) * 2 >= divisor)
                    quotient += mantissa.Sign;
                mantissa = quotient;
                exponent += shift;
            }

            // Trailing zeros are removed, so that 1.50 and 1.5 (or 10 rounded up from 9.9999999) get the same hash code
            while (!mantissa.IsZero && (mantissa % 10).IsZero)
            {
                mantissa /= 10;
                exponent++;
            }

            return unchecked((mantissa.GetHashCode() * 397) ^ exponent);
        }

        private static bool IsInFloatRange(BigDecimal value) => value >= new BigDecimal(-float.MaxValue) && value <= new BigDecimal(float.MaxValue);
    }
}

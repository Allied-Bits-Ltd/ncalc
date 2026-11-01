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
        /// Determines whether two values are equal in the same way as the "=" operator.
        /// </summary>
        public new bool Equals(object? x, object? y)
        {
            return EvaluationHelper.Compare(x, y, ComparisonType.Equal, _comparisonOptions, _mathHelperOptions);
        }

        /// <summary>
        /// Returns a hash code that is the same for all values that <see cref="Equals(object?, object?)"/> considers equal:
        /// numbers of all types (and booleans) are hashed by their value as a double, a string that is a literal by the value
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

        // Numbers of different types are compared after a conversion that rounds a double to 15 and a float to 7 significant digits
        // (0.1f equals 0.1m, and 0.30000000000000004 equals 0.3m). So every number is hashed by its value rounded to 7 significant
        // digits, kept as a BigDecimal, so that values beyond the range of double (1E400) can be hashed too.
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
                case double d:
                    value = Math.Abs(d) < (double)decimal.MaxValue ? new BigDecimal((decimal)d) : new BigDecimal(d);
                    break;
                case float f:
                    value = Math.Abs(f) < (float)decimal.MaxValue ? new BigDecimal((decimal)f) : new BigDecimal((double)f);
                    break;
                case char ch:
                    value = new BigDecimal((int)ch);
                    break;
                default:
                    value = MathHelper.ConvertToBigDecimal(number);
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
    }
}

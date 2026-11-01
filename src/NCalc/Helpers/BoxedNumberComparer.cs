using System;
using System.Collections.Generic;
using System.Data;
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

            if (MathHelper.IsBoxedNumberOrBigNumber(obj))
                return GetNumberHashCode(obj);

            return TypeHelper.GetStringComparer(_comparisonOptions).GetHashCode(Convert.ToString(obj, _comparisonOptions.CultureInfo) ?? string.Empty);
        }

        private int GetNumberHashCode(object number)
        {
            var value = MathHelper.ConvertToDouble(number, _mathHelperOptions);
            // All NaNs are alike, and 0.0 and -0.0 are equal
            return double.IsNaN(value) ? double.NaN.GetHashCode() : (value == 0 ? 0 : value.GetHashCode());
        }
    }
}

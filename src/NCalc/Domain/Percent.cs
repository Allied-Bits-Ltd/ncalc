using System.Globalization;
using System.Numerics;
using ExtendedNumerics;
using NCalc.Exceptions;
using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain
{
    public class Percent : IEquatable<Percent>, IFormattable
    {
        public object? Value { get; }

        public ValueType Type { get; }

        public Type OriginalType { get; }

        public Percent(object value)
        {
            Type = value switch
            {
                decimal or double or float => ValueType.Float,
                byte or sbyte or short or int or long or ushort or uint or ulong => ValueType.Integer,
                BigInteger => ValueType.Integer,
                BigDecimal => ValueType.Float,
                null => throw new NCalcConversionException("A null value cannot be converted to a percent", typeof(Percent)),
                _ => throw new NCalcConversionException($"The value '{value}' of type '{value.GetType().Name}' cannot be converted to percent", value.ToString() ?? string.Empty, value.GetType(), typeof(Percent)),
            };

            OriginalType = value.GetType();
            Value = value;
        }

        public Percent(object value, Type originalType)
        {
            OriginalType = originalType;
            Type = value switch
            {
                decimal or double or float => ValueType.Float,
                byte or sbyte or short or int or long or ushort or uint or ulong => ValueType.Integer,
                BigInteger => ValueType.Integer,
                BigDecimal => ValueType.Float,
                null => throw new NCalcConversionException("A null value cannot be converted to a percent", typeof(Percent)),
                _ => throw new NCalcConversionException($"The value '{value}' of type '{originalType}' cannot be converted to percent", value.ToString() ?? string.Empty, originalType, typeof(Percent)),
            };

            Value = value;
        }

        /// <summary>
        /// Returns the percentage as text, such as "1.5%", formatted with the invariant culture.
        /// </summary>
        public override string ToString() => ToString(null, CultureInfo.InvariantCulture);

        /// <summary>
        /// Returns the percentage as text, with the value formatted with the given format and culture.
        /// </summary>
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            string valueText = Value switch
            {
                null => "null",
                BigDecimal bd => MathHelper.FormatBigDecimal(bd, formatProvider),
                IFormattable formattable => formattable.ToString(format, formatProvider ?? CultureInfo.InvariantCulture),
                _ => Convert.ToString(Value, formatProvider ?? CultureInfo.InvariantCulture) ?? string.Empty
            };
            return valueText + "%";
        }

        /// <summary>
        /// Tells whether two percentages have the same numeric value, whatever the types of the values (10% of an int equals 10% of a double).
        /// </summary>
        public bool Equals(Percent? other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            if (TryGetExactValue(Value, out var value) && TryGetExactValue(other.Value, out var otherValue))
                return value == otherValue;
            return Equals(Value, other.Value);
        }

        public override bool Equals(object? obj) => obj is Percent other && Equals(other);

        public override int GetHashCode()
        {
            if (!TryGetExactValue(Value, out var value))
                return Value?.GetHashCode() ?? 0;

            // Equal values with different scales (1.5 and 1.50) get the same hash code
            BigInteger mantissa = value.Mantissa;
            int exponent = value.Exponent;
            if (mantissa.IsZero)
                return 0;
            while ((mantissa % 10).IsZero)
            {
                mantissa /= 10;
                exponent++;
            }
            return unchecked((mantissa.GetHashCode() * 397) ^ exponent);
        }

        // Finite numbers are compared exactly as BigDecimal; NaN and infinities are compared as they are
        private static bool TryGetExactValue(object? value, out BigDecimal result)
        {
            result = default;
            if (value is double d && (double.IsNaN(d) || double.IsInfinity(d)))
                return false;
            if (value is float f && (float.IsNaN(f) || float.IsInfinity(f)))
                return false;
            if (!MathHelper.IsBoxedNumberOrBigNumber(value))
                return false;
            result = MathHelper.ConvertToBigDecimal(value!);
            return true;
        }
    }

    public sealed class PercentExpression : LogicalExpression
    {
        public LogicalExpression Expression { get; set; }

        public PercentExpression(LogicalExpression expression)
        {
            Expression = expression;
        }

        public override T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default)
        {
            return visitor.Visit(this, cancellationToken);
        }

        internal override T AcceptNoRecurse<T>(ILogicalExpressionNoRecurseVisitor<T> visitor, ExpressionTask<T> task, CancellationToken cancellationToken = default)
        {
            return visitor.Visit(this, task, cancellationToken);
        }
    }
}

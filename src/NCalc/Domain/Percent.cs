using System.Numerics;
using ExtendedNumerics;
using NCalc.Exceptions;
using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain
{
    public class Percent
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
                null => throw new NCalcConversionException("A null value cannot be converted to a percent", typeof(Percent)),
                _ => throw new NCalcConversionException($"The value '{value}' of type '{originalType}' cannot be converted to percent", value.ToString() ?? string.Empty, originalType, typeof(Percent)),
            };

            Value = value;
        }

        public override string ToString()
        {
            if (Value is null)
                return "null";
            return Value + "%";
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

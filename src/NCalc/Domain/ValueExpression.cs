using System.Numerics;
using System.Text.Json.Serialization;

using ExtendedNumerics;

using NCalc.Exceptions;
using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain;

internal enum StringKind
{
    Undefined,
    SingleQuote,
    DoubleQuote,
    BackQuote,
    RawDoubleQuote,
}

public sealed class ValueExpression : LogicalExpression
{
    internal StringKind StringKind { get; private set; }
    internal string? OriginalString { get; private set; }

    [JsonConverter(typeof(ObjectValueJsonConverter))]
    public object? Value { get; set; }
    public ValueType Type { get; set; }

    public ValueExpression()
    {
        Type = ValueType.NoValue;
    }

    public ValueExpression(object value)
    {
        Type = value switch
        {
            bool => ValueType.Boolean,
            DateTime => ValueType.DateTime,
            TimeSpan => ValueType.TimeSpan,
            Guid => ValueType.Guid,
            char => ValueType.Char,
            decimal or double or float or BigDecimal => ValueType.Float,
            byte or sbyte or short or int or long or ushort or uint or ulong or BigInteger => ValueType.Integer,
            string or Parlot.TextSpan => ValueType.String,
            _ => throw new NCalcException("This value could not be handled: " + value)
        };

        Value = value;
    }

    public ValueExpression(string? value)
    {
        Value = value;
        Type = ValueType.String;
    }

    internal ValueExpression(string? value, StringKind kind)
    {
        Value = value;
        Type = ValueType.String;
        StringKind = kind;
    }

    internal ValueExpression(string? value, string originalString, StringKind kind)
    {
        Value = value;
        Type = ValueType.String;
        OriginalString = originalString;
        StringKind = kind;
    }

    public ValueExpression(char value)
    {
        Value = value;
        Type = ValueType.Char;
    }

    public ValueExpression(int value)
    {
        Value = value;
        Type = ValueType.Integer;
    }

    public ValueExpression(long value)
    {
        Value = value;
        Type = ValueType.Integer;
    }

    public ValueExpression(double value)
    {
        Value = value;
        Type = ValueType.Float;
    }

    public ValueExpression(decimal value)
    {
        Value = value;
        Type = ValueType.Float;
    }

    public ValueExpression(DateTime value)
    {
        Value = value;
        Type = ValueType.DateTime;
    }

    public ValueExpression(TimeSpan value)
    {
        Value = value;
        Type = ValueType.TimeSpan;
    }

    public ValueExpression(bool value)
    {
        Value = value;
        Type = ValueType.Boolean;
    }

    public ValueExpression(Guid value)
    {
        Value = value;
        Type = ValueType.Guid;
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

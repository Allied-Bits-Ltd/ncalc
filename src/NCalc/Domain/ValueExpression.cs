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
    // Included in JSON, so that a deserialized string literal is written back with its quotes and escapes
    [JsonInclude]
    internal StringKind StringKind { get; private set; }
    [JsonInclude]
    internal string? OriginalString { get; private set; }

    private object? _value;
    private Func<object>? _valueFactory;

    [JsonConverter(typeof(ObjectValueJsonConverter))]
    public object? Value
    {
        // A relative date or time literal such as #now# or #today# is computed again whenever its value is read,
        // so it is not frozen at the time of parsing (parsed expressions are cached and evaluated many times)
        get => _valueFactory is not null ? _valueFactory() : _value;
        set
        {
            _value = value;
            _valueFactory = null;
        }
    }

    /// <summary>
    /// The function that computes the value of a relative date or time literal, or null for a constant value.
    /// </summary>
    internal Func<object>? ValueFactory => _valueFactory;
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

    internal ValueExpression(Func<object> valueFactory, ValueType type)
    {
        _valueFactory = valueFactory;
        Type = type;
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

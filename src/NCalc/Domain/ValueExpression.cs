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

/// <summary>
/// The kind of a relative date or time literal.
/// </summary>
internal enum RelativeDateTimeKind
{
    // A period without a relation (#2 days#): a time span, computed from the current moment, as "1 month" depends on the month
    Period,
    Now,
    Today,
    Yesterday,
    Tomorrow,
    // A moment before or after the current one by the period (#2 days ago#, #in 3 hours#)
    Past,
    Future,
}

/// <summary>
/// Describes a relative date or time literal (#today#, #now#, #2 days ago#), so that it stays relative when the expression is written
/// by ToString() or stored in JSON, instead of being frozen at the moment of writing.
/// </summary>
internal sealed class RelativeDateTime
{
    // The text of the literal between the '#' characters, as it was written, so that ToString() uses the same indicator words
    public string Text { get; set; } = string.Empty;

    public RelativeDateTimeKind Kind { get; set; }

    public int Years { get; set; }

    public int Months { get; set; }

    public int Weeks { get; set; }

    public int Days { get; set; }

    public int Hours { get; set; }

    public int Minutes { get; set; }

    public int Seconds { get; set; }

    public int Milliseconds { get; set; }

    internal ValueType ValueType => Kind == RelativeDateTimeKind.Period ? ValueType.TimeSpan : ValueType.DateTime;

    // Shifts a moment by the period; the period is applied when the value is read, as "1 month" depends on the month
    private DateTime Shift(DateTime moment, int sign)
    {
        if (Years != 0)
            moment = moment.AddYears(sign * Years);
        if (Months != 0)
            moment = moment.AddMonths(sign * Months);
        if (Weeks != 0)
            moment = moment.AddDays(sign * Weeks * 7);
        if (Days != 0)
            moment = moment.AddDays(sign * Days);
        if (Hours != 0)
            moment = moment.AddHours(sign * Hours);
        if (Minutes != 0)
            moment = moment.AddMinutes(sign * Minutes);
        if (Seconds != 0)
            moment = moment.AddSeconds(sign * Seconds);
        if (Milliseconds != 0)
            moment = moment.AddMilliseconds(sign * Milliseconds);
        return moment;
    }

    /// <summary>
    /// Creates the function that computes the value at the moment of evaluation.
    /// </summary>
    internal Func<object> CreateValueFactory()
    {
        switch (Kind)
        {
            case RelativeDateTimeKind.Period:
                return () =>
                {
                    DateTime current = DateTime.UtcNow;
                    return Shift(current, 1) - current;
                };
            case RelativeDateTimeKind.Now:
                return static () => DateTime.Now;
            case RelativeDateTimeKind.Today:
                return static () => DateTime.Now.Date;
            case RelativeDateTimeKind.Yesterday:
                return static () => DateTime.Now.Date.AddDays(-1);
            case RelativeDateTimeKind.Tomorrow:
                return static () => DateTime.Now.Date.AddDays(1);
            case RelativeDateTimeKind.Past:
                return () => Shift(DateTime.Now, -1);
            default:
                return () => Shift(DateTime.Now, 1);
        }
    }
}

public sealed class ValueExpression : LogicalExpression, IJsonOnDeserialized
{
    // Included in JSON, so that a deserialized string literal is written back with its quotes and escapes
    [JsonInclude]
    internal StringKind StringKind { get; private set; }
    [JsonInclude]
    internal string? OriginalString { get; private set; }

    private object? _value;
    private Func<object>? _valueFactory;
    private RelativeDateTime? _relative;

    /// <summary>
    /// The description of a relative date or time literal, or null for a constant value (also after a constant was assigned to
    /// <see cref="Value"/>). It is kept in JSON, from which the function that computes the value is created again.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    internal RelativeDateTime? Relative
    {
        get => _valueFactory is null ? null : _relative;
        private set => _relative = value;
    }

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

    internal static ValueExpression CreateRelative(RelativeDateTime relative)
        => new(relative.CreateValueFactory(), relative.ValueType) { _relative = relative };

    // The value of a relative literal is read from JSON as the constant of the moment of writing; the literal is made relative again
    void IJsonOnDeserialized.OnDeserialized()
    {
        if (_relative is not null)
        {
            _value = null;
            _valueFactory = _relative.CreateValueFactory();
            Type = _relative.ValueType;
        }
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

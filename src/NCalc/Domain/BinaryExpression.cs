using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain;

public sealed class BinaryExpression(
    BinaryExpressionType type,
    LogicalExpression leftExpression,
    LogicalExpression rightExpression) : LogicalExpression
{
    public LogicalExpression LeftExpression { get; set; } = leftExpression;

    public LogicalExpression RightExpression { get; set; } = rightExpression;

    public BinaryExpressionType Type { get; set; } = type;

    public override T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default)
    {
        return visitor.Visit(this, cancellationToken);
    }

    internal override T AcceptNoRecurse<T>(ILogicalExpressionNoRecurseVisitor<T> visitor, ExpressionTask<T> task, CancellationToken cancellationToken = default)
    {
        return visitor.Visit(this, task, cancellationToken);
    }
}

public readonly struct Index
{
    private readonly int _value = 0;

    public Index(int value, bool fromEnd = false)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The value of an Index cannot be negative");
        }

        if (fromEnd)
            _value = ~value;
        else
            _value = value;
    }

    public int Value
    {
        get
        {
            return (_value < 0) ? ~_value : _value;
        }
    }
    public bool IsFromEnd => _value < 0;
}

public record RangeValue
{
    public Index? LowerBound;
    public Index? UpperBound;
}
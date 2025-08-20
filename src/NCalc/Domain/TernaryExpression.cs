using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain;

public class TernaryExpression(
    LogicalExpression leftExpression,
    LogicalExpression middleExpression,
    LogicalExpression rightExpression)
    : LogicalExpression
{
    public LogicalExpression LeftExpression { get; set; } = leftExpression;

    public LogicalExpression MiddleExpression { get; set; } = middleExpression;

    public LogicalExpression RightExpression { get; set; } = rightExpression;

    public override T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default)
    {
        return visitor.Visit(this, cancellationToken);
    }

    internal override T AcceptNoRecurse<T>(ILogicalExpressionNoRecurseVisitor<T> visitor, ExpressionTask<T> task, CancellationToken cancellationToken = default)
    {
        return visitor.Visit(this, task, cancellationToken);
    }
}

public class IfStatementExpression : TernaryExpression
{
    public IfStatementExpression(
        LogicalExpression leftExpression,
        LogicalExpression middleExpression,
        LogicalExpression rightExpression) : base(leftExpression, middleExpression, rightExpression) { }
}

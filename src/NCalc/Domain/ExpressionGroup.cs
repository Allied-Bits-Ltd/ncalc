using NCalc.Visitors;

namespace NCalc.Domain
{
    public sealed class ExpressionGroup : LogicalExpression
    {
        public LogicalExpression Expression { get; set; }

        public ExpressionGroup(LogicalExpression expression)
        {
            Expression = expression;
        }

        public override T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default)
        {
            return visitor.Visit(this, cancellationToken);
        }
    }
}

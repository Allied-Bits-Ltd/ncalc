using NCalc.Visitors;

namespace NCalc.Domain;

public class Identifier(string name) : LogicalExpression
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = name;

    public override T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default)
    {
        return visitor.Visit(this, cancellationToken);
    }
}

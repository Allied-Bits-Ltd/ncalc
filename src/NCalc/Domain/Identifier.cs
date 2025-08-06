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

/*public class IndexedIdentifier : Identifier
{
    internal LogicalExpression Index {  get; set; }

    public IndexedIdentifier(string name, LogicalExpression index) : base(name)
    {
        Index = index;
    }
}*/
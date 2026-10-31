using System.Text.Json.Serialization;

using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain;

public class Identifier(string name) : LogicalExpression
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; set; } = name;

    // Included in JSON, so that a deserialized "[my var]" is written back with its brackets
    [JsonInclude]
    public bool IsBracketed { get; private set; } = false;

    public override T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default)
    {
        return visitor.Visit(this, cancellationToken);
    }

    internal override T AcceptNoRecurse<T>(ILogicalExpressionNoRecurseVisitor<T> visitor, ExpressionTask<T> task, CancellationToken cancellationToken = default)
    {
        return visitor.Visit(this, task, cancellationToken);
    }

    internal Identifier SetBracketed(bool bracketed)
    {
        IsBracketed = bracketed;
        return this;
    }
}

using NCalc.Domain;

namespace NCalc.Visitors;

/// <summary>
/// Visitor dedicated to collect the functions declared with "fn" in a <see cref="LogicalExpression"/>,
/// including the ones declared inside function bodies, as the parser does.
/// </summary>
internal sealed class FunctionDeclarationExtractionVisitor : ILogicalExpressionVisitor<bool>
{
    public List<Function> Functions { get; } = [];

    public bool Visit(Identifier identifier, CancellationToken cancellationToken = default) => true;

    public bool Visit(LogicalExpressionList list, CancellationToken cancellationToken = default)
    {
        foreach (var value in list)
        {
            cancellationToken.ThrowIfCancellationRequested();
            value.Accept(this, cancellationToken);
        }
        return true;
    }

    public bool Visit(UnaryExpression expression, CancellationToken cancellationToken = default) => expression.Expression.Accept(this, cancellationToken);

    public bool Visit(BinaryExpression expression, CancellationToken cancellationToken = default)
    {
        expression.LeftExpression.Accept(this, cancellationToken);
        return expression.RightExpression.Accept(this, cancellationToken);
    }

    public bool Visit(TernaryExpression expression, CancellationToken cancellationToken = default)
    {
        expression.LeftExpression.Accept(this, cancellationToken);
        expression.MiddleExpression.Accept(this, cancellationToken);
        return expression.RightExpression.Accept(this, cancellationToken);
    }

    public bool Visit(FunctionCall function, CancellationToken cancellationToken = default) => function.Parameters.Accept(this, cancellationToken);

    public bool Visit(PercentExpression expression, CancellationToken cancellationToken = default) => expression.Expression.Accept(this, cancellationToken);

    public bool Visit(ImaginaryNumberExpression expression, CancellationToken cancellationToken = default) => expression.Expression.Accept(this, cancellationToken);

    public bool Visit(VectorExpression expression, CancellationToken cancellationToken = default) => expression.Expressions.Accept(this, cancellationToken);

    public bool Visit(ValueExpression expression, CancellationToken cancellationToken = default) => true;

    public bool Visit(ExpressionGroup group, CancellationToken cancellationToken = default) => group.Expression.Accept(this, cancellationToken);

    public bool Visit(FunctionExpression expression, CancellationToken cancellationToken = default)
    {
        Functions.Add(expression.Function);
        foreach (var parameter in expression.Function.Parameters)
            parameter.DefaultValue?.Accept(this, cancellationToken);
        return expression.Function.Body.Accept(this, cancellationToken);
    }

    public bool Visit(StatementSequence seq, CancellationToken cancellationToken = default)
    {
        foreach (var value in seq)
        {
            cancellationToken.ThrowIfCancellationRequested();
            value.Accept(this, cancellationToken);
        }
        return true;
    }
}

using NCalc.Domain;

namespace NCalc.Visitors;

/// <summary>
/// Defines methods to visit different types of logical expressions in an abstract syntax tree (AST).
/// </summary>
/// <typeparam name="T">The type of result returned from each visit method.</typeparam>
public interface ILogicalExpressionVisitor<out T>
{
    T Visit(TernaryExpression expression, CancellationToken cancellationToken = default);
    T Visit(BinaryExpression expression, CancellationToken cancellationToken = default);
    T Visit(UnaryExpression expression, CancellationToken cancellationToken = default);
    T Visit(PercentExpression expression, CancellationToken cancellationToken = default);
    T Visit(ImaginaryNumberExpression expression, CancellationToken cancellationToken = default);
    T Visit(VectorExpression expression, CancellationToken cancellationToken = default);
    T Visit(ValueExpression expression, CancellationToken cancellationToken = default);
    T Visit(FunctionExpression expression, CancellationToken cancellationToken = default);
    T Visit(FunctionCall function, CancellationToken cancellationToken = default);
    T Visit(Identifier identifier, CancellationToken cancellationToken = default);
    T Visit(LogicalExpressionList list, CancellationToken cancellationToken = default);
    T Visit(ExpressionGroup group, CancellationToken cancellationToken = default);
    T Visit(StatementSequence group, CancellationToken cancellationToken = default);
}

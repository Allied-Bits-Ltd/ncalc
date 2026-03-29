using NCalc.Domain;
using NCalc.Helpers;

namespace NCalc.Visitors
{
    /// <summary>
    /// Defines methods to visit different types of logical expressions in an abstract syntax tree (AST).
    /// </summary>
    /// <typeparam name="T">May be bool or Task&lt;bool&gt;.</typeparam>
    internal interface ILogicalExpressionNoRecurseVisitor<T>
    {
        T Visit(TernaryExpression expression, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(BinaryExpression expression, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(UnaryExpression expression, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(PercentExpression expression, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(ComplexNumberExpression expression, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(ValueExpression expression, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(FunctionExpression expression, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(FunctionCall function, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(Identifier identifier, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(LogicalExpressionList list, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(ExpressionGroup group, ExpressionTask<T> task, CancellationToken cancellationToken = default);
        T Visit(StatementSequence group, ExpressionTask<T> task, CancellationToken cancellationToken = default);
    }
}

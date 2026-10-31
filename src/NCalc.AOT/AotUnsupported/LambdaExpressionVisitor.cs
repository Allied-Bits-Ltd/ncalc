using System.Diagnostics.CodeAnalysis;

using NCalc.Domain;
using NCalc.Parser;

using LinqExpression = System.Linq.Expressions.Expression;
using LinqParameterExpression = System.Linq.Expressions.ParameterExpression;

namespace NCalc.Visitors;

// AOT build counterpart of NCalc/Visitors/LambdaExpressionVisitor.cs, see AotUnsupported.
// The constructor throws, so the instance members are unreachable; they are not annotated because
// ILogicalExpressionVisitor<T> is not.
public sealed class LambdaExpressionVisitor : ILogicalExpressionVisitor<LinqExpression>
{
    [RequiresDynamicCode(AotUnsupported.Message)]
    [RequiresUnreferencedCode(AotUnsupported.Message)]
    public LambdaExpressionVisitor(LinqParameterExpression? context, ExpressionContext? expressionContext, IDictionary<string, object?>? parameters, ExpressionOptions options)
    {
        throw AotUnsupported.Exception();
    }

    public LinqExpression Visit(TernaryExpression expression, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(BinaryExpression expression, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(UnaryExpression expression, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(PercentExpression expression, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(ImaginaryNumberExpression expression, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(VectorExpression expression, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(ExpressionGroup group, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(ValueExpression expression, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(FunctionCall function, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(Identifier identifier, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(LogicalExpressionList list, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(FunctionExpression expression, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression Visit(StatementSequence expression, CancellationToken cancellationToken = default) => throw AotUnsupported.Exception();

    public LinqExpression ConvertDoubleIfNoFraction(LinqExpression originalExpression) => throw AotUnsupported.Exception();

    [RequiresDynamicCode(AotUnsupported.Message)]
    [RequiresUnreferencedCode(AotUnsupported.Message)]
    public static LinqExpression ConvertObjectExprToDouble(LinqExpression inputExpr)
    {
        throw AotUnsupported.Exception();
    }

    [RequiresDynamicCode(AotUnsupported.Message)]
    [RequiresUnreferencedCode(AotUnsupported.Message)]
    public static LinqExpression BuildThrowNCalcFlowControl(LinqExpression returnValueExpr, ExpressionLocation location)
    {
        throw AotUnsupported.Exception();
    }

    public LinqExpression LikeOperator(LinqExpression leftValue, LinqExpression? rightValue) => throw AotUnsupported.Exception();
}

using System.Diagnostics.CodeAnalysis;

using LinqExpression = System.Linq.Expressions.Expression;
using LinqParameterExpression = System.Linq.Expressions.ParameterExpression;

namespace NCalc;

// AOT build counterpart of NCalc/Expression.Lambda.cs, see AotUnsupported.
public partial class Expression
{
    protected record struct LinqExpressionWithParameter(LinqExpression Expression, LinqParameterExpression? Parameter);

    [RequiresDynamicCode(AotUnsupported.Message)]
    [RequiresUnreferencedCode(AotUnsupported.Message)]
    protected virtual LinqExpression ToLinqExpression<TResult>(CancellationToken cancellationToken = default)
    {
        throw AotUnsupported.Exception();
    }

    [RequiresDynamicCode(AotUnsupported.Message)]
    [RequiresUnreferencedCode(AotUnsupported.Message)]
    protected virtual LinqExpressionWithParameter ToLinqExpression<TContext, TResult>(CancellationToken cancellationToken = default)
    {
        throw AotUnsupported.Exception();
    }

    [RequiresDynamicCode(AotUnsupported.Message)]
    [RequiresUnreferencedCode(AotUnsupported.Message)]
    public Func<TResult> ToLambda<TResult>(CancellationToken cancellationToken = default)
    {
        throw AotUnsupported.Exception();
    }

    [RequiresDynamicCode(AotUnsupported.Message)]
    [RequiresUnreferencedCode(AotUnsupported.Message)]
    public Func<TContext, TResult> ToLambda<TContext, TResult>(CancellationToken cancellationToken = default)
    {
        throw AotUnsupported.Exception();
    }
}

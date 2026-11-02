using System.Runtime.CompilerServices;

using FastExpressionCompiler;

using NCalc.Exceptions;
using NCalc.Visitors;

using LinqExpression = System.Linq.Expressions.Expression;
using LinqParameterExpression = System.Linq.Expressions.ParameterExpression;

namespace NCalc;

public partial class Expression
{
    internal static readonly bool UseSystemLinqCompiler;

    static Expression()
    {
        UseSystemLinqCompiler = AppContext.TryGetSwitch("NCalc.UseSystemLinqCompiler", out var enabled) && enabled;
    }

    private readonly struct Void;
    protected record struct LinqExpressionWithParameter(LinqExpression Expression, LinqParameterExpression? Parameter);

    private LinqExpressionWithParameter ToLinqExpressionInternal<TContext, TResult>(CancellationToken cancellationToken)
    {
        LogicalExpression ??= GetLogicalExpression(cancellationToken);

        if (Error is not null)
            throw Error;

        // The lambda uses the parameters and options that the expression has now; changing them later does not change the lambda,
        // also for the parameters that are expressions, which are evaluated when the lambda runs (NCALC-242)
        var context = SnapshotContext();

        LambdaExpressionVisitor visitor;
        LinqParameterExpression? parameter = null;
        if (IsVoidType<TContext>())
        {
            visitor = new(null, context, Parameters, context.Options);
        }
        else
        {
            parameter = LinqExpression.Parameter(typeof(TContext), "ctx");
            visitor = new(parameter, context, null, context.Options);
        }

        // A null expression (allowed with AllowNullOrEmptyExpressions) evaluates to null, so the lambda returns the default value
        if (LogicalExpression is null)
            return new() { Expression = LinqExpression.Default(typeof(TResult)), Parameter = parameter };

        // With OverflowProtection, a result that does not fit in TResult throws instead of being truncated
        var body = visitor.ConvertToResultType(LogicalExpression.Accept(visitor, cancellationToken), typeof(TResult));
        _compiledDepth = visitor.MaxDepth;

        return new() { Expression = body, Parameter = parameter };
    }

    private ExpressionContext SnapshotContext()
    {
        var context = Context;
        return context with
        {
            StaticParameters = CopyDictionary(context.StaticParameters),
            DynamicParameters = CopyDictionary(context.DynamicParameters),
            Functions = CopyDictionary(context.Functions),
            AdvancedOptions = context.AdvancedOptions?.Clone()
        };
    }

    private static Dictionary<string, T> CopyDictionary<T>(IDictionary<string, T> source)
        => new(source, (source as Dictionary<string, T>)?.Comparer ?? StringComparer.Ordinal);

    // Limits of the depth of nesting of a compiled expression (NCALC-202), measured on a thread with a 1 MB stack. Chains of operators
    // such as 1 + 2 + 3 + ... are compiled without nesting and do not count.
    // - FastExpressionCompiler recurses deeply for each level and does not check the stack: it terminated the process with
    //   a StackOverflowException at about 150 levels. Above MaxFastCompileDepth the System.Linq compiler is used, which continues
    //   on another thread when the stack runs low.
    // - The stack frame of the compiled lambda grows with the depth, whichever compiler made it: running it overflowed the stack at
    //   about 300 levels. Above MaxLambdaDepth, ToLambda() throws NCalcException.
    internal static int MaxFastCompileDepth = 64;
    internal static int MaxLambdaDepth = 128;

    // The depth of the expression that ToLinqExpression compiled last on this thread
    [ThreadStatic]
    private static int _compiledDepth;

    private static TDelegate CompileLambda<TDelegate>(System.Linq.Expressions.Expression<TDelegate> lambda) where TDelegate : Delegate
    {
        if (_compiledDepth > MaxLambdaDepth)
            throw new NCalcException($"The expression is nested too deeply ({_compiledDepth} levels) to be compiled into a lambda; at most {MaxLambdaDepth} levels are supported. Use Evaluate() instead.");

        if (UseSystemLinqCompiler || _compiledDepth > MaxFastCompileDepth)
            return lambda.Compile();

        return lambda.CompileFast();
    }

    protected virtual LinqExpression ToLinqExpression<TResult>(CancellationToken cancellationToken = default)
    {
        return ToLinqExpressionInternal<Void, TResult>(cancellationToken).Expression;
    }

    protected virtual LinqExpressionWithParameter ToLinqExpression<TContext, TResult>(CancellationToken cancellationToken = default)
    {
        return ToLinqExpressionInternal<TContext, TResult>(cancellationToken);
    }

    public Func<TResult> ToLambda<TResult>(CancellationToken cancellationToken = default)
    {
        _compiledDepth = 0;
        var body = ToLinqExpression<TResult>(cancellationToken);
        return CompileLambda(LinqExpression.Lambda<Func<TResult>>(body));
    }

    public Func<TContext, TResult> ToLambda<TContext, TResult>(CancellationToken cancellationToken = default)
    {
        _compiledDepth = 0;
        var linqExp = ToLinqExpression<TContext, TResult>(cancellationToken);
        if (linqExp.Parameter != null)
            return CompileLambda(LinqExpression.Lambda<Func<TContext, TResult>>(linqExp.Expression, linqExp.Parameter));

        throw new NCalcException("Linq expression parameter cannot be null");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsVoidType<TContext>() => typeof(TContext) == typeof(Void);
}
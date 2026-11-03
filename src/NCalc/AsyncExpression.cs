using System.Runtime.CompilerServices;
using NCalc.Cache;
using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Factories;
using NCalc.Handlers;
using NCalc.Helpers;
using NCalc.Parser;
using Parlot.Fluent;

namespace NCalc;

/// <summary>
/// This class represents a mathematical or logical expression that can be asynchronous evaluated.
/// It supports caching, custom parameter and function evaluation, and options for handling null parameters and iterating over parameter collections.
/// The class manages the parsing, validation, and evaluation of expressions, and provides mechanisms for error detection and reporting.
/// </summary>
public class AsyncExpression : ExpressionBase<AsyncExpressionContext>
{
    public IDictionary<string, AsyncExpressionParameter> DynamicParameters
    {
        get => Context.DynamicParameters;
        set => Context.DynamicParameters = value;
    }

    public IDictionary<string, AsyncExpressionFunction> Functions
    {
        get => Context.Functions;
        set => Context.Functions = value;
    }

    /// <summary>
    /// Event triggered to handle function evaluation.
    /// </summary>
    public event AsyncEvaluateFunctionHandler EvaluateFunctionAsync
    {
        add => Context.AsyncEvaluateFunctionHandler += value;
        remove => Context.AsyncEvaluateFunctionHandler -= value;
    }

    /// <summary>
    /// Event triggered to handle parameter evaluation. If the value is not returned, the Parameters and DynamicParameters properties are checked for a value.
    /// </summary>
    public event AsyncEvaluateParameterHandler EvaluateParameterAsync
    {
        add => Context.AsyncEvaluateParameterHandler += value;
        remove => Context.AsyncEvaluateParameterHandler -= value;
    }

    /// <summary>
    /// Event triggered to handle parameter update.
    /// </summary>
    public event AsyncUpdateParameterHandler UpdateParameterAsync
    {
        add => Context.AsyncUpdateParameterHandler += value;
        remove => Context.AsyncUpdateParameterHandler -= value;
    }

    /// <summary>
    /// Event triggered to handle string matching. If the event handler is not assigned or does not return a value, the default matching using RegEx is performed.
    /// </summary>
    public event AsyncMatchStringHandler MatchStringAsync
    {
        add => Context.AsyncMatchStringHandler += value;
        remove => Context.AsyncMatchStringHandler -= value;
    }

    protected IAsyncEvaluationVisitorFactory EvaluationVisitorFactory { get; }

    private AsyncExpression(AsyncExpressionContext? context = null) : base(context ?? new AsyncExpressionContext())
    {
        EvaluationVisitorFactory = new AsyncEvaluationVisitorFactory();
    }

    private AsyncExpression(AsyncExpressionContext context, IAsyncEvaluationVisitorFactory evaluationVisitorFactory) : base(context)
    {
        EvaluationVisitorFactory = evaluationVisitorFactory;
    }

    /// <summary>
    /// Creates the expression for an argument of a function, which is a part of the tree that is being evaluated.
    /// </summary>
    internal static AsyncExpression CreateSubExpression(LogicalExpression logicalExpression, AsyncExpressionContext context,
        IAsyncEvaluationVisitorFactory? evaluationVisitorFactory, CancellationToken cancellationToken)
    {
        // The argument is evaluated with a visitor of the factory that evaluates the calling expression
        var expression = new AsyncExpression(context, evaluationVisitorFactory ?? new AsyncEvaluationVisitorFactory());
        expression.InitSubExpression(logicalExpression, cancellationToken);
        return expression;
    }

    /// <summary>
    /// Creates the expression for the body of a user function, evaluated with a visitor of the factory that evaluates the calling expression.
    /// </summary>
    internal static AsyncExpression CreateFunctionBody(LogicalExpression body, AsyncExpressionContext callerContext, IAsyncEvaluationVisitorFactory? evaluationVisitorFactory)
    {
        return new AsyncExpression(new AsyncExpressionContext(callerContext.Options, callerContext.CultureInfo), evaluationVisitorFactory ?? new AsyncEvaluationVisitorFactory())
        {
            LogicalExpression = body
        };
    }

    // The visitor knows its factory, so that the arguments of functions and the bodies of user functions are evaluated with visitors of the same factory
    private Visitors.AsyncEvaluationVisitor CreateEvaluationVisitor()
    {
        var evaluationVisitor = EvaluationVisitorFactory.Create(Context);
        evaluationVisitor.VisitorFactory = EvaluationVisitorFactory;
        return evaluationVisitor;
    }

    /// <summary>
    /// Evaluates this expression as the value of a parameter of an outer asynchronous expression. This expression's own parameters come
    /// first, then those of the outer expression; neither are changed, and the handlers of the outer expression are called after this
    /// expression's own ones.
    /// </summary>
    internal async ValueTask<object?> EvaluateAsParameterAsync(AsyncExpressionContext outerContext, CancellationToken cancellationToken)
    {
        // An expression that shares the context of the outer one has its parameters and handlers already
        // Its loops draw from the budget of the outer evaluation
        var outerLoopBudget = LoopIterationBudget.Current;
        if (ReferenceEquals(Context, outerContext))
            return await ParseAndEvaluateAsync(GetEvaluationCancellationToken(cancellationToken), outerLoopBudget).ConfigureAwait(false);

        LogicalExpression ??= GetLogicalExpression(cancellationToken);
        if (Error is not null)
            throw Error;
        if (LogicalExpression is null)
            return null;

        var ownContext = Context;
        var nestedContext = ownContext with
        {
            StaticParameters = new ParameterOverlay<object?>(ownContext.StaticParameters, outerContext.StaticParameters),
            DynamicParameters = new ParameterOverlay<AsyncExpressionParameter>(ownContext.DynamicParameters, outerContext.DynamicParameters),
            Functions = new ParameterOverlay<AsyncExpressionFunction>(ownContext.Functions, outerContext.Functions),
            AsyncEvaluateFunctionHandler = EvaluationHelper.CombineHandlers(ownContext.AsyncEvaluateFunctionHandler, outerContext.AsyncEvaluateFunctionHandler),
            AsyncEvaluateParameterHandler = EvaluationHelper.CombineHandlers(ownContext.AsyncEvaluateParameterHandler, outerContext.AsyncEvaluateParameterHandler),
            AsyncUpdateParameterHandler = EvaluationHelper.CombineHandlers(ownContext.AsyncUpdateParameterHandler, outerContext.AsyncUpdateParameterHandler),
            AsyncMatchStringHandler = EvaluationHelper.CombineHandlers(ownContext.AsyncMatchStringHandler, outerContext.AsyncMatchStringHandler)
        };

        // The functions declared in the tree are in the shared UserFunctions of the copied context already
        var nested = new AsyncExpression(nestedContext, EvaluationVisitorFactory);
        nested.SetLogicalExpressionWithoutRegistration(LogicalExpression);
        return await nested.InternalEvaluateAsync(cancellationToken, outerLoopBudget).ConfigureAwait(false);
    }

    public AsyncExpression(
        string expression,
        AsyncExpressionContext context,
        ILogicalExpressionFactory factory,
        ILogicalExpressionCache cache,
        IAsyncEvaluationVisitorFactory evaluationVisitorFactory) : base(expression, context, factory, cache)
    {
        EvaluationVisitorFactory = evaluationVisitorFactory;
    }

    public AsyncExpression(
        LogicalExpression logicalExpression,
        AsyncExpressionContext context,
        ILogicalExpressionFactory factory,
        ILogicalExpressionCache cache,
        IAsyncEvaluationVisitorFactory evaluationVisitorFactory) : base(logicalExpression, context, factory, cache)
    {
        EvaluationVisitorFactory = evaluationVisitorFactory;
    }

    public AsyncExpression(string expression, AsyncExpressionContext? context = null) : this(context)
    {
        ExpressionString = expression;
    }

    // ReSharper disable once RedundantOverload.Global
    // Reason: False positive, AsyncExpressionContext have implicit conversions.
    public AsyncExpression(string expression) : this(expression, ExpressionOptions.None)
    {
    }

    public AsyncExpression(string expression, ExpressionOptions options = ExpressionOptions.None,
        CultureInfo? cultureInfo = null) : this(expression, new AsyncExpressionContext(options, cultureInfo))
    {
    }

    public AsyncExpression(LogicalExpression logicalExpression, AsyncExpressionContext? context = null) : this(context)
    {
        if (logicalExpression is null)
            throw new ArgumentNullException(nameof(logicalExpression));

        LogicalExpression = logicalExpression;
    }

    // ReSharper disable once RedundantOverload.Global
    // Reason: False positive, AsyncExpressionContext have implicit conversions.
    public AsyncExpression(LogicalExpression logicalExpression) : this(logicalExpression, ExpressionOptions.None)
    {
    }

    public AsyncExpression(LogicalExpression logicalExpression, ExpressionOptions options = ExpressionOptions.None,
        CultureInfo? cultureInfo = null) : this(logicalExpression, new AsyncExpressionContext(options, cultureInfo))
    {
    }

    /// <summary>
    /// Asynchronously evaluates the logical expression.
    /// </summary>
    /// <returns>The result of the evaluation.</returns>
    /// <exception cref="NCalcException">Thrown when there is an error in the expression.</exception>
    public ValueTask<object?> EvaluateAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<object?>(ParseAndEvaluateAsync(GetEvaluationCancellationToken(cancellationToken), OuterLoopBudget));
    }

    // Parsing errors and cancellation fault the returned task instead of being thrown from the call
    private Task<object?> ParseAndEvaluateAsync(CancellationToken cancellationToken, LoopIterationBudget? outerLoopBudget)
    {
        return ReturnOnFreshStackAsync(() =>
        {
            LogicalExpression ??= GetLogicalExpression(cancellationToken);

            return InternalEvaluateAsync(cancellationToken, outerLoopBudget);
        });
    }

    // Deeply nested parts of an evaluation continue on thread pool threads (EvaluationHelper.EvaluateOnThreadPoolAsync). When they finish,
    // the awaiting methods of the parts resume one inside another, so the evaluation would return to its caller with the stack of the
    // thread nearly used up, and the caller's next evaluation could fail as nested too deeply. The outermost evaluation therefore
    // continues on a fresh stack before it returns or throws.
    private static async Task<object?> ReturnOnFreshStackAsync(Func<Task<object?>> evaluate)
    {
        if (EvaluationHelper.ThreadPoolContinuation.Value is not null)
            return await evaluate().ConfigureAwait(false);

        var continuedOnThreadPool = new StrongBox<bool>();
        EvaluationHelper.ThreadPoolContinuation.Value = continuedOnThreadPool;
        try
        {
            var result = await evaluate().ConfigureAwait(false);
            if (continuedOnThreadPool.Value)
                await Task.Yield();
            return result;
        }
        catch when (continuedOnThreadPool.Value)
        {
            await Task.Yield();
            throw;
        }
    }

    /// <summary>
    /// Asynchronously evaluates the logical expression.
    /// </summary>
    /// <returns>The result of the evaluation.</returns>
    /// <exception cref="NCalcException">Thrown when there is an error in the expression.</exception>
    public ValueTask<object?> EvaluateAsync(Parser<LogicalExpression> parser, LogicalExpressionParserContext parserContext, CancellationToken cancellationToken = default)
    {
        return new ValueTask<object?>(ParseAndEvaluateAsync(parser, parserContext, cancellationToken));
    }

    private Task<object?> ParseAndEvaluateAsync(Parser<LogicalExpression> parser, LogicalExpressionParserContext parserContext, CancellationToken cancellationToken)
    {
        return ReturnOnFreshStackAsync(() =>
        {
            LogicalExpression = GetLogicalExpression(parser, parserContext);

            return InternalEvaluateAsync(cancellationToken, OuterLoopBudget);
        });
    }

    // outerLoopBudget: the loop budget of the evaluation that this one is a part of, if any
    private async Task<object?> InternalEvaluateAsync(CancellationToken cancellationToken, LoopIterationBudget? outerLoopBudget)
    {
        if (Error is not null)
            throw Error;

        // If array evaluation, execute the same expression multiple times. The arguments of functions are not iterated again:
        // the outer evaluation has put the current elements into the parameters
        if (Options.HasFlag(ExpressionOptions.IterateParameters) && !IsSubExpression)
            return await IterateParametersAsync(cancellationToken, outerLoopBudget).ConfigureAwait(false);

        if (LogicalExpression is null)
            return null;

        return await EvaluateOnceAsync(CreateEvaluationVisitor(), cancellationToken, outerLoopBudget).ConfigureAwait(false);
    }

    // The static UseNonRecursiveEvaluator setting selects the evaluator for this evaluation without changing Options
    private async ValueTask<object?> EvaluateOnceAsync(Visitors.AsyncEvaluationVisitor evaluationVisitor, CancellationToken cancellationToken, LoopIterationBudget? outerLoopBudget)
    {
        // MaxLoopIterations limits the iterations of all loops of the evaluation together
        var loopBudget = LoopIterationBudget.Enter(Options.HasFlag(ExpressionOptions.UseLoops), MaxLoopIterations, outerLoopBudget);
        try
        {
            if (UseNonRecursiveEvaluator || Options.HasFlag(ExpressionOptions.UseNonRecursiveEvaluator))
                return await evaluationVisitor.EvaluateNoRecurseAsync(LogicalExpression!, cancellationToken).ConfigureAwait(false);
            else
                return await LogicalExpression!.Accept(evaluationVisitor, cancellationToken).ConfigureAwait(false);
        }
        // "return" in an argument of a function returns from the whole expression (or from the function in whose body it is),
        // as "return" elsewhere does, so the expression that evaluates the argument passes it on
        catch (NCalcFlowControl flex) when (flex.Type == NCalcFlowControl.FlowControlType.Return && !IsSubExpression)
        {
            return flex.ReturnValue;
        }
        // An exception (or "return") coming out of deeply nested evaluations starts a new stack trace now and then,
        // which would otherwise be copied at each await on its way and take time that grows with the square of the depth
        catch (Exception ex) when (EvaluationHelper.ShouldRestartStackTrace(ex))
        {
#pragma warning disable CA2200, RCS1044 // The stack trace is restarted on purpose
            throw ex;
#pragma warning restore CA2200, RCS1044
        }
        finally
        {
            loopBudget.Exit();
        }
    }

    private async ValueTask<object?> IterateParametersAsync(CancellationToken cancellationToken, LoopIterationBudget? outerLoopBudget)
    {
        var parameterEnumerators = ParametersHelper.GetEnumerators(Parameters, out var size);

        var evaluationVisitor = CreateEvaluationVisitor();

        if (LogicalExpression is null)
            return null;

        if (size is null)
            return await EvaluateOnceAsync(evaluationVisitor, cancellationToken, outerLoopBudget).ConfigureAwait(false);

        // The current elements are put into a layer over Parameters for each evaluation; Parameters itself is not changed
        // (it may be read-only), so that the next evaluation iterates the lists again
        var parameters = Parameters;
        var iteratedParameters = new IteratedParameters(parameters, parameterEnumerators.Keys);
        Parameters = iteratedParameters;
        try
        {
            var results = new List<object?>();
            for (var i = 0; i < size; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var kvp in parameterEnumerators)
                {
                    kvp.Value.MoveNext();
                    iteratedParameters.SetCurrent(kvp.Key, kvp.Value.Current);
                }

                results.Add(await EvaluateOnceAsync(evaluationVisitor, cancellationToken, outerLoopBudget).ConfigureAwait(false));
            }

            return results;
        }
        finally
        {
            // Unless a handler has replaced the parameters meanwhile
            if (ReferenceEquals(Parameters, iteratedParameters))
                Parameters = parameters;
        }
    }
}
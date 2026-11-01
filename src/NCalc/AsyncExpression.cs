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
    internal static AsyncExpression CreateSubExpression(LogicalExpression logicalExpression, AsyncExpressionContext context, CancellationToken cancellationToken)
    {
        var expression = new AsyncExpression(context);
        expression.InitSubExpression(logicalExpression, cancellationToken);
        return expression;
    }

    /// <summary>
    /// Evaluates this expression as the value of a parameter of an outer asynchronous expression. This expression's own parameters come
    /// first, then those of the outer expression; neither are changed, and the handlers of the outer expression are called after this
    /// expression's own ones.
    /// </summary>
    internal async ValueTask<object?> EvaluateAsParameterAsync(AsyncExpressionContext outerContext, CancellationToken cancellationToken)
    {
        // An expression that shares the context of the outer one has its parameters and handlers already
        if (ReferenceEquals(Context, outerContext))
            return await EvaluateAsync(cancellationToken).ConfigureAwait(false);

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
            AsyncEvaluateFunctionHandler = EvaluationHelper.CombineHandlers(ownContext.AsyncEvaluateFunctionHandler, outerContext.AsyncEvaluateFunctionHandler),
            AsyncEvaluateParameterHandler = EvaluationHelper.CombineHandlers(ownContext.AsyncEvaluateParameterHandler, outerContext.AsyncEvaluateParameterHandler),
            AsyncUpdateParameterHandler = EvaluationHelper.CombineHandlers(ownContext.AsyncUpdateParameterHandler, outerContext.AsyncUpdateParameterHandler),
            AsyncMatchStringHandler = EvaluationHelper.CombineHandlers(ownContext.AsyncMatchStringHandler, outerContext.AsyncMatchStringHandler)
        };

        // The functions declared in the tree are in the shared UserFunctions of the copied context already
        var nested = new AsyncExpression(nestedContext, EvaluationVisitorFactory);
        nested.SetLogicalExpressionWithoutRegistration(LogicalExpression);
        return await nested.InternalEvaluateAsync(cancellationToken).ConfigureAwait(false);
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
        return new ValueTask<object?>(ParseAndEvaluateAsync(GetEvaluationCancellationToken(cancellationToken)));
    }

    // Parsing errors and cancellation fault the returned task instead of being thrown from the call
    private async Task<object?> ParseAndEvaluateAsync(CancellationToken cancellationToken)
    {
        LogicalExpression ??= GetLogicalExpression(cancellationToken);

        return await InternalEvaluateAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task<object?> ParseAndEvaluateAsync(Parser<LogicalExpression> parser, LogicalExpressionParserContext parserContext, CancellationToken cancellationToken)
    {
        LogicalExpression = GetLogicalExpression(parser, parserContext);

        return await InternalEvaluateAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<object?> InternalEvaluateAsync(CancellationToken cancellationToken = default)
    {
        if (Error is not null)
            throw Error;

        // If array evaluation, execute the same expression multiple times. The arguments of functions are not iterated again:
        // the outer evaluation has put the current elements into the parameters
        if (Options.HasFlag(ExpressionOptions.IterateParameters) && !IsSubExpression)
            return await IterateParametersAsync(cancellationToken).ConfigureAwait(false);

        if (LogicalExpression is null)
            return null;

        return await EvaluateOnceAsync(EvaluationVisitorFactory.Create(Context), cancellationToken).ConfigureAwait(false);
    }

    // The static UseNonRecursiveEvaluator setting selects the evaluator for this evaluation without changing Options
    private async ValueTask<object?> EvaluateOnceAsync(Visitors.AsyncEvaluationVisitor evaluationVisitor, CancellationToken cancellationToken)
    {
        try
        {
            if (UseNonRecursiveEvaluator || Options.HasFlag(ExpressionOptions.UseNonRecursiveEvaluator))
                return await evaluationVisitor.EvaluateNoRecurseAsync(LogicalExpression!, cancellationToken).ConfigureAwait(false);
            else
                return await LogicalExpression!.Accept(evaluationVisitor, cancellationToken).ConfigureAwait(false);
        }
        catch (NCalcFlowControl flex)
        {
            if (flex.Type == NCalcFlowControl.FlowControlType.Return)
                return flex.ReturnValue;
            else
                throw;
        }
    }

    private async ValueTask<object?> IterateParametersAsync(CancellationToken cancellationToken = default)
    {
        var parameterEnumerators = ParametersHelper.GetEnumerators(Parameters, out var size);

        var evaluationVisitor = EvaluationVisitorFactory.Create(Context);

        if (LogicalExpression is null)
            return null;

        if (size is null)
            return await EvaluateOnceAsync(evaluationVisitor, cancellationToken).ConfigureAwait(false);

        // Each element is put into Parameters for one evaluation; the lists are put back afterwards,
        // so that the next evaluation iterates them again
        var originalValues = parameterEnumerators.Keys.ToDictionary(key => key, key => Parameters[key]);
        try
        {
            var results = new List<object?>();
            for (var i = 0; i < size; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var kvp in parameterEnumerators)
                {
                    kvp.Value.MoveNext();
                    Parameters[kvp.Key] = kvp.Value.Current;
                }

                results.Add(await EvaluateOnceAsync(evaluationVisitor, cancellationToken).ConfigureAwait(false));
            }

            return results;
        }
        finally
        {
            foreach (var original in originalValues)
                Parameters[original.Key] = original.Value;
        }
    }
}
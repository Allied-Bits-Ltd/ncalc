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
/// This class represents a mathematical or logical expression that can be evaluated.
/// It supports caching, custom parameter and function evaluation, and options for handling null parameters and iterating over parameter collections.
/// The class manages the parsing, validation, and evaluation of expressions, and provides mechanisms for error detection and reporting.
/// </summary>
public partial class Expression : ExpressionBase<ExpressionContext>
{
    /// <summary>
    /// Event triggered to handle function evaluation.
    /// </summary>
    public event EvaluateFunctionHandler EvaluateFunction
    {
        add => Context.EvaluateFunctionHandler += value;
        remove => Context.EvaluateFunctionHandler -= value;
    }

    /// <summary>
    /// Event triggered to handle parameter evaluation. If the value is not returned, the Parameters and DynamicParameters properties are checked for a value.
    /// </summary>
    public event EvaluateParameterHandler EvaluateParameter
    {
        add => Context.EvaluateParameterHandler += value;
        remove => Context.EvaluateParameterHandler -= value;
    }

    /// <summary>
    /// Event triggered to handle parameter evaluation.
    /// </summary>
    public event UpdateParameterHandler UpdateParameter
    {
        add => Context.UpdateParameterHandler += value;
        remove => Context.UpdateParameterHandler -= value;
    }

    /// <summary>
    /// Event triggered to handle string matching. If the event handler is not assigned or does not return a value, the default matching using RegEx is performed.
    /// </summary>
    public event MatchStringHandler MatchString
    {
        add => Context.MatchStringHandler += value;
        remove => Context.MatchStringHandler -= value;
    }

    public IDictionary<string, ExpressionParameter> DynamicParameters
    {
        get => Context.DynamicParameters;
        set => Context.DynamicParameters = value;
    }

    public IDictionary<string, ExpressionFunction> Functions
    {
        get => Context.Functions;
        set => Context.Functions = value;
    }

    protected IEvaluationVisitorFactory EvaluationVisitorFactory { get; }

    private Expression(ExpressionContext? context = null) : base(context ?? new ExpressionContext())
    {
        EvaluationVisitorFactory = new EvaluationVisitorFactory();
    }

    public Expression(
        string expression,
        ExpressionContext context,
        ILogicalExpressionFactory factory,
        ILogicalExpressionCache cache,
        IEvaluationVisitorFactory evaluationVisitorFactory) : base(expression, context, factory, cache)
    {
        EvaluationVisitorFactory = evaluationVisitorFactory;
    }

    public Expression(
        LogicalExpression logicalExpression,
        ExpressionContext context,
        ILogicalExpressionFactory factory,
        ILogicalExpressionCache cache,
        IEvaluationVisitorFactory evaluationVisitorFactory) : base(logicalExpression, context, factory, cache)
    {
        EvaluationVisitorFactory = evaluationVisitorFactory;
    }

    public Expression(string? expression, ExpressionContext? context = null) : this(context)
    {
        ExpressionString = expression;
    }

    // ReSharper disable once RedundantOverload.Global
    // Reason: False positive, ExpressionContext have implicit conversions.
    public Expression(string? expression) : this(expression, ExpressionOptions.None)
    {
    }

    public Expression(string? expression, ExpressionOptions options = ExpressionOptions.None,
        CultureInfo? cultureInfo = null) : this(expression, new ExpressionContext(options, cultureInfo))
    {
    }

    public Expression(LogicalExpression logicalExpression, ExpressionContext? context = null) : this(context)
    {
        LogicalExpression = logicalExpression ?? throw new
            ArgumentException("Expression can't be null", nameof(logicalExpression));
    }

    // ReSharper disable once RedundantOverload.Global
    // Reason: False positive, ExpressionContext have implicit conversions.
    public Expression(LogicalExpression logicalExpression) : this(logicalExpression, ExpressionOptions.None)
    {
    }

    public Expression(LogicalExpression logicalExpression, ExpressionOptions options = ExpressionOptions.None,
        CultureInfo? cultureInfo = null) : this(logicalExpression, new ExpressionContext(options, cultureInfo))
    {
    }

    /// <summary>
    /// Evaluates the logical expression.
    /// </summary>
    /// <returns>The result of the evaluation.</returns>
    /// <exception cref="NCalcException">Thrown when there is an error in the expression.</exception>
    public object? Evaluate()
    {
        return Evaluate(default);
    }

    /// <summary>
    /// Evaluates the logical expression.
    /// </summary>
    /// <returns>The result of the evaluation.</returns>
    /// <exception cref="NCalcException">Thrown when there is an error in the expression.</exception>
    public object? Evaluate(CancellationToken cancellationToken)
    {
        LogicalExpression ??= GetLogicalExpression(cancellationToken);

        return InternalEvaluate(cancellationToken);
    }

    /// <summary>
    /// Evaluates the logical expression.
    /// </summary>
    /// <returns>The result of the evaluation.</returns>
    /// <exception cref="NCalcException">Thrown when there is an error in the expression.</exception>
    public object? Evaluate(Parser<LogicalExpression> parser, LogicalExpressionParserContext parserContext)
    {
        return Evaluate(parser, parserContext, parserContext.CancellationToken);
    }

    /// <summary>
    /// Evaluates the logical expression.
    /// </summary>
    /// <returns>The result of the evaluation.</returns>
    /// <exception cref="NCalcException">Thrown when there is an error in the expression.</exception>
    public object? Evaluate(Parser<LogicalExpression> parser, LogicalExpressionParserContext parserContext, CancellationToken cancellationToken)
    {
        LogicalExpression = GetLogicalExpression(parser, parserContext);

        return InternalEvaluate(cancellationToken);
    }

    private object? InternalEvaluate(CancellationToken cancellationToken = default)
    {
        if (Error is not null)
            throw Error;

        if (LogicalExpression is null)
            return null;

        // If array evaluation, execute the same expression multiple times
        if (Options.HasFlag(ExpressionOptions.IterateParameters))
            return IterateParameters(cancellationToken);

        return EvaluateOnce(EvaluationVisitorFactory.Create(Context), cancellationToken);
    }

    // The static UseNonRecursiveEvaluator setting selects the evaluator for this evaluation without changing Options
    private object? EvaluateOnce(Visitors.EvaluationVisitor evaluationVisitor, CancellationToken cancellationToken)
    {
        try
        {
            if (UseNonRecursiveEvaluator || Options.HasFlag(ExpressionOptions.UseNonRecursiveEvaluator))
                return evaluationVisitor.EvaluateNoRecurse(LogicalExpression!, cancellationToken);
            else
                return LogicalExpression!.Accept(evaluationVisitor, cancellationToken);
        }
        catch (NCalcFlowControl flex)
        {
            if (flex.Type == NCalcFlowControl.FlowControlType.Return)
                return flex.ReturnValue;
            else
                throw;
        }
    }

    private object? IterateParameters(CancellationToken cancellationToken)
    {
        var parameterEnumerators = ParametersHelper.GetEnumerators(Parameters, out var size);

        var evaluationVisitor = EvaluationVisitorFactory.Create(Context);

        if (LogicalExpression is null)
            return null;

        if (size is null)
            return EvaluateOnce(evaluationVisitor, cancellationToken);

        // Each element is put into Parameters for one evaluation; the lists are put back afterwards,
        // so that the next evaluation iterates them again
        var originalValues = parameterEnumerators.Keys.ToDictionary(key => key, key => Parameters[key]);
        try
        {
            var results = new List<object?>();
            for (int i = 0; i < size; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var kvp in parameterEnumerators)
                {
                    kvp.Value.MoveNext();
                    Parameters[kvp.Key] = kvp.Value.Current;
                }

                results.Add(EvaluateOnce(evaluationVisitor, cancellationToken));
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
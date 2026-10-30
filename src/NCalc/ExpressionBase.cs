using System.Diagnostics.CodeAnalysis;
using NCalc.Cache;
using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Factories;
using NCalc.Parser;
using NCalc.Visitors;
using Parlot.Fluent;

namespace NCalc;

/// <summary>
/// Base class with common utilities of AST parsing and evaluation.
/// </summary>
public abstract class ExpressionBase<TExpressionContext> where TExpressionContext : ExpressionContextBase, new()
{
    internal TExpressionContext Context { get; }

    public IDictionary<string, Function> UserFunctions => Context.UserFunctions;

    /// <summary>
    /// Options for the expression evaluation.
    /// </summary>
    public ExpressionOptions Options
    {
        get => Context.Options;
        set => Context.Options = value;
    }

    /// <summary>
    /// Extended Options for the expression evaluation.
    /// </summary>
    public AdvancedExpressionOptions? AdvancedOptions
    {
        get => Context.AdvancedOptions;
        set
        {
            Context.AdvancedOptions = value;
            if (Context.AdvancedOptions != null)
                Context.AdvancedOptions.CultureInfo = CultureInfo;
        }
    }

    /// <summary>
    /// Culture information for the expression evaluation.
    /// </summary>
    public CultureInfo CultureInfo
    {
        get => Context.CultureInfo;
        set
        {
            Context.CultureInfo = value;
            if (Context.AdvancedOptions != null)
                Context.AdvancedOptions.CultureInfo = value;
        }
    }

    /// <summary>
    /// Parameters for the expression evaluation.
    /// </summary>
    public IDictionary<string, object?> Parameters
    {
        get => Context.StaticParameters;
        set => Context.StaticParameters = value;
    }

    /// <summary>
    /// Textual representation of the expression.
    /// </summary>
    public string? ExpressionString { get; protected init; }

    private LogicalExpression? _logicalExpression;

    public LogicalExpression? LogicalExpression
    {
        get => _logicalExpression;
        protected set
        {
            _logicalExpression = value;
            // A tree that was not parsed here (e.g. one deserialized from JSON) brings its declared functions along
            if (value is not null && Context is not null)
                NCalc.Factories.LogicalExpressionFactory.RegisterDeclaredFunctions(value, Context);
        }
    }

    public Exception? Error { get; private set; }

    public static int MaxLoopIterations = 65536;
    public static bool UseNonRecursiveEvaluator = false;

    /// <summary>
    /// When set (the default), the recursive evaluator checks the remaining stack space before evaluating each node
    /// and throws an <see cref="NCalcEvaluationException"/> when an expression is nested too deeply,
    /// instead of letting the process terminate with an uncatchable StackOverflowException.
    /// Clear it only if all evaluated expressions are known to be small.
    /// The setting does not affect parsing or the non-recursive evaluator.
    /// </summary>
    public static bool CheckExecutionStack = true;

    private ILogicalExpressionCache LogicalExpressionCache { get; }
    private ILogicalExpressionFactory LogicalExpressionFactory { get; }

    protected ExpressionBase(TExpressionContext? context = null)
    {
        LogicalExpressionCache = Cache.LogicalExpressionCache.GetInstance();
        LogicalExpressionFactory = Factories.LogicalExpressionFactory.GetInstance();
        Context = context ?? new TExpressionContext();
    }

    protected ExpressionBase(
        string expressionString,
        TExpressionContext context,
        ILogicalExpressionFactory logicalExpressionFactory,
        ILogicalExpressionCache logicalExpressionCache)
    {
        ExpressionString = expressionString;
        LogicalExpressionCache = logicalExpressionCache;
        LogicalExpressionFactory = logicalExpressionFactory;
        Context = context;
    }

    protected ExpressionBase(
        LogicalExpression logicalExpression,
        TExpressionContext context,
        ILogicalExpressionFactory logicalExpressionFactory,
        ILogicalExpressionCache logicalExpressionCache)
    {
        Context = context;
        LogicalExpression = logicalExpression;
        LogicalExpressionCache = logicalExpressionCache;
        LogicalExpressionFactory = logicalExpressionFactory;
    }

    /// <summary>
    /// Retrieves the names of parameters (variables) referenced in the expression.
    /// </summary>
    /// <returns>The list of variable names.</returns>
    public List<string> GetParameterNames(CancellationToken cancellationToken = default)
    {
        var parameterExtractionVisitor = new ParameterExtractionVisitor();
        LogicalExpression ??= LogicalExpressionFactory.Create(ExpressionString!, Context, CultureInfo, Context.Options, Context.AdvancedOptions, cancellationToken);
        return LogicalExpression.Accept(parameterExtractionVisitor);
    }

    /// <summary>
    /// Retrieves the names of functions referenced in the expression.
    /// </summary>
    /// <returns>The list of function names.</returns>
    public List<string> GetFunctionNames(CancellationToken cancellationToken = default)
    {
        var functionExtractionVisitor = new FunctionCallExtractionVisitor();
        LogicalExpression ??= LogicalExpressionFactory.Create(ExpressionString!, Context, CultureInfo, Context.Options, Context.AdvancedOptions, cancellationToken);
        return LogicalExpression.Accept(functionExtractionVisitor);
    }

    /// <summary>
    /// Create the LogicalExpression in order to check syntax errors.
    /// If errors are detected, the Error property contains the exception.
    /// </summary>
    /// <returns><see langword="false"/> if the expression syntax is correct and <see langword="true"/> otherwise.</returns>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasErrors(CancellationToken cancellationToken = default)
    {
        Error = null;
        if (!(ExpressionString?.Length > 0))
        {
            if (Options.HasFlag(ExpressionOptions.AllowNullOrEmptyExpressions))
            {
                LogicalExpression = ExpressionString is null ? null : new ValueExpression(string.Empty);
                return false;
            }

            Error = new NCalcException($"{nameof(ExpressionString)} cannot be null or empty.");
            return true;
        }

        try
        {
            LogicalExpression = LogicalExpressionFactory.Create(ExpressionString, Context, CultureInfo, Context.Options, Context.AdvancedOptions, cancellationToken);
        }
        catch (Exception exception)
        {
            Error = exception;
            return true;
        }

        return false;
    }

    private string GetCacheKey(string expression)
    {
        const string separator = "\u001F";
        return string.Concat(expression,
            separator, ((long)(Context.Options & ~ExpressionOptions.NoCache)).ToString(CultureInfo.InvariantCulture),
            separator, CultureInfo.Name,
            separator, Context.AdvancedOptions?.GetCacheKey() ?? string.Empty);
    }

    public LogicalExpression? GetLogicalExpression(CancellationToken cancellationToken = default)
    {
        if (!(ExpressionString?.Length > 0))
        {
            if (Options.HasFlag(ExpressionOptions.AllowNullOrEmptyExpressions))
                return ExpressionString is null ? null : new ValueExpression(string.Empty);

            throw new NCalcException($"{nameof(ExpressionString)} cannot be null or empty.");
        }

        var isCacheEnabled = !Options.HasFlag(ExpressionOptions.NoCache);

        LogicalExpression? logicalExpression = null;

        // The parse result depends on the options and the culture, so they are a part of the key
        var cacheKey = isCacheEnabled ? GetCacheKey(ExpressionString) : null;

        if (cacheKey is not null && LogicalExpressionCache.TryGetValue(cacheKey, out logicalExpression))
        {
            // No parsing happens on a cache hit, so the functions declared in the expression are registered here
            NCalc.Factories.LogicalExpressionFactory.RegisterDeclaredFunctions(logicalExpression!, Context);
            return logicalExpression!;
        }

        try
        {
            logicalExpression = LogicalExpressionFactory.Create(ExpressionString, Context, CultureInfo, Context.Options, Context.AdvancedOptions, cancellationToken);
            if (cacheKey is not null)
                LogicalExpressionCache.Set(cacheKey, logicalExpression);
        }
        catch (Exception exception)
        {
            Error = exception;
        }

        return logicalExpression;
    }

    public LogicalExpression? GetLogicalExpression(Parser<LogicalExpression> parser, LogicalExpressionParserContext parserContext)
    {
        if (string.IsNullOrEmpty(parserContext.Scanner.Buffer))
        {
            if (Options.HasFlag(ExpressionOptions.AllowNullOrEmptyExpressions))
            {
                return parserContext.Scanner.Buffer is null ? null : new ValueExpression(string.Empty);
            }

            throw new NCalcException($"The expression in {nameof(parser)} cannot be null or empty.");
        }

        try
        {
            var logicalExpression = NCalc.Factories.LogicalExpressionFactory.Create(parser, parserContext);
            foreach (var function in parserContext.UserFunctions.Values)
                Context.UserFunctions[function.Name] = function;
            return logicalExpression;
        }
        catch (Exception exception)
        {
            Error = exception;
        }

        return null;
    }
}
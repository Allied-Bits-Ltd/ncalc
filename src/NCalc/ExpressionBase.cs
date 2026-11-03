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
    /// <remarks>
    /// The expression keeps a copy of the assigned options, which gets the <see cref="CultureInfo"/> of the expression. The copy belongs to
    /// the expression: change the options through this property (<c>e.AdvancedOptions.Flags |= ...</c>); changes made to the assigned
    /// instance afterwards do not affect the expression, and setting <see cref="CultureInfo"/> never changes an instance that may be shared
    /// with other expressions. The next parse uses the current settings of the copy (the parse cache is keyed by them), and a parsed tree
    /// keeps a snapshot of the options it was parsed with, which later changes do not affect.
    /// </remarks>
    public AdvancedExpressionOptions? AdvancedOptions
    {
        get => Context.AdvancedOptions;
        set
        {
            // Assigning the options the expression already has (e.g. its own copy) keeps them
            if (ReferenceEquals(value, Context.AdvancedOptions) && ReferenceEquals(value, _ownAdvancedOptions))
                return;
            Context.AdvancedOptions = _ownAdvancedOptions = WithCulture(value, CultureInfo);
        }
    }

    // The copy of the advanced options that belongs to this expression and can be changed in place
    private AdvancedExpressionOptions? _ownAdvancedOptions;

    private static AdvancedExpressionOptions? WithCulture(AdvancedExpressionOptions? options, CultureInfo cultureInfo)
    {
        if (options is null)
            return null;

        var copy = options.Clone();
        copy.CultureInfo = cultureInfo;
        return copy;
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
            // The options of the expression get the culture; options that came with the context may be shared, so they are copied first
            if (Context.AdvancedOptions is { } advancedOptions)
            {
                if (ReferenceEquals(advancedOptions, _ownAdvancedOptions))
                    advancedOptions.CultureInfo = value;
                else
                    Context.AdvancedOptions = _ownAdvancedOptions = WithCulture(advancedOptions, value);
            }
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

    // Set for the expressions that the evaluators create for the arguments of functions. Such an expression evaluates a part of a tree
    // whose declared functions are registered already, does not iterate the list parameters again (the outer evaluation does that),
    // and is evaluated with the token of the outer evaluation when no other token is given
    private bool _isSubExpression;
    private CancellationToken _subExpressionCancellationToken;

    private protected bool IsSubExpression => _isSubExpression;

    // Set for the expressions that the evaluators create for the bodies of user functions. The body reads the parameters of its caller,
    // whose list parameters are not iterated again (the outer evaluation does that)
    private protected bool IsFunctionBody { get; set; }

    private protected void InitSubExpression(LogicalExpression logicalExpression, CancellationToken cancellationToken)
    {
        SetLogicalExpressionWithoutRegistration(logicalExpression);
        _isSubExpression = true;
        _subExpressionCancellationToken = cancellationToken;
        OuterLoopBudget = Helpers.LoopIterationBudget.Current;
    }

    // The loop budget of the evaluation that this expression is a part of (an argument of a function, the body of a user function),
    // taken when the expression is created for it; the expression uses it wherever and whenever it is evaluated
    internal Helpers.LoopIterationBudget? OuterLoopBudget { get; set; }

    // Sets the tree without searching it for declared functions, e.g. for a part of a tree that is being evaluated
    private protected void SetLogicalExpressionWithoutRegistration(LogicalExpression logicalExpression)
    {
        _logicalExpression = logicalExpression;
    }

    // A sub-expression evaluated without a token uses the token of the evaluation that created it
    private protected CancellationToken GetEvaluationCancellationToken(CancellationToken cancellationToken)
        => cancellationToken.CanBeCanceled ? cancellationToken : _subExpressionCancellationToken;

    public static int MaxLoopIterations = 65536;

    /// <summary>
    /// The safety fuse for recursion: the maximum number of nested calls of functions declared in an expression with <c>fn</c> (a function
    /// that calls itself or another declared function). A call beyond it throws an <see cref="Exceptions.NCalcEvaluationException"/>.
    /// The recursive evaluator may reach the limit of the stack earlier, which throws an <see cref="Exceptions.NCalcEvaluationException"/> too.
    /// The default value is 1000.
    /// </summary>
    public static int MaxUserFunctionCallDepth = 1000;

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
        if (LogicalExpression is null && !(ExpressionString?.Length > 0) && Options.HasFlag(ExpressionOptions.AllowNullOrEmptyExpressions))
            return [];

        var parameterExtractionVisitor = new ParameterExtractionVisitor(Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup));
        LogicalExpression ??= LogicalExpressionFactory.Create(ExpressionString!, Context, CultureInfo, Context.Options, Context.AdvancedOptions, cancellationToken);
        // Each name is reported once, in the order of its first use
        return LogicalExpression.Accept(parameterExtractionVisitor, cancellationToken).Distinct().ToList();
    }

    /// <summary>
    /// Retrieves the names of functions referenced in the expression.
    /// </summary>
    /// <returns>The list of function names.</returns>
    public List<string> GetFunctionNames(CancellationToken cancellationToken = default)
    {
        if (LogicalExpression is null && !(ExpressionString?.Length > 0) && Options.HasFlag(ExpressionOptions.AllowNullOrEmptyExpressions))
            return [];

        var functionExtractionVisitor = new FunctionCallExtractionVisitor();
        LogicalExpression ??= LogicalExpressionFactory.Create(ExpressionString!, Context, CultureInfo, Context.Options, Context.AdvancedOptions, cancellationToken);
        // Each name is reported once, in the order of its first use
        return LogicalExpression.Accept(functionExtractionVisitor, cancellationToken).Distinct().ToList();
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Error = exception;
            return true;
        }

        return false;
    }

    private string GetCacheKey(string expression)
    {
        // The cultures are keyed as for the parser cache: by their formats, as a customized culture may keep a standard name.
        // The expression comes last, as it may contain the separator
        const string separator = "\u001F";
        return string.Concat(((long)(Context.Options & ~ExpressionOptions.NoCache)).ToString(CultureInfo.InvariantCulture),
            separator, LogicalExpressionParser.GetCultureCacheKey(CultureInfo),
            separator, LogicalExpressionParser.GetCurrentCultureCacheKey(Context.AdvancedOptions),
            separator, Context.AdvancedOptions?.GetCacheKey() ?? string.Empty,
            separator, expression);
    }

    public LogicalExpression? GetLogicalExpression(CancellationToken cancellationToken = default)
    {
        // The error of an earlier attempt (e.g. with other options) does not apply to this one
        Error = null;

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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Error = exception;
        }

        return logicalExpression;
    }

    public LogicalExpression? GetLogicalExpression(Parser<LogicalExpression> parser, LogicalExpressionParserContext parserContext)
    {
        Error = null;

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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Error = exception;
        }

        return null;
    }
}
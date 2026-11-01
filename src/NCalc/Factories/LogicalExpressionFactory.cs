using Microsoft.Extensions.Logging;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Logging;
using NCalc.Parser;
using NCalc.Visitors;
using Parlot.Fluent;

namespace NCalc.Factories;

/// <summary>
/// Class responsible to create <see cref="LogicalExpression"/> objects. Parlot is used for parsing strings.
/// </summary>
public sealed class LogicalExpressionFactory : ILogicalExpressionFactory
{
    private static readonly LogicalExpressionFactory Instance;
    private readonly ILogger<LogicalExpressionFactory>? logger;

    public LogicalExpressionFactory(ILogger<LogicalExpressionFactory>? logger)
    {
        this.logger = logger;
    }

    static LogicalExpressionFactory()
    {
        ILogger<LogicalExpressionFactory>? logger = null;
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception
        try
        {
            logger = DefaultLoggerFactory.Value.CreateLogger<LogicalExpressionFactory>();
        }
        catch (Exception)
        {
        }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception
        Instance = new LogicalExpressionFactory(logger);
    }

    public static LogicalExpressionFactory GetInstance() => Instance;

    public LogicalExpression Create(string expression, ExpressionContextBase? expressionContext = null, ExpressionOptions options = ExpressionOptions.None)
    {
        try
        {
            return Create(expression, expressionContext, CultureInfo.CurrentCulture, options, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogErrorCreatingLogicalExpression(exception, expression);
            throw new NCalcParserException("Error parsing the expression.", exception);
        }
    }

    LogicalExpression ILogicalExpressionFactory.Create(string expression, ExpressionContextBase? expressionContext, CultureInfo cultureInfo, ExpressionOptions options, AdvancedExpressionOptions? advancedOptions, CancellationToken cancellationToken)
    {
        try
        {
            return Create(expression, expressionContext, cultureInfo, options, advancedOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogErrorCreatingLogicalExpression(exception, expression);
            throw new NCalcParserException("Error parsing the expression.", exception);
        }
    }

    public static LogicalExpression Create(string expression, ExpressionContextBase? expressionContext = null, CultureInfo? cultureInfo = null, ExpressionOptions options = ExpressionOptions.None, AdvancedExpressionOptions? advancedOptions = null, CancellationToken cancellationToken = default)
    {
        // The tree keeps a snapshot of the advanced options: a later change of the caller's instance must not change the meaning or
        // the text (ToString) of a tree that was already parsed and may be cached
        advancedOptions = advancedOptions?.Clone();

        var parserContext = new LogicalExpressionParserContext(expression, options, cultureInfo, cancellationToken);
        parserContext.AdvancedOptions = advancedOptions;
        LogicalExpression result = LogicalExpressionParser.Parse(parserContext);

        // The root keeps the settings it was parsed with, so that ToString() writes the text in the same dialect
        result.SetOptions(options, cultureInfo, advancedOptions);

        // An empty list tells that the tree was parsed and has no declarations, so it does not have to be searched for them
        result.DeclaredFunctions = parserContext.UserFunctions.Count > 0 ? parserContext.UserFunctions.Values.ToList() : [];

        RegisterDeclaredFunctions(result, expressionContext);

       return result;
    }

    /// <summary>
    /// Registers the functions declared in the expression in the context. A function that is already registered under the same name is replaced,
    /// so the expression may be parsed into the same context more than once.
    /// </summary>
    internal static void RegisterDeclaredFunctions(LogicalExpression logicalExpression, ExpressionContextBase? expressionContext)
    {
        if (expressionContext is null)
            return;

        // A tree that was not parsed by this factory (e.g. one deserialized from JSON or built in code) is searched for declarations once
        if (logicalExpression.DeclaredFunctions is null)
        {
            var visitor = new FunctionDeclarationExtractionVisitor();
            logicalExpression.Accept(visitor);
            logicalExpression.DeclaredFunctions = visitor.Functions;
        }

        foreach (var function in logicalExpression.DeclaredFunctions)
            expressionContext.UserFunctions[function.Name] = function;
    }

    public static LogicalExpression Create(Parser<LogicalExpression> parser, LogicalExpressionParserContext parserContext)
    {
        return LogicalExpressionParser.Parse(parser, parserContext);
    }
}
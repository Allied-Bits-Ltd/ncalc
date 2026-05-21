using Microsoft.Extensions.Logging;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Logging;
using NCalc.Parser;
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
        catch (Exception exception)
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
        catch (Exception exception)
        {
            logger?.LogErrorCreatingLogicalExpression(exception, expression);
            throw new NCalcParserException("Error parsing the expression.", exception);
        }
    }

    public static LogicalExpression Create(string expression, ExpressionContextBase? expressionContext = null, CultureInfo? cultureInfo = null, ExpressionOptions options = ExpressionOptions.None, AdvancedExpressionOptions? advancedOptions = null, CancellationToken cancellationToken = default)
    {
        var parserContext = new LogicalExpressionParserContext(expression, options, cultureInfo, cancellationToken);
        parserContext.AdvancedOptions = advancedOptions;
        LogicalExpression result = LogicalExpressionParser.Parse(parserContext);

        if (expressionContext is not null)
            foreach (var function in parserContext.UserFunctions)
                expressionContext.UserFunctions.Add(function);

       return result;
    }

    public static LogicalExpression Create(Parser<LogicalExpression> parser, LogicalExpressionParserContext parserContext)
    {
        return LogicalExpressionParser.Parse(parser, parserContext);
    }
}
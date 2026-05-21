using Microsoft.Extensions.Logging;
using NCalc.Domain;
using NCalc.Logging;

namespace NCalc.Cache;

public sealed class LogicalExpressionCache : ILogicalExpressionCache
{
    private readonly ConcurrentDictionary<string, WeakReference<LogicalExpression>> _compiledExpressions = new();

    private static readonly LogicalExpressionCache Instance;
    private readonly ILogger<LogicalExpressionCache>? logger;

    public LogicalExpressionCache(ILogger<LogicalExpressionCache>? logger)
    {
        this.logger = logger;
    }

    static LogicalExpressionCache()
    {
        ILogger<LogicalExpressionCache>? logger = null;
#pragma warning disable RCS1075 // Avoid empty catch clause that catches System.Exception
        try
        {
            logger = DefaultLoggerFactory.Value.CreateLogger<LogicalExpressionCache>();
        }
        catch(Exception)
        {
        }
#pragma warning restore RCS1075 // Avoid empty catch clause that catches System.Exception
        Instance = new LogicalExpressionCache(logger);
    }

    public static LogicalExpressionCache GetInstance() => Instance;

    public bool TryGetValue(string expression, out LogicalExpression? logicalExpression)
    {
        logicalExpression = null;

        if (!_compiledExpressions.TryGetValue(expression, out var wr))
            return false;
        if (!wr.TryGetTarget(out logicalExpression))
            return false;

        logger?.LogRetrievedFromCache(expression);

        return true;
    }

    public void Set(string expression, LogicalExpression logicalExpression)
    {
        _compiledExpressions[expression] = new WeakReference<LogicalExpression>(logicalExpression);
        ClearCache();
        logger?.LogAddedToCache(expression);
    }

    private void ClearCache()
    {
        foreach (var kvp in _compiledExpressions)
        {
            if (kvp.Value.TryGetTarget(out _))
                continue;

            if (_compiledExpressions.TryRemove(kvp.Key, out _))
            {
                logger?.LogRemovedFromCache(kvp.Key);
            }
        }
    }
}

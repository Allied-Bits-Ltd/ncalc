using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NCalc.Cache.Configuration;
using NCalc.Domain;

namespace NCalc.Cache;

internal sealed class LogicalExpressionMemoryCache(
    IMemoryCache memoryCache,
    IOptions<LogicalExpressionMemoryCacheOptions> optionsSnapshot) : ILogicalExpressionCache
{
    public bool TryGetValue(string expression, out LogicalExpression? logicalExpression)
    {
        return memoryCache.TryGetValue(new CacheKey(expression), out logicalExpression);
    }

    public void Set(string expression, LogicalExpression logicalExpression)
    {
        var options = optionsSnapshot.Value;
        // A size is required when the host cache has a SizeLimit; without one, it is ignored
        memoryCache.Set(new CacheKey(expression), logicalExpression, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = options.AbsoluteExpirationRelativeToNow,
            Size = options.EntrySize,
        });
    }

    /// <summary>
    /// The key of an entry. A key of its own type cannot collide with the string keys of other users of a shared cache.
    /// </summary>
    private sealed class CacheKey(string expression) : IEquatable<CacheKey>
    {
        private readonly string _expression = expression;

        public bool Equals(CacheKey? other) => other is not null && string.Equals(_expression, other._expression, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as CacheKey);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_expression);
    }
}
namespace NCalc.Cache.Configuration;

public class LogicalExpressionMemoryCacheOptions
{
    /// <summary>
    /// The duration of the cache relative to now.
    /// Default Value: 15 minutes.
    /// </summary>
    public TimeSpan AbsoluteExpirationRelativeToNow { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The size of each cached expression, used when the <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> of the
    /// application has a <c>SizeLimit</c> (a cache with a limit rejects entries without a size).
    /// Default Value: 1.
    /// </summary>
    public long EntrySize { get; set; } = 1;
}
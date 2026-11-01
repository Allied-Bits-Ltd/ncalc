using Microsoft.Extensions.DependencyInjection;
using NCalc.Cache.Configuration;
using NCalc.DependencyInjection;
using NCalc.Factories;
using NCalc.Tests.Fixtures;

namespace NCalc.Tests;

[Trait("Category", "Plugins")]
public class MemoryCacheTests(FactoriesWithMemoryCacheFixture fixture) : IClassFixture<FactoriesWithMemoryCacheFixture>
{
    private readonly IExpressionFactory _expressionFactory = fixture.ExpressionFactory;

    [Fact]
    public void Logical_Expression_Without_Cache_Should_Not_Be_The_Same()
    {
        var expression = _expressionFactory.Create("'Sergio' != 'Bella'");

        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(true, result);

        var anotherExpression = _expressionFactory.Create("'Sergio' != 'Bella'", ExpressionOptions.NoCache);

        result = anotherExpression.Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(true, result);

        Assert.NotEqual(expression.LogicalExpression, anotherExpression.LogicalExpression);
    }

    [Fact]
    public void MemoryCacheWithSizeLimitShouldAcceptExpressions()
    {
        // NCALC-088: entries had no size, which a cache with SizeLimit rejects
        var serviceProvider = new ServiceCollection()
            .AddMemoryCache(options => options.SizeLimit = 100)
            .AddNCalc()
            .WithMemoryCache()
            .Services.BuildServiceProvider();
        var cache = serviceProvider.GetRequiredService<NCalc.Cache.ILogicalExpressionCache>();
        var memoryCache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();

        var logicalExpression = new NCalc.Domain.ValueExpression(42);
        cache.Set("1 + 1", logicalExpression);
        Assert.True(cache.TryGetValue("1 + 1", out var cached));
        Assert.Same(logicalExpression, cached);

        // The key does not collide with a string key of another user of the same cache
        Assert.False(memoryCache.TryGetValue("1 + 1", out _));
    }
}
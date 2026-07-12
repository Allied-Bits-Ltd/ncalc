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
}
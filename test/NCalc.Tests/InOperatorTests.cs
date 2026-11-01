namespace NCalc.Tests;

[Trait("Category", "Evaluations")]
public class InOperatorTests
{
    [Fact]
    public void ShouldEvaluateInOperatorWithList()
    {
        var context = new ExpressionContext();
        context.StaticParameters["PageState"] = "Insert";
        Assert.Equal(true, new Expression("{PageState} in ('Insert','Update')", context).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateInOperatorWithString()
    {
        var context = new ExpressionContext();
        context.StaticParameters["PageState"] = "Insert";

        Assert.Equal(true, new Expression("{PageState} in 'Insert a quote, you must.'", context).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateNotInOperator()
    {
        var context = new ExpressionContext();
        context.StaticParameters["PageState"] = "Import";
        Assert.Equal(true, new Expression("{PageState} not in  ('Insert','Update')", context).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void InOperatorShouldRespectStringComparer()
    {
        ExpressionContext context = ExpressionOptions.CaseInsensitiveStringComparer;
        context.StaticParameters["PageState"] = "Insert";
        Assert.Equal(true, new Expression("{PageState} in ('INSERT','UPDATE')", context).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateTrueInOperatorWithObjects()
    {
        Assert.Equal(true, new Expression("{tap_int_status} in (5)")
        {
            Parameters = { { "tap_int_status", 5 } }
        }.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateFalseInOperatorWithObjects()
    {
        Assert.Equal(false, new Expression("{PageState} in 4")
        {
            Parameters = { { "PageState", "Insert" } }
        }.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateTrueInOperatorWithNonIntegers()
    {
        Assert.Equal(true, new Expression("{short_value} in (1; 2; 3; 4; 5)")
        {
            Parameters = { { "short_value", (short) 5 } }
        }.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateTrueNotInOperatorWithNonIntegers()
    {
        Assert.Equal(true, new Expression("{short_value} not in (1; 2; 3; 4; 5)")
        {
            Parameters = { { "short_value", (short) 7 } }
        }.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("1 in ('1.0', 2)", ExpressionOptions.None, true)]
    [InlineData("in(1, '1.0')", ExpressionOptions.None, true)]
    [InlineData("in(1, '2', 3.0)", ExpressionOptions.None, false)]
    [InlineData("'a' in 'XAY'", ExpressionOptions.CaseInsensitiveStringComparer, true)]
    [InlineData("'a' in 'XAY'", ExpressionOptions.None, false)]
    [InlineData("'a' in ('A', 'B')", ExpressionOptions.CaseInsensitiveStringComparer, true)]
    [InlineData("x in list", ExpressionOptions.None, false)]
    [InlineData("in(x, x)", ExpressionOptions.None, false)]
    [InlineData("'b' in list2", ExpressionOptions.CaseInsensitiveStringComparer, true)]
    public async Task InShouldAgreeWithEquality(string text, ExpressionOptions options, bool expected)
    {
        // NCALC-065: "x in (...)" must be the same as "x = e1 or x = e2 ..."; NaN is not equal to NaN
        foreach (var evaluator in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var expression = new Expression(text, options | evaluator);
            expression.Parameters["x"] = double.NaN;
            expression.Parameters["list"] = new List<double> { double.NaN, 2 };
            expression.Parameters["list2"] = new List<string> { "A", "B" };
            Assert.Equal(expected, expression.Evaluate(TestContext.Current.CancellationToken));

            var asyncExpression = new AsyncExpression(text, options | evaluator);
            asyncExpression.Parameters["x"] = double.NaN;
            asyncExpression.Parameters["list"] = new List<double> { double.NaN, 2 };
            asyncExpression.Parameters["list2"] = new List<string> { "A", "B" };
            Assert.Equal(expected, await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
        }
    }
}
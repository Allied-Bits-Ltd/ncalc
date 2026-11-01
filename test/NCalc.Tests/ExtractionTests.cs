namespace NCalc.Tests;

[Trait("Category", "Extraction")]
public class ExtractionTests
{
    [Fact]
    public void ShouldGetParametersIssue103()
    {
        var expression = new Expression("PageState == 'LIST' && a == 1 && customFunction() == true || in(1 + 1, 1, 2, 3)", ExpressionOptions.CaseInsensitiveStringComparer)
        {
            Parameters =
            {
                ["a"] = 1
            }
        };
        expression.DynamicParameters["PageState"] = _ => "List";
        expression.Functions["customfunction"] = _ => true;

        var parameters = expression.GetParameterNames(TestContext.Current.CancellationToken);
        Assert.Contains("a", parameters);
        Assert.Contains("PageState", parameters);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void ShouldGetParametersOneTimeIssue141()
    {
        var expression =
            new Expression("if(x=0,x,y)",
                ExpressionOptions.CaseInsensitiveStringComparer);
        var parameters = expression.GetParameterNames(TestContext.Current.CancellationToken);

        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void ShouldGetParametersWithUnary()
    {
        var expression = new Expression("-0.68");
        var p = expression.GetParameterNames(TestContext.Current.CancellationToken);
        Assert.Empty(p);
    }

    [Theory]
    [InlineData("(a, b, c)", 3)]
    [InlineData("725 - 1 == result * secret_operation(secretValue)", 2)]
    [InlineData("getLightsaberColor(selectedJedi) == selectedColor", 2)]
    public void ShouldGetParameters(string formula, int expectedCount)
    {
        var expression = new Expression(formula);
        var p = expression.GetParameterNames(TestContext.Current.CancellationToken);
        Assert.Equal(expectedCount, p.Count);
    }

    [InlineData("(a, drop_database(), c) == toUpper(getName())", 3)]
    [InlineData("Abs(523/2) == Abs(523/2)", 1)]
    [InlineData("getLightsaberColor('Yoda') == selectedColor", 1)]
    [Theory]
    public void ShouldGetFunctions(string formula, int expectedCount)
    {
        var expression = new Expression(formula);
        var functions = expression.GetFunctionNames(TestContext.Current.CancellationToken);
        Assert.Equal(expectedCount, functions.Count);
    }

    [Fact]
    public void ShouldGetParametersInsideFunctionsIssue305()
    {
        var expression = new Expression("if([Value] >= 50, 'background-color: #80ffcc;', null)", ExpressionOptions.AllowNullParameter);
        var parameters = expression.GetParameterNames(TestContext.Current.CancellationToken);
        Assert.Equal(2, parameters.Count);
    }

    [Fact]
    public void ShouldGetFunctionsInsideFunctionsIssue305()
    {
        var expression = new Expression("if(getValue() >= 50, 'background-color: #80ffcc;', null)", ExpressionOptions.AllowNullParameter);
        var functions = expression.GetFunctionNames(TestContext.Current.CancellationToken);
        Assert.Equal(2, functions.Count);
    }

    [Fact]
    public void ShouldGetNestedFunctionsIssue334()
    {
        var expression = new Expression("[a] + GetTimeValue(if([c] > [d]; test([e] > 0; [g]; [h]); [f]); 1; 'sec')");
        var functions = expression.GetFunctionNames(TestContext.Current.CancellationToken);
        Assert.Contains("GetTimeValue", functions);
        Assert.Contains("if", functions);
        Assert.Contains("test", functions);
    }

    [Fact]
    public void ExtractedNamesShouldBeDistinctAndExternal()
    {
        // NCALC-083
        Assert.Equal(["a"], new Expression("a + a").GetParameterNames(TestContext.Current.CancellationToken));
        Assert.Equal(["f", "g"], new Expression("f(g()) + f(g())").GetFunctionNames(TestContext.Current.CancellationToken));
        var withFunction = new Expression("fn f(q) => q * 2; f(r)", ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseCStyleAssignments);
        Assert.Equal(["r"], withFunction.GetParameterNames(TestContext.Current.CancellationToken));
        Assert.Empty(new Expression((string?)null, ExpressionOptions.AllowNullOrEmptyExpressions).GetParameterNames(TestContext.Current.CancellationToken));
        Assert.Empty(new Expression(string.Empty, ExpressionOptions.AllowNullOrEmptyExpressions).GetFunctionNames(TestContext.Current.CancellationToken));
    }

    // NCALC-134
    [Fact]
    public void UserFunctionParametersShouldBeRemovedAsTheyAreLookedUp()
    {
        const ExpressionOptions options = ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseCStyleAssignments;
        // Names are looked up ordinally, so X is another parameter than x
        Assert.Equal(["X"], new Expression("fn f(x) => X + x; f(1)", options).GetParameterNames(TestContext.Current.CancellationToken));
        Assert.Empty(new Expression("fn f(x) => X + x; f(1)", options | ExpressionOptions.LowerCaseIdentifierLookup).GetParameterNames(TestContext.Current.CancellationToken));
    }
}
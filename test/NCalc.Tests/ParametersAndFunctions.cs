namespace NCalc.Tests;

using NCalc.Exceptions;
using NCalc.Handlers;
using NCalc.Tests.TestData;
using Xunit;

[Trait("Category", "Parameters and Functions")]
public class ParametersAndFunctions
{
    [Fact]
    public void ExpressionShouldEvaluateCustomFunctions()
    {
        var e = new Expression("SecretOperation(3, 6)");

        e.Functions["SecretOperation"] = (args) => (int)args[0].Evaluate(TestContext.Current.CancellationToken) + (int)args[1].Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(9, e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExpressionShouldEvaluateCustomFunctionsWithParameters()
    {
        var e = new Expression("SecretOperation([e], 6) + f");
        e.Parameters["e"] = 3;
        e.Parameters["f"] = 1;

        e.Functions["SecretOperation"] = (args) => (int)args[0].Evaluate(TestContext.Current.CancellationToken) + (int)args[1].Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(10, e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ExpressionShouldEvaluateCustomFunctionsWithSameName()
    {
        var e = new Expression("SecretOperation(3, 6) + SecretOperation(1, 2)");

        var d = new Dictionary<string, int>();

        e.Functions["SecretOperation"] = (args) =>
        {
            var id = args.Id.ToString();

            if (id != null)
            {
                if (!d.ContainsKey(id))
                {
                    d[id] = 3;
                }
                else
                {
                    d[id]--;
                }
            }

            return (int)args[0].Evaluate(TestContext.Current.CancellationToken) + (int)args[1].Evaluate(TestContext.Current.CancellationToken);
        };

        Assert.Equal(12, e.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(12, e.Evaluate(TestContext.Current.CancellationToken));

        Assert.Equal(2, d.FirstOrDefault().Value);
    }

    [Fact]
    public void ExpressionShouldEvaluateCustomFunctionsWithEffects()
    {
        var e = new Expression("Repeat([value] > 10, 3)");

        const string id = "Repeat";

        var times = new Dictionary<string, int>();
        e.Functions[id] = (args) =>
        {
            var t = (int)args[1].Evaluate(TestContext.Current.CancellationToken) - 1;
            var r = (bool)args[0].Evaluate(TestContext.Current.CancellationToken);
            if (r)
            {
                if (!times.ContainsKey(id))
                {
                    times[id] = t;
                }
                else
                {
                    times[id]--;
                }
            }

            return r && times[id] == 0;
        };
        e.Parameters["value"] = 9;

        Assert.Equal(false, e.Evaluate(TestContext.Current.CancellationToken));

        e.Parameters["value"] = 11;
        Assert.Equal(false, e.Evaluate(TestContext.Current.CancellationToken));
        e.Parameters["value"] = 12;
        Assert.Equal(false, e.Evaluate(TestContext.Current.CancellationToken));
        e.Parameters["value"] = 13;
        Assert.Equal(true, e.Evaluate(TestContext.Current.CancellationToken));
        Assert.Single(times);
    }

    [Fact]
    public void ExpressionShouldEvaluateParameters()
    {
        var e = new Expression("Round(Pow(Pi, 2) + Pow([Pi Squared], 2) + [X], 2)");

        e.Parameters["Pi Squared"] = new Expression("Pi * [Pi]");
        e.Parameters["X"] = 10;

        e.DynamicParameters["Pi"] = _ => 3.14;

        Assert.Equal(117.07, e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Evaluate_Function_Only_Once_Issue_107()
    {
        var counter = 0;
        var totalCounter = 0;

        var expression = new Expression("MyFunc()");

        expression.Functions["MyFunc"] = Expression_EvaluateFunction;

        for (var i = 0; i < 10; i++)
        {
            counter = 0;
            _ = expression.Evaluate(TestContext.Current.CancellationToken);
        }

        object Expression_EvaluateFunction(ExpressionFunctionData args)
        {
            counter++;
            totalCounter++;
            return 1;
        }

        Assert.Equal(10, totalCounter);
    }

    [Fact]
    public void ShouldOverrideExistingFunctions()
    {
        var e = new Expression("Round(1.99, 2)");

        Assert.Equal(1.99d, e.Evaluate(TestContext.Current.CancellationToken));

        e.Functions["Round"] = (_) => 3;

        Assert.Equal(3, e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldHandleCustomParametersWhenNoSpecificParameterIsDefined()
    {
        var e = new Expression("Round(Pow([Pi], 2) + Pow([Pi], 2) + 10, 2)");

        e.DynamicParameters["Pi"] = _ => 3.14;
        e.Evaluate(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ShouldHandleCustomFunctionsInFunctions()
    {
        var e = new Expression("if(true, func1(x) + func2(func3(y)), 0)");

        e.Functions["func1"] = (_) => 1;
        e.Functions["func2"] = (arg) => 2 * Convert.ToDouble(arg[0].Evaluate(TestContext.Current.CancellationToken));
        e.Functions["func3"] = (arg) => 3 * Convert.ToDouble(arg[0].Evaluate(TestContext.Current.CancellationToken));

        e.DynamicParameters["x"] = _ => 1;
        e.DynamicParameters["y"] = _ => 2;
        e.DynamicParameters["z"] = _ => 3;

        Assert.Equal(13d, e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CustomFunctionShouldReturnNull()
    {
        var e = new Expression("SecretOperation(3, 6)");

        e.Functions["SecretOperation"] = (_) => null;

        Assert.Null(e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CustomParametersShouldReturnNull()
    {
        var e = new Expression("x");

        e.DynamicParameters["x"] = _ => null;

        Assert.Null(e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("notExistingfunction")]
    [InlineData("andDoThis")]
    public void ShouldTreatOperatorsWithoutWhitespaceAsFunctionName(string functionName)
    {
        var expression = new Expression($"{functionName}(3.14)");
        expression.Functions[functionName] = (_) => 1;

        Assert.Equal(1, expression.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldHandleCaseInsensitiveParameter()
    {
        var expression = new Expression("name == 'Beatriz'")
        {
            Parameters = new Dictionary<string, object?>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "Name", "Beatriz" }
            }
        };
        Assert.Equal(true, expression.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetArgsCount()
    {
        int count = 0;
        var expression = new Expression("count(1,2,3)")
        {
            Functions =
            {
                {
                    "count", args =>
                    {
                        count = args.Count();
                        return count;
                    }
                }
            }
        };
        _ = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(3, count);
    }

  [Fact]
  public void ShouldEvaluateIntInOperatorWithParameters()
  {
      double x = 3;
      int[] y = [ 1, 2, 3 ]; //or     List<int> y = [ 1, 2, 3 ];
      var expected = true;

      var expression = new Expression("x in y", ExpressionOptions.None);
      expression.Parameters["x"] = x;
      expression.Parameters["y"] = y;

      var actual = (bool)expression.Evaluate(TestContext.Current.CancellationToken);

      Assert.Equal(expected, actual);
  }

    [Fact]
    public void ShouldExtractUserFunction()
    {
        var expression = new Expression("fn count(a,b,c) { 1 }; 2 + 3", ExpressionOptions.NoCache | ExpressionOptions.UseStatementSequences);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(5, result);
        Assert.Single(expression.UserFunctions);
    }

    [Theory]
    [ClassData(typeof(UserFunctionTestData))]

    public void ShouldHandleUserFunction(string input, object? expected)
    {
        var expression = new Expression(input, ExpressionOptions.NoCache |
            ExpressionOptions.IgnoreCaseAtBuiltInFunctions |
            ExpressionOptions.AllowCharValues |
            ExpressionOptions.NoStringTypeCoercion |
            ExpressionOptions.LowerCaseIdentifierLookup |
            ExpressionOptions.SupportTimeOperations |
            ExpressionOptions.UseUnicodeCharsForOperations |
            ExpressionOptions.UseAssignments |
            ExpressionOptions.UseStatementSequences |
            ExpressionOptions.ReduceDivResultToInteger |
            ExpressionOptions.UseBigNumbers |
            ExpressionOptions.SupportCStyleComments |
            ExpressionOptions.UseIfStatement |
            ExpressionOptions.UseLoops);
        expression.AdvancedOptions = new AdvancedExpressionOptions();
        expression.AdvancedOptions.DateSeparatorType = AdvancedExpressionOptions.SeparatorType.BuiltIn;

        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Theory]
    [ClassData(typeof(UserFunctionTestData))]
    public async Task ShouldHandleUserFunctionAsync(string input, object? expected)
    {
        AsyncExpression expression = new AsyncExpression(input,
            ExpressionOptions.NoCache |
            ExpressionOptions.IgnoreCaseAtBuiltInFunctions |
            ExpressionOptions.AllowCharValues |
            ExpressionOptions.NoStringTypeCoercion |
            ExpressionOptions.LowerCaseIdentifierLookup |
            ExpressionOptions.SupportTimeOperations |
            ExpressionOptions.UseUnicodeCharsForOperations |
            ExpressionOptions.UseAssignments |
            ExpressionOptions.UseStatementSequences |
            ExpressionOptions.ReduceDivResultToInteger |
            ExpressionOptions.UseBigNumbers |
            ExpressionOptions.SupportCStyleComments |
            ExpressionOptions.UseIfStatement |
            ExpressionOptions.UseLoops);

        expression.AdvancedOptions = new AdvancedExpressionOptions();
        expression.AdvancedOptions.DateSeparatorType = AdvancedExpressionOptions.SeparatorType.BuiltIn;

        var result = await expression.EvaluateAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task IterateParametersShouldNotChangeTheParameters()
    {
        var sync = new Expression("x * 1", ExpressionOptions.IterateParameters | ExpressionOptions.NoCache);
        sync.Parameters["x"] = new[] { 11, 22, 33 };
        sync.Parameters["s"] = "abc";
        var asyncExpression = new AsyncExpression("x * 1", ExpressionOptions.IterateParameters | ExpressionOptions.NoCache);
        asyncExpression.Parameters["x"] = new[] { 11, 22, 33 };

        for (int i = 0; i < 2; i++)
        {
            // The second evaluation used to see the last element instead of the list; a string is one value, not a list of characters
            Assert.Equal(new object?[] { 11, 22, 33 }, ((IEnumerable<object?>)sync.Evaluate(TestContext.Current.CancellationToken)!).ToArray());
            Assert.Equal(new object?[] { 11, 22, 33 }, ((IEnumerable<object?>)(await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken))!).ToArray());
        }

        var withReturn = new Expression("return x * 2", ExpressionOptions.IterateParameters | ExpressionOptions.NoCache);
        withReturn.Parameters["x"] = new[] { 1, 2 };
        Assert.Equal(new object?[] { 2, 4 }, ((IEnumerable<object?>)withReturn.Evaluate(TestContext.Current.CancellationToken)!).ToArray());
    }

    [Fact]
    public void EvaluateShouldNotWriteIntoTheParameters()
    {
        var parameters = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(new Dictionary<string, object?> { ["x"] = 1 });
        var expression = new Expression("x + 1 == 2 and null == null", ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache) { Parameters = parameters };
        Assert.Equal(true, expression.Evaluate(TestContext.Current.CancellationToken));
        Assert.Single(parameters);

        var oldStatic = Expression.UseNonRecursiveEvaluator;
        try
        {
            Expression.UseNonRecursiveEvaluator = true;
            var options = new Expression("1 + 1", ExpressionOptions.NoCache);
            Assert.Equal(2, options.Evaluate(TestContext.Current.CancellationToken));
            Assert.False(options.Options.HasFlag(ExpressionOptions.UseNonRecursiveEvaluator));
        }
        finally
        {
            Expression.UseNonRecursiveEvaluator = oldStatic;
        }
    }

    [Fact]
    public void ParameterNamesShouldBeComparedOrdinally()
    {
        // NCALC-084: a linguistic comparer treated "ab" and "a\u200Db" (with a zero-width joiner) as the same name
        var expression = new Expression("ab + [a\u200Db]", ExpressionOptions.None, CultureInfo.InvariantCulture);
        expression.Parameters["ab"] = 1;
        expression.Parameters["a\u200Db"] = 2;
        Assert.Equal(2, expression.Parameters.Count);
        Assert.Equal(3, expression.Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-118
    [Fact]
    public async Task ExpressionParameterShouldUseItsOwnParametersFirstAndStayUnchanged()
    {
        var token = TestContext.Current.CancellationToken;

        var child = new Expression("x * 2 + y");
        child.Parameters["x"] = 5;
        var parent = new Expression("a + 1");
        parent.Parameters["x"] = 1;
        parent.Parameters["y"] = 100;
        parent.Parameters["a"] = child;
        Assert.Equal(111, parent.Evaluate(token));
        Assert.Equal(["x"], child.Parameters.Keys);
        child.Parameters["y"] = 0;
        Assert.Equal(10, child.Evaluate(token));

        var asyncChild = new AsyncExpression("x * 2 + y");
        asyncChild.Parameters["x"] = 5;
        var asyncParent = new AsyncExpression("a + 1");
        asyncParent.Parameters["x"] = 1;
        asyncParent.Parameters["y"] = 100;
        asyncParent.Parameters["a"] = asyncChild;
        Assert.Equal(111, await asyncParent.EvaluateAsync(token));
        Assert.Equal(["x"], asyncChild.Parameters.Keys);
    }

    // NCALC-118
    [Fact]
    public async Task ExpressionParameterWithReadOnlyParametersShouldBeEvaluated()
    {
        var token = TestContext.Current.CancellationToken;
        var child = new Expression("x * 2 + y") { Parameters = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(new Dictionary<string, object?> { ["x"] = 5 }) };
        var parent = new Expression("a + 1");
        parent.Parameters["y"] = 3;
        parent.Parameters["a"] = child;
        Assert.Equal(14, parent.Evaluate(token));

        var asyncChild = new AsyncExpression("x * 2 + y") { Parameters = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(new Dictionary<string, object?> { ["x"] = 5 }) };
        var asyncParent = new AsyncExpression("a + 1");
        asyncParent.Parameters["y"] = 3;
        asyncParent.Parameters["a"] = asyncChild;
        Assert.Equal(14, await asyncParent.EvaluateAsync(token));
    }

    // NCALC-118
    [Fact]
    public void ExpressionParameterShouldNotCallAHandlerTwice()
    {
        var token = TestContext.Current.CancellationToken;
        int calls = 0;
        void Handler(string name, FunctionArgs args)
        {
            calls++;
            args.Result = 1;
        }

        // A shared context
        var context = new ExpressionContext();
        var parent = new Expression("a + 1", context);
        var child = new Expression("g()", context);
        parent.EvaluateFunction += Handler;
        parent.Parameters["a"] = child;
        Assert.Equal(2, parent.Evaluate(token));
        Assert.Equal(1, calls);

        // The same handler attached to both
        calls = 0;
        var otherChild = new Expression("g()");
        otherChild.EvaluateFunction += Handler;
        var otherParent = new Expression("a + 1");
        otherParent.EvaluateFunction += Handler;
        otherParent.Parameters["a"] = otherChild;
        Assert.Equal(2, otherParent.Evaluate(token));
        Assert.Equal(1, calls);

        // A handler of the parent only is used by the child for this evaluation only
        calls = 0;
        var plainChild = new Expression("g()");
        var plainParent = new Expression("a + 1");
        plainParent.EvaluateFunction += Handler;
        plainParent.Parameters["a"] = plainChild;
        Assert.Equal(2, plainParent.Evaluate(token));
        Assert.Equal(2, plainParent.Evaluate(token));
        Assert.Equal(2, calls);
        Assert.ThrowsAny<NCalcException>(() => plainChild.Evaluate(token));
    }

    // NCALC-118
    [Fact]
    public async Task ExpressionParameterOfTheOtherKindShouldBeHandled()
    {
        var token = TestContext.Current.CancellationToken;

        // A synchronous expression in an asynchronous one is evaluated with the parameters of both
        var child = new Expression("x * 2 + y");
        child.Parameters["x"] = 5;
        var asyncParent = new AsyncExpression("a + 1");
        asyncParent.Parameters["y"] = 3;
        asyncParent.Parameters["a"] = child;
        Assert.Equal(14, await asyncParent.EvaluateAsync(token));
        Assert.Equal(["x"], child.Parameters.Keys);

        // An asynchronous expression cannot be evaluated by a synchronous one
        var parent = new Expression("a + 1");
        parent.Parameters["a"] = new AsyncExpression("2");
        var exception = Assert.Throws<NCalcEvaluationException>(() => parent.Evaluate(token));
        Assert.Contains("AsyncExpression", exception.Message);
    }

    // NCALC-119
    [Theory]
    [InlineData("x[0]")]
    [InlineData("x[0] + 0")]
    [InlineData("Abs(x[0])")]
    [InlineData("if(true, x[0], 0)")]
    [InlineData("Max(x[0], 0)")]
    public async Task FunctionArgumentsShouldNotIterateParametersAgain(string text)
    {
        var token = TestContext.Current.CancellationToken;
        static List<object?> Values() => [new List<object?> { 1, 2 }, new List<object?> { 3, 4 }];
        static int[] ToInts(object? result) => ((IEnumerable<object?>)result!).Select(v => Convert.ToInt32(v, CultureInfo.InvariantCulture)).ToArray();

        foreach (var nonRecursive in new[] { false, true })
        {
            var options = ExpressionOptions.IterateParameters | ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

            var expression = new Expression(text, options);
            expression.Parameters["x"] = Values();
            Assert.Equal([1, 3], ToInts(expression.Evaluate(token)));

            var asyncExpression = new AsyncExpression(text, options);
            asyncExpression.Parameters["x"] = Values();
            var asyncResult = await asyncExpression.EvaluateAsync(token);
            Assert.Equal([1, 3], ToInts(asyncResult));
        }
    }
}

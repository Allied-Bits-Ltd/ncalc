using NCalc.Domain;
using NCalc.Factories;
using NCalc.Tests.TestData;

using Newtonsoft.Json;

namespace NCalc.Tests;

[Trait("Category", "Async")]
public class AsyncTests
{
    [Theory]
    [ClassData(typeof(EvaluationTestData))]
    public async Task ShouldEvaluateAsync(string expression, object expected)
    {
        Assert.Equal(expected, await new AsyncExpression(expression).EvaluateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ShouldEvaluateAsyncFunction()
    {
        var expression = new AsyncExpression("database_operation('SELECT FOO') == 'FOO'");
        expression.Functions["database_operation"] = async (_, cancellationToken) =>
        {
            // My heavy database work.
            await Task.Delay(1, cancellationToken);

            return "FOO";
        };

        var result = await expression.EvaluateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(true, result);
    }

    [Fact]
    public async Task ShouldEvaluateAsyncParameter()
    {
        var expression = new AsyncExpression("(a + b) == 'Leo'");
        expression.Parameters["b"] = new AsyncExpression("'eo'");
        expression.DynamicParameters["a"] = async (_, cancellationToken) =>
        {
            await Task.Delay(1, cancellationToken);
            return "L";
        };

        var result = await expression.EvaluateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(true, result);
    }

    [Fact]
    public async Task ShouldEvaluateAsyncFunctionHandler()
    {
        var expression = new AsyncExpression("database_operation('SELECT FOO') == 'FOO'");
        expression.EvaluateFunctionAsync += async (name, args, cancellationToken) =>
        {
            if (name == "database_operation")
            {
                //My heavy database work.
                await Task.Delay(100, cancellationToken);

                args.Result = "FOO";
            }
        };

        var result = await expression.EvaluateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(true, result);
    }

    [Fact]
    public async Task ShouldEvaluateAsyncParameterHandler()
    {
        var expression = new AsyncExpression("(a + b) == 'Leo'");
        expression.Parameters["b"] = new AsyncExpression("'eo'");
        expression.EvaluateParameterAsync += (name, args, cancellationToken) =>
        {
            if (name == "a")
            {
                args.Result = "L";
            }

            return default;
        };

        var result = await expression.EvaluateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(true, result);
    }

    [Fact]
    public async Task ShouldEvaluateArrayParameters()
    {
        var e = new AsyncExpression("x * x", ExpressionOptions.IterateParameters)
        {
            Parameters =
            {
                ["x"] = new [] { 0, 1, 2, 3, 4 }
            }
        };

        var result = (IList<object>)await e.EvaluateAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(0, result[0]);
        Assert.Equal(1, result[1]);
        Assert.Equal(4, result[2]);
        Assert.Equal(9, result[3]);
        Assert.Equal(16, result[4]);
    }

    [Theory]
    [ClassData(typeof(BuiltInFunctionsTestData))]
    public async Task ShouldHandleBuiltInFunctions(string expression, object expected, double? tolerance)
    {
        var result = await new AsyncExpression(expression).EvaluateAsync(TestContext.Current.CancellationToken);

        if (tolerance.HasValue)
        {
            Assert.Equal((double)expected, (double)result, precision: 15);
        }
        else
        {
            Assert.Equal(expected, result);
        }
    }

    [Theory]
    [ClassData(typeof(ValuesTestData))]
    public async Task ShouldParseValues(string input, object expectedValue)
    {
        var expression = new AsyncExpression(input);
        var result = await expression.EvaluateAsync(TestContext.Current.CancellationToken);

        if (expectedValue is double expectedDouble)
        {
            Assert.Equal(expectedDouble, (double)result, precision: 15);
        }
        else
        {
            Assert.Equal(expectedValue, result);
        }
    }

    [Theory]
    [ClassData(typeof(NullCheckTestData))]
    public async Task ShouldAllowOperatorsWithNulls(string expression, object? expected)
    {
        var e = new AsyncExpression(expression, ExpressionOptions.AllowNullParameter);
        Assert.Equal(expected, await e.EvaluateAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [ClassData(typeof(WaterLevelCheckTestData))]
    public async Task SerializeAndDeserializeShouldWork(string expression, bool expected, double inputValue)
    {
        var compiled = LogicalExpressionFactory.Create(expression, cancellationToken: TestContext.Current.CancellationToken);
        var serialized = JsonConvert.SerializeObject(compiled, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All // We need this to allow serializing abstract classes
        });

        var deserialized = JsonConvert.DeserializeObject<LogicalExpression>(serialized, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

        var exp = new AsyncExpression(deserialized, ExpressionOptions.NoCache)
        {
            Parameters =
            {
                { "waterlevel", inputValue }
            }
        };

        object? evaluated;
        try
        {
            evaluated = await exp.EvaluateAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            evaluated = false;
        }

        // Assert
        Assert.Equal(expected, evaluated);
    }

    [Theory]
    [InlineData("1a + ]", true)]
    [InlineData("sergio +", true)]
    [InlineData("42 == 42", false)]
    public void HasErrorsIssue239(string expressionString, bool hasError)
    {
        var expression = new AsyncExpression(expressionString);
        Assert.Equal(hasError, expression.HasErrors(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ShouldEvaluateSubExpressionsAsync()
    {
        var volume = new AsyncExpression("[surface] * h");
        var surface = new AsyncExpression("[l] * [L]");
        volume.Parameters["surface"] = surface;
        volume.Parameters["h"] = 3;
        surface.Parameters["l"] = 1;
        surface.Parameters["L"] = 2;

        Assert.Equal(6, await volume.EvaluateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ShouldEvaluateTernaryAsync()
    {
        var result = await new AsyncExpression("1 == 1 ? 42 : 3/0").EvaluateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ShouldEvaluatePowAsync()
    {
        Assert.Equal(4d, await new AsyncExpression("2**2").EvaluateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ShouldEvaluateExpAsync()
    {
        await new AsyncExpression("Exp(-Ln(2)/0.00016346 * 52185600)*13860").EvaluateAsync(TestContext.Current.CancellationToken);
        Assert.True(true);
    }

    [Theory]
    [InlineData("true xor n")]
    [InlineData("n xor true")]
    [InlineData("!n")]
    public async Task AsyncNullHandlingShouldMatchSync(string expression)
    {
        var sync = new Expression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache);
        sync.Parameters["n"] = null;
        var asyncExpression = new AsyncExpression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache);
        asyncExpression.Parameters["n"] = null;
        Assert.Equal(sync.Evaluate(TestContext.Current.CancellationToken), await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Log2(8)", 3.0)]
    [InlineData("Trunc(1.7)", 1.0)]
    [InlineData("MakeStr(2, 65) == 'AA'", 1.0)]
    [InlineData("MakeStr(3, 'c') == 'ccc'", 1.0)]
    public async Task AsyncBuiltInFunctionsShouldMatchSync(string expression, double expected)
    {
        foreach (var result in new[] { new Expression(expression, ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken), await new AsyncExpression(expression, ExpressionOptions.NoCache).EvaluateAsync(TestContext.Current.CancellationToken) })
            Assert.Equal(expected, result is bool b ? (b ? 1.0 : 0.0) : Convert.ToDouble(result, CultureInfo.InvariantCulture));
    }

    // NCALC-134
    [Fact]
    public async Task EvaluateAsyncShouldFaultTheTaskInsteadOfThrowing()
    {
        // An empty expression: the error comes with the task
        var task = new AsyncExpression(string.Empty).EvaluateAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<NCalc.Exceptions.NCalcException>(async () => await task);

        // An already cancelled token, with and without parsing
        using var source = new CancellationTokenSource();
        source.Cancel();
        task = new AsyncExpression("1 + 2", ExpressionOptions.NoCache).EvaluateAsync(source.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);

        var parsed = new AsyncExpression("1 + 2", ExpressionOptions.NoCache);
        Assert.False(parsed.HasErrors(TestContext.Current.CancellationToken));
        task = parsed.EvaluateAsync(source.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
    }
}

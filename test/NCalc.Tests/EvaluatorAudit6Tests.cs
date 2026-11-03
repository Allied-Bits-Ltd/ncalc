using NCalc.Exceptions;

namespace NCalc.Tests;

/// <summary>
/// Regression tests of the sixth audit (evaluators): every case runs with the recursive and the non-recursive evaluator,
/// each synchronously and asynchronously.
/// </summary>
[Trait("Category", "Evaluations")]
public class EvaluatorAudit6Tests
{
    private const ExpressionOptions St = ExpressionOptions.NoCache | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseAssignments
        | ExpressionOptions.UseLoops;

    private static ExpressionOptions Options(bool nonRecursive) => St | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

    private static TheoryData<string, object?, bool> WithEvaluators(params (string Expression, object? Expected)[] cases)
    {
        var data = new TheoryData<string, object?, bool>();
        foreach (var nonRecursive in new[] { false, true })
        {
            foreach (var (expression, expected) in cases)
                data.Add(expression, expected, nonRecursive);
        }
        return data;
    }

    // Sets up the caller's context of both expressions the same way
    private static async Task<(object? Sync, object? Async)> EvaluateBothAsync(string expression, ExpressionOptions options,
        Action<Expression>? setUpSync = null, Action<AsyncExpression>? setUpAsync = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var syncExpression = new Expression(expression, options, CultureInfo.InvariantCulture);
        setUpSync?.Invoke(syncExpression);
        var syncResult = syncExpression.Evaluate(cancellationToken);
        var asyncExpression = new AsyncExpression(expression, options, CultureInfo.InvariantCulture);
        setUpAsync?.Invoke(asyncExpression);
        var asyncResult = await asyncExpression.EvaluateAsync(cancellationToken);
        return (syncResult, asyncResult);
    }

    private static void AssertResult(object? expected, object? actual)
    {
        if (expected is null || expected is bool || expected is string)
            Assert.Equal(expected, actual);
        else
            Assert.Equal(Convert.ToDecimal(expected, CultureInfo.InvariantCulture), Convert.ToDecimal(actual, CultureInfo.InvariantCulture));
    }

    #region fn scoping: the body calls the declared functions and reads the caller's context

    public static TheoryData<string, object?, bool> DeclaredFunctionCases => WithEvaluators(
        ("fn g(x) => x * 2; fn f(x) => g(x) + 1; f(3)", 7),
        // Declared after the function that calls it
        ("fn f(x) => g(x) + 1; fn g(x) => x * 2; f(3)", 7),
        ("fn fact(n) => if(n <= 1, 1, n * fact(n - 1)); fact(5)", 120),
        ("fn fib(n) => iff(n < 2, n, fib(n - 1) + fib(n - 2)); fib(15)", 610),
        ("fn even(n) => iff(n = 0, true, odd(n - 1)); fn odd(n) => iff(n = 0, false, even(n - 1)); even(10)", true),
        ("fn even(n) => iff(n = 0, true, odd(n - 1)); fn odd(n) => iff(n = 0, false, even(n - 1)); odd(7)", true),
        ("fn sum(n) { return iff(n <= 0, 0, n + sum(n - 1)) }; sum(10)", 55),
        // A function declared in a body, as before, and one that calls a function declared outside
        ("fn outer(x) { fn inner(y) => y + 1; inner(x) * 2 }; outer(4)", 10),
        ("fn g(x) => x * 2; fn outer(x) { fn inner(y) => g(y) + 1; inner(x) }; outer(4)", 9),
        ("fn outer(n) { fn inner(y) => iff(y <= 0, 0, outer(y - 1) + 1); inner(n) }; outer(5)", 5),
        // A function passed through an argument expression is evaluated in the caller
        ("fn g(x) => x * 2; fn f(x) => x + 1; f(g(5))", 11));

    [Theory]
    [MemberData(nameof(DeclaredFunctionCases))]
    public async Task FunctionBodyShouldCallDeclaredFunctions(string expression, object? expected, bool nonRecursive)
    {
        var (syncResult, asyncResult) = await EvaluateBothAsync(expression, Options(nonRecursive));
        AssertResult(expected, syncResult);
        AssertResult(expected, asyncResult);
    }

    public static TheoryData<string, object?, bool> CallerContextCases => WithEvaluators(
        // Parameters of the caller
        ("fn f(x) => x + a; f(1)", 11),
        ("fn f(n) => iff(n <= 0, a, f(n - 1)); f(3)", 10),
        // An argument comes before a parameter of the caller with the same name
        ("fn f(a) => a * 2; f(1)", 2),
        // A value assigned by the caller before the call
        ("b := 5; fn f() => b * a; f()", 50),
        // Functions and dynamic parameters of the caller
        ("fn f(x) => Twice(x); f(4)", 8),
        ("fn f(x) => x + d; f(1)", 8),
        ("fn g(x) => Twice(x) + d; fn f(x) => g(x); f(1)", 9),
        // Assignments in the body stay in the body
        ("fn f() { a := 5; a }; f() * 100 + a", 510),
        ("fn f() { a += 1; a }; f() * 100 + a", 1110),
        ("fn f() { l[0] := 9; l[0] }; f() * 10 + l[0]", 91),
        ("fn f() { s[0] := 'z'; s }; f() + s", "zbcabc"),
        ("fn f() { l[0] := 9; l[0] }; l[0] := 7; f() * 10 + l[0]", 97),
        // A function declared in the body of another assigns to the arguments of that function
        ("fn f(n) { fn g() { n := 7; 0 }; g(); n }; f(5)", 7),
        ("fn f(l2) { fn g() { l2[0] := 9; 0 }; g(); l2[0] }; f(l)", 9),
        ("fn f(n) { fn g() { fn h() { n := 7; 0 }; h() }; g(); n }; f(5)", 7),
        // Another function reads the arguments of its caller, but its assignments stay local and hide them
        ("fn g() { n := 7; 0 }; fn f(n) { g(); n }; f(5)", 5),
        ("fn g() { n := 7; n }; fn f(n) { g() * 10 + n }; f(5)", 75),
        ("fn g() { n += 2; n }; fn f(n) { g() * 10 + n }; f(5)", 75),
        ("fn g() { l2[0] := 9; l2[0] }; fn f(l2) { g() * 10 + l2[0] }; f(l)", 91),
        ("fn g() { l2[^1] := 9; l2[2] }; fn f(l2) { g() * 10 + l2[2] }; f(l)", 93),
        ("fn f(n) { fn g() => k(); g() }; fn k() { n := 7; n }; f(5)", 7),
        // The caller of a function may be another function: its local values and arguments come before the parameters of the expression
        ("fn f() { a := 3; g() }; fn g() => a; f()", 3),
        ("fn f(a) => g(); fn g() => a * 2; f(4) + a", 18));

    [Theory]
    [MemberData(nameof(CallerContextCases))]
    public async Task FunctionBodyShouldReadCallerContext(string expression, object? expected, bool nonRecursive)
    {
        var options = Options(nonRecursive) | ExpressionOptions.AllowNullParameter;
        var syncList = new List<object?> { 1, 2, 3 };
        var asyncList = new List<object?> { 1, 2, 3 };
        var (syncResult, asyncResult) = await EvaluateBothAsync(expression, options,
            e =>
            {
                e.Parameters["a"] = 10;
                e.Parameters["l"] = syncList;
                e.Parameters["s"] = "abc";
                e.Functions["Twice"] = data => Convert.ToInt32(data[0].Evaluate(), CultureInfo.InvariantCulture) * 2;
                e.DynamicParameters["d"] = _ => 7;
            },
            e =>
            {
                e.Parameters["a"] = 10;
                e.Parameters["l"] = asyncList;
                e.Parameters["s"] = "abc";
                e.Functions["Twice"] = async (data, token) => Convert.ToInt32(await data[0].EvaluateAsync(token), CultureInfo.InvariantCulture) * 2;
                e.DynamicParameters["d"] = (_, _) => new ValueTask<object?>(7);
            });
        AssertResult(expected, syncResult);
        AssertResult(expected, asyncResult);

        // The list of the caller is changed only by the caller's own assignment
        var expectedFirst = expression.Contains("l[0] := 7", StringComparison.Ordinal) ? 7 : 1;
        Assert.Equal(expectedFirst, syncList[0]);
        Assert.Equal(expectedFirst, asyncList[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FunctionBodyShouldUseCallerHandlers(bool nonRecursive)
    {
        // The handlers of the caller answer for the names that the body does not define, also in a nested call
        const string expression = "fn g(x) => x + h + Ext(x); fn f(x) => g(x * 2); f(1)";
        var (syncResult, asyncResult) = await EvaluateBothAsync(expression, Options(nonRecursive),
            e =>
            {
                e.EvaluateParameter += (name, args) =>
                {
                    if (name == "h")
                        args.Result = 100;
                };
                e.EvaluateFunction += (name, args) =>
                {
                    if (name == "Ext")
                        args.Result = Convert.ToInt32(args.Parameters[0].Evaluate(), CultureInfo.InvariantCulture) * 1000;
                };
            },
            e =>
            {
                e.EvaluateParameterAsync += (name, args, _) =>
                {
                    if (name == "h")
                        args.Result = 100;
                    return default;
                };
                e.EvaluateFunctionAsync += async (name, args, token) =>
                {
                    if (name == "Ext")
                        args.Result = Convert.ToInt32(await args.Parameters[0].EvaluateAsync(token), CultureInfo.InvariantCulture) * 1000;
                };
            });
        AssertResult(2102, syncResult);
        AssertResult(2102, asyncResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FunctionBodyShouldReadCallerContextIgnoringCase(bool nonRecursive)
    {
        var options = Options(nonRecursive) | ExpressionOptions.LowerCaseIdentifierLookup;
        var (syncResult, asyncResult) = await EvaluateBothAsync("fn F(X) => G(x * 2); fn g(y) => Y + A + X; f(1)", options,
            e => e.Parameters["a"] = 10, e => e.Parameters["a"] = 10);
        AssertResult(13, syncResult);
        AssertResult(13, asyncResult);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FunctionBodyShouldReadIteratedParameters(bool nonRecursive)
    {
        // The body reads the current elements of the iterated parameters and does not iterate them again
        var options = Options(nonRecursive) | ExpressionOptions.IterateParameters;
        var (syncResult, asyncResult) = await EvaluateBothAsync("fn f(x) => x * k + a; f(a)", options,
            e =>
            {
                e.Parameters["a"] = new List<object?> { 1, 2, 3 };
                e.Parameters["k"] = 10;
            },
            e =>
            {
                e.Parameters["a"] = new List<object?> { 1, 2, 3 };
                e.Parameters["k"] = 10;
            });
        Assert.Equal([11, 22, 33], ((IEnumerable<object?>)syncResult!).Select(v => Convert.ToInt32(v, CultureInfo.InvariantCulture)));
        Assert.Equal([11, 22, 33], ((IEnumerable<object?>)asyncResult!).Select(v => Convert.ToInt32(v, CultureInfo.InvariantCulture)));
    }

    public static TheoryData<string, bool> EndlessRecursionCases
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var nonRecursive in new[] { false, true })
            {
                data.Add("fn f(n) => f(n + 1); f(0)", nonRecursive);
                data.Add("fn f(n) => f(n + 1) + 1; f(0)", nonRecursive);
                data.Add("fn f(n) => g(n) ; fn g(n) => f(n + 1); f(0)", nonRecursive);
                data.Add("fn f(n) => iff(n < 0, 0, f(n + 1)); f(0)", nonRecursive);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EndlessRecursionCases))]
    public async Task EndlessRecursionShouldThrow(string expression, bool nonRecursive)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var options = Options(nonRecursive);
        Assert.ThrowsAny<NCalcEvaluationException>(() => new Expression(expression, options, CultureInfo.InvariantCulture).Evaluate(cancellationToken));
        await Assert.ThrowsAnyAsync<NCalcEvaluationException>(async () => await new AsyncExpression(expression, options, CultureInfo.InvariantCulture).EvaluateAsync(cancellationToken));
    }

    [Fact]
    public async Task EndlessRecursionShouldHitDepthLimitInNonRecursiveEvaluator()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        const string expression = "fn f(n) => f(n + 1); f(0)";
        var options = Options(true);
        var syncException = Assert.Throws<NCalcEvaluationException>(() => new Expression(expression, options, CultureInfo.InvariantCulture).Evaluate(cancellationToken));
        var asyncException = await Assert.ThrowsAsync<NCalcEvaluationException>(async () => await new AsyncExpression(expression, options, CultureInfo.InvariantCulture).EvaluateAsync(cancellationToken));
        Assert.Contains("MaxUserFunctionCallDepth", syncException.Message, StringComparison.Ordinal);
        Assert.Equal(syncException.Message, asyncException.Message);
    }

    [Theory]
    [InlineData(false, 20)]
    [InlineData(true, 20)]
    [InlineData(true, 900)]
    public async Task DeepRecursionWithinLimitShouldWork(bool nonRecursive, int depth)
    {
        var (syncResult, asyncResult) = await EvaluateBothAsync($"fn s(n) => iff(n = 0, 0, n + s(n - 1)); s({depth})", Options(nonRecursive));
        AssertResult(depth * (depth + 1) / 2, syncResult);
        AssertResult(depth * (depth + 1) / 2, asyncResult);
    }

    #endregion

    #region HasErrors with a cancelled token throws OperationCanceledException

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HasErrorsShouldThrowWhenCancelled(bool nonRecursive)
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var options = Options(nonRecursive);

        var syncExpression = new Expression("1 + 2 * (3 - x)", options, CultureInfo.InvariantCulture);
        Assert.ThrowsAny<OperationCanceledException>(() => syncExpression.HasErrors(source.Token));
        Assert.Null(syncExpression.Error);
        Assert.False(syncExpression.HasErrors(TestContext.Current.CancellationToken));

        var asyncExpression = new AsyncExpression("1 + 2 * (3 - x)", options, CultureInfo.InvariantCulture);
        Assert.ThrowsAny<OperationCanceledException>(() => asyncExpression.HasErrors(source.Token));
        Assert.Null(asyncExpression.Error);
        Assert.False(asyncExpression.HasErrors(TestContext.Current.CancellationToken));

        // Cancelled while the text is parsed (the parse of the long text takes far longer than the delay)
        var longText = string.Join(" + ", Enumerable.Range(0, 50000).Select(i => $"(x{i} * {i})"));
        using var laterSource = new CancellationTokenSource();
        laterSource.CancelAfter(10);
        var longExpression = new Expression(longText, options, CultureInfo.InvariantCulture);
        Assert.ThrowsAny<OperationCanceledException>(() => longExpression.HasErrors(laterSource.Token));
        Assert.Null(longExpression.Error);

        using var asyncLaterSource = new CancellationTokenSource();
        asyncLaterSource.CancelAfter(10);
        var longAsyncExpression = new AsyncExpression(longText, options, CultureInfo.InvariantCulture);
        Assert.ThrowsAny<OperationCanceledException>(() => longAsyncExpression.HasErrors(asyncLaterSource.Token));
        Assert.Null(longAsyncExpression.Error);

        using var evaluateLaterSource = new CancellationTokenSource();
        evaluateLaterSource.CancelAfter(10);
        Assert.ThrowsAny<OperationCanceledException>(() => new Expression(longText, options, CultureInfo.InvariantCulture).Evaluate(evaluateLaterSource.Token));

        // Evaluate throws the same exception
        Assert.ThrowsAny<OperationCanceledException>(() => new Expression("1 + 2", options, CultureInfo.InvariantCulture).Evaluate(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await new AsyncExpression("1 + 2", options, CultureInfo.InvariantCulture).EvaluateAsync(source.Token));
    }

    #endregion
}

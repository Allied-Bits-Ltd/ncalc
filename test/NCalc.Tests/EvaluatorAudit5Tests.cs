using Microsoft.Extensions.DependencyInjection;

using NCalc.DependencyInjection;
using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Factories;
using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Tests;

/// <summary>
/// Regression tests of the fifth audit (evaluators): every case runs with the recursive and the non-recursive evaluator,
/// each synchronously and asynchronously.
/// </summary>
[Trait("Category", "Evaluations")]
public class EvaluatorAudit5Tests
{
    private const ExpressionOptions St = ExpressionOptions.NoCache | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseAssignments
        | ExpressionOptions.UseLoops | ExpressionOptions.UseIfStatement;

    private const string OutsideLoopMessage = "The 'break' and 'continue' keywords may be used only inside the loop body";

    private static ExpressionOptions Options(bool nonRecursive) => St | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

    private static async Task<(object? Sync, object? Async)> EvaluateBothAsync(string expression, ExpressionOptions options)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var syncResult = new Expression(expression, options, CultureInfo.InvariantCulture).Evaluate(cancellationToken);
        var asyncResult = await new AsyncExpression(expression, options, CultureInfo.InvariantCulture).EvaluateAsync(cancellationToken);
        return (syncResult, asyncResult);
    }

    private static async Task<(TException Sync, TException Async)> ThrowsBothAsync<TException>(string expression, ExpressionOptions options)
        where TException : Exception
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var syncException = Assert.ThrowsAny<TException>(() => new Expression(expression, options, CultureInfo.InvariantCulture).Evaluate(cancellationToken));
        var asyncException = await Assert.ThrowsAnyAsync<TException>(async () => await new AsyncExpression(expression, options, CultureInfo.InvariantCulture).EvaluateAsync(cancellationToken));
        return (syncException, asyncException);
    }

    #region NCALC-408: a custom evaluation visitor evaluates function arguments, if() branches and fn bodies and arguments

    // The visitor of docs/articles/overriding_evaluation.md, with the overload of the non-recursive evaluator overridden too
    private sealed class AnswerVisitor(ExpressionContext context) : EvaluationVisitor(context)
    {
        public override object? Visit(ValueExpression expression, CancellationToken cancellationToken = default)
            => expression.Value is 42 ? "The answer" : base.Visit(expression, cancellationToken);

        public override object? Visit(ValueExpression expression, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
            => expression.Value is 42 ? SetTaskValue(task, "The answer") : base.Visit(expression, task, cancellationToken);
    }

    private sealed class AnswerVisitorFactory : IEvaluationVisitorFactory
    {
        public EvaluationVisitor Create(ExpressionContext context) => new AnswerVisitor(context);
    }

    private sealed class AsyncAnswerVisitor(AsyncExpressionContext context) : AsyncEvaluationVisitor(context)
    {
        public override ValueTask<object?> Visit(ValueExpression expression, CancellationToken cancellationToken = default)
            => expression.Value is 42 ? new ValueTask<object?>("The answer") : base.Visit(expression, cancellationToken);

        public override ValueTask<object?> Visit(ValueExpression expression, ExpressionTask<ValueTask<object?>> task, CancellationToken cancellationToken = default)
            => expression.Value is 42 ? new ValueTask<object?>(SetTaskValue(task, "The answer")) : base.Visit(expression, task, cancellationToken);
    }

    private sealed class AsyncAnswerVisitorFactory : IAsyncEvaluationVisitorFactory
    {
        public AsyncEvaluationVisitor Create(AsyncExpressionContext context) => new AsyncAnswerVisitor(context);
    }

    public static TheoryData<string, bool> CustomVisitorCases
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var nonRecursive in new[] { false, true })
            {
                data.Add("42", nonRecursive);
                data.Add("if(true, 42, 0)", nonRecursive);
                data.Add("ifs(false, 1, 42)", nonRecursive);
                data.Add("Twice(42)", nonRecursive);
                data.Add("fn g() => 42; g()", nonRecursive);
                data.Add("fn h(a) => a; h(42)", nonRecursive);
                data.Add("fn h(a) => a; h(if(true, 42, 0))", nonRecursive);
                data.Add("fn k() => if(true, 42, 0); k()", nonRecursive);
            }
            return data;
        }
    }

    // NCALC-408
    [Theory]
    [MemberData(nameof(CustomVisitorCases))]
    public async Task CustomVisitorFactoryShouldEvaluateSubExpressions(string expression, bool nonRecursive)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var services = new ServiceCollection();
        services.AddNCalc()
            .WithEvaluationVisitorFactory<AnswerVisitorFactory>()
            .WithAsyncEvaluationVisitorFactory<AsyncAnswerVisitorFactory>();
        var serviceProvider = services.BuildServiceProvider();
        var options = Options(nonRecursive);

        var syncExpression = serviceProvider.GetRequiredService<IExpressionFactory>().Create(expression, new ExpressionContext(options, CultureInfo.InvariantCulture));
        syncExpression.Functions["Twice"] = data => data[0].Evaluate();
        Assert.Equal("The answer", syncExpression.Evaluate(cancellationToken));

        var asyncExpression = serviceProvider.GetRequiredService<IAsyncExpressionFactory>().Create(expression, new AsyncExpressionContext(options, CultureInfo.InvariantCulture));
        asyncExpression.Functions["Twice"] = async (data, token) => await data[0].EvaluateAsync(token);
        Assert.Equal("The answer", await asyncExpression.EvaluateAsync(cancellationToken));
    }

    #endregion

    #region NCALC-426: break and continue do not leave a function body towards the caller's loop

    // NCALC-426
    [Theory]
    [InlineData("fn f() { break }; i := 0; while (i < 5) { i += 1; f() }; i", false)]
    [InlineData("fn f() { break }; i := 0; while (i < 5) { i += 1; f() }; i", true)]
    [InlineData("fn f() { continue }; i := 0; while (i < 3) { i += 1; f(); i := 100 }; i", false)]
    [InlineData("fn f() { continue }; i := 0; while (i < 3) { i += 1; f(); i := 100 }; i", true)]
    [InlineData("fn f() { i := 0; while (i < 3) { i += 1 }; break }; j := 0; while (j < 3) { j += 1; f() }; j", false)]
    [InlineData("fn f() { i := 0; while (i < 3) { i += 1 }; break }; j := 0; while (j < 3) { j += 1; f() }; j", true)]
    [InlineData("fn f() { fn g() { break }; i := 0; while (i < 3) { i += 1; g() }; i }; f()", false)]
    [InlineData("fn f() { fn g() { break }; i := 0; while (i < 3) { i += 1; g() }; i }; f()", true)]
    [InlineData("fn f() { break }; f()", false)]
    [InlineData("fn f() { break }; f()", true)]
    public async Task BreakAndContinueShouldNotLeaveFunctionBody(string expression, bool nonRecursive)
    {
        var (syncException, asyncException) = await ThrowsBothAsync<NCalcEvaluationException>(expression, Options(nonRecursive));
        Assert.Equal(OutsideLoopMessage, syncException.Message);
        Assert.Equal(OutsideLoopMessage, asyncException.Message);
    }

    // NCALC-426: the same error as "break" and "continue" outside any loop at the top level
    [Theory]
    [InlineData("i := 0; break; i", false)]
    [InlineData("i := 0; break; i", true)]
    [InlineData("continue", false)]
    [InlineData("continue", true)]
    public async Task BreakAndContinueOutsideLoopShouldFail(string expression, bool nonRecursive)
    {
        var (syncException, asyncException) = await ThrowsBothAsync<NCalcEvaluationException>(expression, Options(nonRecursive));
        Assert.Equal(OutsideLoopMessage, syncException.Message);
        Assert.Equal(OutsideLoopMessage, asyncException.Message);
    }

    // NCALC-426: break and continue in a loop of the body, and in an argument of the call, keep working
    [Theory]
    [InlineData("fn f() { i := 0; while (i < 10) { i += 1; break }; i }; f()", false, 1)]
    [InlineData("fn f() { i := 0; while (i < 10) { i += 1; break }; i }; f()", true, 1)]
    [InlineData("fn f() { i := 0; n := 0; while (i < 4) { i += 1; continue; n := 100 }; i + n }; f()", false, 4)]
    [InlineData("fn f() { i := 0; n := 0; while (i < 4) { i += 1; continue; n := 100 }; i + n }; f()", true, 4)]
    [InlineData("fn f(a) => a; i := 0; while (i < 5) { i += 1; f(break) }; i", false, 1)]
    [InlineData("fn f(a) => a; i := 0; while (i < 5) { i += 1; f(break) }; i", true, 1)]
    [InlineData("fn f() { i := 0; while (i < 5) { i += 1; break }; return i * 10 }; f()", false, 10)]
    [InlineData("fn f() { i := 0; while (i < 5) { i += 1; break }; return i * 10 }; f()", true, 10)]
    public async Task BreakAndContinueInsideFunctionLoopShouldWork(string expression, bool nonRecursive, int expected)
    {
        var (syncResult, asyncResult) = await EvaluateBothAsync(expression, Options(nonRecursive));
        Assert.Equal(expected, Convert.ToInt32(syncResult, CultureInfo.InvariantCulture));
        Assert.Equal(expected, Convert.ToInt32(asyncResult, CultureInfo.InvariantCulture));
    }

    #endregion

    #region NCALC-427: indexed assignment through a from-end index

    // NCALC-427
    [Theory]
    [InlineData("l := (1, 2, 3); l[^1] := 7; l[2]", false, "7")]
    [InlineData("l := (1, 2, 3); l[^1] := 7; l[2]", true, "7")]
    [InlineData("l := (1, 2, 3); l[^3] := 9; l[0]", false, "9")]
    [InlineData("l := (1, 2, 3); l[^3] := 9; l[0]", true, "9")]
    [InlineData("l := (1, 2, 3); l[^1] += 1; l[^1]", false, "4")]
    [InlineData("l := (1, 2, 3); l[^1] += 1; l[^1]", true, "4")]
    [InlineData("l := (1, 2, 3); l[^2] *= 5; l[1]", false, "10")]
    [InlineData("l := (1, 2, 3); l[^2] *= 5; l[1]", true, "10")]
    [InlineData("s := 'abc'; s[^1] := 'z'; s", false, "abz")]
    [InlineData("s := 'abc'; s[^1] := 'z'; s", true, "abz")]
    [InlineData("l := (1, 2, 3); fn f(a) { a[^1] := 7; a[2] }; f(l) * 10 + l[2]", false, "73")]
    [InlineData("l := (1, 2, 3); fn f(a) { a[^1] := 7; a[2] }; f(l) * 10 + l[2]", true, "73")]
    public async Task FromEndIndexedAssignmentShouldWork(string expression, bool nonRecursive, string expected)
    {
        var (syncResult, asyncResult) = await EvaluateBothAsync(expression, Options(nonRecursive));
        Assert.Equal(expected, Convert.ToString(syncResult, CultureInfo.InvariantCulture));
        Assert.Equal(expected, Convert.ToString(asyncResult, CultureInfo.InvariantCulture));
    }

    // NCALC-427: a from-end index outside the list or string is an index error, as in reading
    [Theory]
    [InlineData("l := (1, 2, 3); l[^4] := 7", false)]
    [InlineData("l := (1, 2, 3); l[^4] := 7", true)]
    [InlineData("l := (1, 2, 3); l[^0] := 7", false)]
    [InlineData("l := (1, 2, 3); l[^0] := 7", true)]
    [InlineData("s := 'abc'; s[^4] := 'z'", false)]
    [InlineData("s := 'abc'; s[^4] := 'z'", true)]
    [InlineData("n := 5; n[^1] := 7", false)]
    [InlineData("n := 5; n[^1] := 7", true)]
    public async Task FromEndIndexedAssignmentOutOfRangeShouldFail(string expression, bool nonRecursive)
    {
        var (syncException, asyncException) = await ThrowsBothAsync<NCalcParameterIndexException>(expression, Options(nonRecursive));
        Assert.Equal(syncException.Message, asyncException.Message);
    }

    // The EvaluateParameter calls and the UpdateParameter calls (with their indices) of an evaluation, and its result
    private static async Task<(string Sync, string Async)> TraceHandlersAsync(string expression, ExpressionOptions options)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var syncLog = new List<string>();
        var syncExpression = new Expression(expression, options, CultureInfo.InvariantCulture);
        syncExpression.Parameters["l"] = new List<object?> { 1, 2, 3 };
        syncExpression.EvaluateParameter += (name, _) => syncLog.Add("eval " + name);
        syncExpression.UpdateParameter += (name, args) => syncLog.Add($"update {name} {args.Index}");
        var syncResult = syncExpression.Evaluate(cancellationToken);
        syncLog.Add("result " + string.Join(",", (IEnumerable<object?>)syncExpression.Parameters["l"]!) + " " + Convert.ToString(syncResult, CultureInfo.InvariantCulture));

        var asyncLog = new List<string>();
        var asyncExpression = new AsyncExpression(expression, options, CultureInfo.InvariantCulture);
        asyncExpression.Parameters["l"] = new List<object?> { 1, 2, 3 };
        asyncExpression.EvaluateParameterAsync += (name, _, _) =>
        {
            asyncLog.Add("eval " + name);
            return default;
        };
        asyncExpression.UpdateParameterAsync += (name, args, _) =>
        {
            asyncLog.Add($"update {name} {args.Index}");
            return default;
        };
        var asyncResult = await asyncExpression.EvaluateAsync(cancellationToken);
        asyncLog.Add("result " + string.Join(",", (IEnumerable<object?>)asyncExpression.Parameters["l"]!) + " " + Convert.ToString(asyncResult, CultureInfo.InvariantCulture));

        return (string.Join("; ", syncLog), string.Join("; ", asyncLog));
    }

    // NCALC-427: an assignment with a from-end index evaluates its target as often as one with the same index counted from the start
    // (the handlers may have side effects)
    [Theory]
    [InlineData("l[^1] := 7", "l[2] := 7", false)]
    [InlineData("l[^1] := 7", "l[2] := 7", true)]
    [InlineData("l[^3] += 5", "l[0] += 5", false)]
    [InlineData("l[^3] += 5", "l[0] += 5", true)]
    [InlineData("fn f(a) { a[^1] := 7; a[2] }; f(l)", "fn f(a) { a[2] := 7; a[2] }; f(l)", false)]
    [InlineData("fn f(a) { a[^1] := 7; a[2] }; f(l)", "fn f(a) { a[2] := 7; a[2] }; f(l)", true)]
    [InlineData("fn f(a) { a[^2] -= 1; a[1] }; f(l)", "fn f(a) { a[1] -= 1; a[1] }; f(l)", false)]
    [InlineData("fn f(a) { a[^2] -= 1; a[1] }; f(l)", "fn f(a) { a[1] -= 1; a[1] }; f(l)", true)]
    public async Task FromEndIndexedAssignmentShouldEvaluateTargetAsFromStart(string fromEnd, string fromStart, bool nonRecursive)
    {
        var options = Options(nonRecursive);
        var (fromEndSync, fromEndAsync) = await TraceHandlersAsync(fromEnd, options);
        var (fromStartSync, fromStartAsync) = await TraceHandlersAsync(fromStart, options);

        Assert.Equal(fromStartSync, fromEndSync);
        Assert.Equal(fromStartAsync, fromEndAsync);
        Assert.Equal(fromStartSync, fromStartAsync);
    }

    #endregion

    #region Stack after deeply nested asynchronous evaluations

    // The caller of an asynchronous evaluation whose deeply nested calls continued on thread pool threads resumed with the stack of
    // the thread nearly used up, so its next evaluation failed as nested too deeply (found by the old-vs-new differential of audit 5)
    [Theory]
    [InlineData("Abs(", ")")]
    [InlineData("if(true, ", ", 0)")]
    public async Task CallerShouldContinueOnAFreshStackAfterDeeplyNestedAsyncEvaluation(string open, string close)
    {
        const int depth = 1000;
        var text = string.Concat(Enumerable.Repeat(open, depth)) + "1" + string.Concat(Enumerable.Repeat(close, depth));
        var token = TestContext.Current.CancellationToken;
        var expression = new AsyncExpression(text, ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator);
        Assert.False(expression.HasErrors(token));

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(1, Convert.ToInt32(await expression.EvaluateAsync(token), System.Globalization.CultureInfo.InvariantCulture));
            Assert.True(System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack());
            Assert.Equal(11, new Expression("2 * 3 + 5", ExpressionOptions.NoCache).Evaluate(token));
        }
    }

    #endregion
}

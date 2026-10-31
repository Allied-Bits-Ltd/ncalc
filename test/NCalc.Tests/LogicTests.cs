using System;
using System.Collections.Generic;
using System.Text;

using NCalc.Domain;

namespace NCalc.Tests
{
    [Trait("Category", "Logic")]
    public class LogicTests : TestBase
    {
        [Theory]
        [InlineData("not true", false)]
        [InlineData("not null", null)]
        [InlineData("not false", true)]
        public void ShouldEvaluateNot(string expr, bool? expected)
        {
            object? result = new Expression(expr, ExpressionOptions.UseTernaryLogic).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = new Expression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("true and true", true)]
        [InlineData("true and null", null)]
        [InlineData("true and false", false)]
        [InlineData("null and true", null)]
        [InlineData("null and null", null)]
        [InlineData("null and false", false)]
        [InlineData("false and true", false)]
        [InlineData("false and null", false)]
        [InlineData("false and false", false)]
        public void ShouldEvaluateAnd(string expr, bool? expected)
        {
            object? result = new Expression(expr, ExpressionOptions.UseTernaryLogic).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = new Expression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?) result);
        }

        [Theory]
        [InlineData("true or false", true)]
        [InlineData("true or true", true)]
        [InlineData("true or null", true)]
        [InlineData("null or false", null)]
        [InlineData("null or true", true)]
        [InlineData("null or null", null)]
        [InlineData("false or true", true)]
        [InlineData("false or null", null)]
        [InlineData("false or false", false)]
        public void ShouldEvaluateOr(string expr, bool? expected)
        {
            object? result = new Expression(expr, ExpressionOptions.UseTernaryLogic).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = new Expression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("true xor true", false)]
        [InlineData("true xor null", null)]
        [InlineData("true xor false", true)]
        [InlineData("null xor true", null)]
        [InlineData("null xor null", null)]
        [InlineData("null xor false", null)]
        [InlineData("false xor true", true)]
        [InlineData("false xor null", null)]
        [InlineData("false xor false", false)]
        public void ShouldEvaluateXor(string expr, bool? expected)
        {
            object? result = new Expression(expr, ExpressionOptions.UseTernaryLogic).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = new Expression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("not true", false)]
        [InlineData("not null", null)]
        [InlineData("not false", true)]
        public async Task ShouldEvaluateNotAsync(string expr, bool? expected)
        {
            object? result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("true and true", true)]
        [InlineData("true and null", null)]
        [InlineData("true and false", false)]
        [InlineData("null and true", null)]
        [InlineData("null and null", null)]
        [InlineData("null and false", false)]
        [InlineData("false and true", false)]
        [InlineData("false and null", false)]
        [InlineData("false and false", false)]
        public async Task ShouldEvaluateAndAsync(string expr, bool? expected)
        {
            object? result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?) result);
        }

        [Theory]
        [InlineData("true or false", true)]
        [InlineData("true or true", true)]
        [InlineData("true or null", true)]
        [InlineData("null or false", null)]
        [InlineData("null or true", true)]
        [InlineData("null or null", null)]
        [InlineData("false or true", true)]
        [InlineData("false or null", null)]
        [InlineData("false or false", false)]
        public async Task ShouldEvaluateOrAsync(string expr, bool? expected)
        {
            object? result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("true xor true", false)]
        [InlineData("true xor null", null)]
        [InlineData("true xor false", true)]
        [InlineData("null xor true", null)]
        [InlineData("null xor null", null)]
        [InlineData("null xor false", null)]
        [InlineData("false xor true", true)]
        [InlineData("false xor null", null)]
        [InlineData("false xor false", false)]
        public async Task ShouldEvaluateXorAsync(string expr, bool? expected)
        {
            object? result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("n ? 1 : 2", ExpressionOptions.None, null)]
        [InlineData("(n ? 1 : 2)", ExpressionOptions.None, null)]
        [InlineData("n ? 1 : 2", ExpressionOptions.TreatNullAsZero, 2)]
        public async Task TernaryWithNullConditionShouldNotHang(string expr, ExpressionOptions extraOptions, object? expected)
        {
            // Before the fix, the non-recursive evaluator visited the ternary again forever; the timeout turns a hang into a failure
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var options = ExpressionOptions.UseNonRecursiveEvaluator | ExpressionOptions.AllowNullParameter | extraOptions;

            var expression = new Expression(expr, options);
            expression.Parameters["n"] = null;
            Assert.Equal(expected, expression.Evaluate(timeout.Token));

            var asyncExpression = new AsyncExpression(expr, options);
            asyncExpression.Parameters["n"] = null;
            Assert.Equal(expected, await asyncExpression.EvaluateAsync(timeout.Token));
        }

        private static async Task<object?[]> EvaluateInAllEvaluators(Func<ExpressionOptions, Expression> createSync, Func<ExpressionOptions, AsyncExpression> createAsync, ExpressionOptions options)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var results = new List<object?>();
            foreach (var nonRecursive in new[] { false, true })
            {
                var o = options | ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
                results.Add(await Task.Run(() => createSync(o).Evaluate(timeout.Token), timeout.Token));
                results.Add(await createAsync(o).EvaluateAsync(timeout.Token));
            }
            return results.ToArray();
        }

        [Fact]
        public async Task NonRecursiveEvaluatorShouldHandleSharedNodes()
        {
            // A list or a sequence that contains the same node twice never completed in the non-recursive evaluators
            var node = new ValueExpression(1);
            var list = new LogicalExpressionList([node, node]);
            var results = await EvaluateInAllEvaluators(o => new Expression(list, o), o => new AsyncExpression(list, o), ExpressionOptions.None);
            Assert.All(results, r => Assert.Equal(new object?[] { 1, 1 }, ((IEnumerable<object?>)r!).ToArray()));

            var sequence = new StatementSequence([node, node]);
            results = await EvaluateInAllEvaluators(o => new Expression(sequence, o), o => new AsyncExpression(sequence, o), ExpressionOptions.UseStatementSequences);
            Assert.All(results, r => Assert.Equal(1, r));
        }

        [Fact]
        public async Task NonRecursiveEvaluatorShouldBeLinearOnLongChains()
        {
            // 100,000 terms took more than 100 s because the finished task was looked up from the bottom of the stack
            string expression = string.Join(" + ", Enumerable.Repeat("1", 100_000));
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await Task.Run(() => new Expression(expression, ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
            Assert.Equal(100_000, result);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Evaluation took {stopwatch.Elapsed}");
        }

        [Theory]
        [InlineData("not 1", ExpressionOptions.UseTernaryLogic, null)]
        [InlineData("not 'x'", ExpressionOptions.UseTernaryLogic, null)]
        [InlineData("n div (('abc' ? 'abc' : x) & n)", ExpressionOptions.AllowNullParameter, null)]
        [InlineData("1; n", ExpressionOptions.UseStatementSequences | ExpressionOptions.TreatNullAsZero | ExpressionOptions.AllowNullParameter, 0)]
        [InlineData("n != (not (4 >= (4 != 4)))", ExpressionOptions.None, true)]
        [InlineData("9 ** (not ((-(n)) in (1, 2, (n) != (4))))", ExpressionOptions.AllowNullParameter | ExpressionOptions.TreatNullAsZero, 9.0)]
        public async Task AllEvaluatorsShouldGiveTheSameResult(string expression, ExpressionOptions options, object? expected)
        {
            var results = await EvaluateInAllEvaluators(
                o => { var e = new Expression(expression, o); e.Parameters["n"] = null; return e; },
                o => { var e = new AsyncExpression(expression, o); e.Parameters["n"] = null; return e; },
                options);
            Assert.All(results, r => Assert.Equal(expected, r));
        }

        [Fact]
        public async Task NullLeftOperandShouldNotEvaluateTheRightOperand()
        {
            int calls = 0;
            var results = await EvaluateInAllEvaluators(
                o => { var e = new Expression("n + f()", o); e.Parameters["n"] = null; e.EvaluateFunction += (_, args) => { calls++; args.Result = 1; }; return e; },
                o => { var e = new AsyncExpression("n + f()", o); e.Parameters["n"] = null; e.EvaluateFunctionAsync += (_, args, _) => { calls++; args.Result = 1; return ValueTask.CompletedTask; }; return e; },
                ExpressionOptions.AllowNullParameter);
            Assert.All(results, Assert.Null);
            Assert.Equal(0, calls);
        }

        [Fact]
        public async Task SliceShouldEvaluateExpressionElements()
        {
            var list = new List<object?> { new BinaryExpression(BinaryExpressionType.Plus, new ValueExpression(1), new ValueExpression(2)), 9 };
            var results = await EvaluateInAllEvaluators(
                o => { var e = new Expression("l[0..2]", o); e.Parameters["l"] = list; return e; },
                o => { var e = new AsyncExpression("l[0..2]", o); e.Parameters["l"] = list; return e; },
                ExpressionOptions.None);
            Assert.All(results, r => Assert.Equal(new object?[] { 3, 9 }, ((IEnumerable<object?>)r!).ToArray()));
        }
    }
}

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

        // NCALC-113
        [Theory]
        [InlineData("null xor f()", ExpressionOptions.UseTernaryLogic)]
        [InlineData("n xor f()", ExpressionOptions.UseTernaryLogic)]
        [InlineData("5 xor f()", ExpressionOptions.UseTernaryLogic)]
        [InlineData("n xor f()", ExpressionOptions.AllowNullParameter)]
        public async Task XorShouldNotEvaluateTheRightOperandAfterANullLeftOperand(string expression, ExpressionOptions options)
        {
            int calls = 0;
            var results = await EvaluateInAllEvaluators(
                o => { var e = new Expression(expression, o); e.Parameters["n"] = null; e.EvaluateFunction += (_, args) => { calls++; args.Result = true; }; return e; },
                o => { var e = new AsyncExpression(expression, o); e.Parameters["n"] = null; e.EvaluateFunctionAsync += (_, args, _) => { calls++; args.Result = true; return ValueTask.CompletedTask; }; return e; },
                options);
            Assert.All(results, Assert.Null);
            Assert.Equal(0, calls);
        }

        // NCALC-114
        [Theory]
        [InlineData("'abc' like 'zzz'", true)]
        [InlineData("'abc' not like 'zzz'", false)]
        public async Task LikeShouldUseTheMatchStringHandlersInAllEvaluators(string expression, bool expected)
        {
            int calls = 0;
            var results = await EvaluateInAllEvaluators(
                o => { var e = new Expression(expression, o); e.MatchString += args => { calls++; args.Matches = true; }; return e; },
                o => { var e = new AsyncExpression(expression, o); e.MatchStringAsync += (args, _) => { calls++; args.Matches = true; return ValueTask.CompletedTask; }; return e; },
                ExpressionOptions.None);
            Assert.All(results, r => Assert.Equal(expected, r));
            Assert.Equal(4, calls);
        }

        // NCALC-115
        [Theory]
        [InlineData("return null; 5", ExpressionOptions.AllowNullParameter, null)]
        [InlineData("return null; 5", ExpressionOptions.AllowNullParameter | ExpressionOptions.TreatNullAsZero, null)]
        [InlineData("return y; 5", ExpressionOptions.AllowNullParameter, null)]
        [InlineData("fn f() { return null; 7 }; f()", ExpressionOptions.AllowNullParameter | ExpressionOptions.UseAssignments | ExpressionOptions.UseCStyleAssignments, null)]
        [InlineData("return 3; 5", ExpressionOptions.None, 3)]
        public async Task ReturnShouldReturnNull(string expression, ExpressionOptions options, object? expected)
        {
            var results = await EvaluateInAllEvaluators(
                o => { var e = new Expression(expression, o); e.Parameters["y"] = null; return e; },
                o => { var e = new AsyncExpression(expression, o); e.Parameters["y"] = null; return e; },
                options | ExpressionOptions.UseStatementSequences);
            Assert.All(results, r => Assert.Equal(expected, r));
        }

        // NCALC-105
        [Theory]
        [InlineData("Abs({0})")]
        [InlineData("if(true, {0}, 0)")]
        [InlineData("fn f(a) => a; f({0})")]
        public async Task LongArgumentsShouldBeEvaluatedByTheNonRecursiveEvaluator(string template)
        {
            var sum = string.Join(" + ", Enumerable.Repeat("1", 30_000));
            var text = string.Format(System.Globalization.CultureInfo.InvariantCulture, template, sum);
            const ExpressionOptions options = ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator | ExpressionOptions.UseAssignments
                | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseCStyleAssignments;
            var token = TestContext.Current.CancellationToken;

            var result = await Task.Run(() => new Expression(text, options).Evaluate(token), token);
            Assert.Equal(30_000L, Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture));

            result = await new AsyncExpression(text, options).EvaluateAsync(token);
            Assert.Equal(30_000L, Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture));
        }

        // NCALC-116
        [Theory]
        [InlineData("1 + 2", ExpressionOptions.None)]
        [InlineData("(1, 2, 3)", ExpressionOptions.None)]
        [InlineData("i := 0; while (i < 20000) { i := i + 1 }; i", ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseLoops)]
        public async Task AllEvaluatorsShouldStopWhenTheTokenIsCancelled(string expression, ExpressionOptions options)
        {
            using var source = new CancellationTokenSource();
            source.Cancel();
            foreach (var nonRecursive in new[] { false, true })
            {
                var o = options | ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

                // Parsed first, so that only the evaluation sees the cancelled token
                var e = new Expression(expression, o);
                Assert.False(e.HasErrors(TestContext.Current.CancellationToken));
                Assert.ThrowsAny<OperationCanceledException>(() => e.Evaluate(source.Token));

                var asyncExpression = new AsyncExpression(expression, o);
                Assert.False(asyncExpression.HasErrors(TestContext.Current.CancellationToken));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await asyncExpression.EvaluateAsync(source.Token));
            }
        }

        // NCALC-116
        [Theory]
        [InlineData("Max(Foo() + g(1), 0)")]
        [InlineData("if(true, Foo() + g(1), 0)")]
        [InlineData("Max(Foo(), g(1))")]
        [InlineData("Abs(Max(Foo(), 0) + g(1))")]
        public async Task ArgumentsOfFunctionsShouldStopWhenTheTokenIsCancelled(string expression)
        {
            int calls = 0;
            foreach (var nonRecursive in new[] { false, true })
            {
                var o = ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

                // Foo cancels the token; g must not be called afterwards
                using (var source = new CancellationTokenSource())
                {
                    var e = new Expression(expression, o);
                    e.EvaluateFunction += (name, args) =>
                    {
                        if (name == "Foo")
                            source.Cancel();
                        else if (name == "g")
                            calls++;
                        else
                            return;
                        args.Result = 1;
                    };
                    Assert.ThrowsAny<OperationCanceledException>(() => e.Evaluate(source.Token));
                }

                using (var source = new CancellationTokenSource())
                {
                    var asyncExpression = new AsyncExpression(expression, o);
                    asyncExpression.EvaluateFunctionAsync += (name, args, _) =>
                    {
                        if (name == "Foo")
                            source.Cancel();
                        else if (name == "g")
                            calls++;
                        else
                            return ValueTask.CompletedTask;
                        args.Result = 1;
                        return ValueTask.CompletedTask;
                    };
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await asyncExpression.EvaluateAsync(source.Token));
                }
            }
            Assert.Equal(0, calls);
        }

        // NCALC-116
        [Fact]
        public async Task FunctionArgsShouldEvaluateWithTheTokenOfTheEvaluation()
        {
            int calls = 0;
            using var source = new CancellationTokenSource();
            var e = new Expression("Sum2(Foo(), g())", ExpressionOptions.NoCache);
            e.EvaluateFunction += (name, args) =>
            {
                if (name == "Sum2")
                {
                    // Evaluated without a token, the arguments still use the token of the evaluation
                    var values = args.EvaluateParameters();
                    args.Result = (int)values[0]! + (int)values[1]!;
                }
                else if (name == "Foo")
                {
                    source.Cancel();
                    args.Result = 1;
                }
                else
                {
                    calls++;
                    args.Result = 1;
                }
            };
            Assert.ThrowsAny<OperationCanceledException>(() => e.Evaluate(source.Token));

            using var asyncSource = new CancellationTokenSource();
            var asyncExpression = new AsyncExpression("Sum2(Foo(), g())", ExpressionOptions.NoCache);
            asyncExpression.EvaluateFunctionAsync += async (name, args, _) =>
            {
                if (name == "Sum2")
                {
                    var values = await args.EvaluateParametersAsync();
                    args.Result = (int)values[0]! + (int)values[1]!;
                }
                else if (name == "Foo")
                {
                    asyncSource.Cancel();
                    args.Result = 1;
                }
                else
                {
                    calls++;
                    args.Result = 1;
                }
            };
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await asyncExpression.EvaluateAsync(asyncSource.Token));
            Assert.Equal(0, calls);
        }

        // NCALC-223: cancel() cancels the token of the evaluation; the recursive evaluators went on with the other operand
        [Theory]
        [InlineData("cancel() + p1")]
        [InlineData("cancel() ? p1 : 0")]
        [InlineData("cancel() == 1")]
        [InlineData("-cancel()")]
        [InlineData("cancel() in (p1, p2)")]
        public async Task EvaluatorsShouldStopAfterAnOperandCancelsTheToken(string expression)
        {
            int parameterCalls = 0;
            foreach (var nonRecursive in new[] { false, true })
            {
                var o = ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

                using (var source = new CancellationTokenSource())
                {
                    var e = new Expression(expression, o);
                    e.EvaluateFunction += (_, args) => { source.Cancel(); args.Result = 1; };
                    e.EvaluateParameter += (_, args) => { parameterCalls++; args.Result = 10; };
                    Assert.ThrowsAny<OperationCanceledException>(() => e.Evaluate(source.Token));
                }

                using (var source = new CancellationTokenSource())
                {
                    var asyncExpression = new AsyncExpression(expression, o);
                    asyncExpression.EvaluateFunctionAsync += (_, args, _) => { source.Cancel(); args.Result = 1; return ValueTask.CompletedTask; };
                    asyncExpression.EvaluateParameterAsync += (_, args, _) => { parameterCalls++; args.Result = 10; return ValueTask.CompletedTask; };
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await asyncExpression.EvaluateAsync(source.Token));
                }
            }
            Assert.Equal(0, parameterCalls);
        }

        // NCALC-224: "return" in an argument returned from the argument only
        [Theory]
        [InlineData("Abs(return -3) + 100", -3L)]
        [InlineData("if(true, return 4, 5) + 100", 4L)]
        [InlineData("Max(return 1, 2) + 100", 1L)]
        [InlineData("fn g(a) => a * 2; g(return 5) + 100", 5L)]
        [InlineData("fn g(a) => Abs(a) * 2; g(return 5) + 100", 5L)]
        [InlineData("fn h(b) { Abs(return b + 1); 50 }; h(1) + 100", 102L)]
        [InlineData("a := 0; while (a < 3) { a += 1; Abs(return a) }; 100", 1L)]
        public async Task ReturnInAFunctionArgumentShouldReturnFromTheExpression(string expression, long expected)
        {
            var results = await EvaluateInAllEvaluators(
                o => new Expression(expression, o),
                o => new AsyncExpression(expression, o),
                ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseLoops);
            Assert.All(results, r => Assert.Equal(expected, Convert.ToInt64(r, System.Globalization.CultureInfo.InvariantCulture)));
        }

        // NCALC-209: an indexed assignment replaced the whole argument with the element value
        [Theory]
        [InlineData("fn g(a) { a[1] := 5; a }; g(lst)", "1,5,3")]
        [InlineData("fn g(a) { a[1] := 5; a[0] }; g(lst)", "1")]
        [InlineData("fn g(s) { s[0] := 'X'; s }; g('abc')", "Xbc")]
        [InlineData("fn g(a) { a[1] += 5; a }; g(lst)", "1,7,3")]
        public async Task IndexedAssignmentToAFunctionArgumentShouldChangeTheElement(string expression, string expected)
        {
            static string Format(object? result) => result is IEnumerable<object?> items
                ? string.Join(",", items.Select(item => Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture)))
                : Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture)!;

            var lists = new List<List<object?>>();
            List<object?> NewList()
            {
                var list = new List<object?> { 1, 2, 3 };
                lists.Add(list);
                return list;
            }

            var results = await EvaluateInAllEvaluators(
                o => { var e = new Expression(expression, o); e.Parameters["lst"] = NewList(); return e; },
                o => { var e = new AsyncExpression(expression, o); e.Parameters["lst"] = NewList(); return e; },
                ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences);
            Assert.All(results, r => Assert.Equal(expected, Format(r)));

            // Assignments to arguments are local to the function: the list of the caller is not changed
            Assert.All(lists, list => Assert.Equal([1, 2, 3], list));
        }

        // NCALC-240: the limit applied to each loop separately, so nested loops multiplied it
        [Theory]
        [InlineData("n := 0; i := 0; while (i < 1000) { i += 1; j := 0; while (j < 1000) { j += 1; n += 1 } }; n")]
        [InlineData("fn f() { j := 0; while (j < 1000) { j += 1 }; j }; n := 0; i := 0; while (i < 1000) { i += 1; n += f() }; n")]
        public async Task MaxLoopIterationsShouldLimitAllLoopsOfAnEvaluationTogether(string expression)
        {
            // Not EvaluateInAllEvaluators: 65536 iterations in each evaluator may take seconds on a loaded machine
            var token = TestContext.Current.CancellationToken;
            foreach (var nonRecursive in new[] { false, true })
            {
                var o = ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseLoops | ExpressionOptions.NoCache
                    | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
                var n = Convert.ToInt64(await Task.Run(() => new Expression(expression, o).Evaluate(token), token), System.Globalization.CultureInfo.InvariantCulture);
                Assert.InRange(n, 60_000, Expression.MaxLoopIterations);
                n = Convert.ToInt64(await new AsyncExpression(expression, o).EvaluateAsync(token), System.Globalization.CultureInfo.InvariantCulture);
                Assert.InRange(n, 60_000, AsyncExpression.MaxLoopIterations);
            }
        }

        // NCALC-304: the loop budget of an evaluation leaked through the execution context into the work started during it (a task, a timer),
        // so that later, unrelated evaluations there ran their loops 0 times; unrelated evaluations that a handler ran meanwhile shared it
        [Fact]
        public async Task LoopBudgetShouldNotLeakIntoWorkStartedDuringAnEvaluation()
        {
            var token = TestContext.Current.CancellationToken;
            const string child = "j := 0; while (j < 10) { j := j + 1 }; j";
            // Uses up the whole budget before it calls f()
            const string parent = "i := 0; while (true) { i := i + 1 }; f()";
            foreach (var nonRecursive in new[] { false, true })
            {
                var o = ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseLoops | ExpressionOptions.NoCache
                    | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
                foreach (var asyncParent in new[] { false, true })
                {
                    var parentDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var timerResult = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var later = new List<Task<object?>>();
                    var meanwhile = new List<object?>();
                    Timer? timer = null;

                    void StartWork()
                    {
                        later.Add(Task.Run(async () => { await parentDone.Task; return new Expression(child, o).Evaluate(token); }, token));
                        later.Add(Task.Run(async () => { await parentDone.Task; return await new AsyncExpression(child, o).EvaluateAsync(token); }, token));
                        timer = new Timer(_ =>
                        {
                            try
                            {
                                timerResult.TrySetResult(new Expression(child, o).Evaluate(token));
                            }
                            catch (Exception ex)
                            {
                                timerResult.TrySetException(ex);
                            }
                        }, null, Timeout.Infinite, Timeout.Infinite);
                        var parallel = Enumerable.Range(0, 4).Select(_ => Task.Run(() => new Expression(child, o).Evaluate(token), token)).ToArray();
                        Task.WaitAll(parallel, token);
                        meanwhile.AddRange(parallel.Select(t => t.Result));
                        meanwhile.Add(new Expression(child, o).Evaluate(token));
                    }

                    if (asyncParent)
                    {
                        var expression = new AsyncExpression(parent, o);
                        expression.EvaluateFunctionAsync += (_, args, _) => { StartWork(); args.Result = 1; return ValueTask.CompletedTask; };
                        await expression.EvaluateAsync(token);
                    }
                    else
                    {
                        var expression = new Expression(parent, o);
                        expression.EvaluateFunction += (_, args) => { StartWork(); args.Result = 1; };
                        expression.Evaluate(token);
                    }

                    parentDone.SetResult(true);
                    using (timer)
                    {
                        timer!.Change(0, Timeout.Infinite);
                        var results = (await Task.WhenAll(later)).Concat(meanwhile).Append(await timerResult.Task.WaitAsync(TimeSpan.FromSeconds(30), token));
                        Assert.All(results, r => Assert.Equal(10, Convert.ToInt32(r, System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }
            }
        }

        // NCALC-304: the parts of an evaluation that a function evaluates in parallel draw from the budget of the evaluation together
        [Fact]
        public async Task LoopBudgetShouldBeSharedSafelyByArgumentsEvaluatedInParallel()
        {
            var token = TestContext.Current.CancellationToken;
            const string text = "fn w() { j := 0; while (true) { j := j + 1 }; j }; f(w(), w(), w(), w())";
            foreach (var nonRecursive in new[] { false, true })
            {
                var o = ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseLoops | ExpressionOptions.NoCache
                    | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

                var expression = new Expression(text, o);
                expression.EvaluateFunction += (name, args) =>
                {
                    if (name == "f")
                        args.Result = Task.WhenAll(args.Parameters.Select(p => Task.Run(() => Convert.ToInt64(p.Evaluate(), System.Globalization.CultureInfo.InvariantCulture))))
                            .GetAwaiter().GetResult().Sum();
                };
                Assert.Equal(Expression.MaxLoopIterations, Convert.ToInt64(expression.Evaluate(token), System.Globalization.CultureInfo.InvariantCulture));

                var asyncExpression = new AsyncExpression(text, o);
                asyncExpression.EvaluateFunctionAsync += async (name, args, ct) =>
                {
                    if (name == "f")
                        args.Result = (await Task.WhenAll(args.Parameters.Select(p => Task.Run(async () => Convert.ToInt64(await p.EvaluateAsync(ct), System.Globalization.CultureInfo.InvariantCulture), ct)))).Sum();
                };
                Assert.Equal(AsyncExpression.MaxLoopIterations, Convert.ToInt64(await asyncExpression.EvaluateAsync(token), System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        // NCALC-133
        [Fact]
        public async Task InvertedRangeErrorShouldShowTheBoundaries()
        {
            foreach (var text in new[] { "lst[2..1]", "s[2..1]" })
            {
                foreach (var nonRecursive in new[] { false, true })
                {
                    var o = ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
                    var e = new Expression(text, o);
                    e.Parameters["lst"] = new List<object?> { 1, 2, 3 };
                    e.Parameters["s"] = "abc";
                    var exception = Assert.ThrowsAny<NCalc.Exceptions.NCalcException>(() => e.Evaluate(TestContext.Current.CancellationToken));
                    Assert.Contains("the actual value is 1", exception.Message);
                    Assert.DoesNotContain("{upperBound}", exception.Message);

                    var asyncExpression = new AsyncExpression(text, o);
                    asyncExpression.Parameters["lst"] = new List<object?> { 1, 2, 3 };
                    asyncExpression.Parameters["s"] = "abc";
                    var asyncException = await Assert.ThrowsAnyAsync<NCalc.Exceptions.NCalcException>(async () => await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
                    Assert.Contains("the actual value is 1", asyncException.Message);
                    Assert.DoesNotContain("{upperBound}", asyncException.Message);
                }
            }
        }
    }
}

using System.Globalization;
#nullable enable
using NCalc.Exceptions;
using NCalc.Tests.TestData;

namespace NCalc.Tests;

[Trait("Category", "Evaluations")]
public class EvaluationTests : TestBase
{
    [Theory]
    [ClassData(typeof(EvaluationTestData))]
    public void Expression_Should_Evaluate(string expression, object expected)
    {
        Assert.Equal(expected, new Expression(expression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [ClassData(typeof(ValuesTestData))]
    public void ShouldParseValues(string input, object expectedValue)
    {
        var expression = new Expression(input);

        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        if (expectedValue is double expectedDouble)
        {
            Assert.Equal(expectedDouble, (double)result!, precision: 15);
        }
        else
        {
            Assert.Equal(expectedValue, result);
        }
    }

    [Fact]
    public void ShouldEvaluateInFunction()
    {
        // The last argument should not be evaluated
        var ein = new Expression("in((2 + 2), [1], [2], 1 + 2, 4, 1 / 0)");
        ein.Parameters["1"] = 2;
        ein.Parameters["2"] = 5;

        Assert.Equal(true, ein.Evaluate(TestContext.Current.CancellationToken));

        var eout = new Expression("in((2 + 2), [1], [2], 1 + 2, 3)");
        eout.Parameters["1"] = 2;
        eout.Parameters["2"] = 5;

        Assert.Equal(false, eout.Evaluate(TestContext.Current.CancellationToken));

        // Should work with strings
        var estring = new Expression("in('to' + 'to', 'titi', 'toto')");

        Assert.Equal(true, estring.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateTernaryExpression()
    {
        Assert.Equal(1, new Expression("1+2<3 ? 3+4 : 1").Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Not_Throw_Function_Not_Found_Issue_110()
    {
        const string expressionStr = "IN([acp_associated_person_transactions], 'T', 'Z', 'A')";
        var expression = new Expression(expressionStr)
        {
            Options = ExpressionOptions.RoundAwayFromZero | ExpressionOptions.IgnoreCaseAtBuiltInFunctions,
            Parameters =
            {
                ["acp_associated_person_transactions"] = 'T'
            }
        };

        Assert.Equal(true, expression.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Evaluate_Ifs()
    {
        // Test first case true, return next value
        var eifs = new Expression("ifs([divider] != 0, [divided] / [divider], -1)");
        eifs.Parameters["divider"] = 5;
        eifs.Parameters["divided"] = 5;

        CheckResult(1d, eifs.Evaluate(TestContext.Current.CancellationToken));

        // Test first case false, no next case, return default value
        eifs = new Expression("ifs([divider] != 0, [divided] / [divider], -1)");
        eifs.Parameters["divider"] = 0;
        eifs.Parameters["divided"] = 5;

        CheckResult(-1, eifs.Evaluate(TestContext.Current.CancellationToken));

        // Test first case false, next case true, return next value (eg 4th expr)

        eifs = new Expression("ifs([number] == 3, 5, [number] == 5, 3, 8)");
        eifs.Parameters["number"] = 5;
        CheckResult(3, eifs.Evaluate(TestContext.Current.CancellationToken));

        // Test first case false, next case false, return default value (eg 5th expr)

        eifs = new Expression("ifs([number] == 3, 5, [number] == 5, 3, 8)");
        eifs.Parameters["number"] = 1337;

        CheckResult(8, eifs.Evaluate(TestContext.Current.CancellationToken));

        eifs = new Expression("ifs([number] == 3, 5, [number] == 5, 3)");
        eifs.Parameters["number"] = 1337;

        Assert.Null(eifs.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateConditional()
    {
        var eif = new Expression("if([divider] <> 0, [divided] / [divider], 0)");
        eif.Parameters["divider"] = 5;
        eif.Parameters["divided"] = 5;

        CheckResult(1d, eif.Evaluate(TestContext.Current.CancellationToken));

        eif = new Expression("if([divider] <> 0, [divided] / [divider], 0)");
        eif.Parameters["divider"] = 0;
        eif.Parameters["divided"] = 5;
        CheckResult(0, eif.Evaluate(TestContext.Current.CancellationToken));

        eif = new Expression("if([divider] <> 0, [divided] / [divider])");
        eif.Parameters["divider"] = 0;
        eif.Parameters["divided"] = 5;
        Assert.Null(eif.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldHandleCaseSensitiveness()
    {
        Assert.Equal(1.5M, new Expression("aBs(-1.5)", ExpressionOptions.DecimalAsDefault | ExpressionOptions.IgnoreCaseAtBuiltInFunctions).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(1.5M, new Expression("Abs(-1.5)", ExpressionOptions.DecimalAsDefault).Evaluate(TestContext.Current.CancellationToken));

        Assert.Throws<NCalcFunctionNotFoundException>(() => new Expression("aBs(-1)").Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldCompareDates()
    {
        var dateSeparator = CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator;
        Assert.Equal(true, new Expression($"#1{dateSeparator}1{dateSeparator}2009#==#1{dateSeparator}1{dateSeparator}2009#").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(false, new Expression($"#2{dateSeparator}1{dateSeparator}2009#==#1{dateSeparator}1{dateSeparator}2009#").Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldRoundAwayFromZero()
    {
        Assert.Equal(22d, new Expression("Round(22.5, 0)").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(23d, new Expression("Round(22.5, 0)", ExpressionOptions.RoundAwayFromZero).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldEvaluateSubExpressions()
    {
        var volume = new Expression("[surface] * h");
        var surface = new Expression("[l] * [L]");
        volume.Parameters["surface"] = surface;
        volume.Parameters["h"] = 3;
        surface.Parameters["l"] = 1;
        surface.Parameters["L"] = 2;

        Assert.Equal(6, volume.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Round(1.412; 2)", 1.41)]
    [InlineData("Max(5.1; 10.2)", 10.2)]
    [InlineData("Min(1.3; 2)", 1.3)]
    [InlineData("Pow(5;2)", 25d)]
    public void ShouldAllowSemicolonAsArgumentSeparator(string expression, object expected)
    {
        Assert.Equal(expected, new Expression(expression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldHandleCurlyBraces()
    {
        var volume = new Expression("{h}");
        volume.Parameters["h"] = 3;

        Assert.Equal(3, volume.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldAllowToUseCurlyBraces()
    {
        var volume = new Expression("{surface} * h");
        var surface = new Expression("{l} * {L}");
        volume.Parameters["surface"] = surface;
        volume.Parameters["h"] = 3;
        surface.Parameters["l"] = 1;
        surface.Parameters["L"] = 2;

        Assert.Equal(6, volume.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [ClassData(typeof(NullCheckTestData))]
    public void ShouldAllowOperatorsWithNulls(string expression, object? expected)
    {
        var e = new Expression(expression, ExpressionOptions.AllowNullParameter);
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldEvaluateArrayParameters()
    {
        var e = new Expression("x * x", ExpressionOptions.IterateParameters)
        {
            Parameters =
            {
                ["x"] = new [] { 0, 1, 2, 3, 4 }
            }
        };

        var result = (IList<object>?)e.Evaluate(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(0, result[0]);
        Assert.Equal(1, result[1]);
        Assert.Equal(4, result[2]);
        Assert.Equal(9, result[3]);
        Assert.Equal(16, result[4]);
    }

    [Fact]
    public void ShouldEvaluateArrayParametersWithFunctions()
    {
        var e = new Expression("Round(x, 2)", ExpressionOptions.IterateParameters)
        {
            Parameters =
            {
                ["x"] = new [] { 0.51, 1.671, 2.237, 3.568, 4.11 }
            }
        };

        var result = (IList<object>?)e.Evaluate(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(0.51, result[0]);
        Assert.Equal(1.67, result[1]);
        Assert.Equal(2.24, result[2]);
        Assert.Equal(3.57, result[3]);
        Assert.Equal(4.11, result[4]);
    }

    [Fact]
    public void AllowNullOrEmptyExpressions()
    {
        Assert.Equal("", new Expression("", ExpressionOptions.AllowNullOrEmptyExpressions).Evaluate(TestContext.Current.CancellationToken));
        Assert.Null(new Expression((string?)null, ExpressionOptions.AllowNullOrEmptyExpressions).Evaluate(TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("01 == ''")]
    [InlineData("' ' == 01")]
    [InlineData("\" \" == 01")]
    [InlineData("\"dog\" == 01")]
    public void ShouldUseStrictTypeMatching(string expression)
    {
        Assert.False(new Expression(expression, ExpressionOptions.StrictTypeMatching).Evaluate(TestContext.Current.CancellationToken) as bool?);
    }

    [Theory]
    [InlineData("MakeStr(2, l)")]
    [InlineData("MakeStr(2, 1.5)")]
    [InlineData("MakeStr(1500000000, 'ab')")]
    [InlineData("MakeList(3000000000)")]
    public void MakeStrAndMakeListShouldValidateTheirArguments(string expression)
    {
        var e = new Expression(expression, ExpressionOptions.NoCache);
        e.Parameters["l"] = new List<object?> { 1, 2 };
        Assert.Throws<NCalcEvaluationException>(() => e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void MakeStrShouldAcceptBigIntegerCharacterCodes()
    {
        var e = new Expression("MakeStr(3, x)", ExpressionOptions.NoCache);
        e.Parameters["x"] = new System.Numerics.BigInteger(65);
        Assert.Equal("AAA", e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Round(1.2345 + 2.5678i, 2)", AdvExpressionOptions.ParseComplexNumbers, "1.23 + 2.57i")]
    [InlineData("Round([1.2345; 2.5678], 2)", AdvExpressionOptions.ParseVectors, "[1.23, 2.57]")]
    [InlineData("-[1;2]", AdvExpressionOptions.ParseVectors, "[-1, -2]")]
    [InlineData("-(1+2i)", AdvExpressionOptions.ParseComplexNumbers, "-1 - 2i")]
    [InlineData("-10%", AdvExpressionOptions.CalculatePercent, "-10%")]
    [InlineData("Abs(-10%)", AdvExpressionOptions.CalculatePercent, "10%")]
    // Rounded, because the last digit of Sin and Cos differs between platforms (0.7071067811865475 with glibc on Linux)
    [InlineData("Round(Pow(i, 0.5), 15)", AdvExpressionOptions.ParseComplexNumbers, "0.707106781186548 + 0.707106781186548i")]
    public void ComplexVectorAndPercentOperationsShouldWork(string expression, AdvExpressionOptions advancedOptions, string expected)
    {
        var e = new Expression(expression, ExpressionOptions.NoCache) { AdvancedOptions = new AdvancedExpressionOptions(advancedOptions) };
        Assert.Equal(expected, Convert.ToString(e.Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void LogWithFractionalBaseShouldNotTruncateTheBase()
    {
        var e = new Expression("Log(x, 2.5)", ExpressionOptions.UseBigNumbers | ExpressionOptions.NoCache);
        e.Parameters["x"] = new ExtendedNumerics.BigDecimal(8);
        Assert.Equal(Math.Log(8, 2.5), Convert.ToDouble(e.Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture), 12);
    }

    [Theory]
    [InlineData("20*5%")]
    [InlineData("100+5%")]
    [InlineData("100-5%")]
    [InlineData("20/5%")]
    public void PercentResultTypeShouldNotDependOnDynamicCode(string expression)
    {
        // The non-dynamic path (used by NCalc.AOT) returned integers where the dynamic one returns a decimal or a double
        object? Evaluate(ExpressionOptions extra)
            => new Expression(expression, ExpressionOptions.DecimalAsDefault | ExpressionOptions.NoCache | extra) { AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.CalculatePercent) }.Evaluate(TestContext.Current.CancellationToken);
        var dynamicResult = Evaluate(ExpressionOptions.None);
        var staticResult = Evaluate(ExpressionOptions.AvoidDynamicFunctions);
        Assert.Equal(dynamicResult, staticResult);
        Assert.Equal(dynamicResult?.GetType(), staticResult?.GetType());
    }

    [Theory]
    [InlineData("not 1", false)]
    [InlineData("'a' in 'abc'", true)]
    [InlineData("Ln(1.0) == 0", true)]
    [InlineData("Log2(8) == 3", true)]
    [InlineData("Trunc(1.7) == 1", true)]
    [InlineData("2147483647 * 2 == 4294967294", true)]
    [InlineData("if(false, 1) == null", true)]
    public void LambdaShouldMatchEvaluate(string expression, bool expected)
    {
        Assert.Equal(expected, new Expression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(expected, new Expression(expression, ExpressionOptions.AllowNullParameter | ExpressionOptions.NoCache).ToLambda<bool>()());
    }

    [Fact]
    public void LambdaShouldHandleConditionsWithDifferentBranchTypes()
    {
        Assert.Equal(1.0, new Expression("true ? 1 : 2.5", ExpressionOptions.NoCache).ToLambda<double>()());
        Assert.Equal(2.35m, new Expression("Round(2.345, 2)", ExpressionOptions.DecimalAsDefault | ExpressionOptions.RoundAwayFromZero | ExpressionOptions.NoCache).ToLambda<decimal>()());
    }

    [Fact]
    public void CacheInsertsShouldNotScanTheWholeCache()
    {
        // NCALC-063: every insert used to scan all entries, so many distinct expressions took quadratic time
        var cache = new NCalc.Cache.LogicalExpressionCache(null);
        var alive = new List<NCalc.Domain.LogicalExpression>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 30_000; i++)
        {
            var expression = new NCalc.Domain.ValueExpression(i);
            alive.Add(expression);
            cache.Set("e" + i.ToString(CultureInfo.InvariantCulture), expression);
        }
        stopwatch.Stop();

        Assert.True(cache.TryGetValue("e29999", out var last));
        Assert.Same(alive[^1], last);
        // Milliseconds alone, a few seconds when the other tests load the machine; the quadratic version took about 2 minutes
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"30000 inserts took {stopwatch.Elapsed}");
    }

    // NCALC-131
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.NoCache)]
    public async Task ParseErrorShouldBeClearedWhenTheExpressionParsesLater(ExpressionOptions options)
    {
        var token = TestContext.Current.CancellationToken;

        var expression = new Expression("1 /* c */ + 2", options);
        Assert.ThrowsAny<NCalcException>(() => expression.Evaluate(token));
        expression.Options |= ExpressionOptions.SupportCStyleComments;
        Assert.Equal(3, expression.Evaluate(token));
        Assert.Null(expression.Error);

        var asyncExpression = new AsyncExpression("1 /* c */ + 2", options);
        await Assert.ThrowsAnyAsync<NCalcException>(async () => await asyncExpression.EvaluateAsync(token));
        asyncExpression.Options |= ExpressionOptions.SupportCStyleComments;
        Assert.Equal(3, await asyncExpression.EvaluateAsync(token));
        Assert.Null(asyncExpression.Error);
    }

    // NCALC-232: a negative index from the end and a value of another type in a typed list gave BCL exceptions
    [Theory]
    [InlineData("s[^-1]")]
    [InlineData("lst[^-1]")]
    [InlineData("lst[^-1..]")]
    [InlineData("ints[0] := 'x'")]
    public async Task IndexingErrorsShouldBeNCalcExceptions(string text)
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var nonRecursive in new[] { false, true })
        {
            var options = ExpressionOptions.NoCache | ExpressionOptions.UseAssignments | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

            var expression = new Expression(text, options);
            expression.Parameters["s"] = "abc";
            expression.Parameters["lst"] = new List<object?> { 1, 2, 3 };
            expression.Parameters["ints"] = new[] { 1, 2 };
            Assert.Throws<NCalcParameterIndexException>(() => expression.Evaluate(token));

            var asyncExpression = new AsyncExpression(text, options);
            asyncExpression.Parameters["s"] = "abc";
            asyncExpression.Parameters["lst"] = new List<object?> { 1, 2, 3 };
            asyncExpression.Parameters["ints"] = new[] { 1, 2 };
            await Assert.ThrowsAsync<NCalcParameterIndexException>(async () => await asyncExpression.EvaluateAsync(token));
        }
    }

    // NCALC-232: the value assigned by index to a typed list is converted to the element type
    [Fact]
    public async Task IndexedAssignmentShouldConvertTheValueToTheElementType()
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var nonRecursive in new[] { false, true })
        {
            var options = ExpressionOptions.NoCache | ExpressionOptions.UseAssignments | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

            var doubles = new List<double> { 1.5, 2.5 };
            var longs = new long[] { 1, 2 };
            var expression = new Expression("d[0] := 1; l[1] := 7", options);
            expression.Parameters["d"] = doubles;
            expression.Parameters["l"] = longs;
            expression.Options |= ExpressionOptions.UseStatementSequences;
            expression.Evaluate(token);
            Assert.Equal([1.0, 2.5], doubles);
            Assert.Equal([1L, 7L], longs);

            var asyncDoubles = new List<double> { 1.5, 2.5 };
            var asyncExpression = new AsyncExpression("d[0] := 3", options);
            asyncExpression.Parameters["d"] = asyncDoubles;
            await asyncExpression.EvaluateAsync(token);
            Assert.Equal([3.0, 2.5], asyncDoubles);
        }
    }

    // NCALC-222: nested function calls were limited to a few hundred levels also with UseNonRecursiveEvaluator
    [Theory]
    [InlineData("Abs(", ")")]
    [InlineData("if(true, ", ", 0)")]
    public async Task DeeplyNestedFunctionCallsShouldBeEvaluatedByTheNonRecursiveEvaluator(string open, string close)
    {
        // Well above the former limits (about 260 levels in the synchronous and 100 in the asynchronous evaluator), within those of the parser
        const int depth = 1000;
        var text = string.Concat(Enumerable.Repeat(open, depth)) + "1" + string.Concat(Enumerable.Repeat(close, depth));
        const ExpressionOptions options = ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator;
        var token = TestContext.Current.CancellationToken;

        // Parsed first, on a thread with a large stack if needed, so that only the evaluation runs on the thread pool thread
        var expression = new Expression(text, options);
        Assert.False(expression.HasErrors(token), expression.Error?.Message);
        Assert.Equal(1, Convert.ToInt32(await Task.Run(() => expression.Evaluate(token), token), CultureInfo.InvariantCulture));

        var asyncExpression = new AsyncExpression(text, options);
        Assert.False(asyncExpression.HasErrors(token));
        Assert.Equal(1, Convert.ToInt32(await Task.Run(async () => await asyncExpression.EvaluateAsync(token), token), CultureInfo.InvariantCulture));
    }

    private const ExpressionOptions DeepOptions = ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator | ExpressionOptions.UseStatementSequences;

    private static string Nest(string function, int depth, string inner) => string.Concat(Enumerable.Repeat(function + "(", depth)) + inner + new string(')', depth);

    // Calls of user functions, of handlers and Functions that evaluate their argument, or a chain of expressions used as parameter values
    private static Expression CreateDeepExpression(string kind, int depth)
    {
        var expression = new Expression(kind switch
        {
            "fn" => "fn g(a) => a; " + Nest("g", depth, "1"),
            "handler" => Nest("id", depth, "1"),
            "functions" => Nest("fid", depth, "1"),
            _ => "p0"
        }, DeepOptions);
        expression.EvaluateFunction += (name, args) =>
        {
            if (name == "id")
                args.Result = args.Parameters[0].Evaluate();
        };
        expression.Functions["fid"] = data => data[0].Evaluate();
        for (var i = 0; i < depth; i++)
            expression.Parameters["p" + i] = new Expression("p" + (i + 1), DeepOptions);
        expression.Parameters["p" + depth] = 1;
        return expression;
    }

    private static AsyncExpression CreateDeepAsyncExpression(string kind, int depth)
    {
        var expression = new AsyncExpression(kind switch
        {
            "fn" => "fn g(a) => a; " + Nest("g", depth, "1"),
            "handler" => Nest("id", depth, "1"),
            "functions" => Nest("fid", depth, "1"),
            _ => "p0"
        }, DeepOptions);
        expression.EvaluateFunctionAsync += async (name, args, ct) =>
        {
            if (name == "id")
                args.Result = await args.Parameters[0].EvaluateAsync(ct);
        };
        expression.Functions["fid"] = async (data, ct) => await data[0].EvaluateAsync(ct);
        for (var i = 0; i < depth; i++)
            expression.Parameters["p" + i] = new AsyncExpression("p" + (i + 1), DeepOptions);
        expression.Parameters["p" + depth] = 1;
        return expression;
    }

    // NCALC-305: nested calls of user functions, of handlers that evaluate their argument and chains of expressions used as parameter values
    // still stopped at a few hundred levels under the non-recursive evaluators, with a message that advised the option in use
    [Theory]
    [InlineData("fn")]
    [InlineData("handler")]
    [InlineData("functions")]
    [InlineData("parameters")]
    public async Task DeeplyNestedCallsThatEvaluateTheirArgumentsShouldBeEvaluatedByTheNonRecursiveEvaluator(string kind)
    {
        const int depth = 1000;
        var token = TestContext.Current.CancellationToken;

        // Parsed first, on a thread with a large stack if needed, so that only the evaluation runs on the thread pool thread
        var expression = CreateDeepExpression(kind, depth);
        Assert.False(expression.HasErrors(token), expression.Error?.Message);
        Assert.Equal(1, Convert.ToInt32(await Task.Run(() => expression.Evaluate(token), token), CultureInfo.InvariantCulture));

        var asyncExpression = CreateDeepAsyncExpression(kind, depth);
        Assert.False(asyncExpression.HasErrors(token), asyncExpression.Error?.Message);
        Assert.Equal(1, Convert.ToInt32(await Task.Run(async () => await asyncExpression.EvaluateAsync(token), token), CultureInfo.InvariantCulture));
    }

    // NCALC-306: an error or "return" coming out of deeply nested calls took seconds in the asynchronous non-recursive evaluator,
    // as the stack trace of the exception grew at each level and was copied at each await
    [Theory]
    [InlineData("throw()")]
    [InlineData("1 % 0")]
    [InlineData("return 5")]
    public async Task ErrorOrReturnOutOfDeeplyNestedCallsShouldBeFast(string inner)
    {
        const int depth = 1000;
        var token = TestContext.Current.CancellationToken;
        var text = Nest("Abs", depth, inner) + "; 7";
        var options = DeepOptions | ExpressionOptions.UseAssignments;

        var asyncExpression = new AsyncExpression(text, options);
        asyncExpression.EvaluateFunctionAsync += (name, _, _) => name == "throw" ? throw new InvalidOperationException("thrown") : ValueTask.CompletedTask;
        Assert.False(asyncExpression.HasErrors(token), asyncExpression.Error?.Message);
        var expression = new Expression(text, options);
        expression.EvaluateFunction += (name, _) =>
        {
            if (name == "throw")
                throw new InvalidOperationException("thrown");
        };

        // Once without timing, so that the code is compiled
        Check(await Record.ExceptionAsync(() => Task.Run(() => expression.Evaluate(token), token)));
        Check(await Record.ExceptionAsync(() => Task.Run(async () => await asyncExpression.EvaluateAsync(token), token)));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Check(await Record.ExceptionAsync(() => Task.Run(() => expression.Evaluate(token), token)));
        var syncTime = stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();
        Check(await Record.ExceptionAsync(() => Task.Run(async () => await asyncExpression.EvaluateAsync(token), token)));
        var asyncTime = stopwatch.ElapsedMilliseconds;
        // Compared with the synchronous evaluator, as both are slower on a loaded machine
        // About 0.2 s, a few times as long as in the synchronous evaluator; it took 5 s
        Assert.True(asyncTime <= 1000 + 8 * syncTime, $"asynchronous: {asyncTime} ms, synchronous: {syncTime} ms");
        if (inner == "return 5")
        {
            Assert.Equal(5, Convert.ToInt32(await Task.Run(() => expression.Evaluate(token), token), CultureInfo.InvariantCulture));
            Assert.Equal(5, Convert.ToInt32(await Task.Run(async () => await asyncExpression.EvaluateAsync(token), token), CultureInfo.InvariantCulture));
        }

        void Check(Exception? exception)
        {
            if (inner == "return 5")
                Assert.Null(exception);
            else
                Assert.IsType(inner == "throw()" ? typeof(InvalidOperationException) : typeof(NCalcEvaluationException), exception);
        }
    }
}

// The tests of this collection change LogicalExpressionParser.DeepExpressionStackSize, so no other test runs meanwhile
[CollectionDefinition(nameof(DeepExpressionStackSizeCollection), DisableParallelization = true)]
public sealed class DeepExpressionStackSizeCollection;

[Trait("Category", "Evaluations")]
[Collection(nameof(DeepExpressionStackSizeCollection))]
public class DeepEvaluationThreadTests
{
    private const ExpressionOptions DeepOptions = ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator;

    // Far deeper than the small stack can hold (frames are smaller on Linux than on Windows, where 500 levels were enough), yet within
    // what the parser handles on a stack of DeepExpressionStackSize bytes (about 1700 levels)
    private static readonly string Text = string.Concat(Enumerable.Repeat("id(", 1200)) + "1" + new string(')', 1200);

    // Runs the action on a thread with a small stack, so that the evaluation needs another thread after a few levels
    private static void RunOnSmallStack(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        }, 256 * 1024);
        thread.Start();
        thread.Join();
        if (error is not null)
            throw new Xunit.Sdk.XunitException(error.ToString());
    }

    // NCALC-337: under the non-recursive evaluators, deeply nested calls continue on another thread, and so do the handlers called there;
    // DeepExpressionStackSize = 0 keeps the evaluation on the calling thread, which then fails with an exception (NCALC-305: whose message
    // does not advise the option in use)
    [Fact]
    public void DeepExpressionStackSizeShouldDecideWhetherHandlersOfDeeplyNestedCallsRunOnAnotherThread()
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var stackSize in new[] { Parser.LogicalExpressionParser.DeepExpressionStackSize, 0 })
        {
            var threads = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();
            var expression = new Expression(Text, DeepOptions);
            expression.EvaluateFunction += (_, args) =>
            {
                threads[Environment.CurrentManagedThreadId] = true;
                args.Result = args.Parameters[0].Evaluate();
            };
            var asyncExpression = new AsyncExpression(Text, DeepOptions);
            asyncExpression.EvaluateFunctionAsync += async (_, args, ct) =>
            {
                threads[Environment.CurrentManagedThreadId] = true;
                args.Result = await args.Parameters[0].EvaluateAsync(ct);
            };
            // Parsed here, so that only the evaluation runs on the small stack
            Assert.False(expression.HasErrors(token));
            Assert.False(asyncExpression.HasErrors(token));

            var defaultSize = Parser.LogicalExpressionParser.DeepExpressionStackSize;
            Parser.LogicalExpressionParser.DeepExpressionStackSize = stackSize;
            try
            {
                RunOnSmallStack(() =>
                {
                    var caller = Environment.CurrentManagedThreadId;
                    foreach (var evaluate in new Func<object?>[] { () => expression.Evaluate(token), () => asyncExpression.EvaluateAsync(token).AsTask().GetAwaiter().GetResult() })
                    {
                        threads.Clear();
                        var exception = Record.Exception(() => Assert.Equal(1, Convert.ToInt32(evaluate(), CultureInfo.InvariantCulture)));
                        if (stackSize > 0)
                        {
                            Assert.Null(exception);
                            Assert.Contains(threads.Keys, thread => thread != caller);
                        }
                        else
                        {
                            var evaluationException = Assert.IsType<NCalcEvaluationException>(exception);
                            Assert.DoesNotContain("Use ExpressionOptions.UseNonRecursiveEvaluator", evaluationException.Message);
                            Assert.All(threads.Keys, thread => Assert.Equal(caller, thread));
                        }
                    }
                });
            }
            finally
            {
                Parser.LogicalExpressionParser.DeepExpressionStackSize = defaultSize;
            }
        }
    }
}

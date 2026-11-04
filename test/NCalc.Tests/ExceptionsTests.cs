using NCalc.Exceptions;
using NCalc.Factories;
using NCalc.Parser;

namespace NCalc.Tests;

[Trait("Category", "Exceptions")]
public class ExceptionsTests
{
    [Theory]
    [InlineData("ifs()")]
    [InlineData("ifs([divider] > 0)")]
    //[InlineData("ifs([divider] > 0, [divider] / [divided])")]
    //[InlineData("ifs([divider] > 0, [divider] / [divided], [divider < 0], [divider] + [divided])")]
    public void Ifs_With_Improper_Arguments_Should_Throw_Exceptions(string expression)
    {
        Assert.Throws<NCalcEvaluationException>(() => new Expression(expression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(". + 2")]
    [InlineData("(3 + 2")]
    [InlineData("42a")]
    [InlineData("42a.3")]
    [InlineData("42.3a")]
    [InlineData("42a.3b")]
    [InlineData("42.3e-5a")]
    [InlineData("42 + [a + 10")]
    [InlineData("42 a")]
    [InlineData("42 '")]
    [InlineData("Abs(-1) ]")]
    [InlineData("42. 3")]
    [InlineData("42 .3")]
    [InlineData("42 . 3")]
    public void Should_Throw_Parse_Exception(string expression)
    {
        Assert.Throws<NCalcParserException>(() => new Expression(expression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Detect_Syntax_Errors_Before_Evaluation()
    {
        //Yes, I know at v3 the test was a + b * ( , but it's impossible to define an empty expression.
        var e = new Expression("a + b * ( 1 + 1");
        Assert.Null(e.Error);
        Assert.True(e.HasErrors(TestContext.Current.CancellationToken));
        Assert.NotNull(e.Error);

        e = new Expression("* b ");
        Assert.Null(e.Error);
        Assert.True(e.HasErrors(TestContext.Current.CancellationToken));
        Assert.NotNull(e.Error);
    }

    [Fact]
    public void Should_Display_Error_If_UncompatibleTypes()
    {
        var e = new Expression("(a > b) + 10")
        {
            Parameters =
            {
                ["a"] = 1,
                ["b"] = 2
            }
        };

        Assert.Throws<InvalidOperationException>(() =>
        {
            e.Evaluate(TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public void Should_Throw_Exception_On_Lexer_Errors_Issue_6()
    {
        Assert.Throws<NCalcParserException>(() => LogicalExpressionFactory.Create("#t -chers", cancellationToken: TestContext.Current.CancellationToken));

        var dateSeparator = CultureInfo.CurrentCulture.DateTimeFormat.DateSeparator;
        string dateStr = $"#13{dateSeparator}13{dateSeparator}2222#";
        var invalidDateException = Assert.Throws<FormatException>(() => LogicalExpressionFactory.Create(dateStr, cancellationToken: TestContext.Current.CancellationToken));
        Assert.IsType<FormatException>(invalidDateException);

        //At v4, DateTime is better handled, and this should no longer cause errors.
        // https://github.com/ncalc/ncalc-async/issues/6
        try
        {
            LogicalExpressionFactory.Create("Format(\"{0:(###) ###-####}\", \"9999999999\")", cancellationToken: TestContext.Current.CancellationToken);
        }
        catch
        {
            Assert.Fail("No exception should be thrown here.");
        }
    }

    [Fact]
    public void ShouldProvideErrorPosition()
    {
        var expression = new Expression("42a");
        try
        {
            expression.Evaluate(TestContext.Current.CancellationToken);
            Assert.Throws<NCalcParserException>(() => true);
        }
        catch (NCalcParserException ex)
        {
            // The position of the first character that could not be parsed
            Assert.Equal("Invalid token in expression at position (1:3)", ex.InnerException?.Message);
        }
    }

    [Fact]
    public void Should_Throw_Function_Not_Found()
    {
        var expression = new Expression("drop_database()");
        var exception = Assert.Throws<NCalcFunctionNotFoundException>(() => expression.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal("drop_database", exception.FunctionName);
    }

    [Fact]
    public void Should_Throw_Parameter_Not_Found()
    {
        var expression = new Expression("{Name} == 'Spinella'");
        var exception = Assert.Throws<NCalcParameterNotDefinedException>(() => expression.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal("Name", exception.ParameterName);
    }

    [Theory]
    [InlineData("5+-*10")]
    [InlineData("5+*10")]
    [InlineData("5/-*10")]
    public void Should_Throw_Issue_195(string expressionString)
    {
        var expression = new Expression(expressionString);
        Assert.Throws<NCalcParserException>(() => expression.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("()>0.", typeof(NCalcParserException))]
    [InlineData("009&()", typeof(NCalcConversionException))]
    [InlineData("0000000000000000<<()", typeof(NCalcEvaluationException))]
    [InlineData("(' (' +'2(' , 2)/--1", typeof(NCalcConversionException))]
    [InlineData("!() --19", typeof(NCalcConversionException))]
    public void Should_Throw_Issue_551(string input, Type expectedExceptionType)
    {
        var expression = new Expression(input);
        Assert.Throws(expectedExceptionType, () => expression.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Throw_Issue_208()
    {
        var expression = new Expression("1.3,4.5");
        Assert.Throws<NCalcParserException>(() => expression.Evaluate(TestContext.Current.CancellationToken));
    }

    private const int ExcessiveDepth = 20000;

    // Too deep to be parsed even on the thread with the larger stack
    private const int UnparseableDepth = 200000;

    // Too deep to be parsed on a thread with the default stack, but not on the thread with the larger stack
    private const int LargeStackDepth = 1000;

    public static TheoryData<string> DeeplyNestedExpressions => new()
    {
        new string('(', UnparseableDepth) + "1" + new string(')', UnparseableDepth),
        string.Concat(Enumerable.Repeat("Abs(", UnparseableDepth)) + "1" + new string(')', UnparseableDepth),
        string.Concat(Enumerable.Repeat("Max(1, ", UnparseableDepth)) + "1" + new string(')', UnparseableDepth),
        string.Concat(Enumerable.Repeat("(true ? ", UnparseableDepth)) + "1" + string.Concat(Enumerable.Repeat(" : 0)", UnparseableDepth)),
        new string('-', UnparseableDepth) + "1",
        string.Concat(Enumerable.Repeat("not ", UnparseableDepth)) + "true",
    };

    [Theory]
    [MemberData(nameof(DeeplyNestedExpressions))]
    public void DeeplyNestedExpressionShouldThrowParserExceptionInsteadOfStackOverflow(string expression)
    {
        // Parsing is recursive regardless of the evaluator in use
        Assert.Throws<NCalcParserException>(() => new Expression(expression, ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken));
    }

    public static TheoryData<string, object> LargeStackExpressions => new()
    {
        { new string('(', LargeStackDepth) + "1" + new string(')', LargeStackDepth), 1 },
        { string.Concat(Enumerable.Repeat("1 + (", LargeStackDepth)) + "1" + new string(')', LargeStackDepth), LargeStackDepth + 1 },
        { string.Concat(Enumerable.Repeat("(true ? ", LargeStackDepth)) + "1" + string.Concat(Enumerable.Repeat(" : 0)", LargeStackDepth)), 1 },
        { new string('-', 10 * LargeStackDepth) + "1", 1 },
    };

    [Theory]
    [MemberData(nameof(LargeStackExpressions))]
    public void DeeplyNestedExpressionShouldBeParsedOnLargerStack(string expression, object expected)
    {
        Assert.Equal(expected, new Expression(expression, ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DeeplyNestedFunctionCallsShouldBeParsedOnLargerStack()
    {
        var expression = string.Concat(Enumerable.Repeat("Abs(", LargeStackDepth)) + "-1" + new string(')', LargeStackDepth);

        Assert.IsType<Domain.FunctionCall>(LogicalExpressionFactory.Create(expression, options: ExpressionOptions.NoCache, cancellationToken: TestContext.Current.CancellationToken));

        // Each function call evaluates its arguments as an expression of its own, so even the non-recursive evaluator
        // may run out of stack, but it must say so with an exception instead of terminating the process
        var exception = Record.Exception(() => new Expression(expression, ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken));
        Assert.True(exception is null or NCalcException, exception?.ToString());
    }

    [Fact]
    public void DeeplyNestedExpressionShouldThrowExceptionInsteadOfStackOverflowWhenSerialized()
    {
        var expression = LogicalExpressionFactory.Create(string.Concat(Enumerable.Repeat("Abs(", LargeStackDepth)) + "1" + new string(')', LargeStackDepth),
            options: ExpressionOptions.NoCache, cancellationToken: TestContext.Current.CancellationToken);

        var exception = Record.Exception(() => expression.ToString());
        Assert.True(exception is null or NCalcException, exception?.ToString());
    }

    [Fact]
    public void FunctionDeclaredInDeeplyNestedExpressionShouldBeRegistered()
    {
        // No assignments: with them on, every group is parsed twice, so deep groups take exponential time
        var expression = new Expression("fn Half(x) { return x / 2 }; Half(" + new string('(', LargeStackDepth) + "84" + new string(')', LargeStackDepth) + ")",
            ExpressionOptions.NoCache | ExpressionOptions.UseStatementSequences);

        Assert.Equal(42, Convert.ToInt32(expression.Evaluate(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public void LongChainShouldThrowEvaluationExceptionInRecursiveEvaluator()
    {
        var expression = string.Join("+", Enumerable.Repeat("1", ExcessiveDepth));
        Assert.Throws<NCalcEvaluationException>(() => new Expression(expression, ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LongChainShouldThrowEvaluationExceptionInRecursiveAsyncEvaluator()
    {
        var expression = string.Join("+", Enumerable.Repeat("1", ExcessiveDepth));
        await Assert.ThrowsAsync<NCalcEvaluationException>(async () => await new AsyncExpression(expression, ExpressionOptions.NoCache).EvaluateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void LongChainShouldBeEvaluatedByNonRecursiveEvaluator()
    {
        var expression = string.Join("+", Enumerable.Repeat("1", 5000));
        Assert.Equal(5000, new Expression(expression, ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SelfReferencingParameterShouldThrowEvaluationException()
    {
        var expression = new Expression("x + 1", ExpressionOptions.NoCache);
        expression.Parameters["x"] = expression;
        Assert.Throws<NCalcEvaluationException>(() => expression.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldThrowExceptionWhenNullOrEmpty()
    {
        Assert.Throws<NCalcException>(() => new Expression("").Evaluate(TestContext.Current.CancellationToken));
        Assert.Throws<NCalcException>(() => new Expression((string)null).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("1 +", "(1:4)")]
    [InlineData("1 2", "(1:3)")]
    [InlineData("1)", "(1:2)")]
    [InlineData("(1 + 2", "(1:7)")]
    public void ParseErrorShouldReportTheFurthestPosition(string text, string position)
    {
        // NCALC-071: the position was almost always (1:1)
        var expression = new Expression(text);
        Assert.True(expression.HasErrors(TestContext.Current.CancellationToken));
        Assert.Contains(position, expression.Error!.InnerException?.Message ?? expression.Error.Message);
    }

    [Theory]
    [InlineData("1 + + 2", ExpressionOptions.None)]
    [InlineData("1 * * 2", ExpressionOptions.None)]
    [InlineData("fn f() => 1; fn f() => 2; f()", ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.UseCStyleAssignments)]
    [InlineData("iff('x', 1, 2)", ExpressionOptions.None)]
    [InlineData("ifs('x', 1, 2)", ExpressionOptions.None)]
    [InlineData("l[-1] := 9", ExpressionOptions.UseAssignments)]
    [InlineData("a == b", ExpressionOptions.None)]
    public async Task ErrorsShouldBeNCalcExceptions(string text, ExpressionOptions options)
    {
        // NCALC-072: InvalidOperationException, ArgumentException, FormatException and ArgumentOutOfRangeException escaped
        void SetParameters(IDictionary<string, object?> parameters)
        {
            parameters["l"] = new List<object?> { 1, 2 };
            parameters["a"] = new List<int> { 1 };
            parameters["b"] = new List<int> { 2 };
        }

        var expression = new Expression(text, options);
        SetParameters(expression.Parameters);
        Assert.ThrowsAny<NCalcException>(() => expression.Evaluate(TestContext.Current.CancellationToken));

        var asyncExpression = new AsyncExpression(text, options);
        SetParameters(asyncExpression.Parameters);
        await Assert.ThrowsAnyAsync<NCalcException>(async () => await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));

        if (options == ExpressionOptions.None && text.StartsWith('1'))
            Assert.ThrowsAny<NCalcException>(() => LogicalExpressionFactory.Create(text, null, CultureInfo.InvariantCulture));
    }

    // NCALC-239: the asynchronous ifs/if reported the location of the function instead of the condition, and the non-recursive
    // evaluators gave no location to an error thrown inside a built-in function
    [Theory]
    [InlineData("1 + ifs(false, 1, 'x', 2, 3)")]
    [InlineData("1 + if('x', 1, 2)")]
    [InlineData("1 + Round(1.5, 'x')")]
    [InlineData("1 + Max(1, 'x')")]
    [InlineData("2 * (1 + Abs(Round(1.5, 'x')))")]
    public async Task ErrorLocationsShouldBeTheSameInAllEvaluators(string text)
    {
        var locations = new List<string>();
        foreach (var nonRecursive in new[] { false, true })
        {
            var options = ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
            var syncError = Assert.ThrowsAny<NCalcEvaluationException>(() => new Expression(text, options).Evaluate(TestContext.Current.CancellationToken));
            var asyncError = await Assert.ThrowsAnyAsync<NCalcEvaluationException>(async () => await new AsyncExpression(text, options).EvaluateAsync(TestContext.Current.CancellationToken));
            foreach (var error in new[] { syncError, asyncError })
            {
                Assert.NotEqual(-1, error.Location.Column);
                locations.Add($"{error.Location.Row}:{error.Location.Column}:{error.Location.Offset}");
            }
        }
        Assert.Single(locations.Distinct());
    }

    [Fact]
    public void CancellationDuringParsingShouldNotBeASyntaxError()
    {
        // NCALC-085
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => new Expression("1 + 2 * (3 - 4)", ExpressionOptions.NoCache).HasErrors(source.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => new Expression("1 + 2 * (3 - 4)", ExpressionOptions.NoCache).Evaluate(source.Token));
    }
}

// Changes LogicalExpressionParser.DeepExpressionStackSize, which the non-recursive evaluators read too, so no other test runs meanwhile
[Trait("Category", "Exceptions")]
[Collection(nameof(DeepExpressionStackSizeCollection))]
public class DeepExpressionStackSizeExceptionsTests
{
    [Fact]
    public void DeeplyNestedExpressionShouldThrowParserExceptionWhenLargerStackIsOff()
    {
        // Far deeper than any thread's stack can hold: frames are smaller and the default stack is larger on Linux than on Windows
        var expression = new string('(', 100_000) + "1" + new string(')', 100_000);
        var stackSize = LogicalExpressionParser.DeepExpressionStackSize;
        LogicalExpressionParser.DeepExpressionStackSize = 0;
        try
        {
            Assert.Throws<NCalcParserException>(() => new Expression(expression, ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        }
        finally
        {
            LogicalExpressionParser.DeepExpressionStackSize = stackSize;
        }
    }
}

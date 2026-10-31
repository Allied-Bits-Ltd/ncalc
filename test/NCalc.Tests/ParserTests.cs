using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Factories;
using NCalc.Helpers;
using NCalc.Parser;

namespace NCalc.Tests;

[Trait("Category", "Parser")]
public class ParserTests
{
    [Fact]
    public void ShouldParseMinInt()
    {
        var expression = new Expression("-2147483648");
        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        if (result is BigDecimal bd)
        {
            Assert.True(bd.DecimalPlaces == 0);
            Assert.Equal((long)-2147483648, (long)bd.WholeValue);
        }
        else
        {
            Assert.Equal((long)-2147483648, (long)result);
        }
    }

    [Theory]
    [InlineData("11+33 ", 44)]
    [InlineData(" 11+33", 44)]
    [InlineData(" 11+33 ", 44)]
    [InlineData("0.0-1.1", -1.1)]
    public void ShouldIgnoreWhitespacesIssue222(string formula, object expectedValue)
    {
        var expression = new Expression(formula, CultureInfo.InvariantCulture);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(expectedValue, result);
    }

    [Theory]
    [InlineData("not( true )", false)]
    [InlineData("not ( true )", false)]
    [InlineData("not(true)", false)]
    [InlineData(" not(true) ", false)]
    public void NotBehaviorIssue226(string formula, object expectedValue)
    {
        var expression = new Expression(formula, CultureInfo.InvariantCulture);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void ShouldHandleNewLines()
    {
        const string formula = """
                               2+3


                               """;

        var expression = new Expression(formula, CultureInfo.InvariantCulture);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(5, result);
    }

    [Fact]
    public void RequireClosingAtIdentifiersIssue244()
    {
        const string formula = "[{Diagnostic}.Data]";

        var logicalExpression = LogicalExpressionFactory.Create(formula, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<Identifier>(logicalExpression);

        Assert.Equal("{Diagnostic}.Data", ((Identifier)logicalExpression).Name);
    }

    [Fact]
    public void AllowCharValues()
    {
        const string formula = "'c'";

        var logicalExpression = LogicalExpressionFactory.Create(formula, null, null, ExpressionOptions.AllowCharValues, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<ValueExpression>(logicalExpression);

        Assert.Equal('c', ((ValueExpression)logicalExpression).Value);
    }

    [InlineData("(1+2)*3", 9)]
    [InlineData("(8 * 8) + 1", 65)]
    [InlineData("1 + 1", 2)]
    [InlineData("-1 - 1", -2)]
    [Theory]
    public void ShouldHandleBinaryExpression(string formula, int expectedResult)
    {
        var logicalExpression = LogicalExpressionFactory.Create(formula, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<BinaryExpression>(logicalExpression);

        var expression = new Expression(logicalExpression);

        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(0, EvaluationHelper.Compare(expectedResult, result, new ComparisonOptions(), new MathHelperOptions()));
    }

    [InlineData("φ")]
    [InlineData("π")]
    [InlineData("2 ∶ 2")]
    [InlineData("2 ∙ 2")]
    [InlineData("2 ≠ 2")]
    [Theory]
    public void ShouldParseUnicodeChars(string formula)
    {
        var expression = new Expression(formula, options: ExpressionOptions.UseUnicodeCharsForOperations);
        expression.AdvancedOptions = new();

        expression.EvaluateParameter += (name, args) =>
            {
                args.Result = 1;
            };

        var result = expression.Evaluate(TestContext.Current.CancellationToken);
    }

    [InlineData("(1,2,3,4,5)", 5)]
    [InlineData("()", 0)]
    [InlineData("('Hello', func())", 2)]
    [Theory]
    public void ShouldParseLists(string formula, int arrayExpectedCount)
    {
        var logicalExpression = LogicalExpressionFactory.Create(formula, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<LogicalExpressionList>(logicalExpression);

        Assert.Equal(arrayExpectedCount, ((LogicalExpressionList)logicalExpression).Count);
    }

    [InlineData("(1 2 3 4 5)", 5)]
    [InlineData("('Hello'  func())", 2)]
    [Theory]
    public void ShouldParseListsWithSpaceAsSeparator(string formula, int arrayExpectedCount)
    {
        AdvancedExpressionOptions advancedOptions = new();
        advancedOptions.ListItemSeparator = AdvancedExpressionOptions.ListItemSeparatorKind.Space;
        var logicalExpression = LogicalExpressionFactory.Create(formula, advancedOptions: advancedOptions, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<LogicalExpressionList>(logicalExpression);

        Assert.Equal(arrayExpectedCount, ((LogicalExpressionList)logicalExpression).Count);
    }

    [InlineData("(1;2:3 4 5)", 5)]
    [Theory]
    public void ShouldParseListsWithDifferentSeparators(string formula, int arrayExpectedCount)
    {
        AdvancedExpressionOptions advancedOptions = new();
        advancedOptions.ListItemSeparator = AdvancedExpressionOptions.ListItemSeparatorKind.SemicolonOrColonOrSpace;
        var logicalExpression = LogicalExpressionFactory.Create(formula, advancedOptions: advancedOptions, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<LogicalExpressionList>(logicalExpression);

        Assert.Equal(arrayExpectedCount, ((LogicalExpressionList)logicalExpression).Count);
    }

    [InlineData("78b1941f4e7941c9bef656fad7326538")]
    [InlineData("b1548bd5-2556-4d2a-9f47-bb8d421026dd")]
    [InlineData("f44e449f-b02f-4f81-96d8-9292c5623b8b")]
    [InlineData("db6ff6c1-8290-4bdb-8c32-b78f3f2f6231")]
    [InlineData("e21aefee-8ffe-422c-a03f-7beb60975992")]
    [InlineData("13f722a3-5139-4eda-bffb-ed32e8cc90d1")]
    [InlineData("58ede4c6-2323-4b1a-ad66-c062fccb8473")]
    [InlineData("F6A41126850F41489192C89A949B2392")]
    [InlineData("E3825D69DAC04CA88D73E8CC3C8ADA2F")]
    [InlineData("B0D256EB-E88C-4409-A80B-E7341295B9F1")]
    [InlineData("C785E8B3-D080-45AF-947E-3D8F246788A6")]
    [Theory]
    public void ShouldParseGuids(string formula)
    {
        var logicalExpression = LogicalExpressionFactory.Create(formula, cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<ValueExpression>(logicalExpression);

        Assert.IsType<Guid>(new Expression(logicalExpression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldParseGuidInsideFunction()
    {
        var logicalExpression = LogicalExpressionFactory.Create("getUser(78b1941f4e7941c9bef656fad7326538)", cancellationToken: TestContext.Current.CancellationToken);

        if (logicalExpression is FunctionCall function)
        {
            Assert.True(function.Parameters[0] is ValueExpression { Value: Guid });
        }
        else
        {
            Assert.Fail("Logical expression is not a function.");
        }
    }

    [Fact]
    public void OperatorPriorityIssue337()
    {
        Assert.True((bool)new Expression("true or true and false").Evaluate(TestContext.Current.CancellationToken)!);
    }

    [Fact]
    public void ShouldNotFailIssue372()
    {
        var chars = new List<string>();
        for (var c = 'a'; c < 'z'; ++c)
        {
            chars.Add(c.ToString());
        }

        for (var c = 'A'; c < 'Z'; ++c)
        {
            chars.Add(c.ToString());
        }

        var failed = new List<string>();
        foreach (var c in chars)
        {
            try
            {
                var context = new LogicalExpressionParserContext(c, ExpressionOptions.None);
                LogicalExpressionParser.Parse(context);
            }
            catch (Exception)
            {
                failed.Add(c);
            }
        }

        Assert.Empty(failed);
    }

    [Fact]
    public void ShouldNotFailIssue399()
    {
        bool exceptionThrown = false;

        try
        {
            var expressionOptions = ExpressionOptions.DecimalAsDefault | ExpressionOptions.NoCache;
            var expression = new Expression("0.3333333333333333333333 + 1.6666666666666666666667", expressionOptions, CultureInfo.InvariantCulture);
            var result = expression.Evaluate(TestContext.Current.CancellationToken);
        }
        catch
        {
            exceptionThrown = true;
        }

        Assert.False(exceptionThrown);
    }

    // The cache tests use texts that no other test evaluates, so the first parse happens here

    [Fact]
    public void CachedExpressionShouldNotBeReusedWithDifferentOptions()
    {
        Assert.Equal(0.30000000000000004, new Expression("0.1 + 0.2 + 0", CultureInfo.InvariantCulture).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(0.3m, new Expression("0.1 + 0.2 + 0", ExpressionOptions.DecimalAsDefault, CultureInfo.InvariantCulture).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CachedExpressionShouldNotBeReusedWithDifferentAdvancedOptions()
    {
        Assert.Equal(10, new Expression("010 + 0", CultureInfo.InvariantCulture) { AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture) }.Evaluate(TestContext.Current.CancellationToken));
        var octal = new Expression("010 + 0", CultureInfo.InvariantCulture) { AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture) };
        octal.AdvancedOptions.Flags = AdvExpressionOptions.AcceptCStyleOctals;
        Assert.Equal(8, octal.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CachedExpressionShouldNotBeReusedWithDifferentCulture()
    {
        var us = (DateTime)new Expression("(#01/02/2024#)", CultureInfo.GetCultureInfo("en-US")).Evaluate(TestContext.Current.CancellationToken)!;
        var gb = (DateTime)new Expression("(#01/02/2024#)", CultureInfo.GetCultureInfo("en-GB")).Evaluate(TestContext.Current.CancellationToken)!;
        Assert.Equal(new DateTime(2024, 1, 2), us);
        Assert.Equal(new DateTime(2024, 2, 1), gb);
    }

    [Fact]
    public void CachedExpressionShouldBeReusedWithSameOptions()
    {
        var first = new Expression("17 * 3 + 0", ExpressionOptions.DecimalAsDefault, CultureInfo.InvariantCulture);
        var second = new Expression("17 * 3 + 0", ExpressionOptions.DecimalAsDefault, CultureInfo.InvariantCulture);
        first.Evaluate(TestContext.Current.CancellationToken);
        second.Evaluate(TestContext.Current.CancellationToken);
        Assert.Same(first.LogicalExpression, second.LogicalExpression);
    }

    [Theory]
    [InlineData("trueValue", 5)]
    [InlineData("falsehood", 6)]
    [InlineData("nullable", 7)]
    [InlineData("notValue", 8)]
    [InlineData("true_flag", 9)]
    [InlineData("returnValue * 2", 14)]
    [InlineData("2 mod 3", 2)]
    [InlineData("7 div 2", 3)]
    [InlineData("not false", true)]
    [InlineData("true and not(false)", true)]
    [InlineData("2 in (1, 2)", true)]
    public void IdentifiersStartingWithKeywordsShouldBeIdentifiers(string expression, object expected)
    {
        var e = new Expression(expression, ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences);
        e.Parameters["trueValue"] = 5;
        e.Parameters["falsehood"] = 6;
        e.Parameters["nullable"] = 7;
        e.Parameters["notValue"] = 8;
        e.Parameters["true_flag"] = 9;
        e.Parameters["returnValue"] = 7;
        e.Parameters["Value"] = 1;
        Assert.Equal(expected, e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void EveryExpressionOptionShouldBeASingleBit()
    {
        Assert.Equal(1L << 31, (long)ExpressionOptions.SupportPythonComments);

        foreach (ExpressionOptions option in Enum.GetValues(typeof(ExpressionOptions)))
        {
            var value = (long)option;
            if (value != 0)
                Assert.True((value & (value - 1)) == 0, $"{option} = 0x{value:X} is not a single bit");
        }
    }

    [Fact]
    public void PythonCommentsShouldNotEnableOtherOptions()
    {
        // Before the fix, the option switched on e.g. UseTernaryLogic, which made the result null
        var e = new Expression("'true' and true # comment", ExpressionOptions.SupportPythonComments);
        Assert.Equal(true, e.Evaluate(TestContext.Current.CancellationToken));
        Assert.False(e.Options.HasFlag(ExpressionOptions.UseNonRecursiveEvaluator));
    }

    [Theory]
    [InlineData("2 mod3")]
    [InlineData("2 div3")]
    [InlineData("true andValue")]
    public void KeywordsFollowedByIdentifierCharsShouldNotBeOperators(string expression)
    {
        var e = new Expression(expression);
        e.Parameters["mod3"] = 1;
        e.Parameters["div3"] = 1;
        e.Parameters["andValue"] = true;
        e.Parameters["Value"] = true;
        e.Parameters["3"] = 1;
        Assert.ThrowsAny<NCalcException>(() => e.Evaluate(TestContext.Current.CancellationToken));
    }

    public static TheoryData<string> NestedExpressionsWithAssignments => new()
    {
        new string('(', 1000) + "84" + new string(')', 1000),
        string.Concat(Enumerable.Repeat("Abs(", 200)) + "84" + new string(')', 200),
        string.Concat(Enumerable.Repeat("a[", 200)) + "0" + new string(']', 200),
        "b = " + string.Concat(Enumerable.Repeat("Abs(", 200)) + "84" + new string(')', 200),
    };

    [Theory]
    [MemberData(nameof(NestedExpressionsWithAssignments))]
    public void NestedExpressionsWithAssignmentsShouldParseInLinearTime(string expression)
    {
        // The assignment target used to be parsed again as an ordinary operand at every nesting level, which took exponential time.
        // A large stack keeps the parser's depth guard out of the way, so only the parsing time is measured.
        var cancellationToken = TestContext.Current.CancellationToken;
        Expression e = null!;
        bool hasErrors = true;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var thread = new Thread(() =>
        {
            e = new Expression(expression, ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences);
            hasErrors = e.HasErrors(cancellationToken);
        }, 64 * 1024 * 1024);
        thread.Start();
        thread.Join();
        stopwatch.Stop();
        Assert.False(hasErrors, e.Error?.InnerException?.Message ?? e.Error?.Message);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Parsing took {stopwatch.Elapsed}");
    }
}

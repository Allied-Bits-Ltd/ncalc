using System.Numerics;

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

    // The prefix, the opening part repeated at each nesting level, the innermost operand, the closing part and the nesting depth
    public static TheoryData<string, string, string, string, int> NestedExpressionsWithAssignments => new()
    {
        { "", "(", "84", ")", 1000 },
        { "", "Abs(", "84", ")", 200 },
        { "", "a[", "0", "]", 200 },
        { "b = ", "Abs(", "84", ")", 200 },
    };

    // NCALC-135: the test compares the times of depth n and 2n after a warm-up, so neither the first parse with the options (JIT,
    // building the parser) nor the load of a parallel test run decides the result
    [Theory]
    [MemberData(nameof(NestedExpressionsWithAssignments))]
    public void NestedExpressionsWithAssignmentsShouldParseInLinearTime(string prefix, string open, string operand, string close, int depth)
    {
        // The assignment target used to be parsed again as an ordinary operand at every nesting level, which took exponential time.
        // A large stack keeps the parser's depth guard out of the way, so only the parsing time is measured.
        var cancellationToken = TestContext.Current.CancellationToken;
        const ExpressionOptions options = ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.NoCache;

        string Build(int n) => prefix + string.Concat(Enumerable.Repeat(open, n)) + operand + string.Concat(Enumerable.Repeat(close, n));

        // The shortest of a few runs is the least affected by other work on the machine
        TimeSpan Measure(string expression)
        {
            var best = TimeSpan.MaxValue;
            for (var run = 0; run < 3; run++)
            {
                Expression e = null!;
                bool hasErrors = true;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var thread = new Thread(() =>
                {
                    e = new Expression(expression, options);
                    hasErrors = e.HasErrors(cancellationToken);
                }, 64 * 1024 * 1024);
                thread.Start();
                thread.Join();
                stopwatch.Stop();
                Assert.False(hasErrors, e.Error?.InnerException?.Message ?? e.Error?.Message);
                if (stopwatch.Elapsed < best)
                    best = stopwatch.Elapsed;
            }
            return best;
        }

        // Warm-up: builds the parser for these options and compiles the code paths
        Measure(Build(10));
        Measure(Build(depth));

        var timeN = Measure(Build(depth));
        var time2N = Measure(Build(2 * depth));

        // Linear parsing doubles the time; exponential parsing multiplied it by far more. The bound is generous on purpose
        Assert.True(time2N < TimeSpan.FromTicks(timeN.Ticks * 8) + TimeSpan.FromMilliseconds(100),
            $"Parsing depth {depth} took {timeN}, depth {2 * depth} took {time2N}");
    }

    public static TheoryData<string, ExpressionOptions, object> RadixLiteralData => new()
    {
        { "0xFFFFFFFFFFFFFFFF", ExpressionOptions.None, ulong.MaxValue },
        { "0xFFFFFFFFFFFFFFFF", ExpressionOptions.UseBigNumbers, ulong.MaxValue },
        { "0x8000000000000000", ExpressionOptions.None, 9223372036854775808UL },
        { "0x7FFFFFFFFFFFFFFF", ExpressionOptions.None, long.MaxValue },
        { "0xFFFFFFFF", ExpressionOptions.None, 4294967295L },
        { "0b1111111111111111111111111111111111111111111111111111111111111111", ExpressionOptions.None, ulong.MaxValue },
        { "0o1777777777777777777777", ExpressionOptions.None, ulong.MaxValue },
        { "0x10000000000000000", ExpressionOptions.UseBigNumbers, BigInteger.Parse("18446744073709551616", CultureInfo.InvariantCulture) },
    };

    [Theory]
    [MemberData(nameof(RadixLiteralData))]
    public void RadixLiteralsShouldNotWrapToNegativeValues(string expression, ExpressionOptions options, object expected)
    {
        Assert.Equal(expected, new Expression(expression, options | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void HexRunsShouldBeGuidsOnlyWhenTheyLookLikeGuids()
    {
        var number = new Expression("12345678901234567890123456789012", ExpressionOptions.UseBigNumbers | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(BigInteger.Parse("12345678901234567890123456789012", CultureInfo.InvariantCulture), number);

        var identifier = new Expression("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", ExpressionOptions.NoCache);
        identifier.Parameters["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"] = 7;
        Assert.Equal(7, identifier.Evaluate(TestContext.Current.CancellationToken));

        var subtraction = new Expression("deadbeef - cafe", ExpressionOptions.NoCache);
        subtraction.Parameters["deadbeef"] = 10;
        subtraction.Parameters["cafe"] = 3;
        Assert.Equal(7, subtraction.Evaluate(TestContext.Current.CancellationToken));

        var guid = Guid.Parse("deadbeef-cafe-babe-face-0123456789ab");
        Assert.Equal(guid, new Expression("deadbeef-cafe-babe-face-0123456789ab", ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(guid, new Expression("deadbeefcafebabeface0123456789ab", ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2 ** -x", 0.5)]
    [InlineData("2 ** -3 ** 2", 0.001953125)]
    [InlineData("2 ** - - 1", 2.0)]
    [InlineData("2 ** 3 ** 2", 512.0)]
    public void ExponentMayHaveAPrefixOperator(string expression, double expected)
    {
        var e = new Expression(expression, ExpressionOptions.NoCache);
        e.Parameters["x"] = 1;
        Assert.Equal(expected, Convert.ToDouble(e.Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture), 12);
    }

    [Fact]
    public void ExponentMayBeAPercentOrAnImaginaryNumber()
    {
        var percent = new Expression("2 ** 10%", ExpressionOptions.NoCache) { AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.CalculatePercent) };
        Assert.Equal(Math.Pow(2, 0.1), Convert.ToDouble(percent.Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture), 12);

        var complex = new Expression("2 ** 3i", ExpressionOptions.NoCache) { AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseComplexNumbers) };
        var result = ((ComplexNumber)complex.Evaluate(TestContext.Current.CancellationToken)!).ToComplex();
        var expected = System.Numerics.Complex.Pow(2, new System.Numerics.Complex(0, 3));
        Assert.Equal(expected.Real, result.Real, 12);
        Assert.Equal(expected.Imaginary, result.Imaginary, 12);
    }

    [Fact]
    public void PythonCommentMayEndTheText()
    {
        // NCALC-069: a '#' at the very end was not consumed
        Assert.Equal(3, new Expression("1 + 2 #", ExpressionOptions.SupportPythonComments).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(3, new Expression("1 + 2 # comment", ExpressionOptions.SupportPythonComments).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("3! == 6", true)]
    [InlineData("3! = 6", true)]
    [InlineData("3 != 6", true)]
    [InlineData("3! != 6", false)]
    public void FactorialMayBeCompared(string text, bool expected)
    {
        // NCALC-070
        Assert.Equal(expected, new Expression(text).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SmallestLongShouldBeParsedExactly()
    {
        // NCALC-073: the magnitude was read as a double and negated
        Assert.Equal(long.MinValue, new Expression("-9223372036854775808").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(long.MinValue, new Expression("-9223372036854775808", ExpressionOptions.UseBigNumbers).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(-9.2233720368547758E+18, new Expression("-9223372036854775808.0").Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RangeIndexShouldAcceptOnlyValidBounds()
    {
        // NCALC-074: [..] is the whole value; '^' needs a bound, and an index needs a value
        Assert.Equal("abc", new Expression("'abc'[..]").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal("bc", new Expression("'abcd'[1..^1]").Evaluate(TestContext.Current.CancellationToken));
        foreach (var text in new[] { "'abc'[^]", "'abc'[^..1]", "'abc'[]", "'abc'[1..^]" })
            Assert.True(new Expression(text).HasErrors(TestContext.Current.CancellationToken), text);
    }

    [Fact]
    public void DocumentedSyntaxShouldBeAccepted()
    {
        // NCALC-075: the up arrow is U+2191 (U+2291 is still accepted), and an ISO date may have the 'Z' designator
        Assert.Equal(8.0, new Expression("2 \u2191 3", ExpressionOptions.UseUnicodeCharsForOperations).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(8.0, new Expression("2 \u2291 3", ExpressionOptions.UseUnicodeCharsForOperations).Evaluate(TestContext.Current.CancellationToken));
        var date = (DateTime)new Expression("#2001-01-01Z#").Evaluate(TestContext.Current.CancellationToken)!;
        Assert.Equal(new DateTime(2001, 1, 1), date);
        Assert.Equal(DateTimeKind.Utc, date.Kind);
        Assert.Equal(-25m, Convert.ToDecimal(((NCalc.Domain.Percent)new Expression("PercentDiff(80;60)").Evaluate(TestContext.Current.CancellationToken)!).Value, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("true ? 1 : false ? 2 : 3", 1)]
    [InlineData("false ? 1 : true ? 2 : 3", 2)]
    [InlineData("false ? 1 : false ? 2 : 3", 3)]
    [InlineData("true ? false ? 1 : 2 : 3", 2)]
    public void NestedTernaryShouldNotNeedParentheses(string text, int expected)
    {
        // NCALC-076: the conditional operator is right-associative, as in C#
        Assert.Equal(expected, new Expression(text).Evaluate(TestContext.Current.CancellationToken));
    }

    private static long MeasureParseMilliseconds(string text, ExpressionOptions options)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        new Expression(text, options | ExpressionOptions.NoCache).HasErrors(TestContext.Current.CancellationToken);
        return stopwatch.ElapsedMilliseconds;
    }

    private static string NestedTuples(int depth)
    {
        var text = "1";
        for (int i = 0; i < depth; i++)
            text = "(" + text + ", " + i + ")";
        return text;
    }

    private static string NestedGroups(int depth, string content)
        => new string('(', depth) + content + new string(')', depth);

    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.UseAssignments)]
    [InlineData(ExpressionOptions.SupportPythonComments)]
    public void NestedListsAndFailingGroupsShouldParseInLinearTime(ExpressionOptions options)
    {
        // NCALC-102: each nesting level used to parse its content twice (as a group, then as a list), doubling the time per level.
        // Depth 30 took hours; the bound is generous and the 2n/n ratio is not checked tightly, so the test is not load-sensitive
        MeasureParseMilliseconds(NestedTuples(5), options); // warm up the parser cache

        Assert.True(MeasureParseMilliseconds(NestedTuples(30), options) < 5000);
        Assert.True(MeasureParseMilliseconds(NestedGroups(40, "#abc#"), options) < 5000);
        Assert.True(MeasureParseMilliseconds(NestedGroups(40, "#1/2/2024#"), options) < 5000);
        Assert.True(MeasureParseMilliseconds(NestedGroups(40, "Max((1, 2), #abc#)"), options) < 5000);

        long single = Math.Max(MeasureParseMilliseconds(NestedTuples(60), options), 1);
        long twice = MeasureParseMilliseconds(NestedTuples(120), options);
        Assert.True(twice < single * 20 + 500, $"depth 60: {single} ms, depth 120: {twice} ms");
    }

    [Fact]
    public void NestedListsAndGroupsShouldKeepTheirMeaning()
    {
        // NCALC-102: the memoized operands must not change what is parsed
        var tuple = (LogicalExpressionList)LogicalExpressionFactory.Create(NestedTuples(3), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, tuple.Count);
        Assert.IsType<LogicalExpressionList>(tuple[0]);

        Assert.Equal(7, new Expression("((((1 + 2)) * 2)) + 1").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(true, new Expression("2 in ((1), 2, (3))").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(3, new Expression("Max((1), Min((3), (4)))").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(6, new Expression("x := (1 + 2); x * (2)", ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences).Evaluate(TestContext.Current.CancellationToken));
        Assert.True(new Expression(NestedGroups(40, "#abc#"), ExpressionOptions.NoCache).HasErrors(TestContext.Current.CancellationToken));
        Assert.Equal(1, new Expression(NestedGroups(40, "1")).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("1 + 2 #see #3", ExpressionOptions.SupportPythonComments)]
    [InlineData("1 + 2 #todo: fix#", ExpressionOptions.SupportPythonComments)]
    [InlineData("1 #a#\n+ 2", ExpressionOptions.SupportPythonComments)]
    [InlineData("1 + 2 #v2#", ExpressionOptions.SupportPythonComments | ExpressionOptions.DontParseDates)]
    [InlineData("1 + 2 # see #3", ExpressionOptions.SupportPythonComments)]
    [InlineData("1 + 2 #99:99#", ExpressionOptions.SupportPythonComments)]
    public void PythonCommentWithASecondHashShouldBeAComment(string text, ExpressionOptions options)
    {
        // NCALC-106: '#' starts a date/time literal only when the literal is valid (and dates are parsed); otherwise it is a comment
        Assert.Equal(3, new Expression(text, options | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DateLiteralsShouldStillBeParsedWithPythonComments()
    {
        // NCALC-106
        var options = ExpressionOptions.SupportPythonComments | ExpressionOptions.NoCache;
        Assert.Equal(new DateTime(2001, 1, 1), new Expression("#2001-01-01# # the date", options).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(new TimeSpan(10, 30, 0), new Expression("#10:30# # the time #1#", options).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(new DateTime(2001, 1, 2), new Expression("#2001-01-01# + #1.00:00:00#", options | ExpressionOptions.SupportTimeOperations).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("'5 # five' = 5", ExpressionOptions.SupportPythonComments)]
    [InlineData("'/*x*/ 5' = 5", ExpressionOptions.SupportCStyleComments)]
    [InlineData("'5 // x' = 5", ExpressionOptions.SupportCStyleComments)]
    public void LiteralsInStringsShouldNotContainComments(string text, ExpressionOptions options)
    {
        // NCALC-129: a string compared with a number is converted only when it is a single literal, without comments
        var result = new Expression(text, options | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken);
        Assert.NotEqual(true, result);
    }

    [Fact]
    public void ThirtyTwoCharacterNumbersWithExponentShouldNotBeGuids()
    {
        // NCALC-129: digits with an exponent are a number, also when the run has the length of a GUID
        Assert.Equal(1E+30, new Expression("100000000000000000000000000000e1", ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(1E+30 + 1, new Expression("100000000000000000000000000000e1 + 1", ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(1E+34, new Expression("1000000000000000000000000000000e+4", ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));

        // A run that starts with a letter stays a GUID, as before; a parameter of this shape is written in brackets
        Assert.IsType<Guid>(new Expression("abcdef11111111111111111111111111", ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        var identifier = new Expression("[abcdef11111111111111111111111111] + 1", ExpressionOptions.NoCache);
        identifier.Parameters["abcdef11111111111111111111111111"] = 2;
        Assert.Equal(3, identifier.Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-117: ToString() writes a time span with a fraction of a second, so the parser reads one
    [Theory]
    [InlineData("#00:00:01.5#", 15_000_000L)]
    [InlineData("#1.02:03:04.25#", 937_842_500_000L)]
    [InlineData("#-00:00:00.0000001#", -1L)]
    [InlineData("#-1.00:00:01.123456789#", -864_011_234_567L)]
    [InlineData("#25:00:00.5#", 900_005_000_000L)]
    public void TimeSpanLiteralShouldAcceptAFractionOfASecond(string expression, long ticks)
    {
        Assert.Equal(TimeSpan.FromTicks(ticks), new Expression(expression, ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        var value = new ValueExpression(TimeSpan.FromTicks(ticks));
        Assert.Equal(TimeSpan.FromTicks(ticks), new Expression(value.ToString(), ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-204: whitespace and a word starting with "e", or a separator after a space, made an integer literal a double
    [Fact]
    public void IntegerLiteralShouldStayAnIntegerBeforeWhitespace()
    {
        Assert.IsType<int>(new Expression("if (true) 5 else 6", ExpressionOptions.NoCache | ExpressionOptions.UseIfStatement).Evaluate(TestContext.Current.CancellationToken));
        Assert.IsType<decimal>(new Expression("if (true) 5.5 else 6", ExpressionOptions.NoCache | ExpressionOptions.UseIfStatement | ExpressionOptions.DecimalAsDefault).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(int.MinValue, new Expression("x := if (true) 2147483647 else 0; x + 1",
            ExpressionOptions.NoCache | ExpressionOptions.UseIfStatement | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences).Evaluate(TestContext.Current.CancellationToken));

        // The comma is the decimal separator of sk-SK and the list separator; ToString() writes "1 , 2" in this mode
        var sk = CultureInfo.GetCultureInfo("sk-SK");
        var commaOptions = new AdvancedExpressionOptions(sk)
        {
            ListItemSeparator = AdvancedExpressionOptions.ListItemSeparatorKind.Comma,
            DecimalSeparatorType = AdvancedExpressionOptions.SeparatorType.FromCulture
        };
        var items = (IEnumerable<object?>)new Expression("(1 , 2)", ExpressionOptions.NoCache, sk) { AdvancedOptions = commaOptions }.Evaluate(TestContext.Current.CancellationToken)!;
        Assert.All(items, item => Assert.IsType<int>(item));
        Assert.Equal(int.MinValue, new Expression("Max(2147483647 , 1) + 1", ExpressionOptions.NoCache, sk) { AdvancedOptions = commaOptions }.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(1.5, new Expression("Max(1,5 , 1)", ExpressionOptions.NoCache, sk) { AdvancedOptions = commaOptions }.Evaluate(TestContext.Current.CancellationToken));

        var spaceOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture) { ListItemSeparator = AdvancedExpressionOptions.ListItemSeparatorKind.Space };
        var spaced = new Expression("(5 e)", ExpressionOptions.NoCache, CultureInfo.InvariantCulture) { AdvancedOptions = spaceOptions };
        spaced.Parameters["e"] = 1;
        Assert.IsType<int>(((IEnumerable<object?>)spaced.Evaluate(TestContext.Current.CancellationToken)!).First());

        // An exponent directly after the digits still makes a double
        Assert.Equal(1200.5, new Expression("1e3 + 2E+2 + 5e-1", ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-207: the year of a date literal was read in the calendar of the thread culture (Thai Buddhist, Um Al-Qura)
    [Theory]
    [InlineData("th-TH")]
    [InlineData("ar-SA")]
    [InlineData("en-US")]
    public void DateLiteralsShouldNotDependOnTheCalendarOfTheThreadCulture(string threadCulture)
    {
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(threadCulture);
            foreach (var options in new[] { ExpressionOptions.NoCache, ExpressionOptions.None })
            {
                Assert.Equal(new DateTime(2024, 1, 2), new Expression("#1/2/2024#", options, CultureInfo.InvariantCulture).Evaluate(TestContext.Current.CancellationToken));
                Assert.Equal(new DateTime(2024, 1, 2, 10, 30, 0), new Expression("#1/2/2024 10:30#", options, CultureInfo.InvariantCulture).Evaluate(TestContext.Current.CancellationToken));
                Assert.Equal(new DateTime(2024, 1, 2, 22, 30, 15), new Expression("#1/2/2024 10:30:15 PM#", options, CultureInfo.GetCultureInfo("en-US")).Evaluate(TestContext.Current.CancellationToken));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    // NCALC-208: any operand was accepted as the target of an assignment, and the assignment returned the right-hand side
    [Theory]
    [InlineData("5 = x", ExpressionOptions.UseCStyleAssignments)]
    [InlineData("(x + 1) = 4", ExpressionOptions.UseCStyleAssignments)]
    [InlineData("(x + 1) := 4", ExpressionOptions.None)]
    [InlineData("5 := 7", ExpressionOptions.None)]
    [InlineData("'abc' := 7", ExpressionOptions.None)]
    [InlineData("Abs(x) := 7", ExpressionOptions.None)]
    [InlineData("5 += 1", ExpressionOptions.None)]
    [InlineData("Abs(x) -= 1", ExpressionOptions.None)]
    [InlineData("(x + 1)[0] *= 2", ExpressionOptions.None)]
    public async Task AssignmentToAnOperandThatIsNotAVariableShouldBeAnError(string expression, ExpressionOptions options)
    {
        options |= ExpressionOptions.NoCache | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences;
        Assert.Throws<NCalcParserException>(() => LogicalExpressionFactory.Create(expression, null, CultureInfo.InvariantCulture, options));

        foreach (var evaluatorOptions in new[] { options, options | ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var sync = new Expression(expression, evaluatorOptions);
            sync.Parameters["x"] = 3;
            Assert.ThrowsAny<NCalcException>(() => sync.Evaluate(TestContext.Current.CancellationToken));

            var async = new AsyncExpression(expression, evaluatorOptions);
            async.Parameters["x"] = 3;
            await Assert.ThrowsAnyAsync<NCalcException>(async () => await async.EvaluateAsync(TestContext.Current.CancellationToken));
        }
    }

    // NCALC-208: names and indexed names are still assigned to, and a comparison is not mistaken for an assignment
    [Theory]
    [InlineData("x = 5", ExpressionOptions.UseCStyleAssignments, 5)]
    [InlineData("5 == x", ExpressionOptions.UseCStyleAssignments, false)]
    [InlineData("x := 5", ExpressionOptions.None, 5)]
    [InlineData("x += 1", ExpressionOptions.None, 4)]
    [InlineData("5 = x", ExpressionOptions.None, false)]
    [InlineData("[x] := 7", ExpressionOptions.None, 7)]
    [InlineData("l[1] := 9; l[1]", ExpressionOptions.None, 9)]
    public void AssignmentToAVariableShouldStillWork(string expression, ExpressionOptions options, object expected)
    {
        var e = new Expression(expression, options | ExpressionOptions.NoCache | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences);
        e.Parameters["x"] = 3;
        e.Parameters["l"] = new List<object?> { 1, 2 };
        Assert.Equal(expected, e.Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-229: under a culture whose time separator is '.', no time span literal could be written, not even the #hh:mm:ss# of ToString()
    [Theory]
    [InlineData("#10.30#", 0, 10, 30, 0)]
    [InlineData("#10.30.00#", 0, 10, 30, 0)]
    [InlineData("#1.10.30.00#", 1, 10, 30, 0)]
    [InlineData("#25.00.00#", 0, 25, 0, 0)]
    [InlineData("#10:30#", 0, 10, 30, 0)]
    [InlineData("#10:30:00#", 0, 10, 30, 0)]
    [InlineData("#1.02:03:04#", 1, 2, 3, 4)]
    public void TimeSpanLiteralsShouldBeReadWhereTheTimeSeparatorIsADot(string expression, int days, int hours, int minutes, int seconds)
    {
        var fi = CultureInfo.GetCultureInfo("fi-FI");
        Assert.Equal(".", fi.DateTimeFormat.TimeSeparator);
        Assert.Equal(new TimeSpan(days, hours, minutes, seconds), new Expression(expression, ExpressionOptions.NoCache, fi).Evaluate(TestContext.Current.CancellationToken));

        var written = new ValueExpression(new TimeSpan(days, hours, minutes, seconds)).ToString();
        Assert.Equal(new TimeSpan(days, hours, minutes, seconds), new Expression(written, ExpressionOptions.NoCache, fi).Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-229: dates of fi-FI, whose date separator is also '.', are read as before
    [Fact]
    public void DatesShouldStillBeReadWhereTheTimeSeparatorIsADot()
    {
        var fi = CultureInfo.GetCultureInfo("fi-FI");
        Assert.Equal(new DateTime(2024, 1, 2), new Expression("#02.01.2024#", ExpressionOptions.NoCache, fi).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(new DateTime(2030, 10, 1), new Expression("#1.10.30#", ExpressionOptions.NoCache, fi).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(new DateTime(2024, 1, 2, 10, 30, 0), new Expression("#02.01.2024 10.30.00#", ExpressionOptions.NoCache, fi).Evaluate(TestContext.Current.CancellationToken));
        Assert.True(new Expression("#13.13.2222#", ExpressionOptions.NoCache, fi).HasErrors(TestContext.Current.CancellationToken));
    }

    // NCALC-231: "///" that is not a description of a function is an ordinary comment
    [Theory]
    [InlineData("1 + 2 /// c")]
    [InlineData("1 /// c\n + 2")]
    [InlineData("/// c\n1 + 2")]
    [InlineData("/// doc\nfn f(a) => a + 2; f(1)")]
    [InlineData("/// doc\n\n///  more\nfn f(a) => a + 2; f(1)")]
    public void TripleSlashShouldBeACommentUnlessItDescribesAFunction(string expression)
    {
        var e = new Expression(expression, ExpressionOptions.NoCache | ExpressionOptions.SupportCStyleComments | ExpressionOptions.UseStatementSequences);
        Assert.Equal(3, e.Evaluate(TestContext.Current.CancellationToken));
    }

    // NCALC-243: the errors of an unknown operator sequence and of an invalid date literal tell where they are
    [Fact]
    public void ParseErrorsShouldHaveAPosition()
    {
        var unknown = Assert.Throws<NCalcParserException>(() => LogicalExpressionFactory.Create("1 + * 2", null, CultureInfo.InvariantCulture));
        Assert.Contains("Unknown operator sequence at position (1:6)", unknown.Message, StringComparison.Ordinal);
        unknown = Assert.Throws<NCalcParserException>(() => LogicalExpressionFactory.Create("1 = = 2", null, CultureInfo.InvariantCulture));
        Assert.Contains("Unknown operator sequence at position", unknown.Message, StringComparison.Ordinal);

        var invalidDate = Assert.Throws<FormatException>(() => LogicalExpressionFactory.Create("#2024-13-01#", null, CultureInfo.InvariantCulture));
        Assert.Contains("at position (1:12)", invalidDate.Message, StringComparison.Ordinal);
    }
}

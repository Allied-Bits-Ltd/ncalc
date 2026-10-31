using NCalc.Domain;
using NCalc.Factories;

namespace NCalc.Tests;

[Trait("Category", "Operators")]
public class OperatorsTests : TestBase
{
    [Theory]
    [InlineData("NOT true")]
    [InlineData("not true")]
    public void Should_Evaluate_Not_Unary_Operator(string expression)
    {
        var logicalExpression = LogicalExpressionFactory.Create(expression, cancellationToken: TestContext.Current.CancellationToken);
        var expr = new Expression(logicalExpression);
        Assert.False((bool)expr.Evaluate(TestContext.Current.CancellationToken)!);
    }

    [Theory]
    [InlineData("!true", false)]
    [InlineData("not false", true)]
    [InlineData("Not false", true)]
    [InlineData("NOT false", true)]
    [InlineData("-10", -10)]
    [InlineData("+20", 20)]
    [InlineData("2**-1", 0.5)]
    [InlineData("2**+2", 4.0)]
    [InlineData("2 * 3", 6)]
    [InlineData("6 / 2", 3d)]
    [InlineData("7 % 2", 1)]
    [InlineData("2 + 3", 5)]
    [InlineData("2 - 1", 1)]
    [InlineData("1 < 2", true)]
    [InlineData("1 > 2", false)]
    [InlineData("1 <= 2", true)]
    [InlineData("1 <= 1", true)]
    [InlineData("1 >= 2", false)]
    [InlineData("1 >= 1", true)]
    [InlineData("1 = 1", true)]
    [InlineData("1 == 1", true)]
    [InlineData("1 != 1", false)]
    [InlineData("1 <> 1", false)]
    [InlineData("1 & 1", 1UL)]
    [InlineData("1 | 1", 1UL)]
    [InlineData("1 ^ 1", 0UL)]
    [InlineData("~1", ~1UL)]
    [InlineData("2 >> 1", 1)]
    [InlineData("2 << 1", 4)]
    [InlineData("true && false", false)]
    [InlineData("True and False", false)]
    [InlineData("tRue aNd faLse", false)]
    [InlineData("TRUE ANd fALSE", false)]
    [InlineData("true AND FALSE", false)]
    [InlineData("true || false", true)]
    [InlineData("true or false", true)]
    [InlineData("true Or false", true)]
    [InlineData("true OR false", true)]
    [InlineData("if(true, 0, 1)", 0)]
    [InlineData("if(false, 0, 1)", 1)]
    public void ShouldEvaluateOperators(string expression, object expected)
    {
        var result = new Expression(expression).Evaluate(TestContext.Current.CancellationToken);
        CheckResult(expected, result);
    }

    [Theory]
    [InlineData("2+2+2+2", 8)]
    [InlineData("2*2*2*2", 16)]
    [InlineData("2*2+2", 6)]
    [InlineData("2+2*2", 6)]
    [InlineData("1 + 2 + 3 * 4 / 2", 9d)]
    [InlineData("18/2/2*3", 13.5)]
    [InlineData("-1 ** 2", -1d)]
    [InlineData("(-1) ** 2", 1d)]
    [InlineData("2 ** 3 ** 2", 512d)]
    [InlineData("(2 ** 3) ** 2", 64d)]
    [InlineData("2**2**2**2", 65536d)]
    [InlineData("3 ** 2 ** 2 ** 0", 9d)]
    [InlineData("2 ** 2 ** 2 ** 0 ** 5", 4d)]
    [InlineData("((2 ** 2) ** 2) ** 2", 256d)]
    [InlineData("2 * 3 ** 2", 18d)]
    [InlineData("2 ** 4 / 2", 8d)]
    public void ShouldHandleOperatorsPriority(string expression, object expected)
    {
        CheckResult(expected, new Expression(expression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2 in (1, 2, 3)", true)]
    [InlineData("4 in (1, 2, 3)", false)]
    [InlineData("2 not in (1, 2, 3)", false)]
    [InlineData("4 not in (1, 2, 3)", true)]
    [InlineData("'a' in 'abc'", true)]
    [InlineData("'d' in 'abc'", false)]
    [InlineData("'b' not in 'abc'", false)]
    public async Task InOperatorShouldGiveTheSameResultInAllEvaluators(string expression, bool expected)
    {
        foreach (var options in new[] { ExpressionOptions.NoCache, ExpressionOptions.NoCache | ExpressionOptions.UseNonRecursiveEvaluator })
        {
            Assert.Equal(expected, new Expression(expression, options).Evaluate(TestContext.Current.CancellationToken));
            Assert.Equal(expected, await new AsyncExpression(expression, options).EvaluateAsync(TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData("(1+2i)/(3-4i)", "-0.2 + 0.4i")]
    [InlineData("(1+2i)*(3-4i)", "11 + 2i")]
    [InlineData("c /= (1-1i)", "0.5 + 0.5i")]
    [InlineData("[2;4] / 2", "[1, 2]")]
    [InlineData("t * 2", "02:00:00")]
    [InlineData("t / 2", "00:30:00")]
    [InlineData("2147483647 * 2", "4294967294 Int64")]
    [InlineData("x /= 2", "2 Double")]
    [InlineData("x *= 2147483647", "8589934588 Int64")]
    public async Task MultiplyAndDivideShouldGiveTheSameResultInAllEvaluators(string expression, string expected)
    {
        static void Prepare<T>(ExpressionBase<T> e) where T : ExpressionContextBase, new()
        {
            e.AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseComplexNumbers | AdvExpressionOptions.ParseVectors);
            e.Parameters["c"] = 1;
            e.Parameters["t"] = TimeSpan.FromHours(1);
            e.Parameters["x"] = 4;
        }

        static string Show(object? result) => result switch
        {
            ComplexNumber cn => $"{cn.ToComplex().Real.ToString(CultureInfo.InvariantCulture)} {(cn.ToComplex().Imaginary < 0 ? "-" : "+")} {Math.Abs(cn.ToComplex().Imaginary).ToString(CultureInfo.InvariantCulture)}i",
            NCalc.Domain.Vector or TimeSpan => Convert.ToString(result, CultureInfo.InvariantCulture)!,
            _ => $"{Convert.ToString(result, CultureInfo.InvariantCulture)} {result?.GetType().Name}"
        };

        foreach (var nonRecursive in new[] { false, true })
        {
            var options = ExpressionOptions.NoCache | ExpressionOptions.SupportTimeOperations | ExpressionOptions.UseAssignments
                          | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);

            var syncExpression = new Expression(expression, options);
            Prepare(syncExpression);
            Assert.Equal(expected, Show(syncExpression.Evaluate(TestContext.Current.CancellationToken)));

            var asyncExpression = new AsyncExpression(expression, options);
            Prepare(asyncExpression);
            Assert.Equal(expected, Show(await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken)));
        }
    }

    [Fact]
    public void Should_Compare_Bool_Issue_122()
    {
        var eif = new Expression("foo = true");
        eif.Parameters["foo"] = "true";

        Assert.Equal(true, eif.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_Use_Correct_BitwiseXOr_133()
    {
        var logicalExpression = LogicalExpressionFactory.Create(expression: "1 ^ 2", cancellationToken: TestContext.Current.CancellationToken);

        var serializedString = logicalExpression.ToString();

        Assert.Equal("1 ^ 2", serializedString);
        Assert.Equal(3UL, new Expression(logicalExpression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("1 | 1", 1)]
    [InlineData("1 | 0", 1)]
    [InlineData("1 ^ 1", 0)]
    [InlineData("1 ^ 0", 1)]
    [InlineData("1 & 1", 1)]
    [InlineData("1 & 0", 0)]
    [InlineData("1 ^ 0 ^ 1", 0)]
    public void Should_Handle_Bitwise_Ops(string expr, ulong expected)
    {
        object? result = new Expression(expr).Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Should_Use_Correct_BitwiseXOr_Precedence_540()
    {
        string expr = "1 = 1 ^ 2 = 2 || 3 = 3";
        Assert.Equal(1 == 1 ^ 2 == 2 || 3 == 3, new Expression(expr).Evaluate(TestContext.Current.CancellationToken));

        expr = "1 = 1 ^ 2 = 2 && 2 = 1";
        Assert.Equal(1 == 1 ^ 2 == 2 && 2 == 1, new Expression(expr).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("([a] != 0) && ([b]/[a]>2)", false)]
    [InlineData("([a] == 0) || ([b]/[a]>2)", true)]
    public void Should_Short_Circuit_Boolean_Expressions(string expression, bool expected)
    {
        var e = new Expression(expression);
        e.Parameters["a"] = 0;

        Assert.Equal(expected, (bool)e.Evaluate(TestContext.Current.CancellationToken)!);
    }
}
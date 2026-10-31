using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Factories;
using NCalc.Helpers;

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

    [Fact]
    public async Task LikeShouldNotBacktrackExponentially()
    {
        // A regular expression took minutes for this pattern and a 200-character value
        var e = new Expression("x LIKE '%a%a%a%a%a%b'", ExpressionOptions.NoCache);
        e.Parameters["x"] = new string('a', 200);
        var evaluation = Task.Run(() => e.Evaluate(), TestContext.Current.CancellationToken);
        var finished = await Task.WhenAny(evaluation, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.True(finished == evaluation, "LIKE did not complete in 5 s");
        Assert.Equal(false, await evaluation);
    }

    [Theory]
    [InlineData("'abc' LIKE 'a%'", true)]
    [InlineData("'abc' LIKE 'A_C'", false)]
    [InlineData("'abcabc' LIKE '%bc'", true)]
    [InlineData("'ab' LIKE 'a%b%'", true)]
    [InlineData("'' LIKE '%'", true)]
    [InlineData("'' LIKE '_'", false)]
    [InlineData("'a.c' LIKE 'a.c'", true)]
    [InlineData("'abc' LIKE 'a.c'", false)]
    [InlineData("'a(c' LIKE 'a(_'", true)]
    public void LikeShouldMatchWildcards(string expression, bool expected)
    {
        Assert.Equal(expected, new Expression(expression, ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(expected, new Expression(expression, ExpressionOptions.NoCache).ToLambda<bool>()());
    }

    [Theory]
    [InlineData("0!", ExpressionOptions.None, "1")]
    [InlineData("5!!", ExpressionOptions.None, "15")]
    [InlineData("20!", ExpressionOptions.None, "2432902008176640000")]
    [InlineData("21!", ExpressionOptions.None, "5.109094217170944E+19")]
    [InlineData("21!", ExpressionOptions.OverflowProtection, "5.109094217170944E+19")]
    [InlineData("21!", ExpressionOptions.UseBigNumbers, "51090942171709440000")]
    [InlineData("0!", ExpressionOptions.UseBigNumbers, "1")]
    public void FactorialShouldNotWrapAround(string expression, ExpressionOptions options, string expected)
    {
        var result = new Expression(expression, options | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, Convert.ToString(result, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("(-3)!", ExpressionOptions.None)]
    [InlineData("(-3)!", ExpressionOptions.UseBigNumbers)]
    [InlineData("1000000!", ExpressionOptions.UseBigNumbers)]
    public void FactorialShouldRejectNegativeAndHugeArguments(string expression, ExpressionOptions options)
    {
        Assert.Throws<NCalcEvaluationException>(() => new Expression(expression, options | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    public static TheoryData<string, ExpressionOptions, object?, object?, object> ReducedResultTypeData => new()
    {
        { "Pow(2, 63)", ExpressionOptions.DecimalAsDefault, null, null, 9223372036854775808UL },
        { "Pow(2, 63)", ExpressionOptions.UseBigNumbers, null, null, 9223372036854775808UL },
        { "Pow(3, 40)", ExpressionOptions.DecimalAsDefault, null, null, 12157665459056928801UL },
        { "1 << 63", ExpressionOptions.None, null, null, 9223372036854775808UL },
        { "a + 0", ExpressionOptions.ReduceArithmeticResultType, ulong.MaxValue, null, ulong.MaxValue },
        { "a - b", ExpressionOptions.None, (byte)0, (byte)1, -1 },
        { "a - b", ExpressionOptions.None, (ushort)0, (ushort)1, -1 },
        { "a - b", ExpressionOptions.None, (sbyte)0, 1u, -1L },
        { "a * b", ExpressionOptions.None, (ushort)65535, (ushort)65535, 4294836225u },
        { "a * b", ExpressionOptions.None, uint.MaxValue, uint.MaxValue, 18446744065119617025UL },
    };

    [Theory]
    [MemberData(nameof(ReducedResultTypeData))]
    public void ArithmeticResultShouldKeepItsValueWhenItsTypeIsReduced(string expression, ExpressionOptions options, object? a, object? b, object expected)
    {
        var e = new Expression(expression, options | ExpressionOptions.NoCache);
        e.Parameters["a"] = a;
        e.Parameters["b"] = b;
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("5 % 0", ExpressionOptions.None)]
    [InlineData("5 // 0", ExpressionOptions.None)]
    [InlineData("5 div 0", ExpressionOptions.None)]
    [InlineData("5 % 0", ExpressionOptions.UseBigNumbers)]
    [InlineData("5 / 0", ExpressionOptions.UseBigNumbers)]
    [InlineData("a / b", ExpressionOptions.None)]
    [InlineData("a % b", ExpressionOptions.None)]
    public void DivisionByZeroShouldBeReportedAsSuch(string expression, ExpressionOptions options)
    {
        // It was reported as "Operator '%' is not implemented for operands of types Int32 and Int32"
        var e = new Expression(expression, options | ExpressionOptions.NoCache);
        e.Parameters["a"] = 5m;
        e.Parameters["b"] = 0m;
        Assert.Throws<DivideByZeroException>(() => e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void FloatDividedByDecimalShouldWork()
    {
        var e = new Expression("a / b", ExpressionOptions.NoCache);
        e.Parameters["a"] = 3f;
        e.Parameters["b"] = 1.5m;
        Assert.Equal(2m, e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("-5 // 2", ExpressionOptions.None, -3)]
    [InlineData("-5 // 2", ExpressionOptions.DecimalAsDefault, -3)]
    [InlineData("-5 // 2", ExpressionOptions.UseBigNumbers, -3)]
    [InlineData("-5 div 2", ExpressionOptions.UseBigNumbers, -2)]
    [InlineData("a div 2", ExpressionOptions.UseBigNumbers, -2)]
    [InlineData("a // 2", ExpressionOptions.UseBigNumbers, -3)]
    [InlineData("b // 2", ExpressionOptions.UseBigNumbers, 3)]
    public void IntegerDivisionShouldGiveAnIntegerForBigDecimals(string expression, ExpressionOptions options, int expected)
    {
        var e = new Expression(expression, options | ExpressionOptions.NoCache);
        e.Parameters["a"] = new ExtendedNumerics.BigDecimal(-5);
        e.Parameters["b"] = new ExtendedNumerics.BigDecimal(new System.Numerics.BigInteger(75), -1);
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        Assert.True(MathHelper.IsBoxedIntegerNumberOrBigNumber(result) && result is not ExtendedNumerics.BigDecimal, $"{result} ({result?.GetType().Name}) is not an integer");
        Assert.Equal(expected, Convert.ToInt32(result is System.Numerics.BigInteger bi ? (int)bi : result, CultureInfo.InvariantCulture));
    }
}
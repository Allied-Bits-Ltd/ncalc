using System.Numerics;

using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Helpers;
using NCalc.Tests.TestData;

using Assert = Xunit.Assert;

namespace NCalc.Tests;

[Trait("Category", "Math")]
public class MathsTests : TestBase
{
    [Theory]
    [ClassData(typeof(BuiltInFunctionsTestData))]
    public void BuiltInFunctions_Test(string expression, object expected, double? tolerance)
    {
        var result = new Expression(expression).Evaluate(TestContext.Current.CancellationToken);

        if (tolerance.HasValue)
        {
            Assert.Equal((double)expected, (double)result, precision: 15);
        }
        else
        {
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void Should_Modulo_All_Numeric_Types_Issue_58()
    {
        // https://github.com/ncalc/ncalc/issues/58
        const int expectedResult = 0;
        const string operand = "%";
        const int lhsValue = 50;
        const int rhsValue = 50;

        var allTypes = new List<TypeCode>()
        {
            TypeCode.Boolean, TypeCode.Byte, TypeCode.SByte, TypeCode.Int16, TypeCode.UInt16, TypeCode.Int32,
            TypeCode.UInt32, TypeCode.Int64, TypeCode.UInt64, TypeCode.Single, TypeCode.Double, TypeCode.Decimal
        };

        var shouldNotWork = new Dictionary<TypeCode, List<TypeCode>>
        {
            [TypeCode.Boolean] = allTypes,
            [TypeCode.Byte] = [TypeCode.Boolean],
            [TypeCode.SByte] = [TypeCode.Boolean],
            [TypeCode.Int16] = [TypeCode.Boolean],
            [TypeCode.UInt16] = [TypeCode.Boolean],
            [TypeCode.Int32] =  [TypeCode.Boolean],
            [TypeCode.UInt32] = [TypeCode.Boolean],
            [TypeCode.Int64] = [TypeCode.Boolean],
            [TypeCode.UInt64] = [TypeCode.Boolean],
            [TypeCode.Single] = [TypeCode.Boolean],
            [TypeCode.Double] = [TypeCode.Boolean],
            [TypeCode.Decimal] = [TypeCode.Boolean]
        };

        // These should all work and return a value
        foreach (var typecodeA in allTypes)
        {
            var toTest = allTypes.Except(shouldNotWork[typecodeA]);
            foreach (var typecodeB in toTest)
            {
                const string expr = $"x {operand} y";
                try
                {
                    var result = new Expression(expr, CultureInfo.InvariantCulture)
                    {
                        Parameters =
                            {
                                ["x"] = Convert.ChangeType(lhsValue, typecodeA),
                                ["y"] = Convert.ChangeType(rhsValue, typecodeB)
                            }
                    }
                        .Evaluate(TestContext.Current.CancellationToken);
                    Assert.True(Convert.ToInt64(result) == expectedResult,
                        $"{expr}: {typecodeA} = {lhsValue}, {typecodeB} = {rhsValue} should return {expectedResult}");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"{expr}: {typecodeA}, {typecodeB} should not throw an exception but {ex} was thrown");
                }
            }

            // These should throw exceptions
            foreach (var typecodeB in shouldNotWork[typecodeA])
            {
                const string expr = $"x {operand} y";
                Assert.Throws<InvalidOperationException>(() => new Expression(expr, CultureInfo.InvariantCulture)
                {
                    Parameters =
                            {
                                ["x"] = Convert.ChangeType(lhsValue, typecodeA),
                                ["y"] = Convert.ChangeType(rhsValue, typecodeB)
                            }
                }
                        .Evaluate(TestContext.Current.CancellationToken));
            }
        }
    }

    [Fact]
    public void Should_Add_All_Numeric_Types_Issue_58()
    {
        // https://github.com/ncalc/ncalc/issues/58
        const int expectedResult = 100;
        const string operand = "+";
        const string lhsValue = "50";
        const string rhsValue = "50";

        var allTypes = new List<TypeCode>()
        {
            TypeCode.Boolean, TypeCode.Byte, TypeCode.SByte, TypeCode.Int16, TypeCode.UInt16, TypeCode.Int32,
            TypeCode.UInt32, TypeCode.Int64, TypeCode.UInt64, TypeCode.Single, TypeCode.Double, TypeCode.Decimal
        };

        var shouldNotWork = new Dictionary<TypeCode, List<TypeCode>>
        {
            [TypeCode.Boolean] = allTypes,
            [TypeCode.Byte] = [TypeCode.Boolean],
            [TypeCode.SByte] = [TypeCode.Boolean],
            [TypeCode.Int16] = [TypeCode.Boolean],
            [TypeCode.UInt16] = [TypeCode.Boolean],
            [TypeCode.Int32] = [TypeCode.Boolean],
            [TypeCode.UInt32] = [TypeCode.Boolean],
            [TypeCode.Int64] = [TypeCode.Boolean],
            [TypeCode.UInt64] = [TypeCode.Boolean],
            [TypeCode.Single] = [TypeCode.Boolean],
            [TypeCode.Double] = [TypeCode.Boolean],
            [TypeCode.Decimal] = [TypeCode.Boolean]
        };

        // These should all work and return a value
        foreach (var typecodeA in allTypes)
        {
            var toTest = allTypes.Except(shouldNotWork[typecodeA]);
            foreach (var typecodeB in toTest)
            {
                const string expr = $"x {operand} y";
                try
                {
                    var result = new Expression(expr, CultureInfo.InvariantCulture)
                    {
                        Parameters =
                            {
                                ["x"] = Convert.ChangeType(lhsValue, typecodeA),
                                ["y"] = Convert.ChangeType(rhsValue, typecodeB)
                            }
                    }
                        .Evaluate(TestContext.Current.CancellationToken);
                    Assert.True(Convert.ToInt64(result) == expectedResult,
                        $"{expr}: {typecodeA} = {lhsValue}, {typecodeB} = {rhsValue} should return {expectedResult}");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"{expr}: {typecodeA}, {typecodeB} should not throw an exception but {ex} was thrown");
                }
            }

            // These should throw exceptions

            foreach (var typecodeB in shouldNotWork[typecodeA])
            {
                const string expr = $"x {operand} y";
                Assert.Throws<InvalidOperationException>(() => new Expression(expr, CultureInfo.InvariantCulture)
                {
                    Parameters =
                            {
                                ["x"] = Convert.ChangeType(1, typecodeA),
                                ["y"] = Convert.ChangeType(1, typecodeB)
                            }
                }
                        .Evaluate(TestContext.Current.CancellationToken));
            }
        }
    }

    [Fact]
    public void Should_Subtract_All_Numeric_Types_Issue_58()
    {
        // https://github.com/ncalc/ncalc/issues/58
        const int expectedResult = 0;
        const string operand = "-";
        const int lhsValue = 50;
        const int rhsValue = 50;

        var allTypes = new List<TypeCode>()
        {
            TypeCode.Boolean, TypeCode.Byte, TypeCode.SByte, TypeCode.Int16, TypeCode.UInt16, TypeCode.Int32,
            TypeCode.UInt32, TypeCode.Int64, TypeCode.UInt64, TypeCode.Single, TypeCode.Double, TypeCode.Decimal
        };

        var shouldNotWork = new Dictionary<TypeCode, List<TypeCode>>
        {
            [TypeCode.Boolean] = allTypes,
            [TypeCode.Byte] = [TypeCode.Boolean],
            [TypeCode.SByte] = [TypeCode.Boolean],
            [TypeCode.Int16] = [TypeCode.Boolean],
            [TypeCode.UInt16] = [TypeCode.Boolean],
            [TypeCode.Int32] = [TypeCode.Boolean],
            [TypeCode.UInt32] = [TypeCode.Boolean],
            [TypeCode.Int64] = [TypeCode.Boolean],
            [TypeCode.UInt64] = [TypeCode.Boolean],
            [TypeCode.Single] = [TypeCode.Boolean],
            [TypeCode.Double] = [TypeCode.Boolean],
            [TypeCode.Decimal] = [TypeCode.Boolean]
        };

        // These should all work and return a value
        foreach (var typecodeA in allTypes)
        {
            var toTest = allTypes.Except(shouldNotWork[typecodeA]);
            foreach (var typecodeB in toTest)
            {
                const string expr = $"x {operand} y";
                try
                {
                    var result = new Expression(expr, CultureInfo.InvariantCulture)
                    {
                        Parameters =
                            {
                                ["x"] = Convert.ChangeType(lhsValue, typecodeA),
                                ["y"] = Convert.ChangeType(rhsValue, typecodeB)
                            }
                    }
                        .Evaluate(TestContext.Current.CancellationToken);
                    Assert.True(Convert.ToInt64(result) == expectedResult,
                        $"{expr}: {typecodeA} = {lhsValue}, {typecodeB} = {rhsValue} should return {expectedResult}");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"{expr}: {typecodeA}, {typecodeB} should not throw an exception but {ex} was thrown");
                }
            }

            // These should throw exceptions

            foreach (var typecodeB in shouldNotWork[typecodeA])
            {
                const string expr = $"x {operand} y";
                Assert.Throws<InvalidOperationException>(() => new Expression(expr, CultureInfo.InvariantCulture)
                {
                    Parameters =
                        {
                                ["x"] = Convert.ChangeType(lhsValue, typecodeA),
                                ["y"] = Convert.ChangeType(rhsValue, typecodeB)
                        }
                }.Evaluate(TestContext.Current.CancellationToken));
            }
        }
    }

    [Fact]
    public void IncorrectCalculation_NCalcAsync_Issue_4()
    {
        Expression e = new Expression("(1604326026000-1604325747000)/60000");
        var evalutedResult = e.Evaluate(TestContext.Current.CancellationToken);

        Assert.IsType<double>(evalutedResult);
        Assert.Equal(4.65, (double)evalutedResult, 3);
    }

    [Theory]
    [InlineData("1.22e1", 12.2d)]
    [InlineData("1e2", 100d)]
    [InlineData("1e+2", 100d)]
    [InlineData("1e-2", 0.01d)]
    [InlineData(".1e-2", 0.001d)]
    [InlineData("1e10", 10000000000d)]
    public void ShouldParseScientificNotation(string expression, double expected)
    {
        Assert.Equal(expected, new Expression(expression).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldHandleLongValues()
    {
        Assert.Equal(40_000_000_000 + 1, new Expression("40000000000+1").Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ShouldCompareLongValues()
    {
        Assert.Equal(false, new Expression("(0=1500000)||(((0+2200000000)-1500000)<0)").Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldNotConvertRealTypes(bool useBigNumbers)
    {
        object? result;

        ExpressionOptions options = useBigNumbers ? ExpressionOptions.UseBigNumbers : ExpressionOptions.None;

        Expression e = new Expression("x/2", options);
        e.Parameters["x"] = 2F;
        Assert.IsType<float>(e.Evaluate(TestContext.Current.CancellationToken));

        e = new Expression("x/2", options);
        e.Parameters["x"] = 2D;
        Assert.IsType<double>(e.Evaluate(TestContext.Current.CancellationToken));

        e = new Expression("x/2", options);
        e.Parameters["x"] = 2m;
        Assert.IsType<decimal>(e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldNotConvertRealTypesDecimal(bool useBigNumbers)
    {
        object? result;

        ExpressionOptions options = useBigNumbers ? ExpressionOptions.UseBigNumbers : ExpressionOptions.None;

        Expression e = new Expression("x/2", options);
        e.Parameters["x"] = 2m;
        Assert.IsType<decimal>(e.Evaluate(TestContext.Current.CancellationToken));

        e = new Expression("a / b * 100", options);
        e.Parameters["a"] = 20M;
        e.Parameters["b"] = 20M;

        result = e.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(100M, result);
    }

    [Theory]
    [InlineData("1/2", 0.5)]
    [InlineData("2/5", 0.4)]
    public void ShouldHandleDivision(string input, double expected)
    {
        var expression = new Expression(input);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        CheckResult(expected, result);
    }

    [Theory]
    [InlineData("2/1", 2)]
    [InlineData("6/2", 3)]
    public void ShouldHandleDivisionAsInteger(string input, int expected)
    {
        var expression = new Expression(input, ExpressionOptions.ReduceDivResultToInteger);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        CheckResult(expected, result);
    }

    [Theory]
    [InlineData("7.9\\2.1", 3)]
    public void ShouldHandleBasicIntDivisionOfFloats(string input, int expected)
    {
        var expression = new Expression(input, ExpressionOptions.None);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        CheckResult(expected, result);
    }
    [Theory]
    [InlineData("-7//2", -4)]
    public void ShouldHandlPythonIntDivisionOfFloats(string input, int expected)
    {
        var expression = new Expression(input, ExpressionOptions.None);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        CheckResult(expected, result);
    }

    [Theory]
    [InlineData("7.9\\2.1", 3)]
    public void ShouldHandleBasicIntDivisionOfBigFloats(string input, int expected)
    {
        var expression = new Expression(input, ExpressionOptions.UseBigNumbers);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        CheckResult(expected, result);
    }

    [Theory]
    [InlineData("-7//2", -4)]
    public void ShouldHandlPythonIntDivisionOfBigFloats(string input, int expected)
    {
        var expression = new Expression(input, ExpressionOptions.UseBigNumbers);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);

        CheckResult(expected, result);
    }

    [Theory]
    [InlineData("750/500", 1.5)]
    [InlineData("2000/1000", 2)]
    public void ShouldHandleIntegerDivision(string input, object expected)
    {
        var expression = new Expression(input, BaseExpressionOptions);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        CheckResult(expected, result);
    }

    [Theory]
    [InlineData(750, 500, false, true, 1.5)]
    [InlineData(750, 500, true, true, 1.5)]
    [InlineData(750, 500, false, false, 1.5)]
    [InlineData(750, 500, true, false, 1.5)]
    [InlineData(2000, 1000, false, true, 2)]
    [InlineData(2000, 1000, true, true, 2)]
    [InlineData(2000, 1000, false, false, 2)]
    [InlineData(2000, 1000, true, false, 2)]
    [InlineData(1000, 2000, false, true, 0.5)]
    [InlineData(1000, 2000, true, true, 0.5)]
    [InlineData(1000, 2000, false, false, 0.5)]
    [InlineData(1000, 2000, true, false, 0.5)]
    public void ShouldHandleIntegerDivisionDirect(long a, long b, bool useBigNumbers, bool reduceTypes, object expected)
    {
        ExpressionOptions options = useBigNumbers ? BaseExpressionOptions : BaseExpressionOptions & ~ExpressionOptions.UseBigNumbers;

        var result = MathHelper.Divide(a, b, reduceTypes, new MathHelperOptions(CultureInfo.CurrentCulture, options));
        CheckResult(expected, result);
    }

    [Fact]
    public void Overflow_Issue_190()
    {
        const decimal minValue = decimal.MinValue;
        var expr = new Expression(minValue.ToString(CultureInfo.InvariantCulture), ExpressionOptions.DecimalAsDefault, CultureInfo.InvariantCulture);
        Assert.Equal(0, EvaluationHelper.Compare(minValue, expr.Evaluate(TestContext.Current.CancellationToken), new ComparisonOptions(), new MathHelperOptions()));
    }

#if !AOT_COMPILATION
    [Theory]
    [InlineData("(X1 = 1)/2", 0.5)]
    [InlineData("(X1 = 1)*2", 2)]
    [InlineData("(X1 = 1)+1", 2)]
    [InlineData("(X1 = 1)-1", 0)]
    [InlineData("2*(X1 = 1)", 2)]
    [InlineData("2/(X1 = 1)", 2.0)]
    [InlineData("1+(X1 = 1)", 2)]
    [InlineData("true-(X1 = 1)", 0)]
    [InlineData("true-(X1 = true - false)", 0)]
    public void ShouldOptionallyCalculateWithBoolean(string formula, object expectedValue)
    {
        var expression = new Expression(formula, ExpressionOptions.AllowBooleanCalculation);
        expression.Parameters["X1"] = 1;

        Assert.Equal(expectedValue, expression.Evaluate(TestContext.Current.CancellationToken));

        var lambda = expression.ToLambda<double>(TestContext.Current.CancellationToken);

        Assert.Equal(Convert.ToDouble(expectedValue), lambda());
    }
#endif

    [Fact]
    public void Should_Evaluate_Floor_Of_Double_Max_Value()
    {
        var expr = new Expression($"Floor({double.MaxValue.ToString(CultureInfo.InvariantCulture)})");
        var res = expr.Evaluate(TestContext.Current.CancellationToken);

#if NET8_0_OR_GREATER
        Assert.Equal(Math.Floor(double.MaxValue), res);
#else
        Assert.Equal(double.PositiveInfinity, res);
#endif
    }

    [Fact]
    public void Should_Not_Change_Double_Precision()
    {
        var expr = new Expression("Floor(12e+100)");
        var res = expr.Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(Math.Floor(12e+100), res);
    }

    [Theory]
    [InlineData(".05", 0.05)]
    [InlineData("0.05", 0.05)]
    [InlineData("0.005", 0.005)]
    [InlineData(".0", 0d)]
    public void Should_Correctly_Parse_Floating_Point_Numbers(string formula, object expectedValue)
    {
        var expr = new Expression(formula, CultureInfo.InvariantCulture);
        var res = expr.Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(expectedValue, res);
    }

    [Theory]
    [InlineData("131055 ^ 8", 131047ul)]
    [InlineData("524288 | 128", 524416ul)]
    [InlineData("262143 & 131055", 131055ul)]
    [InlineData("262143 << 2", 1048572)]
    [InlineData("262143 >> 2", 65535)]
    [InlineData("1 << 32", 0x0000000100000000ul)]
    public void Should_Not_Overflow_Bitwise(string formula, object expectedValue)
    {
        var e = new Expression(formula, CultureInfo.InvariantCulture);
        var res = e.Evaluate(TestContext.Current.CancellationToken);

        CheckResult(expectedValue, res);
    }

    [Fact]
    public void Should_Not_Overflow_Shift_BigInt()
    {
        var e = new Expression("1 << 64", ExpressionOptions.UseBigNumbers, CultureInfo.InvariantCulture);
        var res = e.Evaluate(TestContext.Current.CancellationToken);

        BigInteger expected = new BigInteger(0x8000000000000000);

        expected *= 2;

        Assert.Equal(expected, res);
    }

/*    [Theory]
    [InlineData(int.MaxValue, '+', int.MaxValue)]
    [InlineData(int.MinValue, '-', int.MaxValue)]
    [InlineData(int.MaxValue, '*', int.MaxValue)]
    public void Should_Handle_Overflow_Int(int a, char op, int b)
    {
        var e = new Expression($"[a] {op} [b]", ExpressionOptions.OverflowProtection, CultureInfo.InvariantCulture);
        e.Parameters["a"] = a;
        e.Parameters["b"] = b;

        Assert.Throws<OverflowException>(() => e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(double.MaxValue, '+', double.MaxValue)]
    [InlineData(double.MinValue, '-', double.MaxValue)]
    [InlineData(double.MaxValue, '*', double.MaxValue)]
    [InlineData(double.MinValue, '/', 0.001d)]
    public void Should_Handle_Overflow_Double(double a, char op, double b)
    {
        var e = new Expression($"[a] {op} [b]", ExpressionOptions.OverflowProtection, CultureInfo.InvariantCulture);
        e.Parameters["a"] = a;
        e.Parameters["b"] = b;

        Assert.Throws<OverflowException>(() => e.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(float.MaxValue, '+', float.MaxValue)]
    [InlineData(float.MinValue, '-', float.MaxValue)]
    [InlineData(float.MaxValue, '*', float.MaxValue)]
    [InlineData(float.MinValue, '/', 0.001f)]
    public void Should_Handle_Overflow_Float(float a, char op, float b)
    {
        var e = new Expression($"[a] {op} [b]", ExpressionOptions.OverflowProtection, CultureInfo.InvariantCulture);
        e.Parameters["a"] = a;
        e.Parameters["b"] = b;

        Assert.Throws<OverflowException>(() => e.Evaluate(TestContext.Current.CancellationToken));
    }
*/
    [Theory]
    [InlineData("3 + '3'", ExpressionOptions.AllowCharValues, 54l)]
    [InlineData("3 + '3'", ExpressionOptions.None, 6d)]
    [InlineData("'4' + '2'", ExpressionOptions.AllowCharValues, 102l)]
    [InlineData("'4' + '2'", ExpressionOptions.StringConcat, "42")]
    [InlineData("'4' + '2'", ExpressionOptions.None, 6d)]
    public void ShouldHandleCharAddition(string expression, ExpressionOptions options, object expected)
    {
        var result = new Expression(expression, options | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken);

        CheckResult(expected, result);
    }

    [Fact]
    public void ShouldHandleMakeList()
    {
        var expression = new Expression("MakeList(1)");
        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.True(result is IList);
        Assert.Single((IList)result);
        Assert.Null(((IList)result)[0]);

        expression = new Expression("MakeList(2; 'c')", ExpressionOptions.AllowCharValues);
        result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.True(result is IList);
        Assert.Equal(2, ((IList)result).Count);
        Assert.Equal('c', ((IList)result)[0]);
        Assert.Equal('c', ((IList)result)[1]);

        expression = new Expression("MakeList(2; \"cc\")");
        result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.True(result is IList);
        Assert.Equal(2, ((IList)result).Count);
        Assert.Equal("cc", ((IList)result)[0]);
        Assert.Equal("cc", ((IList)result)[1]);

        expression = new Expression("MakeList(-1; null)");
        Assert.Throws<NCalcEvaluationException>(expression.Evaluate);
    }

    [Fact]
    public void ShouldHandleMakeStr()
    {
        var expression = new Expression("MakeStr(5, 32)");
        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal("     ", result.ToString());

        expression = new Expression("MakeStr(1; 'c')", ExpressionOptions.AllowCharValues);
        result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal("c", result);

        expression = new Expression("MakeStr(2; \"ab\")");
        result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal("abab", result);

        expression = new Expression("MakeStr(-1; null)");
        Assert.Throws<NCalcEvaluationException>(expression.Evaluate);
    }

    [Fact]
    public void ShouldHandleSignedAndUnsigned()
    {
        //Fails with: System.InvalidOperationException: 'Operator '+' can't be applied to operands of types 'int' and 'ulong''
        var failExp = new NCalc.Expression("1+(v3^v4)");
        failExp.Parameters["v3"] = (double)2;
        failExp.Parameters["v4"] = (double)3;
        var failRes = failExp.Evaluate(TestContext.Current.CancellationToken);

        //Fails with: System.InvalidOperationException: 'Operator '+' can't be applied to operands of types 'int' and 'ulong''
        var failExp2 = new NCalc.Expression("v1+(v2^v3)");
        failExp2.Parameters["v1"] = 1;
        failExp2.Parameters["v2"] = 2;
        failExp2.Parameters["v3"] = 3;
        var failRes2 = failExp2.Evaluate(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(-32767, 65535, 32768)]
    [InlineData(-32768, 65535, 32767)]
    [InlineData(-1, 65535, 65534)]
    [InlineData(2, 65535, 65537)]
    public void ShouldAddSignedAndUnsignedShorts(short a, ushort b, int expected)
    {
        //Fails with: System.InvalidOperationException: 'Operator '+' can't be applied to operands of types 'int' and 'ulong''
        var failExp = new NCalc.Expression("a + b");
        failExp.Parameters["a"] = a;
        failExp.Parameters["b"] = b;
        var result = failExp.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, ushort.MaxValue, -65534)]
    public void ShouldSubtractSignedAndUnsignedShorts(short a, ushort b, int expected)
    {
        var failExp = new NCalc.Expression("a - b");
        failExp.Parameters["a"] = a;
        failExp.Parameters["b"] = b;
        var result = failExp.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1, uint.MaxValue, -4294967294)]
    public void ShouldSubtractSignedAndUnsignedInts(int a, uint b, long expected)
    {
        var failExp = new NCalc.Expression("a - b");
        failExp.Parameters["a"] = a;
        failExp.Parameters["b"] = b;
        var result = failExp.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(short.MaxValue, short.MaxValue, 65534)]
    public void ShouldAddToOutOfBoundsShorts(short a, short b, int expected)
    {
        var failExp = new NCalc.Expression("a + b");
        failExp.Parameters["a"] = a;
        failExp.Parameters["b"] = b;
        var result = failExp.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(short.MinValue, short.MaxValue, -65535)]
    public void ShouldSubtractToOutOfBoundsShorts(short a, short b, int expected)
    {
        var failExp = new NCalc.Expression("a - b");
        failExp.Parameters["a"] = a;
        failExp.Parameters["b"] = b;
        var result = failExp.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldAddToOutOfBoundsInt64()
    {
        var failExp = new Expression("a + b", CultureInfo.InvariantCulture)
        {
            Parameters =
                {
                    ["a"] = Int64.MaxValue,
                    ["b"] = Int64.MaxValue
                },
            Options = ExpressionOptions.OverflowProtection,
        };
        Assert.Throws<OverflowException>(() => failExp.Evaluate(TestContext.Current.CancellationToken));

        failExp = new NCalc.Expression("a + b")
        {
            Parameters =
            {
                ["a"] = Int64.MaxValue,
                ["b"] = Int64.MaxValue,
            },
            Options = ExpressionOptions.OverflowProtection | ExpressionOptions.UseBigNumbers,
        };
        var result = failExp.Evaluate(TestContext.Current.CancellationToken);
        Assert.True(result is BigInteger);
    }

    [Fact]
    public void ShouldConcatStrings()
    {
        var expression = new NCalc.Expression("`a` + `b`", ExpressionOptions.NoStringTypeCoercion);
        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal("ab", result);

        expression = new NCalc.Expression("`a` + `b`");
        result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal("ab", result);

        expression = new NCalc.Expression("`1` + `2`");
        result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(3.0d, result);
    }

    [Fact]
    public void ShouldConvertBigNumbers()
    {
        BigDecimal x = 1;
        double y = 1.0;

        object? result = MathHelper.Subtract(x, y, true, new MathHelperOptions());

        Assert.Equal((int) 0, result);
    }

    [Fact]
    public void ShouldParseImaginaryNumber()
    {
        var expression = new NCalc.Expression("i", ExpressionOptions.None);
        expression.AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseComplexNumbers);

        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.IsType<ComplexNumber>(result);
        Assert.True(((ComplexNumber)result).Real.IsZero());
        Assert.True(((ComplexNumber)result).Imaginary.Equals(1));
    }

    [Theory]
    [InlineData("2i", 0, 2)]
    [InlineData("2i*2", 0, 4)]
    [InlineData("(1+2i)*2", 2, 4)]
    [InlineData("1 + i", 1, 1)]
    [InlineData("1 - i", 1, -1)]
    [InlineData("(2 + 5) + (2+2)i", 7, 4)]
    public void ShouldHandleComplexNumbers(string expr, double expectedReal, double expectedImaginary)
    {
        var expression = new NCalc.Expression(expr, ExpressionOptions.None);
        expression.AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseComplexNumbers);

        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.IsType<ComplexNumber>(result);
        Assert.True(((ComplexNumber)result).Real.Equals(expectedReal));
        Assert.True(((ComplexNumber)result).Imaginary.Equals(expectedImaginary));
    }

    [Theory]
    [InlineData("2i", 0, 2)]
    [InlineData("2i*2", 0, 4)]
    [InlineData("(1+2i)*2", 2, 4)]
    [InlineData("1 + i", 1, 1)]
    [InlineData("1 - i", 1, -1)]
    [InlineData("(2 + 5) + (2+2)i", 7, 4)]
    public void ShouldHandleComplexNumbers2(string expr, double expectedReal, double expectedImaginary)
    {
        var expression = new NCalc.Expression(expr, ExpressionOptions.IgnoreCaseAtBuiltInFunctions | ExpressionOptions.NoCache | ExpressionOptions.AllowNullParameter | ExpressionOptions.OverflowProtection | ExpressionOptions.AllowCharValues | ExpressionOptions.NoStringTypeCoercion | ExpressionOptions.DontParseGuids | ExpressionOptions.SupportTimeOperations | ExpressionOptions.LowerCaseIdentifierLookup | ExpressionOptions.SkipLogicalAndBitwiseOpChars | ExpressionOptions.UseUnicodeCharsForOperations | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences | ExpressionOptions.ReduceDivResultToInteger | ExpressionOptions.UseBigNumbers | ExpressionOptions.CompareNullValues | ExpressionOptions.SupportCStyleComments | ExpressionOptions.UseLoops | ExpressionOptions.UseIfStatement);
        expression.AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.AcceptUnderscoresInNumbers | AdvExpressionOptions.CalculatePercent | AdvExpressionOptions.UseResultReference | AdvExpressionOptions.AcceptCurrencySymbol | AdvExpressionOptions.ParseHumanePeriods | AdvExpressionOptions.ParseComplexNumbers | AdvExpressionOptions.ParseVectors);

        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.IsType<ComplexNumber>(result);
        Assert.True(((ComplexNumber)result).Real.Equals(expectedReal));
        Assert.True(((ComplexNumber)result).Imaginary.Equals(expectedImaginary));
    }

    [Theory]
    [InlineData("[1; 2]", 2, 2)]
    [InlineData("[2]", 1, 2)]
    [InlineData("[1; 2; 3]", 3, 3)]
    public void ShouldHandleVectors(string expr, int expectedDims, int expectedLastDimValue)
    {
        var expression = new NCalc.Expression(expr, ExpressionOptions.None);
        expression.AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseVectors);

        var result = expression.Evaluate(TestContext.Current.CancellationToken);
        Assert.IsType<Domain.Vector>(result);
        Domain.Vector resultVector = (Domain.Vector)result;
        Assert.Equal(expectedDims, resultVector.Dimensions);
        Assert.Equal(expectedLastDimValue, resultVector.Components[resultVector.Dimensions - 1]);
    }

    // ── Vector arithmetic via expressions ────────────────────────────────────

    private static NCalc.Expression VecExpr(string expr)
    {
        var e = new NCalc.Expression(expr, ExpressionOptions.None);
        e.AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseVectors);
        return e;
    }

    private static Domain.Vector EvalVec(string expr) =>
        (Domain.Vector)VecExpr(expr).Evaluate(TestContext.Current.CancellationToken)!;

    [Fact]
    public void ShouldAddVectors()
    {
        var result = EvalVec("[1; 2; 3] + [4; 5; 6]");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal((BigDecimal)5, result[0]);
        Assert.Equal((BigDecimal)7, result[1]);
        Assert.Equal((BigDecimal)9, result[2]);
    }

    [Fact]
    public void ShouldSubtractVectors()
    {
        var result = EvalVec("[4; 5; 6] - [1; 2; 3]");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal((BigDecimal)3, result[0]);
        Assert.Equal((BigDecimal)3, result[1]);
        Assert.Equal((BigDecimal)3, result[2]);
    }

    [Fact]
    public void ShouldMultiplyVectorByScalarRight()
    {
        var result = EvalVec("[1; 2; 3] * 2");
        Assert.Equal((BigDecimal)2, result[0]);
        Assert.Equal((BigDecimal)4, result[1]);
        Assert.Equal((BigDecimal)6, result[2]);
    }

    [Fact]
    public void ShouldMultiplyVectorByScalarLeft()
    {
        var result = EvalVec("3 * [1; 2; 3]");
        Assert.Equal((BigDecimal)3, result[0]);
        Assert.Equal((BigDecimal)6, result[1]);
        Assert.Equal((BigDecimal)9, result[2]);
    }

    [Fact]
    public void ShouldDivideVectorByScalar()
    {
        var result = EvalVec("[4; 6; 8] / 2");
        Assert.Equal((BigDecimal)2, result[0]);
        Assert.Equal((BigDecimal)3, result[1]);
        Assert.Equal((BigDecimal)4, result[2]);
    }

    [Fact]
    public void ShouldEvaluateVectorArithmeticWithParameters()
    {
        var e = VecExpr("v * scale");
        e.Parameters["v"] = new Domain.Vector([1, 2, 3]);
        e.Parameters["scale"] = 5;
        var result = (Domain.Vector)e.Evaluate(TestContext.Current.CancellationToken)!;
        Assert.Equal((BigDecimal)5,  result[0]);
        Assert.Equal((BigDecimal)10, result[1]);
        Assert.Equal((BigDecimal)15, result[2]);
    }

    [Fact]
    public void ShouldThrowOnVectorDimensionMismatch()
    {
        Assert.Throws<NCalcEvaluationException>(() => EvalVec("[1; 2] + [1; 2; 3]"));
    }

    [Fact]
    public void ShouldThrowOnVectorPlusScalar()
    {
        Assert.Throws<InvalidOperationException>(() => EvalVec("[1; 2] + 5"));
    }

    [Fact]
    public void ShouldThrowOnVectorTimesVector()
    {
        Assert.Throws<InvalidOperationException>(() => EvalVec("[1; 2] * [3; 4]"));
    }

    [Fact]
    public void ShouldThrowOnScalarDividedByVector()
    {
        Assert.Throws<InvalidOperationException>(() => EvalVec("10 / [1; 2]"));
    }

    // ── MathHelper functions for Vector ──────────────────────────────────────

    [Fact]
    public void ShouldComputeAbsForVector()
    {
        var v = new Domain.Vector([-3, 4, -1]);
        var result = (Domain.Vector)MathHelper.Abs(v, new MathHelperOptions())!;
        Assert.Equal((BigDecimal)3, result[0]);
        Assert.Equal((BigDecimal)4, result[1]);
        Assert.Equal((BigDecimal)1, result[2]);
    }

    [Fact]
    public void ShouldComputeFloorForVector()
    {
        var v = new Domain.Vector([new BigDecimal(1.7), new BigDecimal(-2.3), new BigDecimal(3.9)]);
        var result = (Domain.Vector)MathHelper.Floor(v, new MathHelperOptions())!;
        Assert.Equal((BigDecimal)1, result[0]);
        Assert.Equal((BigDecimal)(-3), result[1]);
        Assert.Equal((BigDecimal)3, result[2]);
    }

    [Fact]
    public void ShouldComputeCeilingForVector()
    {
        var v = new Domain.Vector([new BigDecimal(1.2), new BigDecimal(-2.9), new BigDecimal(3.0)]);
        var result = (Domain.Vector)MathHelper.Ceiling(v, new MathHelperOptions())!;
        Assert.Equal((BigDecimal)2, result[0]);
        Assert.Equal((BigDecimal)(-2), result[1]);
        Assert.Equal((BigDecimal)3, result[2]);
    }

    [Fact]
    public void ShouldComputeRoundForVector()
    {
        var v = new Domain.Vector([new BigDecimal(1.5), new BigDecimal(-2.5), new BigDecimal(3.4)]);
        var result = (Domain.Vector)MathHelper.Round(v, 0, MidpointRounding.AwayFromZero, new MathHelperOptions())!;
        Assert.Equal((BigDecimal)2, result[0]);
        Assert.Equal((BigDecimal)(-3), result[1]);
        Assert.Equal((BigDecimal)3, result[2]);
    }

    // ── MathHelper functions for ComplexNumber (previously threw) ─────────────

    [Fact]
    public void ShouldComputeFloorForComplexNumber()
    {
        var z = new ComplexNumber(new BigDecimal(2.7), new BigDecimal(1.9));
        var result = (ComplexNumber)MathHelper.Floor(z, new MathHelperOptions())!;
        Assert.Equal((BigDecimal)2, result.Real);
        Assert.Equal((BigDecimal)1, result.Imaginary);
    }

    [Fact]
    public void ShouldComputeCeilingForComplexNumber()
    {
        var z = new ComplexNumber(new BigDecimal(2.1), new BigDecimal(-1.1));
        var result = (ComplexNumber)MathHelper.Ceiling(z, new MathHelperOptions())!;
        Assert.Equal((BigDecimal)3, result.Real);
        Assert.Equal((BigDecimal)(-1), result.Imaginary);
    }

    [Fact]
    public void ShouldComputeRoundForComplexNumber()
    {
        var z = new ComplexNumber(new BigDecimal(2.5), new BigDecimal(-1.5));
        var result = (ComplexNumber)MathHelper.Round(z, 0, MidpointRounding.AwayFromZero, new MathHelperOptions())!;
        Assert.Equal((BigDecimal)3, result.Real);
        Assert.Equal((BigDecimal)(-2), result.Imaginary);
    }

    [Fact]
    public void ShouldComputeTruncateForComplexNumber()
    {
        var z = new ComplexNumber(new BigDecimal(3.9), new BigDecimal(-2.7));
        var result = (ComplexNumber)MathHelper.Truncate(z, new MathHelperOptions())!;
        Assert.Equal((BigDecimal)3, result.Real);
        Assert.Equal((BigDecimal)(-2), result.Imaginary);
    }

    [Fact]
    public void ShouldComputeExpForComplexNumber()
    {
        // e^(i*π) ≈ -1 + 0i  (Euler's identity)
        var pi = new BigDecimal(Math.PI);
        var z = new ComplexNumber(BigDecimal.Zero, pi);
        var result = (ComplexNumber)MathHelper.Exp(z, new MathHelperOptions())!;
        var comparer = new ComplexNumberToleranceComparer(1e-6);
        Assert.True(comparer.Equals(result, new ComplexNumber(new BigDecimal(-1.0), BigDecimal.Zero)));
    }

    [Fact]
    public void ShouldComputeLog10ForComplexNumber()
    {
        // log10(10) = 1 + 0i
        var z = new ComplexNumber(new BigDecimal(10.0), BigDecimal.Zero);
        var result = ComplexNumber.Log10(z, new MathHelperOptions());
        var comparer = new ComplexNumberToleranceComparer(1e-10);
        Assert.True(comparer.Equals(result, new ComplexNumber(BigDecimal.One, BigDecimal.Zero)));
    }

    [Fact]
    public void ShouldComputeLog10ForComplexNumberViaExpression()
    {
        // Log10(i) = iπ/(2*ln10) — pass the complex number as a parameter to avoid parse ambiguity
        var e = new Expression("Log10(z)");
        e.Parameters["z"] = new ComplexNumber(BigDecimal.Zero, BigDecimal.One);
        var result = (ComplexNumber)e.Evaluate(TestContext.Current.CancellationToken)!;
        double expectedImaginary = Math.PI / 2.0 / Math.Log(10.0);
        var comparer = new ComplexNumberToleranceComparer(1e-6);
        Assert.True(comparer.Equals(result,
            new ComplexNumber(BigDecimal.Zero, new BigDecimal(expectedImaginary))));
    }

    [Fact]
    public void ShouldComputeLog10ForImaginaryComplexNumber()
    {
        // log10(i) = (π/2) / ln(10) * i ≈ 0 + 0.68219i
        var z = new ComplexNumber(BigDecimal.Zero, BigDecimal.One);
        var result = ComplexNumber.Log10(z, new MathHelperOptions());
        double expectedImaginary = Math.PI / 2.0 / Math.Log(10.0);
        var expected = new ComplexNumber(BigDecimal.Zero, new BigDecimal(expectedImaginary));
        var comparer = new ComplexNumberToleranceComparer(1e-6);
        Assert.True(comparer.Equals(result, expected));
    }

    // ── Cosh bug fix (was calling Math.Cos instead of Math.Cosh) ─────────────

    [Fact]
    public void ShouldComputeCoshCorrectly()
    {
        // cosh(0) == 1, cos(0) == 1 — use a value where they differ
        // cosh(1) ≈ 1.5430806, cos(1) ≈ 0.5403023
        var result = (double)MathHelper.Cosh(1.0, new MathHelperOptions())!;
        Assert.Equal(Math.Cosh(1.0), result, precision: 10);
    }

    // ── ComplexNumber ↔ System.Numerics.Complex interop ──────────────────────

    [Fact]
    public void ShouldConvertComplexNumberToSystemComplex()
    {
        var cn = new ComplexNumber(new BigDecimal(3.0), new BigDecimal(4.0));
        Complex sys = cn.ToComplex();
        Assert.Equal(3.0, sys.Real, precision: 10);
        Assert.Equal(4.0, sys.Imaginary, precision: 10);
    }

    [Fact]
    public void ShouldConvertSystemComplexToComplexNumber()
    {
        var sys = new Complex(3.0, 4.0);
        ComplexNumber cn = ComplexNumber.FromComplex(sys);
        Assert.True(cn.Real.Equals(new BigDecimal(3.0)));
        Assert.True(cn.Imaginary.Equals(new BigDecimal(4.0)));
    }

    [Fact]
    public void ShouldRoundTripComplexNumberViaExplicitCasts()
    {
        var original = new ComplexNumber(new BigDecimal(1.5), new BigDecimal(-2.5));
        var sys = (Complex)original;
        var restored = (ComplexNumber)sys;
        var comparer = new ComplexNumberToleranceComparer(1e-10);
        Assert.True(comparer.Equals(original, restored));
    }

    // ── Vector ↔ System.Numerics.Vector2/3/4 interop ─────────────────────────

    [Fact]
    public void ShouldConvertVectorToVector2()
    {
        var v = new Domain.Vector([1.0, 2.0]);
        Vector2 v2 = v.ToVector2();
        Assert.Equal(1.0f, v2.X, precision: 5);
        Assert.Equal(2.0f, v2.Y, precision: 5);
    }

    [Fact]
    public void ShouldConvertVector2ToVector()
    {
        var v2 = new Vector2(3.0f, 4.0f);
        var v = Domain.Vector.FromVector2(v2);
        Assert.Equal(2, v.Dimensions);
        var comparer = new VectorToleranceComparer(1e-5);
        Assert.True(comparer.Equals(v, new Domain.Vector([3.0, 4.0])));
    }

    [Fact]
    public void ShouldConvertVectorToVector3()
    {
        var v = new Domain.Vector([1.0, 2.0, 3.0]);
        Vector3 v3 = v.ToVector3();
        Assert.Equal(1.0f, v3.X, precision: 5);
        Assert.Equal(2.0f, v3.Y, precision: 5);
        Assert.Equal(3.0f, v3.Z, precision: 5);
    }

    [Fact]
    public void ShouldConvertVectorToVector4()
    {
        var v = new Domain.Vector([1.0, 2.0, 3.0, 4.0]);
        Vector4 v4 = v.ToVector4();
        Assert.Equal(1.0f, v4.X, precision: 5);
        Assert.Equal(4.0f, v4.W, precision: 5);
    }

    [Fact]
    public void ShouldRoundTripVectorViaVector3ExplicitCasts()
    {
        var original = new Domain.Vector([1.0, 2.0, 3.0]);
        var v3 = (Vector3)original;
        var restored = (Domain.Vector)v3;
        var comparer = new VectorToleranceComparer(1e-5);
        Assert.True(comparer.Equals(original, restored));
    }

    // ── VectorToleranceComparer ───────────────────────────────────────────────

    [Fact]
    public void VectorToleranceComparerShouldTreatNearlyEqualVectorsAsEqual()
    {
        var a = new Domain.Vector([1.0, 2.0, 3.0]);
        var b = new Domain.Vector([1.0 + 1e-11, 2.0 - 1e-11, 3.0]);
        var comparer = new VectorToleranceComparer(1e-10);
        Assert.True(comparer.Equals(a, b));
    }

    [Fact]
    public void VectorToleranceComparerShouldDistinguishVectorsOutsideTolerance()
    {
        var a = new Domain.Vector([1.0, 2.0, 3.0]);
        var b = new Domain.Vector([1.0, 2.1, 3.0]);
        var comparer = new VectorToleranceComparer(1e-10);
        Assert.False(comparer.Equals(a, b));
    }

    [Fact]
    public void VectorToleranceComparerShouldRejectDifferentDimensions()
    {
        var a = new Domain.Vector([1.0, 2.0]);
        var b = new Domain.Vector([1.0, 2.0, 3.0]);
        var comparer = new VectorToleranceComparer();
        Assert.False(comparer.Equals(a, b));
    }

    // ── ComplexNumberToleranceComparer ────────────────────────────────────────

    [Fact]
    public void ComplexToleranceComparerShouldTreatNearlyEqualNumbersAsEqual()
    {
        var a = new ComplexNumber(new BigDecimal(1.0), new BigDecimal(2.0));
        var b = new ComplexNumber(new BigDecimal(1.0 + 1e-11), new BigDecimal(2.0 - 1e-11));
        var comparer = new ComplexNumberToleranceComparer(1e-10);
        Assert.True(comparer.Equals(a, b));
    }

    [Fact]
    public void ComplexToleranceComparerShouldDistinguishNumbersOutsideTolerance()
    {
        var a = new ComplexNumber(new BigDecimal(1.0), new BigDecimal(2.0));
        var b = new ComplexNumber(new BigDecimal(1.0), new BigDecimal(2.5));
        var comparer = new ComplexNumberToleranceComparer(1e-10);
        Assert.False(comparer.Equals(a, b));
    }

    // ── Vector modulo ────────────────────────────────────────────────────────

    [Fact]
    public void ShouldComputeVectorModuloByScalar()
    {
        var result = EvalVec("[7, 8, 9] % 3");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal(new BigDecimal(1), result[0]);
        Assert.Equal(new BigDecimal(2), result[1]);
        Assert.Equal(new BigDecimal(0), result[2]);
    }

    [Fact]
    public void ShouldComputeVectorModuloByVector()
    {
        var result = EvalVec("[10, 11, 12] % [3, 4, 5]");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal(new BigDecimal(1), result[0]);
        Assert.Equal(new BigDecimal(3), result[1]);
        Assert.Equal(new BigDecimal(2), result[2]);
    }

    [Fact]
    public void ShouldThrowOnScalarModuloVector()
    {
        Assert.ThrowsAny<Exception>(() => EvalVec("5 % [1, 2, 3]"));
    }

    // ── Vector bitwise AND / OR / XOR ────────────────────────────────────────

    [Fact]
    public void ShouldComputeVectorBitwiseAnd()
    {
        // 5=0101, 6=0110, 7=0111 AND 3=0011, 5=0101, 5=0101 = 1, 4, 5
        var result = EvalVec("[5, 6, 7] & [3, 5, 5]");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal(new BigDecimal(1), result[0]);
        Assert.Equal(new BigDecimal(4), result[1]);
        Assert.Equal(new BigDecimal(5), result[2]);
    }

    [Fact]
    public void ShouldComputeVectorBitwiseOr()
    {
        // 5|3=7, 6|5=7, 7|5=7
        var result = EvalVec("[5, 6, 7] | [3, 5, 5]");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal(new BigDecimal(7), result[0]);
        Assert.Equal(new BigDecimal(7), result[1]);
        Assert.Equal(new BigDecimal(7), result[2]);
    }

    [Fact]
    public void ShouldComputeVectorBitwiseXOr()
    {
        // 5^3=6, 6^5=3, 7^5=2
        var result = EvalVec("[5, 6, 7] ^ [3, 5, 5]");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal(new BigDecimal(6), result[0]);
        Assert.Equal(new BigDecimal(3), result[1]);
        Assert.Equal(new BigDecimal(2), result[2]);
    }

    [Fact]
    public void ShouldThrowOnVectorBitwiseAndWithScalar()
    {
        Assert.ThrowsAny<Exception>(() => EvalVec("[1, 2, 3] & 1"));
    }

    [Fact]
    public void ShouldThrowOnVectorBitwiseOrWithScalar()
    {
        Assert.ThrowsAny<Exception>(() => EvalVec("[1, 2, 3] | 1"));
    }

    [Fact]
    public void ShouldThrowOnVectorBitwiseXOrWithScalar()
    {
        Assert.ThrowsAny<Exception>(() => EvalVec("[1, 2, 3] ^ 1"));
    }

    // ── Vector ones complement (~) ────────────────────────────────────────────

    [Fact]
    public void ShouldComputeVectorOnesComplement()
    {
        // ~5 = -6, ~6 = -7, ~7 = -8
        var result = EvalVec("~[5, 6, 7]");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal(new BigDecimal(-6), result[0]);
        Assert.Equal(new BigDecimal(-7), result[1]);
        Assert.Equal(new BigDecimal(-8), result[2]);
    }

    // ── Vector left shift / right shift ──────────────────────────────────────

    [Fact]
    public void ShouldComputeVectorLeftShift()
    {
        // [1, 2, 3] << 2 = [4, 8, 12]
        var result = EvalVec("[1, 2, 3] << 2");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal(new BigDecimal(4),  result[0]);
        Assert.Equal(new BigDecimal(8),  result[1]);
        Assert.Equal(new BigDecimal(12), result[2]);
    }

    [Fact]
    public void ShouldComputeVectorRightShift()
    {
        // [4, 8, 12] >> 2 = [1, 2, 3]
        var result = EvalVec("[4, 8, 12] >> 2");
        Assert.Equal(3, result.Dimensions);
        Assert.Equal(new BigDecimal(1), result[0]);
        Assert.Equal(new BigDecimal(2), result[1]);
        Assert.Equal(new BigDecimal(3), result[2]);
    }

    [Fact]
    public void ShouldThrowOnVectorShiftedByVector()
    {
        Assert.ThrowsAny<Exception>(() => EvalVec("[1, 2, 3] << [1, 1, 1]"));
    }

    // ── Direct operator tests on Vector struct ───────────────────────────────

    [Fact]
    public void VectorModuloOperatorShouldWorkElementWise()
    {
        var v = EvalVec("[13, 14, 15]");
        var s = new BigDecimal(4);
        var result = v % s;
        Assert.Equal(new BigDecimal(1), result[0]);
        Assert.Equal(new BigDecimal(2), result[1]);
        Assert.Equal(new BigDecimal(3), result[2]);
    }

    [Fact]
    public void VectorBitwiseAndOperatorShouldWorkElementWise()
    {
        var a = EvalVec("[12, 10, 6]");  // 1100, 1010, 0110
        var b = EvalVec("[10, 6, 5]");   // 1010, 0110, 0101
        var result = a & b;              // 1000=8, 0010=2, 0100=4
        Assert.Equal(new BigDecimal(8), result[0]);
        Assert.Equal(new BigDecimal(2), result[1]);
        Assert.Equal(new BigDecimal(4), result[2]);
    }

    [Fact]
    public void VectorOnesComplementOperatorShouldNegateAndSubtractOne()
    {
        var v = EvalVec("[0, 1, 255]");
        var result = ~v;
        Assert.Equal(new BigDecimal(-1),   result[0]);
        Assert.Equal(new BigDecimal(-2),   result[1]);
        Assert.Equal(new BigDecimal(-256), result[2]);
    }

    [Fact]
    public void VectorLeftShiftOperatorShouldDoublePerShift()
    {
        var v = EvalVec("[1, 2, 4]");
        var result = v << 3;
        Assert.Equal(new BigDecimal(8),  result[0]);
        Assert.Equal(new BigDecimal(16), result[1]);
        Assert.Equal(new BigDecimal(32), result[2]);
    }

    [Fact]
    public void VectorRightShiftOperatorShouldHalvePerShift()
    {
        var v = EvalVec("[8, 16, 32]");
        var result = v >> 3;
        Assert.Equal(new BigDecimal(1), result[0]);
        Assert.Equal(new BigDecimal(2), result[1]);
        Assert.Equal(new BigDecimal(4), result[2]);
    }

    [Fact]
    public void ShouldNegateDoubleZeroToNegative()
    {
        object result = new Expression("-0.0").Evaluate(TestContext.Current.CancellationToken);
        if (result is double d)
        {
            Assert.Equal(BitConverter.DoubleToUInt64Bits(-0.0), BitConverter.DoubleToUInt64Bits(d));
        }
    }

    [Theory]
    [InlineData("(-(1234567890987654321) < 0) && (-(1234567890987654321) = 0 - 1234567890987654321)")]
    [InlineData("(-(1234567890987654321.98) < 0) && (-(1234567890987654321.98) = 0 - 1234567890987654321.98)")]
    public void ShouldNegateBigNumbersRight(string expr)
    {
        object result = new Expression(expr).Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(true, result);
    }

    [Fact]
    public void ShouldRoundUpCorrectly()
    {
        object result = MathHelper.Round(1.7, 0, MidpointRounding.AwayFromZero, new MathHelperOptions());
        Assert.Equal(2.0, result);
    }

    // ---- MathHelper.RoundToPrecision: midpoint-aware rounding (MidpointRounding affects ONLY exact midpoints) ----

    // net10.0 (the test target) defines all five MidpointRounding modes.
    private static readonly MidpointRounding[] AllMidpointModes =
    {
        MidpointRounding.ToEven,
        MidpointRounding.AwayFromZero,
        MidpointRounding.ToZero,
        MidpointRounding.ToNegativeInfinity,
        MidpointRounding.ToPositiveInfinity,
    };

    // A value that is NOT an exact midpoint must round to the nearest candidate for EVERY mode: no mode may
    // truncate or push a non-midpoint value directionally.
    [Theory]
    [InlineData(1.44, 1, 1.4)]   // below the midpoint
    [InlineData(1.46, 1, 1.5)]   // above the midpoint
    [InlineData(-1.44, 1, -1.4)]
    [InlineData(-1.46, 1, -1.5)]
    [InlineData(2.4, 0, 2.0)]
    [InlineData(2.6, 0, 3.0)]
    [InlineData(-2.4, 0, -2.0)]
    [InlineData(-2.6, 0, -3.0)]
    [InlineData(1.114, 2, 1.11)]
    [InlineData(1.116, 2, 1.12)]
    [InlineData(-1.114, 2, -1.11)]
    [InlineData(-1.116, 2, -1.12)]
    public void RoundToPrecision_Double_NonMidpoint_IgnoresMode(double value, int digits, double expected)
    {
        foreach (var mode in AllMidpointModes)
        {
            Assert.Equal(expected, MathHelper.RoundToPrecision(value, digits, mode), precision: 10);
        }
    }

    [Theory]
    [InlineData("1.44", 1, "1.4")]   // below the midpoint
    [InlineData("1.46", 1, "1.5")]   // above the midpoint
    [InlineData("-1.44", 1, "-1.4")]
    [InlineData("-1.46", 1, "-1.5")]
    [InlineData("2.4", 0, "2")]
    [InlineData("2.6", 0, "3")]
    [InlineData("-2.4", 0, "-2")]
    [InlineData("-2.6", 0, "-3")]
    [InlineData("1.114", 2, "1.11")]
    [InlineData("1.116", 2, "1.12")]
    [InlineData("-1.114", 2, "-1.11")]
    [InlineData("-1.116", 2, "-1.12")]
    public void RoundToPrecision_Decimal_NonMidpoint_IgnoresMode(string value, int digits, string expected)
    {
        decimal v = decimal.Parse(value, CultureInfo.InvariantCulture);
        decimal e = decimal.Parse(expected, CultureInfo.InvariantCulture);
        foreach (var mode in AllMidpointModes)
        {
            Assert.Equal(e, MathHelper.RoundToPrecision(v, digits, mode));
        }
    }

    // Exact midpoints: the supplied MidpointRounding mode decides which neighbor is chosen.
    [Theory]
    // 1.55 -> exactly between 1.5 and 1.6
    [InlineData(1.55, 1, MidpointRounding.ToEven, 1.6)]
    [InlineData(1.55, 1, MidpointRounding.AwayFromZero, 1.6)]
    [InlineData(1.55, 1, MidpointRounding.ToZero, 1.5)]
    [InlineData(1.55, 1, MidpointRounding.ToNegativeInfinity, 1.5)]
    [InlineData(1.55, 1, MidpointRounding.ToPositiveInfinity, 1.6)]
    // 1.45 -> exactly between 1.4 and 1.5
    [InlineData(1.45, 1, MidpointRounding.ToEven, 1.4)]
    [InlineData(1.45, 1, MidpointRounding.AwayFromZero, 1.5)]
    [InlineData(1.45, 1, MidpointRounding.ToZero, 1.4)]
    [InlineData(1.45, 1, MidpointRounding.ToNegativeInfinity, 1.4)]
    [InlineData(1.45, 1, MidpointRounding.ToPositiveInfinity, 1.5)]
    // -1.55 -> exactly between -1.5 and -1.6
    [InlineData(-1.55, 1, MidpointRounding.ToEven, -1.6)]
    [InlineData(-1.55, 1, MidpointRounding.AwayFromZero, -1.6)]
    [InlineData(-1.55, 1, MidpointRounding.ToZero, -1.5)]
    [InlineData(-1.55, 1, MidpointRounding.ToNegativeInfinity, -1.6)]
    [InlineData(-1.55, 1, MidpointRounding.ToPositiveInfinity, -1.5)]
    // -1.45 -> exactly between -1.4 and -1.5
    [InlineData(-1.45, 1, MidpointRounding.ToEven, -1.4)]
    [InlineData(-1.45, 1, MidpointRounding.AwayFromZero, -1.5)]
    [InlineData(-1.45, 1, MidpointRounding.ToZero, -1.4)]
    [InlineData(-1.45, 1, MidpointRounding.ToNegativeInfinity, -1.5)]
    [InlineData(-1.45, 1, MidpointRounding.ToPositiveInfinity, -1.4)]
    // precision 0 midpoints
    [InlineData(2.5, 0, MidpointRounding.ToEven, 2.0)]
    [InlineData(2.5, 0, MidpointRounding.AwayFromZero, 3.0)]
    [InlineData(2.5, 0, MidpointRounding.ToZero, 2.0)]
    [InlineData(2.5, 0, MidpointRounding.ToNegativeInfinity, 2.0)]
    [InlineData(2.5, 0, MidpointRounding.ToPositiveInfinity, 3.0)]
    [InlineData(3.5, 0, MidpointRounding.ToEven, 4.0)]
    [InlineData(-2.5, 0, MidpointRounding.ToEven, -2.0)]
    [InlineData(-2.5, 0, MidpointRounding.AwayFromZero, -3.0)]
    [InlineData(-2.5, 0, MidpointRounding.ToZero, -2.0)]
    [InlineData(-2.5, 0, MidpointRounding.ToNegativeInfinity, -3.0)]
    [InlineData(-2.5, 0, MidpointRounding.ToPositiveInfinity, -2.0)]
    // precision 2 midpoint (1.125 is exactly representable in binary, so the double path is clean too)
    [InlineData(1.125, 2, MidpointRounding.ToEven, 1.12)]
    [InlineData(1.125, 2, MidpointRounding.AwayFromZero, 1.13)]
    [InlineData(1.125, 2, MidpointRounding.ToZero, 1.12)]
    [InlineData(1.125, 2, MidpointRounding.ToNegativeInfinity, 1.12)]
    [InlineData(1.125, 2, MidpointRounding.ToPositiveInfinity, 1.13)]
    public void RoundToPrecision_Double_Midpoint_UsesMode(double value, int digits, MidpointRounding mode, double expected)
    {
        Assert.Equal(expected, MathHelper.RoundToPrecision(value, digits, mode), precision: 10);
    }

    [Theory]
    // 1.55 -> exactly between 1.5 and 1.6
    [InlineData("1.55", 1, MidpointRounding.ToEven, "1.6")]
    [InlineData("1.55", 1, MidpointRounding.AwayFromZero, "1.6")]
    [InlineData("1.55", 1, MidpointRounding.ToZero, "1.5")]
    [InlineData("1.55", 1, MidpointRounding.ToNegativeInfinity, "1.5")]
    [InlineData("1.55", 1, MidpointRounding.ToPositiveInfinity, "1.6")]
    // 1.45 -> exactly between 1.4 and 1.5
    [InlineData("1.45", 1, MidpointRounding.ToEven, "1.4")]
    [InlineData("1.45", 1, MidpointRounding.AwayFromZero, "1.5")]
    [InlineData("1.45", 1, MidpointRounding.ToZero, "1.4")]
    [InlineData("1.45", 1, MidpointRounding.ToNegativeInfinity, "1.4")]
    [InlineData("1.45", 1, MidpointRounding.ToPositiveInfinity, "1.5")]
    // -1.55 -> exactly between -1.5 and -1.6
    [InlineData("-1.55", 1, MidpointRounding.ToEven, "-1.6")]
    [InlineData("-1.55", 1, MidpointRounding.AwayFromZero, "-1.6")]
    [InlineData("-1.55", 1, MidpointRounding.ToZero, "-1.5")]
    [InlineData("-1.55", 1, MidpointRounding.ToNegativeInfinity, "-1.6")]
    [InlineData("-1.55", 1, MidpointRounding.ToPositiveInfinity, "-1.5")]
    // -1.45 -> exactly between -1.4 and -1.5
    [InlineData("-1.45", 1, MidpointRounding.ToEven, "-1.4")]
    [InlineData("-1.45", 1, MidpointRounding.AwayFromZero, "-1.5")]
    [InlineData("-1.45", 1, MidpointRounding.ToZero, "-1.4")]
    [InlineData("-1.45", 1, MidpointRounding.ToNegativeInfinity, "-1.5")]
    [InlineData("-1.45", 1, MidpointRounding.ToPositiveInfinity, "-1.4")]
    // precision 0 midpoints
    [InlineData("2.5", 0, MidpointRounding.ToEven, "2")]
    [InlineData("2.5", 0, MidpointRounding.AwayFromZero, "3")]
    [InlineData("2.5", 0, MidpointRounding.ToZero, "2")]
    [InlineData("2.5", 0, MidpointRounding.ToNegativeInfinity, "2")]
    [InlineData("2.5", 0, MidpointRounding.ToPositiveInfinity, "3")]
    [InlineData("3.5", 0, MidpointRounding.ToEven, "4")]
    [InlineData("-2.5", 0, MidpointRounding.ToEven, "-2")]
    [InlineData("-2.5", 0, MidpointRounding.AwayFromZero, "-3")]
    [InlineData("-2.5", 0, MidpointRounding.ToZero, "-2")]
    [InlineData("-2.5", 0, MidpointRounding.ToNegativeInfinity, "-3")]
    [InlineData("-2.5", 0, MidpointRounding.ToPositiveInfinity, "-2")]
    // precision 2 midpoint
    [InlineData("1.125", 2, MidpointRounding.ToEven, "1.12")]
    [InlineData("1.125", 2, MidpointRounding.AwayFromZero, "1.13")]
    [InlineData("1.125", 2, MidpointRounding.ToZero, "1.12")]
    [InlineData("1.125", 2, MidpointRounding.ToNegativeInfinity, "1.12")]
    [InlineData("1.125", 2, MidpointRounding.ToPositiveInfinity, "1.13")]
    public void RoundToPrecision_Decimal_Midpoint_UsesMode(string value, int digits, MidpointRounding mode, string expected)
    {
        decimal v = decimal.Parse(value, CultureInfo.InvariantCulture);
        decimal e = decimal.Parse(expected, CultureInfo.InvariantCulture);
        Assert.Equal(e, MathHelper.RoundToPrecision(v, digits, mode));
    }

    [Fact]
    public void RoundToPrecision_Double_InvalidDigits_Throws()
    {
        // Mirrors Math.Round(double, int, MidpointRounding): digits must be in [0, 15].
        Assert.Throws<ArgumentOutOfRangeException>(() => MathHelper.RoundToPrecision(1.55, -1, MidpointRounding.ToEven));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathHelper.RoundToPrecision(1.55, 16, MidpointRounding.ToEven));
    }

    [Fact]
    public void RoundToPrecision_Decimal_InvalidDigits_Throws()
    {
        // Mirrors Math.Round(decimal, int, MidpointRounding): digits must be in [0, 28].
        Assert.Throws<ArgumentOutOfRangeException>(() => MathHelper.RoundToPrecision(1.55m, -1, MidpointRounding.ToEven));
        Assert.Throws<ArgumentOutOfRangeException>(() => MathHelper.RoundToPrecision(1.55m, 29, MidpointRounding.ToEven));
    }

    [Fact]
    public void RoundToPrecision_UnknownMode_Throws()
    {
        // Unknown enum values are rejected (not silently ignored), even for non-midpoint values.
        Assert.Throws<ArgumentException>(() => MathHelper.RoundToPrecision(1.44, 1, (MidpointRounding)999));
        Assert.Throws<ArgumentException>(() => MathHelper.RoundToPrecision(1.44m, 1, (MidpointRounding)999));
    }

    // The public Round entry point (modified to call RoundToPrecision) must honor the midpoint mode on both paths.
    [Theory]
    [InlineData(MidpointRounding.ToEven, 1.6)]
    [InlineData(MidpointRounding.AwayFromZero, 1.6)]
    [InlineData(MidpointRounding.ToZero, 1.5)]
    [InlineData(MidpointRounding.ToNegativeInfinity, 1.5)]
    [InlineData(MidpointRounding.ToPositiveInfinity, 1.6)]
    public void Round_DoublePath_AppliesMidpointMode(MidpointRounding mode, double expected)
    {
        object result = MathHelper.Round(1.55, 1, mode, new MathHelperOptions());
        Assert.Equal(expected, Assert.IsType<double>(result), precision: 10);
    }

    [Theory]
    [InlineData(MidpointRounding.ToEven, "1.6")]
    [InlineData(MidpointRounding.AwayFromZero, "1.6")]
    [InlineData(MidpointRounding.ToZero, "1.5")]
    [InlineData(MidpointRounding.ToNegativeInfinity, "1.5")]
    [InlineData(MidpointRounding.ToPositiveInfinity, "1.6")]
    public void Round_DecimalPath_AppliesMidpointMode(MidpointRounding mode, string expected)
    {
        var options = new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.DecimalAsDefault);
        object result = MathHelper.Round(1.55m, 1, mode, options);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), Assert.IsType<decimal>(result));
    }

    // The UseSystemMathRound option opts back into the legacy System.Math.Round behavior on both numeric paths.
    [Fact]
    public void Round_UseSystemMathRound_DelegatesToSystemMath()
    {
        var doubleOptions = new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.UseSystemMathRound);
        var decimalOptions = new MathHelperOptions(CultureInfo.InvariantCulture,
            ExpressionOptions.UseSystemMathRound | ExpressionOptions.DecimalAsDefault);

        foreach (var mode in AllMidpointModes)
        {
            foreach (var digits in new[] { 0, 1, 2 })
            {
                foreach (var value in new[] { 0.15, 1.55, 2.5, 2.675, -1.55, 1.45, -2.675 })
                {
                    object viaDouble = MathHelper.Round(value, digits, mode, doubleOptions);
                    Assert.Equal(Math.Round(value, digits, mode), Assert.IsType<double>(viaDouble), precision: 12);

                    decimal decimalValue = (decimal)value;
                    object viaDecimal = MathHelper.Round(decimalValue, digits, mode, decimalOptions);
                    Assert.Equal(Math.Round(decimalValue, digits, mode), Assert.IsType<decimal>(viaDecimal));
                }
            }
        }
    }

    [Fact]
    public void Round_DefaultVsSystemMathRound_DifferOnDecimalMidpoint()
    {
        // 1.005 is an exact decimal midpoint at 2 digits (rounds away-from-zero to 1.01), but the binary double
        // for 1.005 is slightly below 1.005, so System.Math.Round(double) rounds it down to 1.00. The flag selects
        // between the midpoint-aware (decimal) semantics and the legacy System.Math.Round behavior.
        var defaultOptions = new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.None);
        var systemOptions = new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.UseSystemMathRound);

        object viaDefault = MathHelper.Round(1.005, 2, MidpointRounding.AwayFromZero, defaultOptions);
        object viaSystem = MathHelper.Round(1.005, 2, MidpointRounding.AwayFromZero, systemOptions);

        Assert.Equal(1.01, Assert.IsType<double>(viaDefault), precision: 12);  // midpoint-aware (decimal) semantics
        Assert.Equal(1.00, Assert.IsType<double>(viaSystem), precision: 12);   // legacy System.Math.Round(double) result
        Assert.NotEqual(viaDefault, viaSystem);                                // the option genuinely changes behavior
    }

    // ── BigDecimal exponentiation ─────────────────────────────────────────────

    [Fact]
    public void BigDecimalPow_ExponentZero_ReturnsOne()
    {
        var e = new Expression("x**0", ExpressionOptions.UseBigNumbers);
        e.Parameters["x"] = new BigDecimal(42);
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        CheckResult(1, result);
    }

    [Fact]
    public void BigDecimalPow_ExponentOne_ReturnsBase()
    {
        // Use a base > double.MaxValue (~1.8e308) so the result cannot be reduced to a
        // floating-point type and stays as BigInteger, enabling an exact string comparison.
        var bigBase = new BigDecimal(BigInteger.Parse("1" + new string('0', 400))); // 10^400
        var e = new Expression("x**1", ExpressionOptions.UseBigNumbers);
        e.Parameters["x"] = bigBase;
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        string expected = "1" + new string('0', 400);
        Assert.Equal(expected, result!.ToString());
    }

    [Fact]
    public void BigDecimalPow_LargeExponent_DoesNotOverflowDouble()
    {
        // 10^400 > double.MaxValue (~1.8e308); the double path would return Infinity.
        var e = new Expression("10**400", ExpressionOptions.UseBigNumbers);
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        string expected = "1" + new string('0', 400);
        Assert.Equal(expected, result!.ToString());
    }

    [Fact]
    public void BigDecimalPow_BaseExceedsDecimalRange_NoOverflow()
    {
        // The old code called ConvertToDecimal on the base, which throws for values > decimal.MaxValue
        // (~7.9e28).  The new code uses ConvertToBigDecimal and avoids that overflow.
        // Using exponent 11 so that result (10^29)^11 = 10^319 > double.MaxValue, preserving exact BigInteger.
        var bigBase = new BigDecimal(BigInteger.Parse("1" + new string('0', 29))); // 10^29 > decimal.MaxValue
        var e = new Expression("x**11", ExpressionOptions.UseBigNumbers);
        e.Parameters["x"] = bigBase;
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        string expected = "1" + new string('0', 319); // (10^29)^11 = 10^319
        Assert.Equal(expected, result!.ToString());
    }

    [Fact]
    public void BigDecimalPow_NegativeBaseOddExponent_ReturnsNegative()
    {
        // (-3)^3 = -27
        var e = new Expression("x**3", ExpressionOptions.UseBigNumbers);
        e.Parameters["x"] = new BigDecimal(-3);
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        CheckResult(-27, result);
    }

    [Fact]
    public void BigDecimalPow_NegativeBaseEvenExponent_ReturnsPositive()
    {
        // (-3)^4 = 81
        var e = new Expression("x**4", ExpressionOptions.UseBigNumbers);
        e.Parameters["x"] = new BigDecimal(-3);
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        CheckResult(81, result);
    }

    [Fact]
    public void BigDecimalPow_NegativeIntegerExponent()
    {
        // 2^(-3) = 0.125 via the BigDecimal path when the base is a BigDecimal
        var e = new Expression("x**(-3)", ExpressionOptions.UseBigNumbers);
        e.Parameters["x"] = new BigDecimal(2);
        var result = e.Evaluate(TestContext.Current.CancellationToken);
        CheckResult(0.125, result);
    }

    [Fact]
    public void BigDecimalPow_DoublePath_NoRegression()
    {
        // Ordinary double exponentiation must still work when no big-number options are set.
        var result = new Expression("2.0**3.0").Evaluate(TestContext.Current.CancellationToken);
        Assert.Equal(8.0, result);
    }

    [Fact]
    public void BigDecimalPow_NonIntegerExponent_UsesMathPow()
    {
        // Non-integer exponent falls back to Math.Pow (no exact BigDecimal support for fractional exponents).
        var result = new Expression("4**0.5").Evaluate(TestContext.Current.CancellationToken);
        CheckResult(2.0, result);
    }
}
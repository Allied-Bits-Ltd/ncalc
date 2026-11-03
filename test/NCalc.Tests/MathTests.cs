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
    [InlineData("2 ** 200000", ExpressionOptions.UseBigNumbers)]
    [InlineData("Pow(1.5, 20000)", ExpressionOptions.DecimalAsDefault)]
    [InlineData("1.5 ** 20000", ExpressionOptions.UseBigNumbers)]
    [InlineData("2 ** 2000000", ExpressionOptions.UseBigNumbers)]
    [InlineData("Pow(1.5, 100000)", ExpressionOptions.DecimalAsDefault)]
    [InlineData("1.5 ** 100000", ExpressionOptions.UseBigNumbers)]
    [InlineData("10 ** 5000000000", ExpressionOptions.UseBigNumbers)]
    [InlineData("1 ** 5000000000", ExpressionOptions.UseBigNumbers)]
    [InlineData("(-1) ** 5000000001", ExpressionOptions.UseBigNumbers)]
    public async Task BigPowerShouldCompleteQuickly(string expr, ExpressionOptions options)
    {
        // The exact power used to run for seconds to minutes; it must now either finish or fail with the size limit quickly
        object? result = null;
        Exception? error = null;
        var evaluation = Task.Run(() =>
        {
            try { result = new NCalc.Expression(expr, options | ExpressionOptions.NoCache).Evaluate(); }
            catch (Exception e) { error = e; }
        }, TestContext.Current.CancellationToken);
        var finished = await Task.WhenAny(evaluation, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(finished == evaluation, $"{expr} did not complete in 10 s");

        switch (expr)
        {
            case "2 ** 200000":
                Assert.Equal(BigInteger.Pow(2, 200000), result);
                break;
            case "1.5 ** 20000":
                Assert.Equal(new BigDecimal(BigInteger.Pow(15, 20000), -20000), result);
                break;
            // NCALC-321: without UseBigNumbers, a decimal base gives a double when the result does not fit in decimal
            case "Pow(1.5, 20000)":
            case "Pow(1.5, 100000)":
                Assert.Equal(double.PositiveInfinity, result);
                break;
            case "1 ** 5000000000":
                Assert.Equal(1, result);
                break;
            case "(-1) ** 5000000001":
                Assert.Equal(-1, result);
                break;
            default:
                Assert.IsType<NCalcEvaluationException>(error);
                break;
        }
    }

    [Theory]
    [InlineData("2 ** -3", ExpressionOptions.UseBigNumbers, 0.125)]
    [InlineData("(-2) ** 3", ExpressionOptions.UseBigNumbers, -8)]
    [InlineData("1.1 ** 3", ExpressionOptions.DecimalAsDefault, 1.331)]
    [InlineData("0.5 ** 2", ExpressionOptions.DecimalAsDefault, 0.25)]
    public void ExactPowerShouldKeepSmallResults(string expr, ExpressionOptions options, double expected)
    {
        Assert.Equal(expected, Convert.ToDouble(new NCalc.Expression(expr, options | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken)), 10);
    }

    [Fact]
    public void BigDecimalConversionsShouldNotDependOnCurrentCulture()
    {
        // BigDecimal remembers the culture current at its first use; at least one of these cultures differs from it in the decimal separator
        var oldCulture = CultureInfo.CurrentCulture;
        var x = new BigDecimal(new BigInteger(15), -1);   // 1.5
        try
        {
            foreach (var name in new[] { "en-US", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                var options = new MathHelperOptions(CultureInfo.CurrentCulture, ExpressionOptions.None);

                Assert.Equal(1.5, MathHelper.ConvertToDouble(x, options));
                Assert.Equal(1.5m, MathHelper.ConvertToDecimal(x, options));
                Assert.Equal(1, MathHelper.ConvertToInt(x, options));
                Assert.Equal(1L, MathHelper.ConvertToLong(x, options));
                Assert.Equal(1UL, MathHelper.ConvertToULong(x, options));
                Assert.Equal(-1, MathHelper.ConvertToInt(-x, options));
                Assert.Equal(1.5, new ComplexNumber(x, BigDecimal.Zero).ToComplex().Real);
                Assert.Equal(1.5f, new NCalc.Domain.Vector([x, x]).ToVector2().X);

                var expression = new NCalc.Expression("Sqrt(x * x) + 1", ExpressionOptions.NoCache);
                expression.Parameters["x"] = x;
                Assert.Equal(2.5, Convert.ToDouble(expression.Evaluate(TestContext.Current.CancellationToken)), 10);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    [Fact]
    public void MixedNumericTypesShouldMatchCSharpArithmetic()
    {
        // Every pair of numeric types and every arithmetic operator is compared with what C# computes;
        // pairs that C# does not allow (e.g. decimal with double) are skipped
        object[] lefts = [7, 7L, 7UL, 7.5f, 7.5, 7.5m];
        object[] rights = [2, 2L, 2UL, 2.5f, 2.5, 2.5m];
        var failures = new List<string>();

        foreach (var a in lefts)
        foreach (var b in rights)
        foreach (var op in new[] { "+", "-", "*", "/", "%" })
        {
            double expected;
            try
            {
                dynamic da = a, db = b;
                expected = Convert.ToDouble(op switch { "+" => da + db, "-" => da - db, "*" => da * db, "/" => da / db, _ => da % db });
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                continue;
            }

            var expression = new NCalc.Expression($"a {op} b", ExpressionOptions.NoCache);
            expression.Parameters["a"] = a;
            expression.Parameters["b"] = b;
            var actual = Convert.ToDouble(expression.Evaluate(TestContext.Current.CancellationToken));

            // C# divides integers with truncation, NCalc divides them exactly
            if (op == "/" && MathHelper.IsBoxedIntegerNumber(a) && MathHelper.IsBoxedIntegerNumber(b))
                expected = Convert.ToDouble(a) / Convert.ToDouble(b);

            if (Math.Abs(actual - expected) > 1e-6 * Math.Max(1, Math.Abs(expected)))
                failures.Add($"{a.GetType().Name} {op} {b.GetType().Name}: expected {expected}, got {actual}");
        }

        Assert.Empty(failures);
    }

    [Theory]
    [InlineData("1 / 0.0", ExpressionOptions.None, double.PositiveInfinity)]
    [InlineData("-1 / 0.0", ExpressionOptions.None, double.NegativeInfinity)]
    [InlineData("1 / 0.0", ExpressionOptions.ReduceDivResultToInteger, double.PositiveInfinity)]
    [InlineData("1e20 % 3e20", ExpressionOptions.None, 1e20)]
    [InlineData("1e30 // 1", ExpressionOptions.None, 1e30)]
    [InlineData("-1e30 // 1", ExpressionOptions.None, -1e30)]
    [InlineData("1e20 + 1", ExpressionOptions.ReduceArithmeticResultType, 1e20)]
    [InlineData("1e19 * 1", ExpressionOptions.ReduceArithmeticResultType, 1e19)]
    [InlineData("4 / 2.0", ExpressionOptions.None, 2)]
    [InlineData("7 // 2.0", ExpressionOptions.None, 3)]
    [InlineData("1e18 // 1", ExpressionOptions.None, 1000000000000000000L)]
    [InlineData("4.0 + 2", ExpressionOptions.ReduceArithmeticResultType, 6)]
    public void InfiniteAndLargeDoublesShouldNotBeClampedToLong(string expr, ExpressionOptions options, object expected)
    {
        Assert.Equal(expected, new NCalc.Expression(expr, options | ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("2 in (1, 2)", true)]
    [InlineData("2 in (1, 2) and index = 3", true)]
    [InlineData("index * 2", 6)]
    [InlineData("info", 4)]
    [InlineData("i_count + 1", 6)]
    [InlineData("in(index, 1, 3)", true)]
    public void ComplexNumbersShouldNotBreakIdentifiersAndInOperator(string expr, object expected)
    {
        var expression = new NCalc.Expression(expr, ExpressionOptions.None);
        expression.AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseComplexNumbers);
        expression.Parameters["index"] = 3;
        expression.Parameters["info"] = 4;
        expression.Parameters["i_count"] = 5;

        Assert.Equal(expected, expression.Evaluate(TestContext.Current.CancellationToken));
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
    public void VectorModuloShouldKeepFractionalPart()
    {
        var result = EvalVec("[7.5, -7.5, 9] % 2");
        Assert.Equal(BigDecimal.Parse("1.5", CultureInfo.InvariantCulture), result[0]);
        Assert.Equal(BigDecimal.Parse("-1.5", CultureInfo.InvariantCulture), result[1]);
        Assert.Equal(new BigDecimal(1), result[2]);
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

    [Theory]
    [InlineData("-2 * 3", -6)]
    [InlineData("-2 + 3", 1)]
    [InlineData("-7 % 3", -1)]
    [InlineData("-(1)", -1)]
    [InlineData("-x * 2", -10)]
    public void NegationShouldKeepIntegerType(string expr, int expected)
    {
        var expression = new Expression(expr, ExpressionOptions.NoCache);
        expression.Parameters["x"] = 5;
        Assert.Equal(expected, expression.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NegationShouldKeepFloatingPointAndDecimalTypes()
    {
        Assert.Equal(-2.5, new Expression("-x", ExpressionOptions.NoCache) { Parameters = { ["x"] = 2.5 } }.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(-2.5f, new Expression("-x", ExpressionOptions.NoCache) { Parameters = { ["x"] = 2.5f } }.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(-2.5m, new Expression("-x", ExpressionOptions.NoCache) { Parameters = { ["x"] = 2.5m } }.Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void NegationOfMinValueShouldPromoteToWiderType()
    {
        Assert.Equal(-(long)int.MinValue, new Expression("-x", ExpressionOptions.NoCache) { Parameters = { ["x"] = int.MinValue } }.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(9223372036854775808UL, new Expression("-x", ExpressionOptions.NoCache) { Parameters = { ["x"] = long.MinValue } }.Evaluate(TestContext.Current.CancellationToken));
    }

    public static TheoryData<object, string, object, string> MixedBigNumberOperations => new()
    {
        { new BigDecimal(-36), "%", ulong.MaxValue, "-36" },
        { new BigInteger(-36), "%", ulong.MaxValue, "-36" },
        { 360UL, "//", new BigDecimal(-36), "-10" },
        { ulong.MaxValue, "//", new BigDecimal(-36), "-512409557603043101" },
        { new BigDecimal(-36), "*", ulong.MaxValue, "-664082786653543858140" },
        { new BigDecimal(-36), "%", 2.5, "-1" },
        { 2.5m, "%", new BigInteger(-36), "2.5" },
        { BigDecimal.Parse("7.5", CultureInfo.InvariantCulture), "%", 2, "1.5" },
        { 2.5, "/", new BigDecimal(-36), "-0.06944444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444444" },
        { new BigDecimal(-36), "*", 5, "-180" },
        { 2.5, "*", new BigInteger(-36), "-90" },
        { new BigDecimal(-36), "*", new BigDecimal(5), "-180" },
        { new BigDecimal(-36), "*", 5UL, "-180" },
        { 5UL, "+", new BigDecimal(-36), "-31" },
        { (byte)5, "*", new BigInteger(-36), "-180" },
        { new BigDecimal(-36), "-", 2.5m, "-38.5" },
        { new BigDecimal(-36), "/", 4f, "-9" },
    };

    [Theory]
    [InlineData("1.23456789012345678901 * 1", "1.23456789012345678901")]
    [InlineData("Pow(7.6319, 5)", "25891.86632016877607037599")]
    [InlineData("1.5 * 2.25", "3.375")]
    public void BigDecimalResultShouldNotBeNarrowedToFloat(string expression, string expected)
    {
        var result = new Expression(expression, ExpressionOptions.NoCache | ExpressionOptions.UseBigNumbers, CultureInfo.InvariantCulture).Evaluate(TestContext.Current.CancellationToken);
        Assert.IsNotType<float>(result);
        Assert.Equal(BigDecimal.Parse(expected, CultureInfo.InvariantCulture), MathHelper.ConvertToBigDecimal(result));
    }

    [Theory]
    [MemberData(nameof(MixedBigNumberOperations))]
    public void BigNumberMixedWithOtherTypeShouldBeComputedWithoutUseBigNumbers(object a, string op, object b, string expected)
    {
        foreach (var options in new[] { ExpressionOptions.NoCache, ExpressionOptions.NoCache | ExpressionOptions.UseBigNumbers })
        {
            var result = new Expression($"a {op} b", options, CultureInfo.InvariantCulture) { Parameters = { ["a"] = a, ["b"] = b } }.Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(BigDecimal.Parse(expected, CultureInfo.InvariantCulture), MathHelper.ConvertToBigDecimal(result));
        }
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

    [Theory]
    [InlineData("Round(1234, -2)", 1200)]
    [InlineData("Round(1250, -2)", 1200)]
    [InlineData("Round(1350, -2)", 1400)]
    [InlineData("Round(-1250, -2)", -1200)]
    [InlineData("Round(5, -3)", 0)]
    public void RoundToNegativeDigitsShouldWorkForIntegers(string text, int expected)
    {
        // NCALC-077: negative digits were ignored for integers
        Assert.Equal(expected, new Expression(text).Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RoundShouldHandleAnyNumberOfDigitsForAllTypes()
    {
        // NCALC-077: doubles, decimals and big decimals threw ArgumentOutOfRangeException or ArithmeticException
        Assert.Equal(1200.0, new Expression("Round(1234.5, -2)").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(1300.0, new Expression("Round(1250.5, -2)").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(1200m, new Expression("Round(1234.5, -2)", ExpressionOptions.DecimalAsDefault).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(1.5, new Expression("Round(1.5, 16)").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(1.5m, new Expression("Round(1.5, 40)", ExpressionOptions.DecimalAsDefault).Evaluate(TestContext.Current.CancellationToken));

        var big = new Expression("Round(x, -2)");
        big.Parameters["x"] = new BigDecimal(new BigInteger(12345), -1);
        Assert.Equal(new BigDecimal(1200), big.Evaluate(TestContext.Current.CancellationToken));
        var bigFraction = new Expression("Round(x, 1)", ExpressionOptions.RoundAwayFromZero);
        bigFraction.Parameters["x"] = new BigDecimal(new BigInteger(125), -2);
        Assert.Equal(new BigDecimal(new BigInteger(13), -1), bigFraction.Evaluate(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Abs(x)")]
    [InlineData("Sin(x)")]
    [InlineData("Exp(x)")]
    [InlineData("Sqrt(x)")]
    [InlineData("Log(x, 2)")]
    [InlineData("Log(2, x)")]
    [InlineData("Round(1.5, x)")]
    [InlineData("Round(x, 1)")]
    [InlineData("Pow(x, 2)")]
    [InlineData("Pow(2, x)")]
    [InlineData("Max(x, 1)")]
    [InlineData("Min(1, x)")]
    [InlineData("Ceiling(x)")]
    [InlineData("Truncate(x)")]
    [InlineData("Ln(x)")]
    [InlineData("Atan2(x, 1)")]
    public async Task NullArgumentShouldGiveNull(string text)
    {
        // NCALC-078: the built-in functions handled null arguments in different ways
        var expression = new Expression(text, ExpressionOptions.AllowNullParameter);
        expression.Parameters["x"] = null;
        Assert.Null(expression.Evaluate(TestContext.Current.CancellationToken));

        var asyncExpression = new AsyncExpression(text, ExpressionOptions.AllowNullParameter);
        asyncExpression.Parameters["x"] = null;
        Assert.Null(await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void MaxAndMinShouldWidenPropagateNaNAndRejectMixedKinds()
    {
        // NCALC-079
        Assert.Equal(3.0, new Expression("Max(3, 2.5)").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(2.5, new Expression("Min(3, 2.5)").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(5, new Expression("Max('5', 3)").Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal("b", new Expression("Max('a', 'b')").Evaluate(TestContext.Current.CancellationToken));
        var nan = new Expression("Max(x, 1)");
        nan.Parameters["x"] = double.NaN;
        Assert.Equal(double.NaN, nan.Evaluate(TestContext.Current.CancellationToken));
        Assert.Throws<NCalcEvaluationException>(() => new Expression("Max(1, 'abc')").Evaluate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void VectorModuloShouldMatchScalarModulo()
    {
        // NCALC-080
        var options = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, AdvExpressionOptions.ParseVectors);
        var result = (NCalc.Domain.Vector)new Expression("[5.5; 7] % 2") { AdvancedOptions = options }.Evaluate(TestContext.Current.CancellationToken)!;
        Assert.Equal(new BigDecimal(new BigInteger(15), -1), result[0]);
        Assert.Equal(new BigDecimal(1), result[1]);
        result = (NCalc.Domain.Vector)new Expression("[5.5; 7] % 0.5") { AdvancedOptions = options }.Evaluate(TestContext.Current.CancellationToken)!;
        Assert.Equal(new BigDecimal(0), result[0]);
        Assert.Equal(new BigDecimal(0), result[1]);
    }

    [Fact]
    public void ShiftsShouldNotMaskTheCount()
    {
        // NCALC-081
        Assert.Equal(0, Convert.ToInt32(new Expression("1 << 64").Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture));
        Assert.Equal(0, Convert.ToInt32(new Expression("1 << 100").Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture));
        Assert.Equal(0, Convert.ToInt32(new Expression("16 >> 70").Evaluate(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture));
        Assert.Equal(BigInteger.Pow(2, 100), new Expression("1 << 100", ExpressionOptions.UseBigNumbers).Evaluate(TestContext.Current.CancellationToken));
        Assert.ThrowsAny<OverflowException>(() => new Expression("1 << 100", ExpressionOptions.OverflowProtection).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(4UL, MathHelper.RightShift(16, 2, false, MathHelperOptions.Empty));
        Assert.Equal(64UL, MathHelper.LeftShift(16, 2, false, MathHelperOptions.Empty));
    }

    [Theory]
    [InlineData("Asin(x)", -881251, -6)]
    [InlineData("Acos(x)", 870639, -6)]
    [InlineData("Ln(x)", 8409, -1)]
    [InlineData("Log10(x)", 8409, -1)]
    public void BigDecimalTranscendentalFunctionsShouldBeBounded(string text, int mantissa, int exponent)
    {
        // NCALC-017: the series of ExtendedNumerics grew to tens of thousands of digits and took from seconds to minutes;
        // the result has about BigDecimal.Precision digits
        var x = new BigDecimal(new BigInteger(mantissa), exponent);
        var expression = new Expression(text, ExpressionOptions.UseBigNumbers | ExpressionOptions.NoCache);
        expression.Parameters["x"] = x;
        var result = Assert.IsType<BigDecimal>(expression.Evaluate(TestContext.Current.CancellationToken));

        Assert.True(BigInteger.Abs(result.Mantissa).ToString(CultureInfo.InvariantCulture).Length <= BigDecimal.Precision + 10);
        double value = mantissa * Math.Pow(10, exponent);
        double expected = text switch
        {
            "Asin(x)" => Math.Asin(value),
            "Acos(x)" => Math.Acos(value),
            "Ln(x)" => Math.Log(value),
            _ => Math.Log10(value),
        };
        Assert.Equal(expected, MathHelper.ConvertToDouble(result, MathHelperOptions.Empty), 12);
    }

    [Theory]
    [InlineData("Ln(x)", -690.0823807176538)]
    [InlineData("Log10(x)", -299.69897000433605)]
    [InlineData("Log(x, 7)", -354.6321915493735)]
    public void BigNumberLogarithmsOfTinyArgumentsShouldBeComputed(string text, double expected)
    {
        // NCALC-101: x = 2E-300 was truncated to zero while the logarithm ran, which threw NCalcEvaluationException
        var expression = new Expression(text, ExpressionOptions.UseBigNumbers | ExpressionOptions.NoCache);
        expression.Parameters["x"] = new BigDecimal(2, -300);
        var result = Assert.IsType<BigDecimal>(expression.Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(expected, MathHelper.ConvertToDouble(result, MathHelperOptions.Empty), 12);
    }

    [Fact]
    public void BigNumberRootsAndLogarithmsShouldNotMultiplyOutHugeExponents()
    {
        // NCALC-101: the functions computed by NCalc split off the power of 10 of the argument
        var options = new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.UseBigNumbers);
        Assert.Equal(new BigDecimal(1, 1000000000), MathHelper.Sqrt(new BigDecimal(1, 2000000000), options));
        Assert.Equal(new BigDecimal(-2, 333333333), MathHelper.Cbrt(new BigDecimal(-8, 999999999), options));
        Assert.Equal(new BigDecimal(2000000000), MathHelper.Log10(new BigDecimal(1, 2000000000), options));
        Assert.Equal(-4605170185.2949441874760375999514962937, MathHelper.ConvertToDouble(MathHelper.Ln(new BigDecimal(2, -2000000000), options), MathHelperOptions.Empty), 4);
    }

    [Fact]
    public void BigNumberFunctionsShouldNotTruncateBigDecimalsOfOtherThreads()
    {
        // NCALC-101: the big-number functions set the process-wide BigDecimal.AlwaysTruncate while they ran, which truncated the
        // BigDecimal results of every other thread to BigDecimal.Precision digits
        const ExpressionOptions options = ExpressionOptions.UseBigNumbers | ExpressionOptions.NoCache;
        var expected = new Expression("1.5 ** 400", options, CultureInfo.InvariantCulture).Evaluate(TestContext.Current.CancellationToken);
        string[] functions = ["Ln(x)", "Log10(x)", "Log(x, 3)", "Exp(x)", "Sqrt(x)", "∛x", "∜x","Atan(x)", "Asin(1 / x)", "Acos(1 / x)"];
        var x = new BigDecimal(123456789, -5);
        var helperOptions = new MathHelperOptions(CultureInfo.InvariantCulture, options);
        int rounds = 0;
        using var stop = new CancellationTokenSource();
        var background = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                foreach (var function in functions)
                {
                    var expression = new Expression(function, options);
                    expression.Parameters["x"] = x;
                    expression.Evaluate();
                }
                MathHelper.Tanh(x / 100000, helperOptions);
                MathHelper.Sinh(BigDecimal.One / x, helperOptions);
                MathHelper.Arccot(-x, helperOptions);
                Interlocked.Increment(ref rounds);
            }
        }, TestContext.Current.CancellationToken);

        try
        {
            // until the functions have run a few times
            for (int i = 0; i < 200 || Volatile.Read(ref rounds) < 3; i++)
            {
                Assert.False(background.IsFaulted);
                Assert.False(BigDecimal.AlwaysTruncate);
                Assert.Equal(expected, new Expression("1.5 ** 400", options, CultureInfo.InvariantCulture).Evaluate(TestContext.Current.CancellationToken));
            }
        }
        finally
        {
            stop.Cancel();
            background.Wait(TestContext.Current.CancellationToken);
        }
    }

    private static object? EvaluateWith(string text, ExpressionOptions options, params (string Name, object Value)[] parameters)
    {
        var expression = new Expression(text, options | ExpressionOptions.NoCache);
        foreach (var (name, value) in parameters)
            expression.Parameters[name] = value;
        return expression.Evaluate(TestContext.Current.CancellationToken);
    }

    private static BigInteger AsBigInteger(object? value) => MathHelper.ConvertToBigInteger(value!);

    // NCALC-103
    [Fact]
    public void BigNumberDivisionShouldNotDependOnCurrentCulture()
    {
        // BigDecimal remembers the culture current at its first use; Divide used its culture-dependent conversions
        var oldCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("sk-SK");
            _ = new BigDecimal(new BigInteger(15), -1).ToString();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            foreach (var options in new[] { ExpressionOptions.UseBigNumbers, ExpressionOptions.UseBigNumbers | ExpressionOptions.DecimalAsDefault })
            {
                Assert.Equal(0.25, Convert.ToDouble(EvaluateWith("a / b", options, ("a", 1.0), ("b", 4.0)), CultureInfo.InvariantCulture));
                Assert.Equal(0.25, Convert.ToDouble(EvaluateWith("a / b", options, ("a", 1.0m), ("b", 4.0m)), CultureInfo.InvariantCulture));
                Assert.Equal(0.75, Convert.ToDouble(EvaluateWith("a / b", options, ("a", 1.5f), ("b", 2f)), CultureInfo.InvariantCulture));
            }

            var mathOptions = new MathHelperOptions(CultureInfo.CurrentCulture, ExpressionOptions.UseBigNumbers);
            Assert.Equal(0.25, Convert.ToDouble(MathHelper.Divide(1.0, 4.0, true, mathOptions), CultureInfo.InvariantCulture));
            Assert.Equal(0.25, Convert.ToDouble(MathHelper.Divide(1.0, 4.0, false, mathOptions), CultureInfo.InvariantCulture));
            Assert.Equal(1.5, Convert.ToDouble(EvaluateWith("1.5", ExpressionOptions.None), CultureInfo.InvariantCulture));
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
        }
    }

    // NCALC-107
    [Fact]
    public void IntegerDivisionShouldHandleULongAboveLongMaxValue()
    {
        Assert.Equal(9223372036854775807m, Convert.ToDecimal(EvaluateWith("a // 2", ExpressionOptions.None, ("a", ulong.MaxValue)), CultureInfo.InvariantCulture));
        Assert.Equal(9223372036854775807m, Convert.ToDecimal(EvaluateWith("a \\ 2", ExpressionOptions.None, ("a", ulong.MaxValue)), CultureInfo.InvariantCulture));
        Assert.Equal(ulong.MaxValue, Convert.ToUInt64(EvaluateWith("a \\ 1", ExpressionOptions.None, ("a", ulong.MaxValue)), CultureInfo.InvariantCulture));
        Assert.Equal(9223372036854775807.5m, Convert.ToDecimal(MathHelper.Divide(ulong.MaxValue, 2, true, MathHelperOptions.Empty), CultureInfo.InvariantCulture));

        object[] ones = [(byte)1, (sbyte)1, (short)1, (ushort)1, 1, 1u, 1L, 1UL, 1f, 1.0, 1m, BigInteger.One];
        foreach (var one in ones)
        {
            Assert.Equal(0m, Convert.ToDecimal(EvaluateWith("x // a", ExpressionOptions.None, ("x", one), ("a", ulong.MaxValue)), CultureInfo.InvariantCulture));
            Assert.Equal(0m, Convert.ToDecimal(EvaluateWith("x \\ a", ExpressionOptions.None, ("x", one), ("a", ulong.MaxValue)), CultureInfo.InvariantCulture));
        }
    }

    // NCALC-108
    [Fact]
    public void ULongArithmeticShouldNotOverflowWhenTheResultFits()
    {
        Assert.Equal(ulong.MaxValue, EvaluateWith("a * 1", ExpressionOptions.None, ("a", ulong.MaxValue)));
        Assert.Equal(long.MaxValue, Convert.ToInt64(EvaluateWith("a + b", ExpressionOptions.None, ("a", 1UL << 63), ("b", -1L)), CultureInfo.InvariantCulture));
        Assert.Equal(long.MaxValue, Convert.ToInt64(EvaluateWith("a - b", ExpressionOptions.None, ("a", 1UL << 63), ("b", 1L)), CultureInfo.InvariantCulture));
        Assert.Equal(ulong.MaxValue, EvaluateWith("Max(a, b)", ExpressionOptions.None, ("a", ulong.MaxValue), ("b", -1)));
        Assert.Equal(-1, EvaluateWith("Min(a, b)", ExpressionOptions.None, ("a", ulong.MaxValue), ("b", -1)));

        // Without big numbers, a ulong product wraps around as in C#; with OverflowProtection it overflows
        Assert.Equal(ulong.MaxValue - 1, EvaluateWith("a * b", ExpressionOptions.None, ("a", ulong.MaxValue), ("b", 2UL)));
        Assert.Throws<OverflowException>(() => EvaluateWith("a * b", ExpressionOptions.OverflowProtection, ("a", ulong.MaxValue), ("b", 2UL)));

        // NCALC-315: a ulong and a signed integer have no common 64-bit type; their product is exact
        Assert.Equal(new BigInteger(ulong.MaxValue) * 2, EvaluateWith("a * 2", ExpressionOptions.None, ("a", ulong.MaxValue)));
        Assert.Equal(new BigInteger(ulong.MaxValue) * 2, EvaluateWith("a * 2", ExpressionOptions.OverflowProtection, ("a", ulong.MaxValue)));
    }

    // NCALC-109
    [Fact]
    public void BigNumbersShouldPromoteIntegerAdditionAndSubtraction()
    {
        var options = ExpressionOptions.UseBigNumbers;
        Assert.Equal(new BigInteger(int.MaxValue) + 1, AsBigInteger(EvaluateWith("a + 1", options, ("a", int.MaxValue))));
        Assert.Equal(new BigInteger(ulong.MaxValue) + 1, AsBigInteger(EvaluateWith("a + 1", options, ("a", ulong.MaxValue))));
        Assert.Equal(new BigInteger(long.MinValue) - 1, AsBigInteger(EvaluateWith("a - 1", options, ("a", long.MinValue))));
        Assert.Equal(BigInteger.MinusOne, AsBigInteger(EvaluateWith("a - b", options, ("a", 1UL), ("b", 2UL))));
        Assert.Equal(BigInteger.MinusOne, AsBigInteger(EvaluateWith("a - b", options | ExpressionOptions.OverflowProtection, ("a", 1UL), ("b", 2UL))));

        // Results that fit keep their type
        Assert.Equal(3, EvaluateWith("a + b", options, ("a", 1), ("b", 2)));

        // Without UseBigNumbers, the sum wraps around as in C#
        Assert.Equal(int.MinValue, EvaluateWith("a + 1", ExpressionOptions.None, ("a", int.MaxValue)));
    }

    // NCALC-110
    [Theory]
    [InlineData("10 ** 200000", ExpressionOptions.UseBigNumbers)]
    [InlineData("10 ** 200000", ExpressionOptions.DecimalAsDefault)]
    [InlineData("100 ** 150000", ExpressionOptions.UseBigNumbers)]
    [InlineData("Pow(10, 200000)", ExpressionOptions.UseBigNumbers)]
    public void PowerOfTenShouldRespectTheDigitLimit(string text, ExpressionOptions options)
    {
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith(text, options));
    }

    // NCALC-110
    [Fact]
    public void PowerAndShiftDigitLimitShouldStillAllowSmallResults()
    {
        Assert.Equal(BigInteger.Pow(10, 20), AsBigInteger(EvaluateWith("10 ** 20", ExpressionOptions.UseBigNumbers)));
        Assert.Equal(BigInteger.Pow(100, 30), AsBigInteger(EvaluateWith("100 ** 30", ExpressionOptions.UseBigNumbers)));
        Assert.Equal(0.001, Convert.ToDouble(EvaluateWith("10 ** -3", ExpressionOptions.UseBigNumbers), CultureInfo.InvariantCulture), 12);

        // A negative shift count is rejected for big integers as for the other integer types, so the limit cannot be bypassed
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("a >> -400000", ExpressionOptions.UseBigNumbers, ("a", BigInteger.One)));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("a << -3", ExpressionOptions.UseBigNumbers, ("a", new BigInteger(8))));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("a << 400000", ExpressionOptions.UseBigNumbers, ("a", BigInteger.One)));
        Assert.Equal(new BigInteger(1), AsBigInteger(EvaluateWith("a >> 3", ExpressionOptions.UseBigNumbers, ("a", new BigInteger(8)))));
    }

    // NCALC-122
    [Fact]
    public void ShiftsShouldHandleNegativeValuesAndBigIntegerCounts()
    {
        Assert.Equal(-2L, Convert.ToInt64(EvaluateWith("-1 << 1", ExpressionOptions.None), CultureInfo.InvariantCulture));
        Assert.Equal(-4L, Convert.ToInt64(EvaluateWith("a >> 1", ExpressionOptions.None, ("a", -8)), CultureInfo.InvariantCulture));
        Assert.Equal(-1L, Convert.ToInt64(EvaluateWith("a >> 100", ExpressionOptions.None, ("a", -8)), CultureInfo.InvariantCulture));
        Assert.Equal(long.MinValue, Convert.ToInt64(EvaluateWith("a << 63", ExpressionOptions.None, ("a", -1L)), CultureInfo.InvariantCulture));
        Assert.Equal(0L, Convert.ToInt64(EvaluateWith("a << 64", ExpressionOptions.None, ("a", -1L)), CultureInfo.InvariantCulture));
        Assert.Throws<OverflowException>(() => EvaluateWith("a << 64", ExpressionOptions.OverflowProtection, ("a", -1L)));
        Assert.Equal(-(BigInteger.One << 64), AsBigInteger(EvaluateWith("a << 64", ExpressionOptions.UseBigNumbers, ("a", -1L))));

        Assert.Equal(8L, Convert.ToInt64(EvaluateWith("1 << b", ExpressionOptions.None, ("b", new BigInteger(3))), CultureInfo.InvariantCulture));
        Assert.Equal(1L, Convert.ToInt64(EvaluateWith("8 >> b", ExpressionOptions.None, ("b", new BigInteger(3))), CultureInfo.InvariantCulture));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("1 << b", ExpressionOptions.None, ("b", BigInteger.Pow(2, 40))));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("1 << -3", ExpressionOptions.None));
    }

    // NCALC-124
    [Fact]
    public void RealComplexOperandsShouldNotThrowInvalidCastException()
    {
        var options = new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.None);
        var one = new ComplexNumber(new BigDecimal(1), BigDecimal.Zero);
        var two = new ComplexNumber(new BigDecimal(2), BigDecimal.Zero);
        var seven = new ComplexNumber(new BigDecimal(7), BigDecimal.Zero);
        var nonReal = new ComplexNumber(new BigDecimal(1), new BigDecimal(2));

        Assert.Equal(-1, MathHelper.Compare(one, two, options, options));
        Assert.Equal(2.0, MathHelper.ConvertToDouble(MathHelper.Max(one, two, options), options));
        Assert.Equal(1.0, MathHelper.ConvertToDouble(MathHelper.Min(one, two, options), options));
        Assert.Equal(3L, Convert.ToInt64(MathHelper.IntegerDivide(seven, two, true, true, options), CultureInfo.InvariantCulture));
        Assert.Equal(1.0, Convert.ToDouble(MathHelper.Modulo(seven, two, true, options), CultureInfo.InvariantCulture));
        // NCALC-434: an NCalc exception, not InvalidOperationException
        Assert.Throws<NCalcEvaluationException>(() => MathHelper.IntegerDivide(3, nonReal, true, true, options));
        Assert.Throws<NCalcEvaluationException>(() => MathHelper.Modulo(3, nonReal, true, options));
    }

    // NCALC-130
    [Fact]
    public void IntegerEdgeCasesShouldGiveCorrectResults()
    {
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("3.5!", ExpressionOptions.None));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("3.5!", ExpressionOptions.UseBigNumbers));
        Assert.Equal(24L, Convert.ToInt64(EvaluateWith("a!", ExpressionOptions.None, ("a", 4.0)), CultureInfo.InvariantCulture));
        Assert.Equal(24L, Convert.ToInt64(EvaluateWith("a!", ExpressionOptions.UseBigNumbers, ("a", 4.0)), CultureInfo.InvariantCulture));

        Assert.True(double.IsNaN(Convert.ToDouble(EvaluateWith("a \\ 1", ExpressionOptions.None, ("a", double.NaN)), CultureInfo.InvariantCulture)));
        Assert.Equal(1e30, Convert.ToDouble(EvaluateWith("a \\ 1", ExpressionOptions.None, ("a", 1e30)), CultureInfo.InvariantCulture));
        Assert.Equal(3L, Convert.ToInt64(EvaluateWith("a \\ 2", ExpressionOptions.None, ("a", 7.9)), CultureInfo.InvariantCulture));

        Assert.Equal(0L, Convert.ToInt64(EvaluateWith("a % b", ExpressionOptions.None, ("a", long.MinValue), ("b", -1L)), CultureInfo.InvariantCulture));
        Assert.Equal(new BigInteger(long.MinValue) * -1, AsBigInteger(EvaluateWith("a \\ b", ExpressionOptions.None, ("a", long.MinValue), ("b", -1L))));

        Assert.Equal(long.MinValue, EvaluateWith("-a", ExpressionOptions.None, ("a", 1UL << 63)));
        Assert.Equal(long.MinValue, Convert.ToInt64(EvaluateWith("-(-a)", ExpressionOptions.None, ("a", long.MinValue)), CultureInfo.InvariantCulture));
    }

    // NCALC-121
    [Fact]
    public void PercentArithmeticShouldNotDependOnDynamicFunctions()
    {
        // NCalc.AOT always computes percentages without dynamic code, as AvoidDynamicFunctions does
        object[] values = [200, 200L, 200f, 200.0, 200m, new BigInteger(200), new BigDecimal(200)];
        var expected = new Dictionary<string, double> { ["+"] = 220, ["-"] = 180, ["*"] = 20, ["/"] = 2000 };
        foreach (var baseOptions in new[] { ExpressionOptions.None, ExpressionOptions.DecimalAsDefault })
        {
            foreach (var op in expected.Keys)
            {
                foreach (var a in values)
                {
                    object? Run(ExpressionOptions options)
                    {
                        var expression = new Expression($"a {op} 10%", options | ExpressionOptions.NoCache)
                        {
                            AdvancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.CalculatePercent)
                        };
                        expression.Parameters["a"] = a;
                        return expression.Evaluate(TestContext.Current.CancellationToken);
                    }

                    var dynamicResult = Run(baseOptions);
                    var staticResult = Run(baseOptions | ExpressionOptions.AvoidDynamicFunctions);
                    string description = $"{a} ({a.GetType().Name}) {op} 10% with {baseOptions}";
                    Assert.True(staticResult?.GetType() == dynamicResult?.GetType(), $"{description}: {dynamicResult?.GetType().Name} and {staticResult?.GetType().Name}");
                    Assert.Equal(expected[op], MathHelper.ConvertToDouble(dynamicResult, MathHelperOptions.Empty), 10);
                    Assert.Equal(expected[op], MathHelper.ConvertToDouble(staticResult, MathHelperOptions.Empty), 10);
                }
            }
        }
    }

    // NCALC-121
    [Fact]
    public void RoundingFunctionsShouldAcceptPercentages()
    {
        var p = new Percent(12.346);
        Assert.Equal(new Percent(12.35), EvaluateWith("Round(p, 2)", ExpressionOptions.None, ("p", p)));
        Assert.Equal(new Percent(13.0), EvaluateWith("Ceiling(p)", ExpressionOptions.None, ("p", p)));
        Assert.Equal(new Percent(12.0), EvaluateWith("Floor(p)", ExpressionOptions.None, ("p", p)));
        Assert.Equal(new Percent(12.0), EvaluateWith("Truncate(p)", ExpressionOptions.None, ("p", p)));
        Assert.Equal(new Percent(-12.0), EvaluateWith("Truncate(p)", ExpressionOptions.None, ("p", new Percent(-12.9))));
        Assert.Equal(new Percent(12.345), EvaluateWith("Abs(p)", ExpressionOptions.None, ("p", new Percent(-12.345))));
        Assert.Equal(-1, EvaluateWith("Sign(p)", ExpressionOptions.None, ("p", new Percent(-5))));
        Assert.Equal(0.5, Convert.ToDouble(EvaluateWith("Sqrt(p)", ExpressionOptions.None, ("p", new Percent(25))), CultureInfo.InvariantCulture), 12);
        Assert.Equal(new Percent(20), EvaluateWith("Max(a, b)", ExpressionOptions.None, ("a", new Percent(10)), ("b", new Percent(20))));
        Assert.Equal(new Percent(10), EvaluateWith("Min(a, b)", ExpressionOptions.None, ("a", new Percent(10)), ("b", new Percent(20))));
    }

    // NCALC-125
    [Fact]
    public void RoundShouldHandleExtremeDigitCounts()
    {
        Assert.Equal(0, EvaluateWith("Round(15, -2147483648)", ExpressionOptions.None));
        Assert.Equal(0, EvaluateWith("Round(15, -2147483647)", ExpressionOptions.None));
        Assert.Equal(15, EvaluateWith("Round(15, 2147483647)", ExpressionOptions.None));
        Assert.Equal(0.0, EvaluateWith("Round(0.12345, -2147483647)", ExpressionOptions.None));
        Assert.Equal(0.0, EvaluateWith("Round(0.12345, -2147483648)", ExpressionOptions.None));
        Assert.Equal(0.0, EvaluateWith("Round(12345.678, -2147483648)", ExpressionOptions.None));
        Assert.Equal(0m, EvaluateWith("Round(a, -2147483647)", ExpressionOptions.None, ("a", 0.12345m)));
        Assert.Equal(0m, EvaluateWith("Round(0.12345, -2147483648)", ExpressionOptions.DecimalAsDefault));
        Assert.Equal(BigDecimal.Zero, EvaluateWith("Round(a, -2147483648)", ExpressionOptions.None, ("a", new BigDecimal(12345, -2))));
        Assert.Equal(0.123, EvaluateWith("Round(0.12345, 3)", ExpressionOptions.None));
        Assert.Equal(12300.0, EvaluateWith("Round(12345.678, -2)", ExpressionOptions.None));

        // A result that does not fit the type of the argument gets the next integer type, not BigInteger
        Assert.Equal(2147483650L, EvaluateWith("Round(2147483647, -1)", ExpressionOptions.None));
        Assert.Equal(9223372036854775810UL, EvaluateWith("Round(a, -1)", ExpressionOptions.None, ("a", long.MaxValue)));
        Assert.Equal(260, EvaluateWith("Round(a, -1)", ExpressionOptions.None, ("a", (byte)255)));
        Assert.Equal(BigInteger.Parse("18446744073709551620", CultureInfo.InvariantCulture), EvaluateWith("Round(a, -1)", ExpressionOptions.None, ("a", ulong.MaxValue)));
    }

    // NCALC-126
    [Fact]
    public void PowWithAnInfiniteExponentShouldNotThrow()
    {
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.DecimalAsDefault, ExpressionOptions.UseBigNumbers })
        {
            Assert.Equal(double.PositiveInfinity, EvaluateWith("Pow(2, y)", options, ("y", double.PositiveInfinity)));
            Assert.Equal(0.0, EvaluateWith("Pow(2, y)", options, ("y", double.NegativeInfinity)));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("Pow(x, y)", options, ("x", 2.5m), ("y", double.PositiveInfinity)));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("Pow(x, y)", options, ("x", new BigDecimal(3)), ("y", double.PositiveInfinity)));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("Pow(x, y)", options, ("x", new BigInteger(3)), ("y", float.PositiveInfinity)));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("x ** y", options, ("x", 2m), ("y", double.PositiveInfinity)));
        }
    }

    // NCALC-127
    [Fact]
    public void MaxAndMinShouldOrderValuesLikeTheComparisonOperators()
    {
        // Booleans take part as numbers when boolean calculation is allowed, as in true + 5
        Assert.Equal(5, EvaluateWith("Max(true, 5)", ExpressionOptions.AllowBooleanCalculation));
        Assert.Equal(1, EvaluateWith("Min(true, 5)", ExpressionOptions.AllowBooleanCalculation));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("Max(true, 5)", ExpressionOptions.None));

        // A complex number with no imaginary part is a real number
        var real = new ComplexNumber(new BigDecimal(3), BigDecimal.Zero, MathHelperOptions.Empty);
        Assert.Equal(3.0, Convert.ToDouble(EvaluateWith("Max(c, 2)", ExpressionOptions.None, ("c", real)), CultureInfo.InvariantCulture));
        Assert.Equal(2.0, Convert.ToDouble(EvaluateWith("Min(c, 2)", ExpressionOptions.None, ("c", real)), CultureInfo.InvariantCulture));
        var c1 = new ComplexNumber(new BigDecimal(1), new BigDecimal(2), MathHelperOptions.Empty);
        var c2 = new ComplexNumber(new BigDecimal(3), new BigDecimal(4), MathHelperOptions.Empty);
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("Max(a, b)", ExpressionOptions.None, ("a", c1), ("b", c2)));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("Min(a, 2)", ExpressionOptions.None, ("a", c1)));
        // Two vectors give the element-wise maximum (NCALC-331); a vector and a scalar cannot be ordered
        Assert.Equal(new Domain.Vector(new BigDecimal[] { 3, 4 }), EvaluateWith("Max(a, b)", ExpressionOptions.None,
            ("a", new Domain.Vector(new BigDecimal[] { 1, 4 })), ("b", new Domain.Vector(new BigDecimal[] { 3, 2 }))));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("Max(a, 2)", ExpressionOptions.None,
            ("a", new Domain.Vector(new BigDecimal[] { 1, 2 }))));

        // NaN wins and an infinity is compared with a big number without being converted to it
        var big = BigInteger.Pow(10, 400);
        foreach (var bigValue in new object[] { big, new BigDecimal(big) })
        {
            Assert.True(double.IsNaN((double)EvaluateWith("Max(a, b)", ExpressionOptions.None, ("a", bigValue), ("b", double.NaN))!));
            Assert.True(double.IsNaN((double)EvaluateWith("Min(b, a)", ExpressionOptions.UseBigNumbers, ("a", bigValue), ("b", double.NaN))!));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("Max(a, b)", ExpressionOptions.None, ("a", bigValue), ("b", double.PositiveInfinity)));
            Assert.Equal(bigValue, EvaluateWith("Min(a, b)", ExpressionOptions.None, ("a", bigValue), ("b", double.PositiveInfinity)));
            Assert.Equal(float.NegativeInfinity, EvaluateWith("Min(a, b)", ExpressionOptions.None, ("a", bigValue), ("b", float.NegativeInfinity)));
            Assert.Equal(bigValue, EvaluateWith("Max(b, a)", ExpressionOptions.None, ("a", bigValue), ("b", double.NegativeInfinity)));
        }
        Assert.True(float.IsNaN((float)EvaluateWith("Max(a, b)", ExpressionOptions.None, ("a", 1f), ("b", float.NaN))!));

        // Values that the comparison operators cannot order cannot be compared by Max and Min either
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("Max(a, b)", ExpressionOptions.CompareIncompatibleTypes,
            ("a", new object[] { 1 }), ("b", new object[] { 2 })));
        Assert.Equal("b", EvaluateWith("Max('a', 'b')", ExpressionOptions.None));
        Assert.Equal(new DateTime(2021, 1, 1), EvaluateWith("Max(a, b)", ExpressionOptions.None, ("a", new DateTime(2020, 1, 1)), ("b", new DateTime(2021, 1, 1))));
    }

    // NCALC-128
    [Fact]
    public void CeilingFloorAndAbsShouldKeepIntegersExact()
    {
        const long x = 9007199254740993L;
        foreach (var function in new[] { "Ceiling", "Floor", "Abs", "Truncate" })
        {
            Assert.Equal(x, EvaluateWith($"{function}(x)", ExpressionOptions.None, ("x", x)));
            Assert.Equal(x, EvaluateWith($"{function}(x)", ExpressionOptions.DecimalAsDefault, ("x", x)));
        }
        Assert.Equal(x, EvaluateWith("Abs(x)", ExpressionOptions.None, ("x", -x)));
        Assert.Equal(5, EvaluateWith("Abs(-5)", ExpressionOptions.None));
        Assert.Equal(ulong.MaxValue, EvaluateWith("Abs(x)", ExpressionOptions.None, ("x", ulong.MaxValue)));

        // The minimum value of a signed type is widened, as by negation
        Assert.Equal(9223372036854775808UL, EvaluateWith("Abs(x)", ExpressionOptions.None, ("x", long.MinValue)));
        Assert.Equal(2147483648L, EvaluateWith("Abs(x)", ExpressionOptions.None, ("x", int.MinValue)));

        // Fractional values are still rounded
        Assert.Equal(2d, EvaluateWith("Ceiling(1.5)", ExpressionOptions.None));
        Assert.Equal(-2d, EvaluateWith("Floor(-1.5)", ExpressionOptions.None));

        // Sign of NaN is NaN
        Assert.True(double.IsNaN((double)EvaluateWith("Sign(Sqrt(-1))", ExpressionOptions.None)!));
        Assert.True(float.IsNaN((float)EvaluateWith("Sign(x)", ExpressionOptions.None, ("x", float.NaN))!));
        Assert.Equal(-1, EvaluateWith("Sign(x)", ExpressionOptions.None, ("x", double.NegativeInfinity)));
    }

    // NCALC-127, NCALC-128: the asynchronous evaluation uses the same built-in functions
    [Fact]
    public async Task AsyncBuiltInFunctionsShouldMatchTheSynchronousOnes()
    {
        foreach (var text in new[] { "Abs(-5)", "Ceiling(7)", "Floor(7)", "Round(2147483647, -1)", "Max(2, 3.5)", "Min('a', 'b')" })
        {
            var sync = new Expression(text, ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken);
            var async = await new AsyncExpression(text, ExpressionOptions.NoCache).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(sync, async);
        }
    }

    // Evaluates an expression with the synchronous and asynchronous evaluators, each recursive and non-recursive
    private static async Task<object?[]> EvaluateInAllEvaluators(string text, ExpressionOptions options, AdvExpressionOptions advancedOptions, params (string Name, object Value)[] parameters)
    {
        var results = new List<object?>();
        foreach (var evaluator in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var o = options | evaluator | ExpressionOptions.NoCache;
            var sync = new Expression(text, o, CultureInfo.InvariantCulture);
            var async = new AsyncExpression(text, o, CultureInfo.InvariantCulture);
            if (advancedOptions != AdvExpressionOptions.None)
            {
                sync.AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, advancedOptions);
                async.AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, advancedOptions);
            }
            foreach (var (name, value) in parameters)
            {
                sync.Parameters[name] = value;
                async.Parameters[name] = value;
            }
            results.Add(sync.Evaluate(TestContext.Current.CancellationToken));
            results.Add(await async.EvaluateAsync(TestContext.Current.CancellationToken));
        }
        return results.ToArray();
    }

    // Evaluates in the recursive and the non-recursive evaluator, sync and async
    private static async Task<object?[]> EvaluateInAllEvaluators(string text, ExpressionOptions options, params (string Name, object Value)[] parameters)
    {
        var results = new List<object?>();
        foreach (var nonRecursive in new[] { false, true })
        {
            var o = options | ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
            var expression = new Expression(text, o);
            var asyncExpression = new AsyncExpression(text, o);
            foreach (var (name, value) in parameters)
            {
                expression.Parameters[name] = value;
                asyncExpression.Parameters[name] = value;
            }
            results.Add(expression.Evaluate(TestContext.Current.CancellationToken));
            results.Add(await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
        }
        return results.ToArray();
    }

    private static async Task AssertThrowsInAllEvaluators<T>(string text, ExpressionOptions options, params (string Name, object Value)[] parameters) where T : Exception
    {
        foreach (var evaluator in new[] { ExpressionOptions.None, ExpressionOptions.UseNonRecursiveEvaluator })
        {
            var o = options | evaluator | ExpressionOptions.NoCache;
            var sync = new Expression(text, o, CultureInfo.InvariantCulture);
            var async = new AsyncExpression(text, o, CultureInfo.InvariantCulture);
            foreach (var (name, value) in parameters)
            {
                sync.Parameters[name] = value;
                async.Parameters[name] = value;
            }
            Assert.ThrowsAny<T>(() => sync.Evaluate(TestContext.Current.CancellationToken));
            await Assert.ThrowsAnyAsync<T>(async () => await async.EvaluateAsync(TestContext.Current.CancellationToken));
        }
    }

    private static object? EvaluatePercent(string text, ExpressionOptions options, params (string Name, object Value)[] parameters)
    {
        var expression = new Expression(text, options | ExpressionOptions.NoCache, CultureInfo.InvariantCulture)
        {
            AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, AdvExpressionOptions.CalculatePercent)
        };
        foreach (var (name, value) in parameters)
            expression.Parameters[name] = value;
        return expression.Evaluate(TestContext.Current.CancellationToken);
    }

    // NCALC-201: the percent formula was applied in the integer type of the operands, so 300000000 * 10 wrapped around
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.AvoidDynamicFunctions)]
    [InlineData(ExpressionOptions.OverflowProtection)]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    public async Task PercentOfALargeIntegerShouldNotOverflow(ExpressionOptions options)
    {
        foreach (var (text, expected) in new[] { ("300000000 * 10%", 30000000.0), ("50000000 * 50%", 25000000.0), ("300000000 + 10%", 330000000.0), ("300000000 - 10%", 270000000.0), ("300000000 / 1000%", 30000000.0) })
        {
            var results = await EvaluateInAllEvaluators(text, options, AdvExpressionOptions.CalculatePercent);
            Assert.All(results, r => Assert.Equal(expected, MathHelper.ConvertToDouble(r, MathHelperOptions.Empty), 6));
        }
    }

    // NCALC-201: NCalc.AOT computes percentages as NCalc does with AvoidDynamicFunctions; every operand gives the same outcome either way
    [Fact]
    public void PercentArithmeticShouldGiveTheSameOutcomeWithAndWithoutDynamicFunctions()
    {
        object[] values = [0, 7, 300000000, -5L, long.MaxValue, ulong.MaxValue, 2.5f, 1e308, double.NaN, 1.25m, decimal.MaxValue, new BigInteger(7), new BigDecimal(7), TimeSpan.FromHours(1), true];
        object[] percents = [0, 3, 10, 2.5, 50m, 1e308];
        foreach (var baseOptions in new[] { ExpressionOptions.None, ExpressionOptions.OverflowProtection, ExpressionOptions.DecimalAsDefault, ExpressionOptions.UseBigNumbers })
        {
            foreach (var op in new[] { "+", "-", "*", "/" })
            {
                foreach (var a in values)
                {
                    foreach (var p in percents)
                    {
                        string Outcome(ExpressionOptions options)
                        {
                            try
                            {
                                var result = EvaluatePercent($"a {op} p", options, ("a", a), ("p", new Percent(p)));
                                return $"{result?.GetType().Name}: {Convert.ToString(result, CultureInfo.InvariantCulture)}";
                            }
                            catch (Exception ex)
                            {
                                return ex.GetType().Name;
                            }
                        }

                        string withDynamicCode = Outcome(baseOptions);
                        string withoutDynamicCode = Outcome(baseOptions | ExpressionOptions.AvoidDynamicFunctions);
                        Assert.True(withDynamicCode == withoutDynamicCode, $"{a} ({a.GetType().Name}) {op} {p}% with {baseOptions}: {withDynamicCode} and {withoutDynamicCode}");
                    }
                }
            }
        }
    }

    // NCALC-201: division by 0%, an overflowing intermediate product, time spans and booleans
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.AvoidDynamicFunctions)]
    public void PercentEdgeCasesShouldGiveTheSameResultsInBothImplementations(ExpressionOptions options)
    {
        Assert.True(double.IsNaN((double)EvaluatePercent("0 / 0%", options)!));
        Assert.Equal(double.PositiveInfinity, EvaluatePercent("200 / 0%", options));
        Assert.Equal(1e307, (double)EvaluatePercent("1e308 * 10%", options)!, 1e293);
        Assert.Equal(233.33333333333334, (double)EvaluatePercent("7 / 3%", options)!, 12);
        Assert.Equal(TimeSpan.FromMinutes(66), EvaluatePercent("t + 10%", options, ("t", TimeSpan.FromHours(1))));
        Assert.Equal(TimeSpan.FromMinutes(6), EvaluatePercent("t * 10%", options, ("t", TimeSpan.FromHours(1))));
        var ex = Assert.ThrowsAny<Exception>(() => EvaluatePercent("true + 10%", options));
        Assert.DoesNotContain("UseDecimalAsDefault", ex.Message);
    }

    // NCALC-211: & | ^ ~ use two's complement for negative integers, as the shifts and BigInteger operands do
    [Theory]
    [InlineData("-1 & 3", 3UL)]
    [InlineData("-1 & 255", 255UL)]
    [InlineData("-8 | 1", -7L)]
    [InlineData("-1 ^ 1", -2L)]
    [InlineData("-8 & -4", -8L)]
    [InlineData("~(-1)", 0UL)]
    [InlineData("~(-6)", 5UL)]
    [InlineData("6 & 3", 2UL)]
    [InlineData("~5", 18446744073709551610UL)]
    [InlineData("4.0 | 1", 5UL)]
    public async Task BitwiseOperatorsShouldAcceptNegativeIntegers(string text, object expected)
    {
        var results = await EvaluateInAllEvaluators(text, ExpressionOptions.None, AdvExpressionOptions.None);
        Assert.All(results, r => Assert.Equal(expected, r));
        Assert.Equal(expected, new Expression(text, ExpressionOptions.NoCache).ToLambda<object>(TestContext.Current.CancellationToken)());
    }

    // NCALC-211
    [Theory]
    [InlineData("a := -1; a &= 3; a", 3UL)]
    [InlineData("a := -8; a |= 1; a", -7L)]
    [InlineData("a := -1; a ^= 1; a", -2L)]
    public async Task BitwiseCompoundAssignmentsShouldAcceptNegativeIntegers(string text, object expected)
    {
        var results = await EvaluateInAllEvaluators(text, ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences, AdvExpressionOptions.None);
        Assert.All(results, r => Assert.Equal(expected, r));
    }

    // NCALC-211: a fraction has no bits; it was silently truncated (1.5 & 3 gave 1) or saturated
    [Theory]
    [InlineData("1.5 & 3")]
    [InlineData("3 | 0.5")]
    [InlineData("a ^ 1")]
    [InlineData("~a")]
    public async Task BitwiseOperatorsShouldRejectFractions(string text)
    {
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>(text, ExpressionOptions.None, ("a", double.NaN));
        var lambda = new Expression(text, ExpressionOptions.NoCache) { Parameters = { ["a"] = double.NaN } };
        Assert.ThrowsAny<NCalcEvaluationException>(() => lambda.ToLambda<object>(TestContext.Current.CancellationToken)());
    }

    // NCALC-212: a ulong and a signed integer of any width follow the rule of a ulong and a long
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.OverflowProtection)]
    public void ULongWithASmallerSignedIntegerShouldNotWrapAround(ExpressionOptions options)
    {
        Assert.Equal(-2L, EvaluateWith("a - b", options, ("a", 5UL), ("b", 7)));
        Assert.Equal(-2L, EvaluateWith("a - b", options, ("a", 5UL), ("b", (short)7)));
        Assert.Equal(-2L, EvaluateWith("a - b", options, ("a", 5UL), ("b", (sbyte)7)));
        Assert.Equal(-2L, EvaluateWith("a - b", options, ("a", 5UL), ("b", 7L)));
        Assert.Equal(-2L, EvaluateWith("a + b", options, ("a", 5UL), ("b", -7)));
        Assert.Equal(-2L, EvaluateWith("b - a", options, ("a", 7UL), ("b", 5)));
        Assert.Equal(-10L, EvaluateWith("a * b", options, ("a", 5UL), ("b", -2)));
        Assert.Equal(-9223372036854775803L, EvaluateWith("5 - a", options, ("a", 1UL << 63)));
        Assert.Equal(9223372036854775812UL, EvaluateWith("a + 5", options, ("a", (ulong)long.MaxValue + 0)));
        Assert.Equal(12UL, EvaluateWith("a + b", options, ("a", 5UL), ("b", 7)));
    }

    // NCALC-213: % and // of big decimals were computed with a quotient rounded to BigDecimal.Precision (100) digits
    [Fact]
    public void BigDecimalRemainderAndFloorDivisionShouldBeExact()
    {
        var options = ExpressionOptions.UseBigNumbers;
        string tenTo206 = "1" + new string('0', 206);
        Assert.Equal(new BigDecimal(2), MathHelper.ConvertToBigDecimal(EvaluateWith(tenTo206 + " % 7", options)!));
        Assert.Equal(new BigDecimal(new BigInteger(25), -1), MathHelper.ConvertToBigDecimal(EvaluateWith(tenTo206 + ".5 % 7", options)!));
        Assert.Equal(BigDecimal.One, MathHelper.ConvertToBigDecimal(EvaluateWith("a % b", options, ("a", new BigDecimal(BigInteger.One, 300)), ("b", new BigDecimal(new BigInteger(15), -1)))!));
        Assert.Equal(-new BigDecimal(BigInteger.One, -1), MathHelper.ConvertToBigDecimal(EvaluateWith("a % b", options, ("a", new BigDecimal(new BigInteger(-21), -1)), ("b", new BigDecimal(BigInteger.One, 0)))!));
        Assert.Equal(BigInteger.Pow(10, 206) / 7, AsBigInteger(EvaluateWith(tenTo206 + " // 7", options)));
        Assert.Equal(-(BigInteger.Pow(10, 206) / 7) - 1, AsBigInteger(EvaluateWith("-" + tenTo206 + " // 7", options)));
        Assert.Equal(BigInteger.Pow(10, 300) * 2 / 3, AsBigInteger(EvaluateWith("a // b", options, ("a", new BigDecimal(BigInteger.One, 300)), ("b", new BigDecimal(new BigInteger(15), -1)))));

        var vector = (NCalc.Domain.Vector)EvaluateWith("v % 7", options, ("v", new NCalc.Domain.Vector([new BigDecimal(BigInteger.One, 206), new BigDecimal(BigInteger.One, 300)])))!;
        Assert.Equal(new BigDecimal(2), vector[0]);
        Assert.Equal(BigDecimal.One, vector[1]);
    }

    // NCALC-214: the digit limit was bypassed by factorials of numbers beyond the range of double and by powers of 0.1
    [Fact(Timeout = 30000)]
    public async Task FactorialsAndPowersOfFractionsShouldRespectTheDigitLimit()
    {
        await Task.Run(() =>
        {
            Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("(2 ** 1100)!", ExpressionOptions.UseBigNumbers));
            Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("a!", ExpressionOptions.UseBigNumbers, ("a", BigInteger.Pow(10, 400))));
            Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("0.1 ** 3000000", ExpressionOptions.UseBigNumbers));
            // NCALC-321: without UseBigNumbers, the power is computed in double instead of exactly and is 0 as a decimal
            Assert.Equal(0m, EvaluateWith("0.1 ** 3000000", ExpressionOptions.DecimalAsDefault));
            Assert.Equal(0.001, MathHelper.ConvertToDouble(EvaluateWith("0.1 ** 3", ExpressionOptions.UseBigNumbers), MathHelperOptions.Empty), 15);
        }, TestContext.Current.CancellationToken);
    }

    // NCALC-215: products of big numbers and concatenated strings had no size limit, so repeated squaring built unbounded values
    [Fact]
    public async Task ProductsAndConcatenationsShouldRespectTheSizeLimits()
    {
        var bigOptions = ExpressionOptions.UseBigNumbers | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences;
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("fn sq(x) => x * x; sq(sq(2 ** 300000))", bigOptions);
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("a * a", ExpressionOptions.None, ("a", BigInteger.Pow(2, 200000)));
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("a + b", ExpressionOptions.None, ("a", new BigDecimal(BigInteger.One, 200000)), ("b", new BigDecimal(BigInteger.One, -200000)));
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("s + s", ExpressionOptions.StringConcat, ("s", new string('a', 6_000_000)));

        // Values within the limits are still computed
        Assert.Equal(BigInteger.Pow(2, 200000), AsBigInteger(EvaluateWith("a * a", ExpressionOptions.None, ("a", BigInteger.Pow(2, 100000)))));
        Assert.Equal(new string('a', 2_000_000), EvaluateWith("s + s", ExpressionOptions.StringConcat, ("s", new string('a', 1_000_000))));
    }

    // NCALC-219: a decimal with a double beyond the range of decimal (or NaN, or infinity) threw OverflowException
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.OverflowProtection)]
    public async Task DecimalWithADoubleBeyondItsRangeShouldBeComputedInDouble(ExpressionOptions options)
    {
        (string, object)[] parameters = [("m", 1m), ("z", 0m), ("d", 1e300), ("n", double.NaN), ("i", double.PositiveInfinity)];
        Assert.All(await EvaluateInAllEvaluators("m + d", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(1e300, r));
        Assert.All(await EvaluateInAllEvaluators("z * d", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(0.0, r));
        Assert.All(await EvaluateInAllEvaluators("d / m", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(1e300, r));
        Assert.All(await EvaluateInAllEvaluators("m + n", options, AdvExpressionOptions.None, parameters), r => Assert.True(double.IsNaN((double)r!)));
        Assert.All(await EvaluateInAllEvaluators("Max(m, d)", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(1e300, r));
        Assert.All(await EvaluateInAllEvaluators("Min(m, d)", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(1.0, MathHelper.ConvertToDouble(r, MathHelperOptions.Empty)));
        Assert.All(await EvaluateInAllEvaluators("m < d", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(true, r));
        Assert.Equal(1e300, MathHelper.ConvertToDouble(EvaluateWith("m + d", options | ExpressionOptions.UseBigNumbers, ("m", 1m), ("d", 1e300)), MathHelperOptions.Empty), 1e286);
        Assert.Equal(double.PositiveInfinity, EvaluateWith("m + i", ExpressionOptions.None, ("m", 1m), ("i", double.PositiveInfinity)));
    }

    // NCALC-220: with DecimalAsDefault, big-number results were narrowed to decimal even when digits were lost
    [Fact]
    public void DecimalAsDefaultShouldNotNarrowBigNumbersWithALossOfDigits()
    {
        var options = ExpressionOptions.DecimalAsDefault;
        Assert.Equal(new BigDecimal(new BigInteger(5), -40), EvaluateWith("a / b", options, ("a", new BigInteger(5)), ("b", new BigDecimal(BigInteger.One, 40))));
        Assert.IsType<BigDecimal>(EvaluateWith("a / b", options, ("a", BigDecimal.One), ("b", new BigDecimal(3))));

        // A result that decimal holds exactly is still a decimal
        Assert.Equal(0.25m, EvaluateWith("a / b", options, ("a", BigDecimal.One), ("b", new BigDecimal(4))));
    }

    // NCALC-221: negating a ulong above 2^63 threw a raw OverflowException
    [Fact]
    public async Task NegatingALargeULongShouldGiveABigInteger()
    {
        foreach (var value in new[] { (1UL << 63) + 1, ulong.MaxValue })
        {
            foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.OverflowProtection })
            {
                var results = await EvaluateInAllEvaluators("-a", options, AdvExpressionOptions.None, ("a", value));
                Assert.All(results, r => Assert.Equal(-new BigInteger(value), r));
            }
        }
        Assert.Equal(long.MinValue, EvaluateWith("-a", ExpressionOptions.None, ("a", 1UL << 63)));
    }

    // NCALC-243: the message of an unsupported negation had a stray '$'
    [Fact]
    public void NegatingAnUnsupportedTypeShouldNameTheType()
    {
        var ex = Assert.ThrowsAny<NCalcException>(() => MathHelper.Negate('a', false, MathHelperOptions.Empty));
        Assert.Contains("Unsupported type Char", ex.Message);
    }

    // NCALC-303: the size estimates of powers and products counted the decimal places of a fractional number twice,
    // so the digit limit triggered at about half the real size
    [Fact(Timeout = 60000)]
    public async Task PowersAndProductsOfFractionsShouldNotCountDecimalPlacesTwice()
    {
        await Task.Run(() =>
        {
            // 58,805 digits (the old estimate was 108,805)
            Assert.IsType<BigDecimal>(EvaluateWith("1.5 ** 50000", ExpressionOptions.UseBigNumbers));
            var power = EvaluateWith("Pow(1.000001, 10000)", ExpressionOptions.DecimalAsDefault);
            Assert.IsType<decimal>(power);
            Assert.Equal(1.01005016203392, MathHelper.ConvertToDouble(power, MathHelperOptions.Empty), 13);
            var compound = EvaluateWith("Pow(1 + rate, 1825)", ExpressionOptions.None, ("rate", 0.05m / 365m));
            Assert.IsType<decimal>(compound);
            Assert.Equal(1.28400343214708, MathHelper.ConvertToDouble(compound, MathHelperOptions.Empty), 13);

            // A product of fractions with 35,000 significant digits and 30,000 decimal places each has about 70,000 digits
            var fraction = new BigDecimal(BigInteger.Pow(15, 30000), -30000);
            Assert.Equal(new BigDecimal(BigInteger.Pow(15, 60000), -60000), EvaluateWith("x * x", ExpressionOptions.None, ("x", fraction)));

            // Powers and products whose digits or decimal places exceed the limit are still rejected
            Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("1.5 ** 90000", ExpressionOptions.UseBigNumbers));
            Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("0.1 ** 3000000", ExpressionOptions.UseBigNumbers));
            Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("s * s", ExpressionOptions.None, ("s", new BigDecimal(BigInteger.One, -60000))));
        }, TestContext.Current.CancellationToken);
    }

    // NCALC-321: an exact power of a decimal base threw the digit limit where the double result is finite, and big results came back
    // as BigDecimal without UseBigNumbers
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    public async Task PowerOfADecimalBaseShouldGiveADecimalOrADouble(ExpressionOptions options)
    {
        (string, object)[] parameters = [("m", 2.5m), ("one", 1.0000001m)];
        foreach (var r in await EvaluateInAllEvaluators("Pow(one, 1000000)", options, AdvExpressionOptions.None, parameters))
        {
            Assert.IsType<decimal>(r);
            Assert.Equal(1.10517091261432, MathHelper.ConvertToDouble(r, MathHelperOptions.Empty), 13);
        }
        foreach (var r in await EvaluateInAllEvaluators("Pow(1.0000001, 1000000)", options, AdvExpressionOptions.None, parameters))
        {
            Assert.IsType(options == ExpressionOptions.DecimalAsDefault ? typeof(decimal) : typeof(double), r);
            Assert.Equal(1.10517091261432, MathHelper.ConvertToDouble(r, MathHelperOptions.Empty), 13);
        }
        Assert.All(await EvaluateInAllEvaluators("Pow(m, 100000)", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(double.PositiveInfinity, r));
        Assert.All(await EvaluateInAllEvaluators("m ** 100000", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(double.PositiveInfinity, r));
        Assert.All(await EvaluateInAllEvaluators("Pow(m, 80)", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(Math.Pow(2.5, 80), r));
        Assert.All(await EvaluateInAllEvaluators("Pow(m, 3)", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(15.625m, r));
    }

    // NCALC-307: a quotient computed in double was narrowed back to the type of the dividend, so 2.5m / 1e300 was silently 0
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.DecimalAsDefault)]
    [InlineData(ExpressionOptions.ReduceArithmeticResultType)]
    [InlineData(ExpressionOptions.OverflowProtection)]
    public async Task QuotientBelowTheResolutionOfTheDividendTypeShouldNotBecomeZero(ExpressionOptions options)
    {
        (string, object)[] parameters = [("m", 2.5m), ("d", 1e300), ("f", 2.5f), ("fm", float.MaxValue), ("x", 1e-20), ("y", 1e20)];
        Assert.All(await EvaluateInAllEvaluators("m / d", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(2.5e-300, r));
        Assert.All(await EvaluateInAllEvaluators("f / d", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(2.5e-300, r));
        Assert.All(await EvaluateInAllEvaluators("m / fm", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(2.5 / float.MaxValue, (double)r!, 1e-50));
        Assert.All(await EvaluateInAllEvaluators("x / y", options | ExpressionOptions.DecimalAsDefault, AdvExpressionOptions.None, parameters), r => Assert.Equal(1e-40, r));

        // Quotients in the range of the dividend type keep it
        Assert.All(await EvaluateInAllEvaluators("m / 4", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(0.625m, r));

        foreach (var expression in new[] { "m / d", "f / d" })
            Assert.All(await EvaluateInAllEvaluators(expression, options | ExpressionOptions.UseBigNumbers, AdvExpressionOptions.None, parameters),
                r => Assert.Equal(2.5e-300, MathHelper.ConvertToDouble(r, MathHelperOptions.Empty), 1e-310));
    }

    // NCALC-308: with UseBigNumbers or a big-number operand, *, / and // with NaN or an infinity threw .NET exceptions,
    // while + and - returned NaN or infinity; n + 10% threw InvalidOperationException
    [Fact]
    public async Task BigNumberOperationsWithNaNOrInfinityShouldBeComputedInDouble()
    {
        (string, object)[] parameters = [("n", double.NaN), ("i", double.PositiveInfinity), ("bi", new BigInteger(3)), ("bd", new BigDecimal(new BigInteger(15), -1))];
        foreach (var expression in new[] { "Sqrt(-1) * 2", "n * 2", "n / 2", "2 / n", "n // 2", "n % 2" })
            Assert.All(await EvaluateInAllEvaluators(expression, ExpressionOptions.UseBigNumbers, AdvExpressionOptions.None, parameters), r => Assert.True(double.IsNaN((double)r!), expression));
        foreach (var expression in new[] { "i * 2", "i / 2", "i + 1" })
            Assert.All(await EvaluateInAllEvaluators(expression, ExpressionOptions.UseBigNumbers, AdvExpressionOptions.None, parameters), r => Assert.Equal(double.PositiveInfinity, r));
        Assert.All(await EvaluateInAllEvaluators("2 / i", ExpressionOptions.UseBigNumbers, AdvExpressionOptions.None, parameters), r => Assert.Equal(0.0, MathHelper.ConvertToDouble(r, MathHelperOptions.Empty)));

        // A big-number operand without options
        foreach (var expression in new[] { "bi * n", "bd + n", "bi % n" })
            Assert.All(await EvaluateInAllEvaluators(expression, ExpressionOptions.None, AdvExpressionOptions.None, parameters), r => Assert.True(double.IsNaN((double)r!), expression));
        Assert.All(await EvaluateInAllEvaluators("bd * i", ExpressionOptions.None, AdvExpressionOptions.None, parameters), r => Assert.Equal(double.PositiveInfinity, r));
        Assert.All(await EvaluateInAllEvaluators("bi / i", ExpressionOptions.None, AdvExpressionOptions.None, parameters), r => Assert.Equal(0.0, MathHelper.ConvertToDouble(r, MathHelperOptions.Empty)));

        // Percent, with and without UseBigNumbers
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.UseBigNumbers })
        {
            Assert.All(await EvaluateInAllEvaluators("n + 10%", options, AdvExpressionOptions.CalculatePercent, parameters), r => Assert.True(double.IsNaN((double)r!)));
            Assert.All(await EvaluateInAllEvaluators("i * 10%", options, AdvExpressionOptions.CalculatePercent, parameters), r => Assert.Equal(double.PositiveInfinity, r));
        }
    }

    // NCALC-309: division had no result-size check, so repeated division by a small fraction doubled the number of digits
    [Fact(Timeout = 60000)]
    public async Task DivisionShouldRespectTheDigitLimit()
    {
        var options = ExpressionOptions.UseBigNumbers | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences;
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("a := 10 ** 60000; a / (1 / a)", options);
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("(10 ** 99999) / 0.1", options);

        // Quotients within the limit are still computed
        Assert.Equal(BigInteger.Pow(10, 1001), AsBigInteger(EvaluateWith("(10 ** 1000) / 0.1", options)));
        Assert.Equal(new BigDecimal(BigInteger.One, -1000), EvaluateWith("1 / (10 ** 1000)", options));
    }

    // NCALC-315: the ulong rules contradicted each other: -a, 0 - a and a * -1 gave different values for a = ulong.MaxValue
    [Fact]
    public async Task NegationOfALargeULongShouldEqualItsSubtractionFromZero()
    {
        foreach (var value in new[] { (1UL << 63) + 1, ulong.MaxValue })
        {
            foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.OverflowProtection, ExpressionOptions.UseBigNumbers })
            {
                foreach (var expression in new[] { "-a", "0 - a", "a * -1", "a * b" })
                {
                    var results = await EvaluateInAllEvaluators(expression, options, AdvExpressionOptions.None, ("a", value), ("b", -1L));
                    Assert.All(results, r => Assert.Equal(-new BigInteger(value), r));
                }
                Assert.All(await EvaluateInAllEvaluators("a - (-1)", options, AdvExpressionOptions.None, ("a", ulong.MaxValue)), r => Assert.Equal(new BigInteger(ulong.MaxValue) + 1, r));
            }
        }
    }

    // NCALC-316: TimeSpan / TimeSpan (and other time operators that .NET defines) worked in NCalc but threw in NCalc.AOT
    [Fact]
    public async Task TimeOperatorsShouldNotDependOnDynamicOperators()
    {
        (string, object)[] parameters = [("t", new TimeSpan(1, 2, 3)), ("u", new TimeSpan(0, 0, 30)), ("d1", new DateTime(2020, 1, 2)), ("d2", new DateTime(2020, 1, 1))];
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.SupportTimeOperations, ExpressionOptions.AvoidDynamicFunctions })
        {
            Assert.All(await EvaluateInAllEvaluators("t / u", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(124.1, (double)r!, 12));
            Assert.All(await EvaluateInAllEvaluators("t + u", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(new TimeSpan(1, 2, 33), r));
            Assert.All(await EvaluateInAllEvaluators("t - u", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(new TimeSpan(1, 1, 33), r));
            Assert.All(await EvaluateInAllEvaluators("d1 - d2", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(TimeSpan.FromDays(1), r));
        }
    }

    // NCALC-324: bitwise operators with a BigInteger operand rejected whole floating values and truncated fractional shift counts
    [Fact]
    public async Task BitwiseOperatorsWithABigIntegerShouldFollowTheOperandRules()
    {
        (string, object)[] parameters = [("bi", new BigInteger(12)), ("w", 4.0), ("wm", 6m), ("f", 1.5), ("nan", double.NaN)];
        Assert.All(await EvaluateInAllEvaluators("bi & w", ExpressionOptions.None, AdvExpressionOptions.None, parameters), r => Assert.Equal(new BigInteger(4), r));
        Assert.All(await EvaluateInAllEvaluators("bi & 4.0", ExpressionOptions.None, AdvExpressionOptions.None, parameters), r => Assert.Equal(new BigInteger(4), r));
        Assert.All(await EvaluateInAllEvaluators("bi ^ wm", ExpressionOptions.None, AdvExpressionOptions.None, parameters), r => Assert.Equal(new BigInteger(10), r));
        Assert.All(await EvaluateInAllEvaluators("bi << 2.0", ExpressionOptions.None, AdvExpressionOptions.None, parameters), r => Assert.Equal(new BigInteger(48), r));
        var assignments = ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences;
        Assert.All(await EvaluateInAllEvaluators("x := bi; x |= w; x", assignments, AdvExpressionOptions.None, parameters), r => Assert.Equal(new BigInteger(12), r));

        foreach (var expression in new[] { "bi & 1.5", "bi | nan", "bi << f", "1 << f", "bi << nan" })
            await AssertThrowsInAllEvaluators<NCalcEvaluationException>(expression, ExpressionOptions.None, parameters);
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("x := bi; x &= f; x", assignments, parameters);
    }

    // NCALC-325: a percent of a large decimal overflowed in the intermediate product and threw a .NET OverflowException
    [Theory]
    [InlineData(ExpressionOptions.None)]
    [InlineData(ExpressionOptions.AvoidDynamicFunctions)]
    [InlineData(ExpressionOptions.UseBigNumbers)]
    public async Task PercentOfALargeDecimalShouldNotOverflowInTheIntermediateProduct(ExpressionOptions options)
    {
        (string, object)[] parameters = [("m", 70000000000000000000000000000m), ("mx", decimal.MaxValue)];
        Assert.All(await EvaluateInAllEvaluators("m * 10%", options, AdvExpressionOptions.CalculatePercent, parameters), r => Assert.Equal(7000000000000000000000000000m, r));
        Assert.All(await EvaluateInAllEvaluators("m + 10%", options, AdvExpressionOptions.CalculatePercent, parameters), r => Assert.Equal(77000000000000000000000000000m, r));
        Assert.All(await EvaluateInAllEvaluators("m - 10%", options, AdvExpressionOptions.CalculatePercent, parameters), r => Assert.Equal(63000000000000000000000000000m, r));
        Assert.All(await EvaluateInAllEvaluators("m / 1000%", options, AdvExpressionOptions.CalculatePercent, parameters), r => Assert.Equal(7000000000000000000000000000m, r));
        Assert.All(await EvaluateInAllEvaluators("70000000000000000000000000000 * 10%", options | ExpressionOptions.DecimalAsDefault, AdvExpressionOptions.CalculatePercent, parameters),
            r => Assert.Equal(7000000000000000000000000000m, MathHelper.ConvertToDecimal(r, MathHelperOptions.Empty)));

        // A result beyond the range of decimal throws an NCalc exception (with UseBigNumbers, it is a big number)
        if (options != ExpressionOptions.UseBigNumbers)
        {
            var percent = new Expression("mx + 10%", options | ExpressionOptions.NoCache, CultureInfo.InvariantCulture)
            {
                AdvancedOptions = new AdvancedExpressionOptions(CultureInfo.InvariantCulture, AdvExpressionOptions.CalculatePercent),
                Parameters = { ["mx"] = decimal.MaxValue }
            };
            Assert.Throws<NCalcEvaluationException>(() => percent.Evaluate(TestContext.Current.CancellationToken));
        }
    }

    // NCALC-326: under UseBigNumbers, the precision and type of a percent depended on the type of the intermediate result
    [Fact]
    public async Task PercentWithBigNumbersShouldKeepTheExactValue()
    {
        (string, object)[] parameters = [("u", ulong.MaxValue), ("l", long.MaxValue)];
        var options = ExpressionOptions.UseBigNumbers;
        Assert.All(await EvaluateInAllEvaluators("u + 10%", options, AdvExpressionOptions.CalculatePercent, parameters),
            r => Assert.Equal(BigDecimal.Parse("20291418481080506776.5", CultureInfo.InvariantCulture), r));
        Assert.All(await EvaluateInAllEvaluators("u * 10%", options, AdvExpressionOptions.CalculatePercent, parameters),
            r => Assert.Equal(BigDecimal.Parse("1844674407370955161.5", CultureInfo.InvariantCulture), r));
        Assert.All(await EvaluateInAllEvaluators("u - 10%", options, AdvExpressionOptions.CalculatePercent, parameters),
            r => Assert.Equal(BigDecimal.Parse("16602069666338596453.5", CultureInfo.InvariantCulture), r));
        Assert.All(await EvaluateInAllEvaluators("l + 1%", options, AdvExpressionOptions.CalculatePercent, parameters),
            r => Assert.Equal(BigDecimal.Parse("9315605757223323565.07", CultureInfo.InvariantCulture), r));

        // Results that a double holds exactly are still doubles
        Assert.All(await EvaluateInAllEvaluators("1 + 10%", options, AdvExpressionOptions.CalculatePercent, parameters), r => Assert.Equal(1.1, r));
        Assert.All(await EvaluateInAllEvaluators("3 * 10%", options, AdvExpressionOptions.CalculatePercent, parameters), r => Assert.Equal(0.3, r));
    }

    // NCALC-327: uint * uint with OverflowProtection threw although the product fits in ulong
    [Fact]
    public async Task UnsignedProductsShouldBeWidenedWithOverflowProtection()
    {
        (string, object)[] parameters = [("ui", uint.MaxValue), ("us", ushort.MaxValue)];
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.OverflowProtection })
        {
            Assert.All(await EvaluateInAllEvaluators("ui * ui", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(18446744065119617025UL, r));
            Assert.All(await EvaluateInAllEvaluators("us * us", options, AdvExpressionOptions.None, parameters), r => Assert.Equal(4294836225U, r));
        }
    }

    // NCALC-328: BigDecimal values with a huge exponent made Floor, Truncate, &, !, comparisons and hashing run for seconds
    [Fact(Timeout = 60000)]
    public async Task BigDecimalsWithAHugeExponentShouldBeHandledQuickly()
    {
        await Task.Run(() =>
        {
            (string, object)[] parameters = [("x", new BigDecimal(BigInteger.One, 300000)), ("y", new BigDecimal(BigInteger.One, 1000000)), ("t", new BigDecimal(BigInteger.One, -1000000))];
            foreach (var (expression, expected) in new (string, object?)[]
            {
                ("Floor(x)", null), ("Truncate(x)", null), ("Ceiling(x)", null), ("y & 1", null), ("y!", null), ("Floor(y)", null),
                ("x = 1", false), ("x < 1", false), ("x > y", false), ("t < 1", true), ("t = 0", false), ("x in (1, 2)", false),
                ("x + x", new BigDecimal(new BigInteger(2), 300000)), ("t + t", new BigDecimal(new BigInteger(2), -1000000))
            })
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                object? result = null;
                try
                {
                    result = EvaluateWith(expression, ExpressionOptions.None, parameters);
                }
                catch (NCalcEvaluationException)
                {
                    Assert.Null(expected);
                }
                Assert.True(watch.ElapsedMilliseconds < 2000, $"{expression} took {watch.ElapsedMilliseconds} ms");
                if (expected is not null)
                    Assert.Equal(expected, result);
            }

            var comparer = new BoxedNumberComparer(new ComparisonOptions(CultureInfo.InvariantCulture, ExpressionOptions.None), new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.None));
            var hashWatch = System.Diagnostics.Stopwatch.StartNew();
            comparer.GetHashCode(new BigDecimal(BigInteger.One, 1000000));
            Assert.False(comparer.Equals(new BigDecimal(BigInteger.One, 1000000), 1));
            Assert.True(hashWatch.ElapsedMilliseconds < 2000, $"hashing took {hashWatch.ElapsedMilliseconds} ms");
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertCloseTo(double expected, object? actual)
    {
        Assert.NotNull(actual);
        double value = MathHelper.ConvertToDouble(actual, MathHelperOptions.Empty);
        if (double.IsNaN(expected) || double.IsInfinity(expected))
            Assert.Equal(expected, value);
        else
            Assert.True(Math.Abs(value - expected) <= 1e-12 * Math.Max(1, Math.Abs(expected)), $"Expected {expected:R}, got {value:R} ({actual})");
    }

    // A big-number argument: an integer, or a decimal number such as "-2.5"
    private static object ParseBigNumber(string text)
    {
        int point = text.IndexOf('.');
        if (point < 0)
            return BigInteger.Parse(text, CultureInfo.InvariantCulture);
        return new BigDecimal(BigInteger.Parse(text.Remove(point, 1), CultureInfo.InvariantCulture), -(text.Length - point - 1));
    }

    // NCALC-216
    [Fact]
    public async Task PowWithANonFiniteBaseShouldNotThrow()
    {
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.DecimalAsDefault, ExpressionOptions.UseBigNumbers })
        {
            Assert.All(await EvaluateInAllEvaluators("Sqrt(-1) ** 2", options), r => Assert.Equal(double.NaN, r));
            Assert.Equal(double.NaN, EvaluateWith("Pow(Sqrt(-1), 2)", options));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("Pow(Exp(1000), 2)", options));
            Assert.Equal(double.NegativeInfinity, EvaluateWith("Pow(-Exp(1000), 3)", options));
            Assert.Equal(0.0, EvaluateWith("Pow(Exp(1000), -1)", options));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("x ** 2", options, ("x", float.NegativeInfinity)));
            Assert.Equal(double.NaN, EvaluateWith("Pow(x, 3)", options, ("x", double.NaN)));
        }
    }

    // NCALC-232: zero to a negative power is infinite, as 1 / 0 and Math.Pow(0, -1) are, also when the power is computed exactly
    [Fact]
    public async Task ZeroToANegativePowerShouldBeInfinite()
    {
        foreach (var options in new[] { ExpressionOptions.None, ExpressionOptions.DecimalAsDefault, ExpressionOptions.UseBigNumbers })
        {
            Assert.All(await EvaluateInAllEvaluators("Pow(0, -1)", options), r => Assert.Equal(double.PositiveInfinity, r));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("0 ** -1", options));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("Pow(x, -2)", options, ("x", 0m)));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("Pow(x, -3)", options, ("x", BigInteger.Zero)));
            Assert.Equal(double.PositiveInfinity, EvaluateWith("Pow(x, y)", options, ("x", BigDecimal.Zero), ("y", new BigInteger(-1))));
        }
    }

    // NCALC-217
    [Fact]
    public async Task RoundingFunctionsShouldAcceptDoublesOutsideTheDecimalRangeWithDecimalAsDefault()
    {
        const ExpressionOptions options = ExpressionOptions.DecimalAsDefault;
        Assert.All(await EvaluateInAllEvaluators("Abs(Sqrt(-1))", options), r => Assert.Equal(double.NaN, r));
        Assert.Equal(double.NaN, EvaluateWith("Floor(Sqrt(-1))", options));
        Assert.Equal(double.PositiveInfinity, EvaluateWith("Round(Exp(1000), 2)", options));
        Assert.Equal(double.PositiveInfinity, EvaluateWith("Round(Exp(1000), -2)", options));
        Assert.Equal(1, EvaluateWith("Sign(Exp(1000))", options));
        Assert.Equal(double.PositiveInfinity, EvaluateWith("Truncate(Exp(1000))", options));
        Assert.Equal(1e30, EvaluateWith("Ceiling(d)", options, ("d", 1e30)));
        Assert.Equal(1e30, EvaluateWith("Floor(d)", options, ("d", 1e30)));
        Assert.Equal(1e30, EvaluateWith("Abs(d)", options, ("d", -1e30)));
        Assert.Equal(1e30, EvaluateWith("Round(d, 2)", options, ("d", 1e30)));
        Assert.Equal(-1, EvaluateWith("Sign(d)", options, ("d", -1e30)));
        Assert.Equal(-1, EvaluateWith("Sign(d)", options, ("d", float.NegativeInfinity)));
        Assert.Equal(double.NaN, EvaluateWith("Truncate(d)", options, ("d", float.NaN)));

        // A double within the range of decimal is still computed as a decimal
        Assert.Equal(2.5m, EvaluateWith("Abs(d)", options, ("d", -2.5)));
        Assert.Equal(3m, EvaluateWith("Ceiling(d)", options, ("d", 2.5)));
    }

    // NCALC-218
    [Theory]
    [InlineData("Exp(x)", "-100", 3.7200759760208360e-44)]
    [InlineData("Atan(x)", "1", Math.PI / 4)]
    [InlineData("Atan(x)", "-1", -Math.PI / 4)]
    [InlineData("Asin(x)", "1", Math.PI / 2)]
    [InlineData("Asin(x)", "-1", -Math.PI / 2)]
    [InlineData("Asin(x)", "2", double.NaN)]
    [InlineData("Acos(x)", "0", Math.PI / 2)]
    [InlineData("Acos(x)", "-2", double.NaN)]
    [InlineData("Ln(x)", "-1", double.NaN)]
    [InlineData("Ln(x)", "0", double.NegativeInfinity)]
    [InlineData("Log10(x)", "-1", double.NaN)]
    [InlineData("Log10(x)", "0", double.NegativeInfinity)]
    [InlineData("Log2(x)", "-4", double.NaN)]
    [InlineData("Log(x, 10)", "-4", double.NaN)]
    [InlineData("Log(x, 10)", "0", double.NegativeInfinity)]
    [InlineData("Log(x, -2)", "8", double.NaN)]
    [InlineData("Log(x, 0)", "8", double.NaN)]
    [InlineData("Log(x, 1)", "8", double.NaN)]
    [InlineData("Sqrt(x)", "-4", double.NaN)]
    [InlineData("Sin(x)", "2", 0.9092974268256817)]
    [InlineData("Sin(x)", "100", -0.5063656411097588)]
    [InlineData("Sin(x)", "-2.5", -0.5984721441039564)]
    [InlineData("Sin(x)", "1000000000000000000000000000000", -0.0901169019121380580)]
    [InlineData("Cos(x)", "0.3", 0.955336489125606)]
    [InlineData("Cos(x)", "1000000000000000000000000000000", -0.9959311944053957)]
    [InlineData("Tan(x)", "0", 0.0)]
    [InlineData("Tan(x)", "1", 1.5574077246549023)]
    [InlineData("Tan(x)", "-2.5", 0.7470222972386602)]
    public void BigNumberTranscendentalFunctionsShouldMatchSystemMath(string expression, string x, double expected)
    {
        AssertCloseTo(expected, EvaluateWith(expression, ExpressionOptions.UseBigNumbers, ("x", ParseBigNumber(x))));
    }

    // NCALC-218
    [Fact]
    public async Task BigNumberTranscendentalFunctionsShouldHandleExtremeArguments()
    {
        var huge = BigInteger.Pow(10, 400);

        // e^-1000 is 5.0759588975494567652918E-435, below the range of double
        var tiny = Assert.IsType<BigDecimal>(EvaluateWith("Exp(x)", ExpressionOptions.UseBigNumbers, ("x", new BigInteger(-1000))));
        string digits = tiny.Mantissa.ToString(CultureInfo.InvariantCulture);
        Assert.StartsWith("50759588975494567652918", digits);
        Assert.Equal(-435, digits.Length - 1 + tiny.Exponent);

        // The result of Exp is limited like that of a power, and a huge negative argument gives zero, as Math.Exp does; both used to hang
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("Exp(x)", ExpressionOptions.UseBigNumbers, ("x", huge)));
        Assert.Equal(BigDecimal.Zero, EvaluateWith("Exp(-x)", ExpressionOptions.UseBigNumbers, ("x", huge)));

        // Trigonometric arguments beyond the range of int were an OverflowException; sin(10^400) = -0.99853823198309772291...
        AssertCloseTo(-0.9985382319830977, EvaluateWith("Sin(x)", ExpressionOptions.UseBigNumbers, ("x", huge)));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("Sin(x)", ExpressionOptions.UseBigNumbers, ("x", BigInteger.Pow(10, 20000))));
        AssertCloseTo(Math.PI / 2, EvaluateWith("Atan(x)", ExpressionOptions.UseBigNumbers, ("x", huge)));

        // A base beyond the range of int was an OverflowException, or NaN (an infinite double) with an integer argument
        AssertCloseTo(2.0, EvaluateWith("Log(x, b)", ExpressionOptions.UseBigNumbers, ("x", huge * huge), ("b", huge)));
        AssertCloseTo(Math.Log10(8) / 400, EvaluateWith("Log(8, b)", ExpressionOptions.UseBigNumbers, ("b", huge)));

        // The same in all evaluators
        Assert.All(await EvaluateInAllEvaluators("Ln(x)", ExpressionOptions.UseBigNumbers, ("x", new BigInteger(-1))), r => Assert.Equal(double.NaN, r));
        Assert.All(await EvaluateInAllEvaluators("Asin(x)", ExpressionOptions.UseBigNumbers, ("x", BigInteger.One)), r => AssertCloseTo(Math.PI / 2, r));
    }

    // NCALC-225
    [Fact]
    public void RoundShouldRoundSmallDoublesToMoreThanFifteenPlaces()
    {
        Assert.Equal(0.0, EvaluateWith("Round(t, 16)", ExpressionOptions.None, ("t", 1.23e-17)));
        Assert.Equal(1e-17, EvaluateWith("Round(t, 17)", ExpressionOptions.None, ("t", 1.23e-17)));
        Assert.Equal(2e-16, EvaluateWith("Round(t, 16)", ExpressionOptions.None, ("t", 1.5e-16)));
        Assert.Equal(3e-16, EvaluateWith("Round(t, 16)", ExpressionOptions.RoundAwayFromZero, ("t", 2.5e-16)));
        Assert.Equal(-1.2e-20, EvaluateWith("Round(t, 21)", ExpressionOptions.None, ("t", -1.23e-20)));

        // A place beyond the last digit leaves the value unchanged
        Assert.Equal(1.23e-17, EvaluateWith("Round(t, 30)", ExpressionOptions.None, ("t", 1.23e-17)));
        Assert.Equal(1.5, EvaluateWith("Round(1.5, 16)", ExpressionOptions.None));
        Assert.Equal(double.NaN, EvaluateWith("Round(Sqrt(-1), 20)", ExpressionOptions.None));
    }

    // NCALC-238
    [Fact]
    public void TruncateShouldAcceptVectorsAndKeepBigDecimals()
    {
        var result = EvalVec("Truncate([1.5; -2.5])");
        Assert.Equal(2, result.Dimensions);
        Assert.Equal((BigDecimal)1, result[0]);
        Assert.Equal((BigDecimal)(-2), result[1]);

        var truncated = EvaluateWith("Truncate(x)", ExpressionOptions.None, ("x", new BigDecimal(-25, -1)));
        Assert.Equal(new BigDecimal(-2), Assert.IsType<BigDecimal>(truncated));
        Assert.IsType<BigDecimal>(EvaluateWith("Floor(x)", ExpressionOptions.None, ("x", new BigDecimal(-25, -1))));
    }

    // NCALC-232
    [Fact]
    public async Task PercentFunctionsShouldDivideByZeroLikeTheDivisionOperator()
    {
        Assert.All(await EvaluateInAllEvaluators("PercentOf(10, 0)", ExpressionOptions.None),
            r => Assert.Equal(double.PositiveInfinity, Assert.IsType<Percent>(r).Value));
        Assert.All(await EvaluateInAllEvaluators("PercentDiff(0, 5)", ExpressionOptions.None),
            r => Assert.Equal(double.PositiveInfinity, Assert.IsType<Percent>(r).Value));
        Assert.Equal(double.NaN, Assert.IsType<Percent>(EvaluateWith("PercentOf(0, 0)", ExpressionOptions.None)).Value);
        Assert.Equal(double.NegativeInfinity, Assert.IsType<Percent>(EvaluateWith("PercentDiff(0, -5)", ExpressionOptions.DecimalAsDefault)).Value);

        // Other divisors are still divided exactly
        Assert.Equal("50%", EvaluateWith("PercentOf(25, 50)", ExpressionOptions.None)?.ToString());
        Assert.Equal("33.333333333333333333333333333%", EvaluateWith("PercentOf(1, 3)", ExpressionOptions.None)?.ToString());
        Assert.Equal("-25%", EvaluateWith("PercentDiff(80, 60)", ExpressionOptions.None)?.ToString());
    }

    // NCALC-232
    [Fact]
    public async Task BuiltInFunctionsShouldThrowNCalcExceptionsForUnsupportedArguments()
    {
        var complex = new ComplexNumber(BigDecimal.Zero, new BigDecimal(2), MathHelperOptions.Empty);
        var percent = new Percent(10);
        foreach (var (text, name, value) in new (string, string, object)[]
        {
            ("PercentOf(p, 200)", "p", percent),
            ("PercentOf(200, p)", "p", percent),
            ("PercentDiff(p, 200)", "p", percent),
            ("Sign(c)", "c", complex),
            ("IEEERemainder(c, 1)", "c", complex),
        })
        {
            foreach (var nonRecursive in new[] { false, true })
            {
                var options = ExpressionOptions.NoCache | (nonRecursive ? ExpressionOptions.UseNonRecursiveEvaluator : ExpressionOptions.None);
                var expression = new Expression(text, options);
                expression.Parameters[name] = value;
                Assert.Throws<NCalcEvaluationException>(() => expression.Evaluate(TestContext.Current.CancellationToken));
                var asyncExpression = new AsyncExpression(text, options);
                asyncExpression.Parameters[name] = value;
                await Assert.ThrowsAsync<NCalcEvaluationException>(async () => await asyncExpression.EvaluateAsync(TestContext.Current.CancellationToken));
            }
        }
    }

    // NCALC-310: the series of ExtendedNumerics for Arcsin was wrong in the 7th digit for ±0.7
    [Theory]
    [InlineData("Asin(x)", "0.7")]
    [InlineData("Asin(x)", "-0.7")]
    [InlineData("Acos(x)", "0.7")]
    [InlineData("Acos(x)", "-0.7")]
    [InlineData("Asin(x)", "0.99")]
    [InlineData("Acos(x)", "0.24")]
    public void BigNumberAsinAndAcosShouldBeAccurate(string expression, string x)
    {
        double value = double.Parse(x, CultureInfo.InvariantCulture);
        double expected = expression.StartsWith("Asin", StringComparison.Ordinal) ? Math.Asin(value) : Math.Acos(value);
        var result = EvaluateWith(expression, ExpressionOptions.UseBigNumbers, ("x", ParseBigNumber(x)));
        Assert.IsType<BigDecimal>(result);
        AssertCloseTo(expected, result);
    }

    // NCALC-311: a double below the resolution of decimal became 0 in the rounding functions with DecimalAsDefault
    [Fact]
    public async Task RoundingFunctionsShouldNotZeroTinyDoublesWithDecimalAsDefault()
    {
        const ExpressionOptions options = ExpressionOptions.DecimalAsDefault;
        Assert.All(await EvaluateInAllEvaluators("Sign(Exp(-100))", options), r => Assert.Equal(1, r));
        Assert.All(await EvaluateInAllEvaluators("Ceiling(Exp(-100))", options), r => Assert.Equal(1.0, r));
        Assert.All(await EvaluateInAllEvaluators("Floor(-Exp(-100))", options), r => Assert.Equal(-1.0, r));
        Assert.Equal(Math.Exp(-100), EvaluateWith("Abs(-Exp(-100))", options));
        Assert.Equal(true, EvaluateWith("Abs(t) > 0", options, ("t", 1e-30)));
        Assert.Equal(1e-30, EvaluateWith("Round(t, 30)", options, ("t", 1e-30)));
        Assert.Equal(3e-29, EvaluateWith("Round(t, 29)", options, ("t", 3e-29)));
        Assert.Equal(-1, EvaluateWith("Sign(t)", options, ("t", -1e-30f)));
        // Digits beyond the 28 places of decimal were cut: 1.234E-27 became 1E-27
        Assert.Equal(1.234e-27, EvaluateWith("Round(t, 30)", options, ("t", 1.234e-27)));
        Assert.Equal(1.234e-27, new Expression("Abs(t)", options | ExpressionOptions.NoCache) { Parameters = { ["t"] = -1.234e-27 } }.ToLambda<object>(TestContext.Current.CancellationToken)());

        // Other doubles are still computed as decimals
        Assert.Equal(0.25m, EvaluateWith("Abs(t)", options, ("t", -0.25)));
        Assert.Equal(1E-20m, EvaluateWith("Abs(t)", options, ("t", 1e-20)));
    }

    // NCALC-322: a big argument with a fractional base was converted to double and gave ±∞
    [Fact]
    public async Task LogOfABigArgumentShouldAcceptAFractionalBase()
    {
        var huge = BigInteger.Pow(10, 400);
        double ln = 400 * Math.Log(10);
        AssertCloseTo(ln / Math.Log(2.5), EvaluateWith("Log(x, 2.5)", ExpressionOptions.UseBigNumbers, ("x", huge)));
        AssertCloseTo(ln / Math.Log(0.5), EvaluateWith("Log(x, 0.5)", ExpressionOptions.UseBigNumbers, ("x", huge)));
        AssertCloseTo(ln / Math.Log(0.5), EvaluateWith("Log(x, half)", ExpressionOptions.UseBigNumbers, ("x", huge), ("half", new BigDecimal(5, -1))));
        AssertCloseTo(-ln / Math.Log(2.5), EvaluateWith("Log(x, 2.5)", ExpressionOptions.UseBigNumbers, ("x", new BigDecimal(BigInteger.One, -400))));
        Assert.All(await EvaluateInAllEvaluators("Log(x, 2.5)", ExpressionOptions.UseBigNumbers, ("x", huge)), r => AssertCloseTo(ln / Math.Log(2.5), r));

        // A big argument within the range of double is as accurate as Math.Log
        Assert.Equal(Math.Log(1.0000001, 2.5), EvaluateWith("Log(x, 2.5)", ExpressionOptions.UseBigNumbers, ("x", new BigDecimal(10000001, -7))));
        Assert.Equal(double.NaN, EvaluateWith("Log(x, -2.5)", ExpressionOptions.UseBigNumbers, ("x", huge)));
    }

    // NCALC-323: BCL exceptions escaped from Factorial and Round
    [Fact]
    public async Task FactorialAndRoundShouldNotThrowBclExceptions()
    {
        var complex = new ComplexNumber(BigDecimal.Zero, new BigDecimal(2), MathHelperOptions.Empty);
        await AssertThrowsInAllEvaluators<NCalcEvaluationException>("c!", ExpressionOptions.None, ("c", complex));

        // A whole number beyond the range of long gives infinity, as the same value as a double does
        Assert.Equal(double.PositiveInfinity, EvaluateWith("x!", ExpressionOptions.None, ("x", BigInteger.Pow(10, 30))));
        Assert.Equal(double.PositiveInfinity, EvaluateWith("x!", ExpressionOptions.None, ("x", 1e25m)));
        Assert.Equal(double.PositiveInfinity, EvaluateWith("(2 ** 70)!", ExpressionOptions.DecimalAsDefault));
        Assert.Equal(double.PositiveInfinity, EvaluateWith("(10 ** 30)!", ExpressionOptions.DecimalAsDefault));
        Assert.Equal(double.PositiveInfinity, EvaluateWith("x!", ExpressionOptions.None, ("x", 1e21)));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("x!", ExpressionOptions.None, ("x", -BigInteger.Pow(10, 30))));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("x!", ExpressionOptions.DecimalAsDefault, ("x", -1e25m)));

        // A number of places beyond the range of int is clamped, as a double one is
        Assert.All(await EvaluateInAllEvaluators("Round(2.345, 2147483648)", ExpressionOptions.None), r => Assert.Equal(2.345, r));
        Assert.Equal(2.345, EvaluateWith("Round(2.345, d)", ExpressionOptions.None, ("d", BigInteger.Pow(10, 20))));
        Assert.Equal(0.0, EvaluateWith("Round(2.345, d)", ExpressionOptions.None, ("d", -BigInteger.Pow(10, 30))));
        Assert.Equal(1234, EvaluateWith("Round(1234, d)", ExpressionOptions.None, ("d", long.MaxValue)));
        Assert.Equal(0, EvaluateWith("Round(1234, d)", ExpressionOptions.None, ("d", long.MinValue)));
        Assert.Equal(2.345, EvaluateWith("Round(2.345, d)", ExpressionOptions.None, ("d", new BigDecimal(BigInteger.Pow(10, 20)))));
        Assert.Equal(2.345m, EvaluateWith("Round(2.345, d)", ExpressionOptions.DecimalAsDefault, ("d", 1e20m)));
    }

    // NCALC-338: the casts of a double to int or long are unspecified for NaN and out-of-range values (net8 and earlier gave
    // int.MinValue, so Round(2.345, 1e10) was 0 and (2 ** 70)! was "not defined for negative numbers")
    [Fact]
    public void ConversionsOfDoublesToIntegersShouldNotDependOnTheTargetFramework()
    {
        Assert.Equal(2.345, EvaluateWith("Round(2.345, 1e10)", ExpressionOptions.None));
        Assert.Equal(2.345, EvaluateWith("Round(2.345, d)", ExpressionOptions.None, ("d", double.PositiveInfinity)));
        Assert.Equal(0.0, EvaluateWith("Round(2.345, -1e10)", ExpressionOptions.None));
        Assert.Equal(0.0, EvaluateWith("Round(2.345, d)", ExpressionOptions.None, ("d", float.NegativeInfinity)));
        Assert.Throws<NCalcEvaluationException>(() => EvaluateWith("Round(2.345, Sqrt(-1))", ExpressionOptions.None));
        Assert.Equal(double.PositiveInfinity, EvaluateWith("(2 ** 70)!", ExpressionOptions.None));

        Assert.Throws<NCalcConversionException>(() => MathHelper.ConvertToInt(1e10, "Test", CultureInfo.InvariantCulture, null));
        Assert.Throws<NCalcConversionException>(() => MathHelper.ConvertToInt(double.NaN, "Test", CultureInfo.InvariantCulture, null));
        Assert.Throws<NCalcConversionException>(() => MathHelper.ConvertToInt(-3e9f, "Test", CultureInfo.InvariantCulture, null));
        Assert.Throws<NCalcConversionException>(() => MathHelper.ConvertToLong(1e19, "Test", CultureInfo.InvariantCulture, null));
        Assert.Throws<NCalcConversionException>(() => MathHelper.ConvertToLong(double.NaN, "Test", CultureInfo.InvariantCulture, null));
        Assert.Throws<NCalcConversionException>(() => MathHelper.ConvertToLong(BigInteger.Pow(10, 30), "Test", CultureInfo.InvariantCulture, null));
        Assert.Throws<OverflowException>(() => MathHelper.ConvertToInt(double.NaN, MathHelperOptions.Empty));
        Assert.Throws<OverflowException>(() => MathHelper.ConvertToLong(9.3e18, MathHelperOptions.Empty));
        Assert.Equal(long.MinValue, MathHelper.ConvertToLong(-9.2233720368547758E18, "Test", CultureInfo.InvariantCulture, null));
        Assert.Equal(int.MaxValue, MathHelper.ConvertToInt(2147483647.9, "Test", CultureInfo.InvariantCulture, null));
        Assert.Equal(-3, MathHelper.ConvertToInt(-3.7, "Test", CultureInfo.InvariantCulture, null));
    }

    // NCALC-329: after one call with a huge argument, π was cut from thousands of digits on every call
    [Fact]
    public void BigNumberTrigonometryShouldStayCorrectAfterAHugeArgument()
    {
        var x = new BigDecimal(15, -1);
        AssertCloseTo(Math.Sin(1.5), EvaluateWith("Sin(x)", ExpressionOptions.UseBigNumbers, ("x", x)));
        EvaluateWith("Sin(x)", ExpressionOptions.UseBigNumbers, ("x", new BigDecimal(BigInteger.One, 5000)));
        for (int i = 0; i < 3; i++)
        {
            AssertCloseTo(Math.Sin(1.5), EvaluateWith("Sin(x)", ExpressionOptions.UseBigNumbers, ("x", x)));
            AssertCloseTo(Math.Cos(1.5), EvaluateWith("Cos(x)", ExpressionOptions.UseBigNumbers, ("x", x)));
            AssertCloseTo(Math.Atan(1.5), EvaluateWith("Atan(x)", ExpressionOptions.UseBigNumbers, ("x", x)));
        }
    }

    // NCALC-330: the fixed point of the big-number trigonometric functions made arguments below 1E-110 zero
    [Theory]
    [InlineData("Sin(x)", 1, -101, 1E-101)]
    [InlineData("Sin(x)", -1, -150, -1E-150)]
    [InlineData("Tan(x)", 1, -120, 1E-120)]
    [InlineData("Asin(x)", 1, -200, 1E-200)]
    [InlineData("Atan(x)", -1, -150, -1E-150)]
    [InlineData("Cos(x)", 1, -150, 1.0)]
    [InlineData("Acos(x)", 1, -150, Math.PI / 2)]
    public void BigNumberTrigonometryShouldKeepTinyArguments(string expression, int mantissa, int exponent, double expected)
    {
        var argument = new BigDecimal(mantissa, exponent);
        var result = Assert.IsType<BigDecimal>(EvaluateWith(expression, ExpressionOptions.UseBigNumbers, ("x", argument)));
        Assert.Equal(expected, MathHelper.ConvertToDouble(result, MathHelperOptions.Empty), Math.Abs(expected) * 1e-14);
        Assert.NotEqual(BigDecimal.Zero, result);
    }

    // NCALC-330: Csc and Cot of a tiny argument threw "undefined for this argument"
    [Fact]
    public void BigNumberCscAndCotShouldAcceptTinyArguments()
    {
        var options = new MathHelperOptions(CultureInfo.InvariantCulture, ExpressionOptions.UseBigNumbers);
        var tiny = new BigDecimal(2, -150);
        Assert.Equal(5E149, MathHelper.ConvertToDouble(MathHelper.Csc(tiny, options), MathHelperOptions.Empty), 1E136);
        Assert.Equal(5E149, MathHelper.ConvertToDouble(MathHelper.Cot(tiny, options), MathHelperOptions.Empty), 1E136);
    }

    // NCALC-331: Convert.ToBoolean does not know big numbers
    [Theory]
    [InlineData("if(x, 1, 2)")]
    [InlineData("ifs(x, 1, true, 2)")]
    [InlineData("x ? 1 : 2")]
    [InlineData("if(not x, 2, 1)")]
    [InlineData("if(x and true, 1, 2)")]
    public async Task BigNumbersShouldBeConditions(string text)
    {
        foreach (var (value, expected) in new (object, int)[] { (BigInteger.Pow(2, 70), 1), (BigInteger.Zero, 2), (new BigDecimal(5, -1), 1), (BigDecimal.Zero, 2) })
        {
            Assert.All(await EvaluateInAllEvaluators(text, ExpressionOptions.UseBigNumbers, ("x", value)), r => Assert.Equal(expected, r));
            var lambda = new Expression(text, ExpressionOptions.UseBigNumbers | ExpressionOptions.NoCache) { Parameters = { ["x"] = value } };
            Assert.Equal(expected, lambda.ToLambda<object>(TestContext.Current.CancellationToken)());
        }
        Assert.All(await EvaluateInAllEvaluators("if(2 ** 70, 1, 2)", ExpressionOptions.UseBigNumbers), r => Assert.Equal(1, r));
    }

    // NCALC-331: ToLambda visited the arguments first and reported "Parameter null is not defined"
    [Theory]
    [InlineData("Pow(null)", "Pow() takes exactly 2 arguments")]
    [InlineData("Atan2(null)", "Atan2() takes exactly 2 arguments")]
    [InlineData("Max(null)", "Max() takes exactly 2 arguments")]
    [InlineData("Log(null)", "Log() takes exactly 2 arguments")]
    [InlineData("MakeStr(null)", "MakeStr() takes exactly 2 arguments")]
    [InlineData("Abs(null, null)", "Abs() takes exactly 1 argument")]
    [InlineData("PercentOf(null)", "PercentOf() takes exactly 2 arguments")]
    public void LambdaShouldReportTheArgumentCountLikeTheEvaluators(string text, string message)
    {
        var evaluatorError = Assert.Throws<NCalcEvaluationException>(() => new Expression(text, ExpressionOptions.NoCache).Evaluate(TestContext.Current.CancellationToken));
        Assert.Equal(message, evaluatorError.Message);
        var lambdaError = Assert.Throws<NCalcEvaluationException>(() => new Expression(text, ExpressionOptions.NoCache).ToLambda<object>(TestContext.Current.CancellationToken)());
        Assert.Equal(message, lambdaError.Message);
    }

    // NCALC-331: Round to 15 or fewer places read only 15 significant digits of the double
    [Fact]
    public async Task RoundShouldReadAllDigitsOfADouble()
    {
        Assert.All(await EvaluateInAllEvaluators("Round(t, 11)", ExpressionOptions.None, ("t", 123456.78901234567)), r => Assert.Equal(123456.78901234567, r));
        Assert.Equal(123456.7890123457, EvaluateWith("Round(t, 10)", ExpressionOptions.None, ("t", 123456.78901234567)));
        // 2.4999999999999996 was read as the midpoint 2.5
        Assert.Equal(2.0, EvaluateWith("Round(t, 0)", ExpressionOptions.RoundAwayFromZero, ("t", 2.4999999999999996)));
        Assert.Equal(2.0, new Expression("Round(t, 0)", ExpressionOptions.RoundAwayFromZero | ExpressionOptions.NoCache) { Parameters = { ["t"] = 2.4999999999999996 } }.ToLambda<object>(TestContext.Current.CancellationToken)());

        // Decimal midpoints are still honored
        Assert.Equal(1.6, EvaluateWith("Round(1.55, 1)", ExpressionOptions.RoundAwayFromZero));
        Assert.Equal(2.68, EvaluateWith("Round(2.675, 2)", ExpressionOptions.None));
        Assert.Equal(Math.Round(2.675, 2), EvaluateWith("Round(2.675, 2)", ExpressionOptions.UseSystemMathRound));
        Assert.Equal(-0.5, EvaluateWith("Round(-0.45, 1)", ExpressionOptions.RoundAwayFromZero));
    }

    // NCALC-331: Sign, Sqrt, Pow, Max and Min of vectors threw
    [Fact]
    public void VectorFunctionsShouldWorkElementWise()
    {
        Assert.Equal(new Domain.Vector(new BigDecimal[] { -1, 0, 1 }), EvalVec("Sign([-2.5; 0; 3])"));
        Assert.Equal(new Domain.Vector(new BigDecimal[] { 2, 3 }), EvalVec("Sqrt([4; 9])"));
        Assert.Equal(new Domain.Vector(new BigDecimal[] { 4, 9 }), EvalVec("Pow([2; 3], 2)"));
        Assert.Equal(new Domain.Vector(new BigDecimal[] { 8, 27 }), EvalVec("[2; 3] ** 3"));
        Assert.Equal(new Domain.Vector(new BigDecimal[] { 1, 2 }), EvalVec("Min([1; 5], [3; 2])"));
        Assert.Equal(new Domain.Vector(new BigDecimal[] { 3, 5 }), EvalVec("Max([1; 5], [3; 2])"));
        Assert.Throws<NCalcEvaluationException>(() => EvalVec("Sqrt([-4; 9])"));
        Assert.Throws<NCalcEvaluationException>(() => EvalVec("Max([1; 5], [3; 2; 1])"));
    }
}
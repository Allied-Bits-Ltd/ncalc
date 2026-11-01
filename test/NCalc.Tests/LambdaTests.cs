#if NET8_0_OR_GREATER
#nullable enable
using System.Diagnostics.CodeAnalysis;

// ReSharper disable MemberCanBeProtected.Local

namespace NCalc.Tests;

[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Local", Justification = "Reflection")]
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Reflection")]
[SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Reflection")]
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Style")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local", Justification = "Reflection")]
[Trait("Category", "Lambdas")]

public class LambdaTests
{
    private class Context
    {
        public int FieldA { get; set; }
        public string? FieldB { get; set; }
        public decimal FieldC { get; set; }
        public decimal? FieldD { get; set; }
        public int? FieldE { get; set; }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        public int Test(int a, int b)
        {
            return a + b;
        }

        public string Test(string a, string b)
        {
            return a + b;
        }

        public int Test(int a, int b, int c)
        {
            return a + b + c;
        }

        public double Test(double a, double b, double c)
        {
            return a + b + c;
        }

        public string Sum(string msg, params int[] numbers)
        {
            var total = 0;
            foreach (var num in numbers)
            {
                total += num;
            }
            return msg + total;
        }

        public int Sum(params int[] numbers)
        {
            var total = 0;
            foreach (var num in numbers)
            {
                total += num;
            }
            return total;
        }

        public int Sum(TestObject1 obj1, TestObject2 obj2)
        {
            return obj1.Count1 + obj2.Count2;
        }

        public int Sum(TestObject2 obj1, TestObject1 obj2)
        {
            return obj1.Count2 + obj2.Count1;
        }

        public int Sum(TestObject1 obj1, TestObject1 obj2)
        {
            return obj1.Count1 + obj2.Count1;
        }

        public int Sum(TestObject2 obj1, TestObject2 obj2)
        {
            return obj1.Count2 + obj2.Count2;
        }

        public double Max(TestObject1 obj1, TestObject1 obj2)
        {
            return Math.Max(obj1.Count1, obj2.Count1);
        }

        public double Min(TestObject1 obj1, TestObject1 obj2)
        {
            return Math.Min(obj1.Count1, obj2.Count1);
        }

        public class TestObject1
        {
            public int Count1 { get; set; }
        }

        public class TestObject2
        {
            public int Count2 { get; set; }
        }

        public TestObject1 CreateTestObject1(int count)
        {
            return new TestObject1() { Count1 = count };
        }

        public TestObject2 CreateTestObject2(int count)
        {
            return new TestObject2() { Count2 = count };
        }
    }

    private class SubContext : Context
    {
        public int Multiply(int a, int b)
        {
            return a * b;
        }

        public new int Test(int a, int b)
        {
            return base.Test(a, b) / 2;
        }

        public int Test(int a, int b, int c, int d)
        {
            return a + b + c + d;
        }

        public int Sum(TestObject1 obj1, TestObject2 obj2, TestObject2 obj3)
        {
            return obj1.Count1 + obj2.Count2 + obj3.Count2 + 100;
        }
    }

    [Theory]
    [InlineData("1+2", 3)]
    [InlineData("1-2", -1)]
    [InlineData("2*2", 4)]
    [InlineData("10/2", 5)]
    [InlineData("7%2", 1)]
    public void ShouldHandleIntegers(string input, int expected)
    {
        var expression = new Expression(input);
        var sut = expression.ToLambda<int>(TestContext.Current.CancellationToken);

        Assert.Equal(expected, sut());
    }

    [Theory]
    [InlineData("1/2", 0.5)]
    [InlineData("2/5", 0.4)]
    public void ShouldHandleDivision(string input, double expected)
    {
        var expression = new Expression(input);
        var sut = expression.ToLambda<double>(TestContext.Current.CancellationToken);

        Assert.Equal(expected, sut());
    }

    [Theory]
    [InlineData("2/1", 2)]
    [InlineData("6/2", 3)]
    public void ShouldHandleDivisionAsInteger(string input, int expected)
    {
        var expression = new Expression(input);
        var sut = expression.ToLambda<int>(TestContext.Current.CancellationToken);

        Assert.Equal(expected, sut());
    }

    [Fact]
    public void ShouldHandleParameters()
    {
        var expression = new Expression("[FieldA] > 5 && [FieldB] = 'test'");
        var sut = expression.ToLambda<Context, bool>(TestContext.Current.CancellationToken);
        var context = new Context { FieldA = 7, FieldB = "test" };

        Assert.True(sut(context));
    }

    [Fact]
    public void ShouldHandleOverloadingSameParamCount()
    {
        var expression = new Expression("Test('Hello', ' world!')");
        var sut = expression.ToLambda<Context, string>(TestContext.Current.CancellationToken);
        var context = new Context();

        Assert.Equal("Hello world!", sut(context));
    }

    [Fact]
    public void ShouldHandleOverloadingDifferentParamCount()
    {
        var expression = new Expression("Test(Test(1, 2), 3, 4)");
        var sut = expression.ToLambda<Context, int>(TestContext.Current.CancellationToken);
        var context = new Context();

        Assert.Equal(10, sut(context));
    }

    [Fact]
    public void ShouldHandleOverloadingObjectParameters()
    {
        var expression = new Expression("Sum(CreateTestObject1(2), CreateTestObject2(2)) + Sum(CreateTestObject2(1), CreateTestObject1(5))");
        var sut = expression.ToLambda<Context, int>(TestContext.Current.CancellationToken);
        var context = new Context();

        Assert.Equal(10, sut(context));
    }

    [Fact]
    public void ShouldHandleParamsKeyword()
    {
        var expression = new Expression("Sum(Test(1,1),2)");
        var sut = expression.ToLambda<Context, int>(TestContext.Current.CancellationToken);
        var context = new Context();

        Assert.Equal(4, sut(context));
    }

    [Fact]
    public void ShouldHandleMixedParamsKeyword()
    {
        var expression = new Expression("Sum('Your total is: ', Test(1,1), 2, 3)");
        var sut = expression.ToLambda<Context, string>(TestContext.Current.CancellationToken);
        var context = new Context();

        Assert.Equal("Your total is: 7", sut(context));
    }

    [Fact]
    public void ShouldHandleCustomFunctions()
    {
        var expression = new Expression("Test(Test(1, 2), 3)");
        var sut = expression.ToLambda<Context, int>(TestContext.Current.CancellationToken);
        var context = new Context();

        Assert.Equal(6, sut(context));
    }

    [Fact]
    public void ShouldHandleContextInheritance()
    {
        var lambda1 = new Expression("Multiply(5, 2)").ToLambda<SubContext, int>(TestContext.Current.CancellationToken);
        var lambda2 = new Expression("Test(5, 5)").ToLambda<SubContext, int>(TestContext.Current.CancellationToken);
        var lambda3 = new Expression("Test(1,2,3,4)").ToLambda<SubContext, int>(TestContext.Current.CancellationToken);
        var lambda4 = new Expression("Sum(CreateTestObject1(100), CreateTestObject2(100), CreateTestObject2(100))")
            .ToLambda<SubContext, int>(TestContext.Current.CancellationToken);

        var context = new SubContext();
        Assert.Equal(10, lambda1(context));
        Assert.Equal(5, lambda2(context));
        Assert.Equal(10, lambda3(context));
        Assert.Equal(400, lambda4(context));
    }

    [Theory]
    [InlineData("Test(1, 1, 1)")]
    [InlineData("Test(1.0, 1.0, 1.0)")]
    [InlineData("Test(1.0, 1, 1.0)")]
    public void ShouldHandleImplicitConversion(string input)
    {
        var lambda = new Expression(input).ToLambda<Context, int>(TestContext.Current.CancellationToken);

        var context = new Context();
        Assert.Equal(3, lambda(context));
    }

    [Fact]
    public void MissingMethod()
    {
        var expression = new Expression("MissingMethod(1)");
        try
        {
            _ = expression.ToLambda<Context, int>(TestContext.Current.CancellationToken);
        }
        catch (MissingMethodException ex)
        {
            System.Diagnostics.Debug.Write(ex);
            Assert.True(true);
            return;
        }
        Assert.True(false);
    }

    [Fact]
    public void ShouldHandleTernaryOperator()
    {
        var expression = new Expression("Test(1, 2) = 3 ? 1 : 2");
        var sut = expression.ToLambda<Context, int>(TestContext.Current.CancellationToken);
        var context = new Context();

        Assert.Equal(1, sut(context));
    }

    [Fact]
    public void Issue1()
    {
        var expr = new Expression("2 + 2 - a - b - x");

        const decimal x = 5m;
        const decimal a = 6m;
        const decimal b = 7m;

        expr.Parameters["x"] = x;
        expr.Parameters["a"] = a;
        expr.Parameters["b"] = b;

        var f = expr.ToLambda<float>(TestContext.Current.CancellationToken); // Here it throws System.ArgumentNullException. Parameter name: expression
        Assert.Equal(-14, f());
    }

    [Theory]
    [InlineData("if(true, true, false)")]
    [InlineData("in(3, 1, 2, 3, 4)")]
    public void ShouldHandleBuiltInFunctions(string input)
    {
        var expression = new Expression(input);
        var sut = expression.ToLambda<bool>(TestContext.Current.CancellationToken);
        Assert.True(sut());
    }

    [Theory]
    [InlineData("Min(CreateTestObject1(1), CreateTestObject1(2))", 1)]
    [InlineData("Max(CreateTestObject1(1), CreateTestObject1(2))", 2)]
    [InlineData("Min(1, 2)", 1)]
    [InlineData("Max(1, 2)", 2)]
    public void ShouldProritiseContextFunctions(string input, double expected)
    {
        var expression = new Expression(input);
        var lambda = expression.ToLambda<Context, double>(TestContext.Current.CancellationToken);
        var context = new Context();
        var actual = lambda(context);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("[FieldA] > [FieldC]", true)]
    [InlineData("[FieldC] > 1.34", true)]
    [InlineData("[FieldC] > (1.34 * 2) % 3", false)]
    [InlineData("[FieldE] = 2", true)]
    [InlineData("[FieldD] > 0", false)]
    public void ShouldHandleDataConversions(string input, bool expected)
    {
        var expression = new Expression(input);
        var sut = expression.ToLambda<Context, bool>(TestContext.Current.CancellationToken);
        var context = new Context { FieldA = 7, FieldB = "test", FieldC = 2.4m, FieldE = 2 };

        Assert.Equal(expected, sut(context));
    }

    [Theory]
    [InlineData("Min(3,2)", 2)]
    [InlineData("Min(3.2,6.3)", 3.2)]
    [InlineData("Max(2.6,9.6)", 9.6)]
    [InlineData("Max(9,6)", 9.0)]
    [InlineData("Pow(5,2)", 25)]
    public void ShouldHandleNumericBuiltInFunctions(string input, double expected)
    {
        var expression = new Expression(input);
        var sut = expression.ToLambda<object>(TestContext.Current.CancellationToken);
        // The value has the type that Evaluate() gives (Min(3,2) is an int)
        Assert.Equal(expected, Convert.ToDouble(sut()));
        Assert.Equal(expression.Evaluate(TestContext.Current.CancellationToken), sut());
    }

    [Theory]
    [InlineData("if(true, 1, 0.0)", 1)]
    [InlineData("if(true, 1.0, 0)", 1.0)]
    [InlineData("if(true, 1.0, 0.0)", 1.0)]
    public void ShouldHandleFloatIfFunction(string input, object expected)
    {
        // As with Evaluate(), the value of the chosen argument keeps its type
        var expression = new Expression(input);
        var sut = expression.ToLambda<object>(TestContext.Current.CancellationToken);
        Assert.Equal(expected, sut());
    }

    [Theory]
    [InlineData("if(true, 1, 0)", 1)]
    public void ShouldHandleIntIfFunction(string input, int expected)
    {
        var expression = new Expression(input);
        var sut = expression.ToLambda<object>(TestContext.Current.CancellationToken);
        Assert.Equal(expected, sut());
    }

    [Theory]
    [InlineData("if(true, 'a', 'b')", "a")]
    public void ShouldHandleStringIfFunction(string input, string expected)
    {
        var expression = new Expression(input);
        var sut = expression.ToLambda<object>(TestContext.Current.CancellationToken);
        Assert.Equal(expected, sut());
    }

    [Fact]
    public void ShouldAllowValueTypeContexts()
    {
        // Arrange
        const decimal expected = 6.908m;
        var expression = new Expression("Foo * 3.14");
        var sut = expression.ToLambda<FooStruct, decimal>(TestContext.Current.CancellationToken);
        var context = new FooStruct();

        // Act
        var actual = sut(context);

        // Assert
        Assert.Equal(expected, actual);
    }

    // https://github.com/sklose/NCalc2/issues/54
    [Fact]
    public void Issue54()
    {
        // Arrange
        const long expected = 9999999999L;
        var expression = $"if(true, {expected}, 0)";
        var e = new Expression(expression);
        var context = new object();

        var lambda = e.ToLambda<object, long>(TestContext.Current.CancellationToken);

        // Act
        var actual = lambda(context);

        // Assert
        Assert.Equal(expected, actual);
    }

    internal struct FooStruct
    {
        public double Foo => 2.2;
    }

    struct ContextAndResult
    {
        public double x;
        public double y;
        public string Func;
        public double ExpressionResult;
        public double LambdaResult;
    }

    /*
    [Fact]
    public void ExpressionAndLambdaFuncBehaviorMatch()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        // Arrange
        double[] testValues = [double.MinValue, -Math.PI, -1, Math.BitDecrement(0), 0, Math.BitIncrement(0), 0.001, 1, 2, 3.14, Math.PI, 10, 100, double.MaxValue
        ];

        string[] functionsImplemented =
        [
            "Abs", "Acos", "Asin", "Atan", "Ceiling", "Cos", "Exp", "Floor", "IEEERemainder",
            "Log", "Log10", "Max", "Min", "Pow", "Round", "Sin", "Sqrt", "Tan", "Truncate"
        ];
        var functionsWithTwoArguments = new HashSet<string>
        {
            "Round", "IEEERemainder", "Log", "Max", "Min", "Pow"
        };

        var doubleToStringFormats = new[] { "F17", "0" };

        List<ContextAndResult> testResults = [];
        ContextAndResult currentContext = new();

        // Act
        try
        {
            // Iterate each function name with context
            foreach (var funcName in functionsImplemented)
            {
                var isTwoArgumentFunc = functionsWithTwoArguments.Contains(funcName);
                var expressionString = isTwoArgumentFunc ? funcName + "(x, y)" : funcName + "(x)";

                currentContext.Func = expressionString;

                var expression = new Expression(expressionString, CultureInfo.InvariantCulture);
                var lambda = expression.ToLambda<ContextAndResult, double>(TestContext.Current.CancellationToken);

                for (var i = 0; i < testValues.Length; ++i)
                {
                    currentContext.x = testValues[i];
                    expression.Parameters["x"] = currentContext.x;

                    if (isTwoArgumentFunc)
                    {
                        currentContext.y = testValues[(i + 1) % testValues.Length];
                        // Edge case (Round second argument is int decimal places to round from 0 to 15)
                        if (funcName == "Round")
                            currentContext.y = Math.Clamp(currentContext.y, 0, 15);
                        expression.Parameters["y"] = currentContext.y;
                    }

                    currentContext.ExpressionResult = (double)expression.Evaluate(TestContext.Current.CancellationToken)!;
                    currentContext.LambdaResult = lambda(currentContext);

                    testResults.Add(currentContext);
                }
            }

            // Iterate each function name without context with numbers of format 123.456 (doubles) or 123 (integers)
            foreach (var doubleToStringFormat in doubleToStringFormats)
            {
                for (var i = 0; i < testValues.Length; ++i)
                {
                    currentContext.x = testValues[i];
                    // Edge case (Exception when too big doubles not fit into Int64)
                    // We are multiplying by 0.99 because after clamping exception is still thrown
                    // Int64.MinValue = -9223372036854775808, (double)Int64.MinValue = -9223372036854780000 which is lesser)
                    if (doubleToStringFormat == "0" && Math.Abs(currentContext.x) > long.MaxValue)
                    {
                        currentContext.x = Math.Clamp(currentContext.x, long.MinValue, long.MaxValue) * 0.99;
                    }

                    var doubleParam1 = currentContext.x.ToString(doubleToStringFormat);

                    foreach (var funcName in functionsImplemented)
                    {
                        var isTwoArgumentFunc = functionsWithTwoArguments.Contains(funcName);

                        string expressionString;

                        if (isTwoArgumentFunc)
                        {
                            currentContext.y = testValues[(i + 1) % testValues.Length];
                            // Edge case (see previous)
                            if (doubleToStringFormat == "0" && Math.Abs(currentContext.y) > long.MaxValue)
                            {
                                currentContext.y = Math.Clamp(currentContext.y, long.MinValue, long.MaxValue) * 0.99;
                            }
                            // Edge case (Round second argument is int decimal places to round from 0 to 15)
                            if (funcName == "Round")
                                currentContext.y = Math.Clamp(currentContext.y, 0, 15);

                            var doubleParam2 = currentContext.y.ToString(doubleToStringFormat);
                            expressionString = funcName + $"({doubleParam1}, {doubleParam2})";
                        }
                        else
                        {
                            expressionString = funcName + $"({doubleParam1})";
                        }
                        currentContext.Func = expressionString;

                        var expression = new Expression(expressionString);
                        var lambda = expression.ToLambda<double>(TestContext.Current.CancellationToken);

                        currentContext.ExpressionResult = Convert.ToDouble(expression.Evaluate(TestContext.Current.CancellationToken));
                        currentContext.LambdaResult = lambda();
                        testResults.Add(currentContext);
                    }
                }
            }
        }
        // Serves to find an exact spot of error. Change Exception to non-related (for ex. OutOfMemoryException) to navigate via links in Test Explorer
        catch (Exception ex)
        {
            Assert.Fail($"""
                         {ex.GetType()}, context x: {currentContext.x},
                                             func: {currentContext.Func},
                                             Expression result: {currentContext.ExpressionResult},
                                             Lambda result: {currentContext.LambdaResult}
                                             exception message: {ex.Message}
                                             exception stack: {ex.StackTrace}
                         """);
        }

        // Assert
        foreach (var testContext in testResults)
        {
            Assert.True(IsEqual(testContext.ExpressionResult, testContext.LambdaResult),
                $"""
                 context x: {testContext.x},
                                     func: {testContext.Func},
                                     Expression result: {testContext.ExpressionResult},
                                     Lambda result: {testContext.LambdaResult}
                 """);
        }

        return;

        static bool IsEqual(double a, double b)
        {
            if (double.IsNaN(a) && double.IsNaN(b))
            {
                return true;
            }

            return a == b;
            //double tol = Math.Clamp(Math.Max(a, b) * 1e-12, 1e-12, 1);
            //return Math.Abs(a - b) < tol;
        }
    }
    */
    struct ContextWithOverridenMethods
    {
        public double x;
        public double y;

        public double Cos(double val) => Math.Sin(val) + 1;

        public double Log(double val) => Math.Sin(val) + 2;

        public double Log(double val1, double val2) => Math.Sin(val1) + 3;
    }

    [Fact]
    public void LambdaOverrideMathFunction()
    {
        // Arrange
        ContextWithOverridenMethods context = new() { x = 3.5, y = 2.5 };

        // Not overriden function
        var expressionAbs = new Expression("Abs(x)");
        var lambdaAbs = expressionAbs.ToLambda<ContextWithOverridenMethods, double>(TestContext.Current.CancellationToken);

        // Overriden functions
        var expressionCos = new Expression("Cos(x)");
        var lambdaCos = expressionCos.ToLambda<ContextWithOverridenMethods, double>(TestContext.Current.CancellationToken);

        var expressionLog1 = new Expression("Log(x)");
        var lambdaLog1 = expressionLog1.ToLambda<ContextWithOverridenMethods, double>(TestContext.Current.CancellationToken);

        var expressionLog2 = new Expression("Log(x, y)");
        var lambdaLog2 = expressionLog2.ToLambda<ContextWithOverridenMethods, double>(TestContext.Current.CancellationToken);

        // Act
        var actualAbs = lambdaAbs(context);
        var expectedAbs = Math.Abs(context.x);

        var actualCos = lambdaCos(context);
        var expectedCos = context.Cos(context.x);

        var actualLog1 = lambdaLog1(context);
        var expectedLog1 = context.Log(context.x);

        var actualLog2 = lambdaLog2(context);
        var expectedLog2 = context.Log(context.x, context.y);

        // Assert
        Assert.Equal(expectedAbs, actualAbs);
        Assert.Equal(expectedCos, actualCos);
        Assert.Equal(expectedLog1, actualLog1);
        Assert.Equal(expectedLog2, actualLog2);
    }

    [Theory]
    [InlineData(int.MaxValue, '+', int.MaxValue)]
    [InlineData(int.MinValue, '-', int.MaxValue)]
    [InlineData(int.MaxValue, '*', int.MaxValue)]
    public void ShouldHandleOverflowInt(int a, char op, int b)
    {
        var e = new Expression($"[a] {op} [b]", ExpressionOptions.OverflowProtection, CultureInfo.InvariantCulture);
        e.Parameters["a"] = a;
        e.Parameters["b"] = b;

        var lambda = e.ToLambda<int>(TestContext.Current.CancellationToken);

        Assert.Throws<OverflowException>(() => lambda());
    }

    [Fact]
    public void ShouldAllowPowWithDecimals()
    {
        var e = new Expression("Pow(3.1, 2)", ExpressionOptions.DecimalAsDefault, CultureInfo.InvariantCulture);
        var lambda = e.ToLambda<decimal>(TestContext.Current.CancellationToken);
        Assert.Equal(9.61m, lambda());
    }

    [Fact]
    public void ShouldUseDecimalsWithDecimalAsDefault()
    {
        decimal val = 3.1m;

        // Arrange
        var expressionAbs = new Expression("Abs(x)", ExpressionOptions.DecimalAsDefault);
        expressionAbs.Parameters["x"] = val;
        var lambdaAbs = expressionAbs.ToLambda<decimal>(TestContext.Current.CancellationToken);

        var expressionCeiling = new Expression("Ceiling(x)", ExpressionOptions.DecimalAsDefault);
        expressionCeiling.Parameters["x"] = val;
        var lambdaCeiling = expressionCeiling.ToLambda<decimal>(TestContext.Current.CancellationToken);

        var expressionFloor = new Expression("Floor(x)", ExpressionOptions.DecimalAsDefault);
        expressionFloor.Parameters["x"] = val;
        var lambdaFloor = expressionFloor.ToLambda<decimal>(TestContext.Current.CancellationToken);

        var expressionSign = new Expression("Sign(x)", ExpressionOptions.DecimalAsDefault);
        expressionSign.Parameters["x"] = val;
        var lambdaSign = expressionSign.ToLambda<decimal>(TestContext.Current.CancellationToken);

        var expressionTruncate = new Expression("Truncate(x)", ExpressionOptions.DecimalAsDefault);
        expressionTruncate.Parameters["x"] = val;
        var lambdaTruncate = expressionTruncate.ToLambda<decimal>(TestContext.Current.CancellationToken);

        // Act
        var actualAbs = lambdaAbs();
        var expectedAbs = Math.Abs(val);

        var actualCeiling = lambdaCeiling();
        var expectedCeiling = Math.Ceiling(val);

        var actualFloor = lambdaFloor();
        var expectedFloor = Math.Floor(val);

        var actualSign = lambdaSign();
        var expectedSign = Math.Sign(val);

        var actualTruncate = lambdaTruncate();
        var expectedTruncate = Math.Truncate(val);

        // Assert
        Assert.Equal(expectedAbs, actualAbs);
        Assert.Equal(expectedCeiling, actualCeiling);
        Assert.Equal(expectedFloor, actualFloor);
        Assert.Equal(expectedSign, actualSign);
        Assert.Equal(expectedTruncate, actualTruncate);
    }

    [Theory]
    [InlineData("test", "%est", true, ExpressionOptions.None)]
    [InlineData("TEST", "%est", false, ExpressionOptions.None)]
    [InlineData("TEST", "%est", true, ExpressionOptions.CaseInsensitiveStringComparer)]
    [InlineData("this is a test", "%is a%", true, ExpressionOptions.None)]
    [InlineData("this is a test", "%ITS A%", false, ExpressionOptions.None)]
    public void ShouldHandleLikeOperator(string val, string right, bool expected, ExpressionOptions opts)
    {
        string[] ops = ["like", "not like"];
        // Arrange
        foreach (var op in ops)
        {
            if (op == "not like")
                expected = !expected;

            var expressionLike = new Expression($"x {op} '{right}'", opts);
            expressionLike.Parameters["x"] = val;

            var lambdaAbs = expressionLike.ToLambda<bool>(TestContext.Current.CancellationToken);

            // Act
            var actualAbs = lambdaAbs();
            var expectedAbs = expressionLike.Evaluate(TestContext.Current.CancellationToken);
            Console.WriteLine($"Value: {val}, Pattern: {right}, Result: {actualAbs}");

            // Assert
            Assert.Equal(expected, actualAbs);
            Assert.Equal(expectedAbs, actualAbs);
        }
    }

    [Theory]
    [InlineData("test", "'a','test','z'", true, ExpressionOptions.None)]
    [InlineData("TEST", "'a','test','z'", false, ExpressionOptions.None)]
    [InlineData("TEST", "'a','test','z'", true, ExpressionOptions.CaseInsensitiveStringComparer)]
    [InlineData("this", "'this','is','a','test'", true, ExpressionOptions.None)]
    [InlineData("THIS", "'this','is','a','test'", false, ExpressionOptions.None)]
    [InlineData("THIS", "'this','is','a','test'", true, ExpressionOptions.CaseInsensitiveStringComparer)]
    public void ShouldHandleInOperator_String(string val, string list, bool expected, ExpressionOptions opts)
    {
        string[] ops = ["in", "not in"];

        foreach (var op in ops)
        {
            var exp = expected;
            if (op == "not in") exp = !exp;

            var expression = new Expression($"x {op} ({list})", opts);
            expression.Parameters["x"] = val;

            var lambda = expression.ToLambda<bool>(TestContext.Current.CancellationToken);

            var actual = lambda();
            var expectedEval = (bool?) expression.Evaluate(TestContext.Current.CancellationToken);

            Assert.Equal(exp, actual);
            Assert.Equal(expectedEval, actual);
        }
    }

    [Theory]
    [InlineData("test", new[] { "a", "test", "z" }, true)]
    [InlineData("nope", new[] { "a", "test", "z" }, false)]
    [InlineData("x",    new string[] { },             false)] // empty list
    [InlineData("dup",  new[] { "dup", "dup" },       true)]  // duplicates
    [InlineData("Test", new[] { "a", "test", "z" },   false)] // case sensitivity
    public void ShouldHandleInOperatorWithVariables_String(string x, string[] y, bool expected)
    {
        var expression = new Expression("x in y", ExpressionOptions.None);
        expression.Parameters["x"] = x;
        expression.Parameters["y"] = y; // string[] is fine

        var lambda = expression.ToLambda<bool>(TestContext.Current.CancellationToken);

        var actual = lambda();
        var expectedEval = (bool?)expression.Evaluate(TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
        Assert.Equal(expectedEval, actual);
}

    [Theory]
    [InlineData(3, new[] { 1, 2, 3 }, true)]
    [InlineData(4, new[] { 1, 2, 3 }, false)]
    [InlineData(7, new int[] { },     false)] // empty list
    public void ShouldHandleInOperatorWithVariables_Int(int x, int[] y, bool expected)
    {
        var expression = new Expression("x in y", ExpressionOptions.None);
        expression.Parameters["x"] = x;
        // NCalc is happy with arrays; if needed you can box: y.Cast<object>().ToArray()
        expression.Parameters["y"] = y;

        var lambda = expression.ToLambda<bool>(TestContext.Current.CancellationToken);

        var actual = lambda();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ShouldHandleInOperatorWithVariables_ListInt()
    {
        var expression = new Expression("x in y", ExpressionOptions.None);
        expression.Parameters["x"] = 3;
        // NCalc is happy with arrays; if needed you can box: y.Cast<object>().ToArray()
        expression.Parameters["y"] = new System.Collections.Generic.List<int> { 1, 2, 3 };

        var lambda = expression.ToLambda<bool>(TestContext.Current.CancellationToken);

        var actual = lambda();

        Assert.True(actual);
    }

    [Theory]
    [InlineData(3.0, new[] { 1.0, 2.0, 3.0 }, true)]
    [InlineData(3.0, new[] { 1.1, 2.2, 3.3 }, false)]
    public void ShouldHandleInOperatorWithVariables_Double(double x, double[] y, bool expected)
    {
        var expression = new Expression("x in y", ExpressionOptions.None);
        expression.Parameters["x"] = x;
        expression.Parameters["y"] = y;

        var lambda = expression.ToLambda<bool>(TestContext.Current.CancellationToken);

        var actual = lambda();

        Assert.Equal(expected, actual);
    }

    // NCALC-134
    [Fact]
    public void NullExpressionShouldGiveALambdaThatReturnsTheDefaultValue()
    {
        // Evaluate() returns null for a null expression with AllowNullOrEmptyExpressions; ToLambda() threw a NullReferenceException
        var expression = new Expression((string?)null, ExpressionOptions.AllowNullOrEmptyExpressions);
        Assert.Null(expression.Evaluate(TestContext.Current.CancellationToken));
        Assert.Null(expression.ToLambda<object?>(TestContext.Current.CancellationToken)());
        Assert.Equal(0, expression.ToLambda<int>(TestContext.Current.CancellationToken)());
        Assert.Equal(0, expression.ToLambda<Context, int>(TestContext.Current.CancellationToken)(new Context()));

        // Without the option, the parse error is thrown
        Assert.ThrowsAny<NCalc.Exceptions.NCalcException>(() => new Expression((string?)null).ToLambda<object?>(TestContext.Current.CancellationToken));
    }

    // NCALC-104: a lambda gives the value that Evaluate() gives, of the same type, or the same exception when the lambda runs
    private static void AssertLambdaMatchesEvaluate(string input, ExpressionOptions options = ExpressionOptions.None,
        Dictionary<string, object?>? parameters = null, AdvExpressionOptions advancedOptions = AdvExpressionOptions.None)
    {
        Expression Create()
        {
            var expression = new Expression(input, options | ExpressionOptions.NoCache);
            if (advancedOptions != AdvExpressionOptions.None)
            {
                expression.AdvancedOptions = new AdvancedExpressionOptions();
                expression.AdvancedOptions.Flags |= advancedOptions;
            }
            foreach (var parameter in parameters ?? [])
                expression.Parameters[parameter.Key] = parameter.Value;
            return expression;
        }

        object? expected = null;
        Exception? expectedException = null;
        try
        {
            expected = Create().Evaluate(TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            expectedException = ex;
        }

        if (expectedException is not null)
        {
            Assert.Throws(expectedException.GetType(), () => Create().ToLambda<object?>(TestContext.Current.CancellationToken)());
            return;
        }

        var actual = Create().ToLambda<object?>(TestContext.Current.CancellationToken)();
        Assert.True(Equals(expected, actual) || (expected is IEnumerable<object?> e && actual is IEnumerable<object?> a && e.SequenceEqual(a)),
            $"{input}: Evaluate gives {expected} ({expected?.GetType().Name}), the lambda gives {actual} ({actual?.GetType().Name})");
        Assert.Equal(expected?.GetType(), actual?.GetType());
    }

    public static TheoryData<string, ExpressionOptions> LambdaParityData => new()
    {
        // The divergences listed in the audit
        { "1 << 40", ExpressionOptions.None },
        { "1 << 63", ExpressionOptions.None },
        { "1 << 64", ExpressionOptions.None },
        { "~0", ExpressionOptions.None },
        { "~5", ExpressionOptions.None },
        { "'a' < 'B'", ExpressionOptions.None },
        { "'i' = 'I'", ExpressionOptions.CaseInsensitiveStringComparer },
        { "'a' in 'XAY'", ExpressionOptions.CaseInsensitiveStringComparer },
        { "1 = 1.0", ExpressionOptions.StrictTypeMatching },
        { "not null", ExpressionOptions.AllowNullParameter },
        { "null ? 1 : 2", ExpressionOptions.AllowNullParameter },
        { "false ? 0xFFFFFFFFFFFFFFFF : -1", ExpressionOptions.None },
        { "false ? 0.5 : 9007199254740993", ExpressionOptions.None },
        { "Pow(2, 0.5)", ExpressionOptions.DecimalAsDefault },
        { "Pow(2.5, 1.5)", ExpressionOptions.DecimalAsDefault },
        { "Round(1.005, 2)", ExpressionOptions.RoundAwayFromZero },
        { "Round(1.005, 2)", ExpressionOptions.RoundAwayFromZero | ExpressionOptions.DecimalAsDefault },
        { "Round(2.345, 2)", ExpressionOptions.None },
        { "Max(9007199254740993, 9007199254740992)", ExpressionOptions.None },
        { "Max(Sqrt(-1), 1)", ExpressionOptions.None },
        { "Log2(2147483648)", ExpressionOptions.None },
        { "abs(-2)", ExpressionOptions.IgnoreCaseAtBuiltInFunctions },
        { "1 in (1.0, 2.0)", ExpressionOptions.None },
        { "'1' in (1, 2)", ExpressionOptions.None },
        { "Round(1250, -2)", ExpressionOptions.None },
        { "Round(1.5, 16)", ExpressionOptions.None },
        { "1 and 0", ExpressionOptions.None },
        { "'5' + 1", ExpressionOptions.None },
        { "7 // 2", ExpressionOptions.None },
        { "7 \\ 2", ExpressionOptions.None },
        { "-7.5 \\ 2", ExpressionOptions.None },
        // More operators, functions and options
        { "2147483647 + 1", ExpressionOptions.None },
        { "2147483647 + 1", ExpressionOptions.OverflowProtection },
        { "2147483647 * 2", ExpressionOptions.None },
        { "10 / 4", ExpressionOptions.None },
        { "10 / 5", ExpressionOptions.ReduceDivResultToInteger },
        { "1 / 0", ExpressionOptions.None },
        { "1.5 + 2.25", ExpressionOptions.DecimalAsDefault },
        { "2 ** 0.5", ExpressionOptions.None },
        { "5!", ExpressionOptions.None },
        { "7 % 3", ExpressionOptions.None },
        { "6 & 3 | 8 ^ 1", ExpressionOptions.None },
        { "-8 >> 1", ExpressionOptions.None },
        { "true + 1", ExpressionOptions.AllowBooleanCalculation },
        { "null + 1", ExpressionOptions.AllowNullParameter },
        { "null + 1", ExpressionOptions.AllowNullParameter | ExpressionOptions.TreatNullAsZero },
        { "null = null", ExpressionOptions.AllowNullParameter },
        { "null = 1", ExpressionOptions.AllowNullParameter | ExpressionOptions.CompareNullValues },
        { "null and true", ExpressionOptions.UseTernaryLogic },
        { "false and null", ExpressionOptions.UseTernaryLogic },
        { "null or true", ExpressionOptions.UseTernaryLogic },
        { "true xor null", ExpressionOptions.UseTernaryLogic },
        { "not null", ExpressionOptions.UseTernaryLogic },
        { "false and 1/0 > 0", ExpressionOptions.None },
        { "'abc' like 'A%'", ExpressionOptions.CaseInsensitiveStringComparer },
        { "'abc' not like 'a_c'", ExpressionOptions.None },
        { "2 in (1, 2, 3)", ExpressionOptions.None },
        { "2 not in (1, 3)", ExpressionOptions.None },
        { "if(false, 1)", ExpressionOptions.None },
        { "if(true, 1, 0.0)", ExpressionOptions.None },
        { "IF(true, 1, 2)", ExpressionOptions.IgnoreCaseAtBuiltInFunctions },
        { "ifs(false, 1, true, 2, 3)", ExpressionOptions.None },
        { "ifs(false, 1, false, 2)", ExpressionOptions.None },
        { "in(2, 1, 2.0)", ExpressionOptions.None },
        { "Abs(-2)", ExpressionOptions.None },
        { "Ceiling(7)", ExpressionOptions.None },
        { "Sign(-2.5)", ExpressionOptions.DecimalAsDefault },
        { "Truncate(-2.5)", ExpressionOptions.None },
        { "Min(3, 2.5)", ExpressionOptions.None },
        { "Sqrt(16)", ExpressionOptions.None },
        { "Exp(1)", ExpressionOptions.DecimalAsDefault },
        { "MakeStr(3, 'ab')", ExpressionOptions.None },
        { "MakeList(2, 1)", ExpressionOptions.None },
        { "Abs(1, 2)", ExpressionOptions.None },
        { "(1, 'a', 2.5)", ExpressionOptions.None },
        { "1; 2 + 3", ExpressionOptions.UseStatementSequences },
        { "#2024-01-02# - #2024-01-01#", ExpressionOptions.SupportTimeOperations },
        { "-(2)", ExpressionOptions.None },
    };

    // NCALC-104
    [Theory]
    [MemberData(nameof(LambdaParityData))]
    public void LambdaShouldGiveTheValuesOfEvaluate(string input, ExpressionOptions options)
    {
        AssertLambdaMatchesEvaluate(input, options);
    }

    // NCALC-104
    [Fact]
    public void LambdaShouldGiveTheValuesOfEvaluateWithParametersAndPercents()
    {
        AssertLambdaMatchesEvaluate("x = 1", ExpressionOptions.StrictTypeMatching, new() { ["x"] = 1L });
        AssertLambdaMatchesEvaluate("a + b", parameters: new() { ["a"] = (byte)200, ["b"] = (byte)100 });
        AssertLambdaMatchesEvaluate("a // b", parameters: new() { ["a"] = 7, ["b"] = 2 });
        AssertLambdaMatchesEvaluate("a \\ b", parameters: new() { ["a"] = 7.5m, ["b"] = 2 });
        AssertLambdaMatchesEvaluate("a + 1", parameters: new() { ["a"] = new Expression("2 * 3") });
        AssertLambdaMatchesEvaluate("10% * 10%", advancedOptions: AdvExpressionOptions.CalculatePercent);
        AssertLambdaMatchesEvaluate("200 + 10%", advancedOptions: AdvExpressionOptions.CalculatePercent);
        AssertLambdaMatchesEvaluate("200 / 10%", advancedOptions: AdvExpressionOptions.CalculatePercent);
        AssertLambdaMatchesEvaluate("10% + 200", advancedOptions: AdvExpressionOptions.CalculatePercent);
        AssertLambdaMatchesEvaluate("10%", advancedOptions: AdvExpressionOptions.CalculatePercent);
        AssertLambdaMatchesEvaluate("Round(12.346%, 2)", advancedOptions: AdvExpressionOptions.CalculatePercent);
        AssertLambdaMatchesEvaluate("(1+2i) * (1-2i)", advancedOptions: AdvExpressionOptions.ParseComplexNumbers);
        AssertLambdaMatchesEvaluate("i*i == -1", advancedOptions: AdvExpressionOptions.ParseComplexNumbers);
        AssertLambdaMatchesEvaluate("[1;2] == [1;2]", advancedOptions: AdvExpressionOptions.ParseVectors);
        AssertLambdaMatchesEvaluate("[1;2] & [3;4]", advancedOptions: AdvExpressionOptions.ParseVectors);

        // A built-in function is found ignoring case only with IgnoreCaseAtBuiltInFunctions
        Assert.Throws<NCalc.Exceptions.NCalcFunctionNotFoundException>(() => new Expression("abs(-2)").Evaluate(TestContext.Current.CancellationToken));
        Assert.Throws<MissingMethodException>(() => new Expression("abs(-2)").ToLambda<object?>(TestContext.Current.CancellationToken));
    }

    // NCALC-104
    [Theory]
    [ClassData(typeof(TestData.EvaluationTestData))]
    [ClassData(typeof(TestData.ValuesTestData))]
    public void LambdaShouldGiveTheValuesOfEvaluateForTheTestData(string input, object expected)
    {
        Assert.Equal(expected, new Expression(input).ToLambda<object?>(TestContext.Current.CancellationToken)());
        AssertLambdaMatchesEvaluate(input);
    }

    // NCALC-104
    [Theory]
    [ClassData(typeof(TestData.BuiltInFunctionsTestData))]
    public void LambdaShouldGiveTheValuesOfEvaluateForTheBuiltInFunctions(string input, object expected, double? tolerance)
    {
        _ = expected;
        _ = tolerance;
        AssertLambdaMatchesEvaluate(input);
    }

    // NCALC-104
    [Theory]
    [ClassData(typeof(TestData.NullCheckTestData))]
    public void LambdaShouldGiveTheValuesOfEvaluateForNulls(string input, object? expected)
    {
        Assert.Equal(expected, new Expression(input, ExpressionOptions.AllowNullParameter).ToLambda<object?>(TestContext.Current.CancellationToken)());
        AssertLambdaMatchesEvaluate(input, ExpressionOptions.AllowNullParameter);
    }

    // NCALC-104
    [Theory]
    [ClassData(typeof(TestData.WaterLevelCheckTestData))]
    public void LambdaShouldGiveTheValuesOfEvaluateForTheWaterLevel(string input, bool expected, double waterLevel)
    {
        _ = expected;
        AssertLambdaMatchesEvaluate(input, parameters: new() { ["waterlevel"] = waterLevel });
    }

    // NCALC-104: what a lambda cannot do is refused when it is created instead of being compiled as something else
    [Theory]
    [InlineData("x := 5; x * 2")]
    [InlineData("i += 2; i")]
    [InlineData("i *= 3; i")]
    [InlineData("x += 10%; x")]
    [InlineData("a := 1; while (a < 5) { a += 1 }; a")]
    [InlineData("return 42")]
    [InlineData("return 1; 2")]
    public void LambdaShouldRefuseAssignmentsLoopsAndReturnWithoutAContext(string input)
    {
        var expression = new Expression(input, ExpressionOptions.NoCache | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences
            | ExpressionOptions.UseLoops);
        expression.AdvancedOptions = new AdvancedExpressionOptions();
        expression.AdvancedOptions.Flags |= AdvExpressionOptions.CalculatePercent;
        expression.Parameters["x"] = 1;
        expression.Parameters["i"] = 1;
        Assert.Throws<NotSupportedException>(() => expression.ToLambda<object?>(TestContext.Current.CancellationToken));
    }

    private class AssignmentContext
    {
        public int x { get; set; } = 1;
        public double i { get; set; } = 1;
    }

    // NCALC-104: with a context object, an assignment changes its member and later reads see the new value
    [Fact]
    public void LambdaShouldAssignToTheMembersOfTheContext()
    {
        const ExpressionOptions options = ExpressionOptions.NoCache | ExpressionOptions.UseAssignments | ExpressionOptions.UseStatementSequences;

        var context = new AssignmentContext();
        Assert.Equal(10, new Expression("x := 5; x * 2", options).ToLambda<AssignmentContext, object?>(TestContext.Current.CancellationToken)(context));
        Assert.Equal(5, context.x);

        context = new AssignmentContext();
        Assert.Equal(3.0, new Expression("i += 2; i", options).ToLambda<AssignmentContext, object?>(TestContext.Current.CancellationToken)(context));
        Assert.Equal(3.0, context.i);

        Assert.Throws<NotSupportedException>(() => new Expression("while (x < 5) { x += 1 }", options | ExpressionOptions.UseLoops)
            .ToLambda<AssignmentContext, object?>(TestContext.Current.CancellationToken));
    }

    // NCALC-104
    [Fact]
    public void LambdaWithAContextShouldAcceptNullAndConvertTheResult()
    {
        Assert.Null(new Expression("null", ExpressionOptions.AllowNullParameter).ToLambda<Context, object?>(TestContext.Current.CancellationToken)(new Context()));
        Assert.Equal(8, new Expression("FieldA + 1").ToLambda<Context, int>(TestContext.Current.CancellationToken)(new Context { FieldA = 7 }));
        Assert.Equal(4, new Expression("Test(1 + 1, 2)").ToLambda<Context, int>(TestContext.Current.CancellationToken)(new Context()));
        Assert.Equal(4294967294L, new Expression("2147483647 * 2").ToLambda<long>(TestContext.Current.CancellationToken)());
        Assert.Throws<OverflowException>(() => new Expression("2147483647 * 2", ExpressionOptions.OverflowProtection).ToLambda<int>(TestContext.Current.CancellationToken)());
    }
}
#endif
using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Factories;
using NCalc.Helpers;
using NCalc.Tests.TestData;

using Newtonsoft.Json;

using JsonSerializer = System.Text.Json.JsonSerializer;
using NCalcVector = NCalc.Domain.Vector;

namespace NCalc.Tests;

[Trait("Category", "Serialization")]
public class SerializationTests
{
    [Theory]
    [ClassData(typeof(WaterLevelCheckTestData))]
    public void SerializeAndDeserializeShouldWork(string expression, bool expected, double inputValue)
    {
        var compiled = LogicalExpressionFactory.Create(expression, cancellationToken: TestContext.Current.CancellationToken);
        var serialized = JsonConvert.SerializeObject(compiled, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All // We need this to allow serializing abstract classes
        });

        var deserialized = JsonConvert.DeserializeObject<LogicalExpression>(serialized, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        });

        var exp = new Expression(deserialized, ExpressionOptions.NoCache)
        {
            Parameters =
            {
                { "waterlevel", inputValue }
            }
        };

        object evaluated;
        try
        {
            evaluated = exp.Evaluate(TestContext.Current.CancellationToken);
        }
        catch
        {
            evaluated = false;
        }

        // Assert
        Assert.Equal(expected, evaluated);
    }

#if NET
    [Fact]
    public void SystemTextJsonPolymorphicSerializeAndDeserializeShouldWork()
    {
        var expression = LogicalExpressionFactory.Create("1 == 1", cancellationToken: TestContext.Current.CancellationToken);
        var expressionJson = JsonSerializer.Serialize(expression);
        Assert.True(JsonSerializer.Deserialize<LogicalExpression>(expressionJson) is BinaryExpression);
    }

    [Fact]
    public void DeserializationShouldWork_Issue_552()
    {
        const string expressionString = "waterLevel > 4.0";

        var logicalExpression = LogicalExpressionFactory.Create(expressionString, options: ExpressionOptions.NoCache | ExpressionOptions.UseBigNumbers, cancellationToken: TestContext.Current.CancellationToken); //Created a BinaryExpression object.

        var jsonExpression = JsonSerializer.Serialize(logicalExpression);

        var deserializedLogicalExpression = JsonSerializer.Deserialize<LogicalExpression>(jsonExpression); //The object is still a BinaryExpression.
        Assert.NotNull(deserializedLogicalExpression);

        var expression = new Expression(deserializedLogicalExpression);

        expression.Parameters = new Dictionary<string, object?> { {"waterLevel", 4.0}, };

        var result = expression.Evaluate(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(false, result);
    }
#endif

    [Fact]
    public void Binary_Expression_Serialization_Test()
    {
        Assert.Equal("True and False",
    new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false))
        .ToString());
        Assert.Equal("1 / 2",
            new BinaryExpression(BinaryExpressionType.Div, new ValueExpression(1), new ValueExpression(2)).ToString());
        Assert.Equal("1 = 2",
            new BinaryExpression(BinaryExpressionType.Equal, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 > 2",
            new BinaryExpression(BinaryExpressionType.Greater, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 >= 2",
            new BinaryExpression(BinaryExpressionType.GreaterOrEqual, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 < 2",
            new BinaryExpression(BinaryExpressionType.Less, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 <= 2",
            new BinaryExpression(BinaryExpressionType.LessOrEqual, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 - 2",
            new BinaryExpression(BinaryExpressionType.Minus, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 % 2",
            new BinaryExpression(BinaryExpressionType.Modulo, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("1 != 2",
            new BinaryExpression(BinaryExpressionType.NotEqual, new ValueExpression(1), new ValueExpression(2))
                .ToString());
        Assert.Equal("True or False",
            new BinaryExpression(BinaryExpressionType.Or, new ValueExpression(true), new ValueExpression(false))
                .ToString());
        Assert.Equal("1 + 2",
            new BinaryExpression(BinaryExpressionType.Plus, new ValueExpression(1), new ValueExpression(2)).ToString());
        Assert.Equal("1 * 2",
            new BinaryExpression(BinaryExpressionType.Times, new ValueExpression(1), new ValueExpression(2))
                .ToString());
    }

    [Fact]
    public void Unary_Expression_Serialization_Test()
    {
        Assert.Equal("-(True and False)",
            new UnaryExpression(UnaryExpressionType.Negate,
                    new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true),
                        new ValueExpression(false)))
                .ToString());
        Assert.Equal("!(True and False)",
            new UnaryExpression(UnaryExpressionType.Not,
                    new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true),
                        new ValueExpression(false)))
                .ToString());
    }

    [Fact]
    public void Function_Serialization_Test()
    {
        Assert.Equal("test(True and False; -(True and False))", new FunctionCall(new Identifier("test"), [
            new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false)),
            new UnaryExpression(UnaryExpressionType.Negate,
                new BinaryExpression(BinaryExpressionType.And, new ValueExpression(true), new ValueExpression(false)))
        ]).ToString());

        Assert.Equal("Sum(1 + 2)", new FunctionCall(new Identifier("Sum"), [
            new BinaryExpression(BinaryExpressionType.Plus, new ValueExpression(1), new ValueExpression(2))
        ]).ToString());
    }

    [Fact]
    public void Value_Serialization_Test()
    {
        Assert.Equal("True", new ValueExpression(true).ToString());
        Assert.Equal("False", new ValueExpression(false).ToString());
        Assert.Equal("1", new ValueExpression(1).ToString());
        Assert.Equal("1" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "234", new ValueExpression(1.234).ToString());
        Assert.Equal("'hello'", new ValueExpression("hello").ToString());
        Assert.Equal("'c'", new ValueExpression('c').ToString());
        Assert.Equal("#" + new DateTime(2009, 1, 1) + "#", new ValueExpression(new DateTime(2009, 1, 1)).ToString());
    }

    [Fact]
    public void ArraySerializationTest()
    {
        var trueArrayExpression = new LogicalExpressionList([new ValueExpression(true)]);
        var helloWorldArrayExpression = new LogicalExpressionList([new ValueExpression("Hello"), new ValueExpression("World")]);
        Assert.Equal("(True)", trueArrayExpression.ToString());
        Assert.Equal("('Hello'; 'World')", helloWorldArrayExpression.ToString());
        Assert.Equal("()", new LogicalExpressionList([]).ToString());
        Assert.Equal("((True); ('Hello'; 'World'))", new LogicalExpressionList([trueArrayExpression, helloWorldArrayExpression]).ToString());
    }

#if NET
    [Fact]
    public void ShouldSerializeImaginaryNumberExpression()
    {
        // "2i" parses to an ImaginaryNumberExpression wrapping ValueExpression(2)
        var advancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseComplexNumbers);
        var parsed = LogicalExpressionFactory.Create("2i", advancedOptions: advancedOptions,
            cancellationToken: TestContext.Current.CancellationToken);

        var json = JsonSerializer.Serialize(parsed);
        var restored = JsonSerializer.Deserialize<LogicalExpression>(json);

        Assert.NotNull(restored);
        Assert.IsType<ImaginaryNumberExpression>(restored);
    }

    [Fact]
    public void ShouldSerializeVectorExpression()
    {
        // "[1; 2; 3]" parses to a VectorExpression
        var advancedOptions = new AdvancedExpressionOptions(AdvExpressionOptions.ParseVectors);
        var parsed = LogicalExpressionFactory.Create("[1; 2; 3]", advancedOptions: advancedOptions,
            cancellationToken: TestContext.Current.CancellationToken);

        var json = JsonSerializer.Serialize(parsed);
        var restored = JsonSerializer.Deserialize<LogicalExpression>(json);

        Assert.NotNull(restored);
        Assert.IsType<VectorExpression>(restored);
        var restoredVec = (VectorExpression)restored;
        Assert.Equal(3, restoredVec.Expressions.Count);
    }

    [Fact]
    public void ShouldSerializeComplexNumberValue()
    {
        // A ValueExpression whose Value is a ComplexNumber should round-trip through JSON
        var complexNumber = new ComplexNumber(new BigDecimal(3), new BigDecimal(4));
        var ve = new ValueExpression { Value = complexNumber };

        var json = JsonSerializer.Serialize<LogicalExpression>(ve);
        var restored = (ValueExpression)JsonSerializer.Deserialize<LogicalExpression>(json)!;

        Assert.NotNull(restored.Value);
        Assert.IsType<ComplexNumber>(restored.Value);
        var cn = (ComplexNumber)restored.Value;
        Assert.Equal(complexNumber.Real, cn.Real);
        Assert.Equal(complexNumber.Imaginary, cn.Imaginary);
    }

    [Fact]
    public void ShouldSerializeVectorValue()
    {
        // A ValueExpression whose Value is a Vector should round-trip through JSON
        var vector = new NCalcVector(new BigDecimal[] { 1, 2, 3 });
        var ve = new ValueExpression { Value = vector };

        var json = JsonSerializer.Serialize<LogicalExpression>(ve);
        var restored = (ValueExpression)JsonSerializer.Deserialize<LogicalExpression>(json)!;

        Assert.NotNull(restored.Value);
        Assert.IsType<NCalcVector>(restored.Value);
        var v = (NCalcVector)restored.Value;
        Assert.Equal(3, v.Dimensions);
        Assert.Equal((BigDecimal)1, v.Components[0]);
        Assert.Equal((BigDecimal)2, v.Components[1]);
        Assert.Equal((BigDecimal)3, v.Components[2]);
    }
#endif

    [Fact]
    public void ShouldSerializeDouble()
    {
        var ve = new ValueExpression { Value = 0.00001d };

        var json = JsonSerializer.Serialize<LogicalExpression>(ve);
        var restored = (ValueExpression)JsonSerializer.Deserialize<LogicalExpression>(json)!;

        Assert.NotNull(restored.Value);
        Assert.Equal(0.00001d, restored.Value);

        var expr = new Expression("0.00001", ExpressionOptions.DecimalAsDefault);
        string s = expr.GetLogicalExpression().ToString();
        Assert.Equal("0" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "00001", s);

        expr = new Expression("0.00001", ExpressionOptions.None);
        s = expr.GetLogicalExpression().ToString();
        Assert.Equal("0" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + "00001", s);
    }
}
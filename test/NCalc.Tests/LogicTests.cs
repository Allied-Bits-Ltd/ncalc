using System;
using System.Collections.Generic;
using System.Text;

namespace NCalc.Tests
{
    [Trait("Category", "Logic")]
    public class LogicTests : TestBase
    {
        [Theory]
        [InlineData("not true", false)]
        [InlineData("not null", null)]
        [InlineData("not false", true)]
        public void ShouldEvaluateNot(string expr, bool? expected)
        {
            object? result = new Expression(expr, ExpressionOptions.UseTernaryLogic).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = new Expression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("true and true", true)]
        [InlineData("true and null", null)]
        [InlineData("true and false", false)]
        [InlineData("null and true", null)]
        [InlineData("null and null", null)]
        [InlineData("null and false", false)]
        [InlineData("false and true", false)]
        [InlineData("false and null", false)]
        [InlineData("false and false", false)]
        public void ShouldEvaluateAnd(string expr, bool? expected)
        {
            object? result = new Expression(expr, ExpressionOptions.UseTernaryLogic).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = new Expression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?) result);
        }

        [Theory]
        [InlineData("true or false", true)]
        [InlineData("true or true", true)]
        [InlineData("true or null", true)]
        [InlineData("null or false", null)]
        [InlineData("null or true", true)]
        [InlineData("null or null", null)]
        [InlineData("false or true", true)]
        [InlineData("false or null", null)]
        [InlineData("false or false", false)]
        public void ShouldEvaluateOr(string expr, bool? expected)
        {
            object? result = new Expression(expr, ExpressionOptions.UseTernaryLogic).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = new Expression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("true xor true", false)]
        [InlineData("true xor null", null)]
        [InlineData("true xor false", true)]
        [InlineData("null xor true", null)]
        [InlineData("null xor null", null)]
        [InlineData("null xor false", null)]
        [InlineData("false xor true", true)]
        [InlineData("false xor null", null)]
        [InlineData("false xor false", false)]
        public void ShouldEvaluateXor(string expr, bool? expected)
        {
            object? result = new Expression(expr, ExpressionOptions.UseTernaryLogic).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = new Expression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("not true", false)]
        [InlineData("not null", null)]
        [InlineData("not false", true)]
        public async Task ShouldEvaluateNotAsync(string expr, bool? expected)
        {
            object? result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("true and true", true)]
        [InlineData("true and null", null)]
        [InlineData("true and false", false)]
        [InlineData("null and true", null)]
        [InlineData("null and null", null)]
        [InlineData("null and false", false)]
        [InlineData("false and true", false)]
        [InlineData("false and null", false)]
        [InlineData("false and false", false)]
        public async Task ShouldEvaluateAndAsync(string expr, bool? expected)
        {
            object? result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?) result);
        }

        [Theory]
        [InlineData("true or false", true)]
        [InlineData("true or true", true)]
        [InlineData("true or null", true)]
        [InlineData("null or false", null)]
        [InlineData("null or true", true)]
        [InlineData("null or null", null)]
        [InlineData("false or true", true)]
        [InlineData("false or null", null)]
        [InlineData("false or false", false)]
        public async Task ShouldEvaluateOrAsync(string expr, bool? expected)
        {
            object? result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }

        [Theory]
        [InlineData("true xor true", false)]
        [InlineData("true xor null", null)]
        [InlineData("true xor false", true)]
        [InlineData("null xor true", null)]
        [InlineData("null xor null", null)]
        [InlineData("null xor false", null)]
        [InlineData("false xor true", true)]
        [InlineData("false xor null", null)]
        [InlineData("false xor false", false)]
        public async Task ShouldEvaluateXorAsync(string expr, bool? expected)
        {
            object? result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);

            result = await new AsyncExpression(expr, ExpressionOptions.UseTernaryLogic | ExpressionOptions.UseNonRecursiveEvaluator).EvaluateAsync(TestContext.Current.CancellationToken);
            Assert.Equal(expected, (bool?)result);
        }
    }
}

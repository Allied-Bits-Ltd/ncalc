using System;
using System.Collections.Generic;
using System.Text;

namespace NCalc.Tests.AOT
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var tests = new AdvFeatureTests();
            tests.SerializeFactorialExpressionsTest();
            tests.SerializePercentExpressionsTest();
            tests.ShouldAcceptCurrencyCulture("500.50", 0, (decimal) 500.50);
            tests.ShouldAddSubtractDateAndTime("#2025/06/05# + #08:00:00#", new int[] { 2025, 6, 5, 8, 0, 0 });

            tests.ShouldCalculateBigIntegers("1 + 110680464442257309690 + 1", "110680464442257309692");
            tests.ShouldCalculateBigIntegers("1 - 110680464442257309690", "-110680464442257309689");

            tests.ShouldCalculateIntegerDiv("8.5 \\ 2.5", (long)4);
            tests.ShouldCalculateIntegerDiv("8.5 div 2.5", (long)4);
            tests.ShouldCalculateIntegerDiv("8.5 // 2.5", (long)3);

            tests.ShouldCalculatePercentAsNumber("20*5%", 1);
            tests.ShouldCalculatePercentAsNumber("20/5%", 400);
            tests.ShouldCalculatePercentAsNumber("20/2.5%", 800);
            tests.ShouldCalculatePercentAsNumber("100+5%", 105);
            tests.ShouldCalculatePercentAsNumber("100+(3+2)%", 105);

            tests.ShouldCalculatePercentAsPercent("5%+2%", "7%");
            tests.ShouldCalculatePercentAsPercent("(2/5)%", "0.4%");
            tests.ShouldCalculatePercentAsPercent("3.5% + 2.5%", "6%");
            tests.ShouldCalculatePercentAsPercent("5%-2%", "3%");
            tests.ShouldCalculatePercentAsPercent("5%*2", "10%");
            tests.ShouldCalculatePercentAsPercent("10%/2", "5%");
            tests.ShouldCalculatePercentAsPercent("10%/2 + 3%*3", "14%");
            tests.ShouldCalculatePercentAsPercent("%(25,50)", "50%");
            tests.ShouldCalculatePercentAsPercent("PercentDiff(80,60)", "-25%");
            tests.ShouldCalculatePercentAsPercent("PercentDiff(50,75)", "50%");

            // NCALC-316: time operators that NCalc computes with dynamic operators must give the same results in NCalc.AOT
            var mathTests = new MathsTests();
            mathTests.TimeOperatorsShouldNotDependOnDynamicOperators().GetAwaiter().GetResult();
            mathTests.BitwiseOperatorsWithABigIntegerShouldFollowTheOperandRules().GetAwaiter().GetResult();
            mathTests.PercentOfALargeDecimalShouldNotOverflowInTheIntermediateProduct(ExpressionOptions.None).GetAwaiter().GetResult();
            mathTests.PercentWithBigNumbersShouldKeepTheExactValue().GetAwaiter().GetResult();
            mathTests.BigNumberOperationsWithNaNOrInfinityShouldBeComputedInDouble().GetAwaiter().GetResult();
            mathTests.QuotientBelowTheResolutionOfTheDividendTypeShouldNotBecomeZero(ExpressionOptions.None).GetAwaiter().GetResult();
            mathTests.NegationOfALargeULongShouldEqualItsSubtractionFromZero().GetAwaiter().GetResult();
            mathTests.UnsignedProductsShouldBeWidenedWithOverflowProtection().GetAwaiter().GetResult();

            // NCALC-414: DateTimeOffset and TimeOnly arithmetic in NCalc.AOT, and an out-of-range result as NCalcEvaluationException
            var dto = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
            object? EvaluateTime(string text)
            {
                var expression = new Expression(text, ExpressionOptions.NoCache);
                expression.Parameters["dto"] = dto;
                expression.Parameters["dto2"] = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
                expression.Parameters["t"] = TimeSpan.FromHours(1);
                expression.Parameters["ton"] = new TimeOnly(23, 30);
                expression.Parameters["ton2"] = new TimeOnly(1, 0);
                expression.Parameters["dtomax"] = DateTimeOffset.MaxValue;
                return expression.Evaluate();
            }
            Assert.Equal(dto.AddHours(1), EvaluateTime("dto + t"));
            Assert.Equal(dto.AddHours(-1), EvaluateTime("dto - t"));
            Assert.Equal(TimeSpan.FromHours(-2), EvaluateTime("dto - dto2"));
            Assert.Equal(new TimeSpan(22, 30, 0), EvaluateTime("ton - ton2"));
            Assert.Throws<NCalc.Exceptions.NCalcEvaluationException>(() => EvaluateTime("dtomax + t"));
            Console.WriteLine("NCalc.Tests.AOT: all checks passed");
        }
    }
}

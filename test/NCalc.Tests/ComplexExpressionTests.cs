using System;
using System.Collections.Generic;
using System.Text;

namespace NCalc.Tests
{
    [Trait("Category", "ComplexExpressions")]
    public class ComplexExpressionTests : TestBase
    {
        [Fact]
        public void ShouldEvaluateSubRanges0()
        {
            var expr = new Expression("phrase := 'The quick brown fox jumps'; first  := phrase[..3]",
            ExpressionOptions.UseBigNumbers |
            ExpressionOptions.UseAssignments |
            ExpressionOptions.UseStatementSequences |
            ExpressionOptions.NoCache /*|
            ExpressionOptions.OverflowProtection |
            ExpressionOptions.IgnoreCaseAtBuiltInFunctions |
            ExpressionOptions.AllowCharValues |
            ExpressionOptions.NoStringTypeCoercion |
            ExpressionOptions.LowerCaseIdentifierLookup |
            ExpressionOptions.SupportTimeOperations |
            ExpressionOptions.UseUnicodeCharsForOperations |
            ExpressionOptions.ReduceDivResultToInteger |
            ExpressionOptions.UseIfStatement*/);

            expr.AdvancedOptions = new AdvancedExpressionOptions();
            expr.AdvancedOptions.Flags = AdvExpressionOptions.ParseHumanePeriods;

            object result = expr.Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal("The", result);
        }

        [Fact]
        public void ShouldEvaluateSubRanges1()
        {
            var expr = new Expression("""
// First and last word via range slices
phrase := 'The quick brown fox jumps';
first  := phrase[..3];
last   := phrase[^5..];
first + ' ... ' + last
""",
            ExpressionOptions.NoCache |
            ExpressionOptions.OverflowProtection |
            ExpressionOptions.IgnoreCaseAtBuiltInFunctions |
            ExpressionOptions.AllowCharValues |
            ExpressionOptions.NoStringTypeCoercion |
            ExpressionOptions.LowerCaseIdentifierLookup |
            ExpressionOptions.SupportTimeOperations |
            ExpressionOptions.UseUnicodeCharsForOperations |
            ExpressionOptions.UseAssignments |
            ExpressionOptions.UseStatementSequences |
            ExpressionOptions.ReduceDivResultToInteger |
            ExpressionOptions.UseBigNumbers |
            ExpressionOptions.AllowNullParameter |
            ExpressionOptions.CompareNullValues |
            ExpressionOptions.SupportCStyleComments |
            ExpressionOptions.UseIfStatement |
            ExpressionOptions.UseLoops);

            expr.AdvancedOptions = new AdvancedExpressionOptions();
            expr.AdvancedOptions.Flags = AdvExpressionOptions.ParseHumanePeriods;

            object result = expr.Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal("The ... jumps", result);
        }
    }
}

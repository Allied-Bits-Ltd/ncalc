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
            ExpressionOptions.NoCache |
            ExpressionOptions.OverflowProtection |
            ExpressionOptions.IgnoreCaseAtBuiltInFunctions |
            ExpressionOptions.AllowCharValues |
            ExpressionOptions.NoStringTypeCoercion |
            ExpressionOptions.LowerCaseIdentifierLookup |
            ExpressionOptions.SupportTimeOperations |
            ExpressionOptions.UseUnicodeCharsForOperations |
            ExpressionOptions.ReduceDivResultToInteger |
            ExpressionOptions.UseIfStatement);

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

        [Fact]
        public void ShouldEvaluateLoop1()
        {
            var expr = new Expression("""
// Early-exit list search
data := (18; 42; 7; 99; 3; 55; 21);
i := 0;
while (i < Count(data)) {
    if (data[i] > 50) { return data[i]; };
    i += 1;
};
null
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

            expr.EvaluateFunction += (name, args) =>
            {
                if (name == "Count")
                {
                    if (args.Parameters.Length != 1)
                        throw new ArgumentException("Count() takes exactly one argument");
                    var param = args.Parameters[0].Evaluate(TestContext.Current.CancellationToken);
                    if (param is IList<object> list)
                    {
                        args.Result = list.Count;
                    }
                    else
                    {
                        throw new ArgumentException("Count() argument must be a list");
                    }
                }
            };

            object? data = null;
            object? i = null;

            expr.EvaluateParameter += (name, args) =>
            {
                if (name == "data")
                {
                    args.Result = data;
                }
                else
                    if (name == "i")
                    {
                        args.Result = i;
                    }
            };

            expr.UpdateParameter += (name, args) =>
            {
                if (name == "data")
                {
                    data = args.Value;
                }
                else
                    if (name == "i")
                    {
                        i = args.Value;
                    }
            };

            object result = expr.Evaluate(TestContext.Current.CancellationToken);
            Assert.Equal(99, result);
        }
    }
}

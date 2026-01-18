using NCalc;
using NCalc.Helpers;

public class TestBase
{
    public const ExpressionOptions BaseExpressionOptions =
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
            ExpressionOptions.UseLoops
            ;
    public TestBase()
    {
    }

    internal void CheckResult(object expected, object result, MathHelperOptions options = default)
    {
        if (MathHelper.Compare(expected, result, options, options) != 0)
        {
            Assert.Fail($"Comparison failed: {expected} of type '{expected.GetType()}' expected, {result} of type '{result.GetType()}' obtained.");
        }
    }
}

using NCalc.Domain;

namespace NCalc.Factories;

public interface ILogicalExpressionFactory
{
    public LogicalExpression Create(string expression, ExpressionContextBase? expressionContext = null, ExpressionOptions options = ExpressionOptions.None);

    public LogicalExpression Create(string expression, ExpressionContextBase? expressionContext, CultureInfo cultureInfo, ExpressionOptions options = ExpressionOptions.None, AdvancedExpressionOptions? extendedOptions = null, CancellationToken cancellationToken = default);
}
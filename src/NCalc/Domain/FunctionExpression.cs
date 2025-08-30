using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain
{
    public class FunctionParameter(string name, bool optional, LogicalExpression? defaultValue)
    {
        public string Name { get; internal set; } = name;

        public bool IsOptional { get; internal set; } = optional;

        public LogicalExpression? DefaultValue { get; internal set; } = defaultValue;
    }

    public class Function(string name, LogicalExpression body)
    {
        public string Name { get; internal set; } = name;

        public IList<FunctionParameter> Parameters { get; } = new List<FunctionParameter>();

        public LogicalExpression Body { get; } = body;

        public string? Description { get; internal set; } = string.Empty;

        internal int MandatoryParamCount { get; set; }
    }

    public sealed class FunctionExpression(Function function) : LogicalExpression
    {
        public Function Function { get; } = function;

        public override T Accept<T>(ILogicalExpressionVisitor<T> visitor, CancellationToken cancellationToken = default)
        {
            return visitor.Visit(this, cancellationToken);
        }

        internal override T AcceptNoRecurse<T>(ILogicalExpressionNoRecurseVisitor<T> visitor, ExpressionTask<T> task, CancellationToken cancellationToken = default)
        {
            return visitor.Visit(this, task, cancellationToken);
        }
    }
}

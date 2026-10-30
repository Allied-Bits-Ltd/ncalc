#if NET
using System.Text.Json.Serialization;
#endif

using NCalc.Helpers;
using NCalc.Visitors;

namespace NCalc.Domain
{
    public class FunctionParameter(string name, bool optional, LogicalExpression? defaultValue)
    {
#if NET
        // Used by the JSON deserializer, which then sets the properties
        [JsonConstructor]
        private FunctionParameter() : this(string.Empty, false, null)
        {
        }

        [JsonInclude]
#endif
        public string Name { get; internal set; } = name;

#if NET
        [JsonInclude]
#endif
        public bool IsOptional { get; internal set; } = optional;

#if NET
        [JsonInclude]
#endif
        public LogicalExpression? DefaultValue { get; internal set; } = defaultValue;
    }

    public class Function(string name, LogicalExpression body)
    {
#if NET
        // Used by the JSON deserializer, which then sets the properties
        [JsonConstructor]
        private Function() : this(string.Empty, null!)
        {
        }

        [JsonInclude]
#endif
        public string Name { get; internal set; } = name;

#if NET
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
#endif
        public IList<FunctionParameter> Parameters { get; } = new List<FunctionParameter>();

#if NET
        [JsonInclude]
#endif
        public LogicalExpression Body { get; private set; } = body;

#if NET
        [JsonInclude]
#endif
        public string? Description { get; internal set; } = string.Empty;

#if NET
        [JsonInclude]
#endif
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

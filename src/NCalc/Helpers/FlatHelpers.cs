using NCalc.Domain;

namespace NCalc.Helpers
{
    public class ExpressionState<T>
    {
        public LogicalExpression Expression { get; }
        public object? Value { get; private set; } = default;
        public bool ValueSet { get; private set; } = false;

        public ExpressionState(LogicalExpression expression)
        {
            Expression = expression;
        }

        public object? SetValue(object? value)
        {
            Value = value;
            ValueSet = true;
            return value;
        }
    }

    public class ExpressionTask<T>
    {
        public ExpressionState<T> State;
        public List<ExpressionState<T>> ChildStates = [];

        public ExpressionTask<T>? ParentTask;

        public ExpressionTask(ExpressionTask<T>? parentTask, ExpressionState<T> state)
        {
            ParentTask = parentTask;
            State = state;
        }
    }
}

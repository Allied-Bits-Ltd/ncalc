using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Parser;

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

        /// <summary>
        /// Gives an evaluation error that has no location the location of the nearest binary expression that contains this task,
        /// as the recursive evaluators do (they set it when the error passes through the binary expression).
        /// Always returns <see langword="false"/>, so that it is used as an exception filter: a handler that rethrows at every
        /// level of nested evaluations would itself exhaust the stack.
        /// </summary>
        internal bool SetMissingErrorLocation(NCalcEvaluationException exception)
        {
            if (exception.Location != ExpressionLocation.Empty)
                return false;

            for (var task = this; task is not null; task = task.ParentTask)
            {
                if (task.State.Expression is BinaryExpression binary)
                {
                    exception.Location = binary.Location;
                    break;
                }
            }
            return false;
        }
    }
}

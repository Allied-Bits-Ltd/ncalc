using NCalc.Parser;

namespace NCalc.Exceptions
{
    internal class NCalcFlowControl : NCalcEvaluationException
    {
        const string DefaultMessage = "The 'break' and 'continue' keywords may be used only inside the loop body";

        internal enum FlowControlType
        {
            Break,
            Continue,
            Return
        }

        internal FlowControlType Type { get; }

        internal object? ReturnValue { get; }

        public NCalcFlowControl(FlowControlType type) : base(DefaultMessage)
        {
            Type = type;
        }

        public NCalcFlowControl(FlowControlType type, ExpressionLocation location) : base(DefaultMessage, location)
        {
            Type = type;
        }

        public NCalcFlowControl(object? returnValue, ExpressionLocation location) : base(string.Empty, location)
        {
            Type = FlowControlType.Return;
            ReturnValue = returnValue;
        }
    }
}

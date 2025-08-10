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
        }

        internal FlowControlType Type { get; }

        public NCalcFlowControl(FlowControlType type) : base(DefaultMessage)
        {
            Type = type;
        }

        public NCalcFlowControl(FlowControlType type, ExpressionLocation location) : base(DefaultMessage, location)
        {
            Type = type;
        }
    }
}

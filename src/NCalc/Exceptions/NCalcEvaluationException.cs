using NCalc.Parser;

namespace NCalc.Exceptions;

public class NCalcEvaluationException : NCalcException
{
    public ExpressionLocation Location { get; internal set;  }

    public NCalcEvaluationException(string message) : base(message)
    {
        Location = ExpressionLocation.Empty;
    }

    public NCalcEvaluationException(string message, ExpressionLocation location) : base(message)
    {
        Location = location;
    }
}

using NCalc.Parser;

namespace NCalc.Exceptions;

public class NCalcEvaluationException : NCalcException
{
    protected ExpressionLocation _location;

    public ExpressionLocation Location => _location;

    public NCalcEvaluationException(string message) : base(message)
    {
        _location = ExpressionLocation.Empty;
    }

    public NCalcEvaluationException(string message, ExpressionLocation location) : base(message)
    {
        _location = location;
    }
}

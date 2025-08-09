using NCalc.Parser;

namespace NCalc.Exceptions;

public sealed class NCalcParameterIndexException: NCalcEvaluationException
{
    public const string MessageCantIndexNull = "Cannot access an element of parameter '{0}' because the parameter value is null";

    public string ParameterName { get; }

    public NCalcParameterIndexException(string parameterName, string message) : base(message)
    {
        ParameterName = parameterName;
    }

    public NCalcParameterIndexException(string parameterName, string message, ExpressionLocation location) : base(message, location)
    {
        ParameterName = parameterName;
    }

    public NCalcParameterIndexException(string message) : base(message)
    {
        ParameterName = string.Empty;
    }

    public NCalcParameterIndexException(string message, ExpressionLocation location) : base(message, location)
    {
        ParameterName = string.Empty;
    }
}
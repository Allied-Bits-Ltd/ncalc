using NCalc.Parser;

namespace NCalc.Exceptions;

public sealed class NCalcParameterNotDefinedException : NCalcEvaluationException
{
    private const string DefaultMessage = "Parameter {0} is not defined.";

    public string ParameterName { get; }

    public NCalcParameterNotDefinedException(string parameterName) : base(string.Format(DefaultMessage, parameterName))
    {
        ParameterName = parameterName;
    }

    public NCalcParameterNotDefinedException(string parameterName, ExpressionLocation location) : base(string.Format(DefaultMessage, parameterName), location)
    {
        ParameterName = parameterName;
    }
}
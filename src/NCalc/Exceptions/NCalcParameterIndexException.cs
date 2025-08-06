namespace NCalc.Exceptions;

public sealed class NCalcParameterIndexException: NCalcEvaluationException
{
    public const string MessageCantIndexNull = "Cannot access an element of parameter '{0}' because the parameter value is null";

    public string ParameterName { get; }

    public NCalcParameterIndexException(string parameterName, string message) : base(message)
    {
        ParameterName = parameterName;
    }
}
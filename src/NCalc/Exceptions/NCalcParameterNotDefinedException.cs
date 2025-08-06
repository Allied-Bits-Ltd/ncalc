namespace NCalc.Exceptions;

public sealed class NCalcParameterNotDefinedException(string parameterName)
    : NCalcEvaluationException($"Parameter {parameterName} is not defined.")
{
    public string ParameterName { get; } = parameterName;
}
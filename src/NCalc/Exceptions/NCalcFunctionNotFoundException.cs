using NCalc.Parser;

namespace NCalc.Exceptions;

public sealed class NCalcFunctionNotFoundException : NCalcEvaluationException
{
    private const string DefaultMessage = "Function '{0}' not found.";

    public string FunctionName { get; }

    public NCalcFunctionNotFoundException(string functionName) : base(string.Format(DefaultMessage, functionName))
    {
        FunctionName = functionName;
    }

    public NCalcFunctionNotFoundException(string functionName, ExpressionLocation location) : base(string.Format(DefaultMessage, functionName), location)
    {
        FunctionName = functionName;
    }
}
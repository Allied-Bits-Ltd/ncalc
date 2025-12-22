using NCalc.Parser;

namespace NCalc.Exceptions;

public class NCalcConversionException : NCalcEvaluationException
{
    public string SourceValue { get; }
    public Type SourceType { get; }
    public Type TargetType { get; }

    public NCalcConversionException(string message, string sourceValue, Type sourceType, Type targetType) : base(message)
    {
        SourceValue = sourceValue;
        SourceType = sourceType;
        TargetType = targetType;
    }
}
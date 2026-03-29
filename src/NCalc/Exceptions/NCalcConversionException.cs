using Microsoft.Extensions.Logging.Abstractions;

using NCalc.Parser;

namespace NCalc.Exceptions;

public class NCalcConversionException : NCalcEvaluationException
{
    public string? SourceValue { get; }
    public Type? SourceType { get; }
    public Type TargetType { get; }

    public NCalcConversionException(string message, string sourceValue, Type sourceType, Type targetType)
        : base(message)
    {
        SourceValue = sourceValue;
        SourceType = sourceType;
        TargetType = targetType;
    }

    public NCalcConversionException(string message, string sourceValue, Type sourceType, Type targetType, Exception innerException)
        : base(message, innerException)
    {
        SourceValue = sourceValue;
        SourceType = sourceType;
        TargetType = targetType;
    }

    public NCalcConversionException(string message, string sourceValue, Type sourceType, Type targetType, ExpressionLocation location, Exception innerException)
        : base(message, location, innerException)
    {
        SourceValue = sourceValue;
        SourceType = sourceType;
        TargetType = targetType;
    }

    public NCalcConversionException(string message, Type targetType, Exception innerException)
        : base(message, innerException)
    {
        SourceValue = null;
        SourceType = null;
        TargetType = targetType;
    }

    public NCalcConversionException(string message, Type targetType)
        : base(message)
    {
        SourceValue = null;
        SourceType = null;
        TargetType = targetType;
    }

    public NCalcConversionException(string message, Type targetType, ExpressionLocation location, Exception innerException)
        : base(message, location, innerException)
    {
        SourceValue = null;
        SourceType = null;
        TargetType = targetType;
    }
}
using System.Runtime.CompilerServices;

namespace NCalc.Helpers;

public readonly struct MathHelperOptions(CultureInfo cultureInfo, ExpressionOptions options)
{
    public CultureInfo CultureInfo { get; } = cultureInfo;

    public ExpressionOptions AllOptions => options;

    public static MathHelperOptions Empty = new();

    public bool AvoidDynamicFunctions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if AOT_COMPILATION
        get => true;
#else
        get => options.HasFlag(ExpressionOptions.AvoidDynamicFunctions);
#endif
    }

    public bool AllowBooleanCalculation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => options.HasFlag(ExpressionOptions.AllowBooleanCalculation);
    }

    public bool DecimalAsDefault
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => options.HasFlag(ExpressionOptions.DecimalAsDefault);
    }

    public bool UseSystemMathRound
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => options.HasFlag(ExpressionOptions.UseSystemMathRound);
    }

    public bool OverflowProtection
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => options.HasFlag(ExpressionOptions.OverflowProtection);
    }

    public bool AllowCharValues
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => options.HasFlag(ExpressionOptions.AllowCharValues);
    }

    public bool ReduceDivResultToInteger
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => options.HasFlag(ExpressionOptions.ReduceDivResultToInteger);
    }

    public bool SupportTimeOperations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => options.HasFlag(ExpressionOptions.SupportTimeOperations);
    }

    public bool UseBigNumbers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => options.HasFlag(ExpressionOptions.UseBigNumbers);
    }

    public static implicit operator MathHelperOptions(CultureInfo cultureInfo)
    {
        return new MathHelperOptions(cultureInfo, ExpressionOptions.None);
    }

    public static implicit operator ComparisonOptions(MathHelperOptions options)
    {
        return new ComparisonOptions(options.CultureInfo, options.AllOptions);
    }
}
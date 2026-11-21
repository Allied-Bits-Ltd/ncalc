using System.Diagnostics.CodeAnalysis;
#if NET
using System.Runtime.CompilerServices;
#endif

namespace NCalc.Helpers;

/// <summary>
/// Whether NCalc applies operators through the C# runtime binder (<c>dynamic</c>) and finds user-defined operators by reflection.
/// Neither works under Native AOT, where dynamic code is not supported, nor safely in a trimmed application, where the targets of
/// the package set the <see cref="SwitchName"/> switch to false so that trimming removes that code. NCalc then computes as with
/// <see cref="ExpressionOptions.AvoidDynamicFunctions"/>, which gives the same results for the built-in types.
/// </summary>
internal static class DynamicArithmetic
{
    internal const string SwitchName = "NCalc.DynamicArithmetic.IsSupported";

    internal const string Message =
        "Operators applied through the C# runtime binder and user-defined operators found by reflection need dynamic code.";

    // The code that IsEnabled guards is removed by trimming when its switch is false, so its trimming warnings are suppressed
    internal const string TrimmingJustification =
        "Used only when DynamicArithmetic.IsEnabled is true; the targets of the package set its switch to false in trimmed applications, and trimming then removes this code.";

    private static readonly bool IsEnabledBySwitch = !AppContext.TryGetSwitch(SwitchName, out bool isEnabled) || isEnabled;

    /// <summary>Whether dynamic arithmetic is used: dynamic code is supported and the switch does not turn it off.</summary>
    internal static bool IsUsed => IsDynamicCodeSupported && IsEnabled;

    /// <summary>False under Native AOT. The code that calls dynamic arithmetic checks it directly, as the AOT analyzer requires.</summary>
#if NET9_0_OR_GREATER
    [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
#endif
    internal static bool IsDynamicCodeSupported
#if NET
        => RuntimeFeature.IsDynamicCodeSupported;
#else
        => true;
#endif

    /// <summary>The switch, which is false in trimmed applications.</summary>
#if NET9_0_OR_GREATER
    [FeatureSwitchDefinition(SwitchName)]
#endif
    internal static bool IsEnabled => IsEnabledBySwitch;
}

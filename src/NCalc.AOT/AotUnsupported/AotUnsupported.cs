namespace NCalc;

/// <summary>
/// Members that compile NCalc expressions to LINQ expression trees need dynamic code, so the AOT-compatible build
/// contains them only as stubs that throw. The stubs keep the public API identical to the regular build, so that
/// assemblies compiled against the regular build still load, and Native AOT publishing reports their callers.
/// </summary>
internal static class AotUnsupported
{
    internal const string Message =
        "Compiling NCalc expressions to LINQ expression trees requires dynamic code and is not available in the AOT-compatible build of NCalc (UseAotMode). Use Evaluate() instead.";

    internal static NotSupportedException Exception() => new(Message);
}

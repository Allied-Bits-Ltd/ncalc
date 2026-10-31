using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using LinqExpression = System.Linq.Expressions.Expression;

namespace NCalc.Reflection;

// AOT build counterpart of NCalc/Reflection/LinqUtils.cs, see AotUnsupported.
public static class LinqUtils
{
    [RequiresDynamicCode(AotUnsupported.Message)]
    [RequiresUnreferencedCode(AotUnsupported.Message)]
    public static Tuple<int, LinqExpression[]>? PrepareMethodArgumentsIfValid(ParameterInfo[] parameters, LinqExpression[] arguments)
    {
        throw AotUnsupported.Exception();
    }

    [RequiresDynamicCode(AotUnsupported.Message)]
    [RequiresUnreferencedCode(AotUnsupported.Message)]
    public static LinqExpression UnwrapNullable(LinqExpression expression)
    {
        throw AotUnsupported.Exception();
    }
}

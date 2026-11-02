using System.Numerics;
using System.Reflection;
using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Helpers;

namespace NCalc.Visitors;

/// <summary>
/// An overload of a method of the context object that a compiled lambda chooses when it runs, by the types of the argument values.
/// </summary>
internal sealed class ContextMethodCandidate
{
    public ContextMethodCandidate(MethodInfo method)
    {
        Method = method;
        var parameters = method.GetParameters();
        ParameterTypes = parameters.Select(p => p.ParameterType).ToArray();
        if (parameters.Length > 0 && parameters[^1].IsDefined(typeof(ParamArrayAttribute)))
            ParamsElementType = ParameterTypes[^1].GetElementType();
    }

    public MethodInfo Method { get; }

    /// <summary>The types of all the parameters, the params array included.</summary>
    public Type[] ParameterTypes { get; }

    /// <summary>The element type of the params array, or null when the method has none.</summary>
    public Type? ParamsElementType { get; }

    public int FixedParameterCount => ParamsElementType is null ? ParameterTypes.Length : ParameterTypes.Length - 1;

    // The type of the parameter that takes the argument at the given position
    public Type GetArgumentType(int position)
        => position < FixedParameterCount ? ParameterTypes[position] : ParamsElementType!;
}

/// <summary>
/// A call of a method of the context object whose overload is chosen when the lambda runs.
/// </summary>
internal sealed class ContextMethodCall(string name, ContextMethodCandidate[] candidates, FunctionCall function)
{
    public string Name { get; } = name;
    public ContextMethodCandidate[] Candidates { get; } = candidates;
    public FunctionCall Function { get; } = function;
}

internal static partial class LambdaRuntime
{
    // The costs of converting an argument value to the type of a parameter; the overload with the lowest total cost is chosen
    private const int ExactCost = 0;
    private const int ImplicitCost = 1;
    private const int ReferenceCost = 2;
    private const int LosslessCost = 3;

    /// <summary>
    /// Chooses the overload of a context method by the types of the argument values, as C# chooses one for arguments of these types,
    /// and converts the arguments in place to the types of its parameters (the params arguments are collected into an array).
    /// A number is converted to a parameter of another numeric type only without loss: a value with a fraction, out of the range
    /// of the type, or not a number is not converted. The arguments array has one element more than there are arguments.
    /// Returns the index of the chosen overload in <see cref="ContextMethodCall.Candidates"/>.
    /// </summary>
    internal static int BindContextMethod(object?[] arguments, ContextMethodCall call)
    {
        var argumentCount = arguments.Length - 1;
        var candidates = call.Candidates;
        var best = -1;
        var bestCost = int.MaxValue;
        object?[]? bestArguments = null;

        for (var i = 0; i < candidates.Length; i++)
        {
            if (!TryBindArguments(candidates[i], arguments, argumentCount, out var converted, out var cost))
                continue;

            if (best < 0 || cost < bestCost || (cost == bestCost && IsBetterOverload(candidates[i], candidates[best], argumentCount)))
            {
                best = i;
                bestCost = cost;
                bestArguments = converted;
            }
        }

        if (best < 0)
            throw NoMatchingOverload(arguments, argumentCount, call);

        Array.Copy(bestArguments!, arguments, bestArguments!.Length);
        return best;
    }

    /// <summary>
    /// Converts a value assigned to a member of the context object to the type of the member, without loss.
    /// </summary>
    internal static T ConvertAssignedValue<T>(object? value, Identifier identifier)
    {
        if (TryConvertLossless(value, typeof(T), out var result, out _))
            return (T)result!;

        throw new NCalcEvaluationException($"{CannotConvertMessage(value, typeof(T))}, so it cannot be assigned to '{identifier.Name}'", identifier.Location);
    }

    private static bool TryBindArguments(ContextMethodCandidate candidate, object?[] arguments, int argumentCount, out object?[] converted, out int cost)
    {
        converted = [];
        cost = 0;
        var fixedCount = candidate.FixedParameterCount;
        if (candidate.ParamsElementType is null ? argumentCount != fixedCount : argumentCount < fixedCount)
            return false;

        var result = new object?[argumentCount + 1];
        for (var i = 0; i < fixedCount; i++)
        {
            if (!TryConvertLossless(arguments[i], candidate.ParameterTypes[i], out result[i], out var argumentCost))
                return false;
            cost += argumentCost;
        }

        if (candidate.ParamsElementType is { } elementType)
        {
            var array = Array.CreateInstance(elementType, argumentCount - fixedCount);
            for (var i = fixedCount; i < argumentCount; i++)
            {
                if (!TryConvertLossless(arguments[i], elementType, out var element, out var argumentCost))
                    return false;
                array.SetValue(element, i - fixedCount);
                cost += argumentCost;
            }

            result[fixedCount] = array;

            // As in C#, a method without a params array is preferred
            cost++;
        }

        converted = result;
        return true;
    }

    // Whether the parameters of a are more specific than those of b. When neither is, the overloads are ordered by their signatures,
    // so that the choice does not depend on the order in which they are declared.
    private static bool IsBetterOverload(ContextMethodCandidate a, ContextMethodCandidate b, int argumentCount)
    {
        bool aBetter = false, bBetter = false;
        for (var i = 0; i < argumentCount; i++)
        {
            var aType = a.GetArgumentType(i);
            var bType = b.GetArgumentType(i);
            if (aType == bType)
                continue;
            if (IsMoreSpecific(aType, bType))
                aBetter = true;
            else if (IsMoreSpecific(bType, aType))
                bBetter = true;
        }

        if (aBetter != bBetter)
            return aBetter;

        return string.CompareOrdinal(a.Method.ToString(), b.Method.ToString()) < 0;
    }

    private static bool IsMoreSpecific(Type type, Type than)
        => (TypeHelper.ImplicitPrimitiveConversionTable.TryGetValue(type, out var conversions) && conversions.Contains(than))
            || than.IsAssignableFrom(type);

    private static NCalcEvaluationException NoMatchingOverload(object?[] arguments, int argumentCount, ContextMethodCall call)
    {
        var location = call.Function.Location;

        // With a single method that takes these many arguments, the argument that cannot be converted is reported
        var candidates = call.Candidates;
        if (candidates.Length == 1 && (candidates[0].ParamsElementType is null ? argumentCount == candidates[0].FixedParameterCount : argumentCount >= candidates[0].FixedParameterCount))
        {
            for (var i = 0; i < argumentCount; i++)
            {
                var type = candidates[0].GetArgumentType(i);
                if (!TryConvertLossless(arguments[i], type, out _, out _))
                    return new NCalcEvaluationException($"{CannotConvertMessage(arguments[i], type)}, so it cannot be passed as argument {i + 1} of the method '{call.Name}'", location);
            }
        }

        var types = string.Join(", ", arguments.Take(argumentCount).Select(a => a?.GetType().Name ?? "null"));
        return new NCalcEvaluationException($"No overload of the method '{call.Name}' of the context accepts arguments of the types ({types}) without loss", location);
    }

    private static string CannotConvertMessage(object? value, Type type)
        => value is null
            ? $"The value null cannot be converted to {type.Name}"
            : $"The value {Convert.ToString(value, CultureInfo.InvariantCulture)} ({value.GetType().Name}) cannot be converted to {type.Name} without loss";

    /// <summary>
    /// Converts a value to a type without losing information: as C# converts it implicitly, or a number to another numeric type when
    /// the value has no fraction and is in the range of the type. Null is converted only to a reference or nullable type.
    /// The cost tells how good the conversion is: 0 when the value has the type already.
    /// </summary>
    internal static bool TryConvertLossless(object? value, Type type, out object? result, out int cost)
    {
        result = null;
        cost = ExactCost;

        var underlyingType = Nullable.GetUnderlyingType(type);
        if (value is null)
        {
            cost = ImplicitCost;
            return !type.IsValueType || underlyingType is not null;
        }

        var target = underlyingType ?? type;
        var valueType = value.GetType();
        if (valueType == target)
        {
            result = value;
            return true;
        }

        if (TypeHelper.ImplicitPrimitiveConversionTable.TryGetValue(valueType, out var conversions) && conversions.Contains(target))
        {
            result = Converters.GetOrAdd(Tuple.Create(valueType, target, true), BuildConverter)(value);
            cost = ImplicitCost;
            return true;
        }

        if (type.IsInstanceOfType(value))
        {
            result = value;
            cost = ReferenceCost;
            return true;
        }

        cost = LosslessCost;
        return TryConvertNumber(value, target, out result);
    }

    private static bool IsNumber(object value)
        => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal or BigInteger or BigDecimal;

    internal static bool IsNumericType(Type type)
        => type == typeof(sbyte) || type == typeof(byte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int)
            || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) || type == typeof(float) || type == typeof(double)
            || type == typeof(decimal) || type == typeof(BigInteger) || type == typeof(BigDecimal);

    private static bool TryConvertNumber(object value, Type target, out object? result)
    {
        result = null;
        if (!IsNumber(value) || !IsNumericType(target))
            return false;

        var isInfinite = value is double.PositiveInfinity or double.NegativeInfinity or float.PositiveInfinity or float.NegativeInfinity;
        var isNaN = value is double.NaN or float.NaN;
        try
        {
            if (target == typeof(double) || target == typeof(float))
            {
                var d = value switch
                {
                    BigInteger bi => (double)bi,
                    BigDecimal bd => MathHelper.BigDecimalToDouble(bd),
                    _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
                };

                // A value too large for the type is not converted to infinity
                if (target == typeof(float))
                {
                    var f = (float)d;
                    if (float.IsInfinity(f) && !isInfinite)
                        return false;
                    result = f;
                    return true;
                }

                if (double.IsInfinity(d) && !isInfinite)
                    return false;
                result = d;
                return true;
            }

            if (isInfinite || isNaN)
                return false;

            if (target == typeof(BigDecimal))
            {
                result = MathHelper.ConvertToBigDecimal(value);
                return true;
            }

            if (target == typeof(decimal))
            {
                result = value switch
                {
                    double d => (decimal)d,
                    float f => (decimal)f,
                    BigDecimal bd => MathHelper.BigDecimalToDecimal(bd),
                    BigInteger bi => (decimal)bi,
                    _ => Convert.ToDecimal(value, CultureInfo.InvariantCulture)
                };
                return true;
            }

            return TryGetWholeValue(value, out var whole) && TryConvertWholeValue(whole, target, out result);
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryGetWholeValue(object value, out BigInteger whole)
    {
        whole = BigInteger.Zero;
        switch (value)
        {
            case double or float:
            {
                var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (Math.Truncate(d) != d)
                    return false;
                whole = new BigInteger(d);
                return true;
            }
            case decimal m:
                if (decimal.Truncate(m) != m)
                    return false;
                whole = new BigInteger(m);
                return true;
            case BigDecimal bd:
                // An exponent this large gives a value out of the range of every type, and computing it would take long
                if (Math.Abs((long)bd.Exponent) > MathHelper.MaxPowResultDigits || !MathHelper.IsWholeNumber(bd))
                    return false;
                whole = MathHelper.GetWholeValue(bd);
                return true;
            case BigInteger bi:
                whole = bi;
                return true;
            case ulong ul:
                whole = new BigInteger(ul);
                return true;
            default:
                whole = new BigInteger(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                return true;
        }
    }

    private static bool TryConvertWholeValue(BigInteger value, Type target, out object? result)
    {
        result = null;
        if (target == typeof(BigInteger))
            result = value;
        else if (target == typeof(int) && value >= int.MinValue && value <= int.MaxValue)
            result = (int)value;
        else if (target == typeof(long) && value >= long.MinValue && value <= long.MaxValue)
            result = (long)value;
        else if (target == typeof(short) && value >= short.MinValue && value <= short.MaxValue)
            result = (short)value;
        else if (target == typeof(sbyte) && value >= sbyte.MinValue && value <= sbyte.MaxValue)
            result = (sbyte)value;
        else if (target == typeof(byte) && value >= byte.MinValue && value <= byte.MaxValue)
            result = (byte)value;
        else if (target == typeof(ushort) && value >= ushort.MinValue && value <= ushort.MaxValue)
            result = (ushort)value;
        else if (target == typeof(uint) && value >= uint.MinValue && value <= uint.MaxValue)
            result = (uint)value;
        else if (target == typeof(ulong) && value >= ulong.MinValue && value <= ulong.MaxValue)
            result = (ulong)value;
        return result is not null;
    }
}

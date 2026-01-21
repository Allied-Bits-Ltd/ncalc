using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;

using ExtendedNumerics;

using NCalc.Exceptions;

namespace NCalc.Helpers;

/// <summary>
/// Utilities for doing mathematical operations between different object types.
/// </summary>
public static class MathHelper
{
    enum ArithmeticOperation
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        Modulo,
        AddPercent,
        SubtractPercent,
        MultiplyPercent,
        DividePercent,
    }

    // unchecked
#if !AOT_COMPILATION
    private static object DynamicAddFunc(dynamic a, dynamic b) => unchecked(a + b);
    private static object DynamicSubtractFunc(dynamic a, dynamic b) => unchecked(a - b);
    private static object DynamicMultiplyFunc(dynamic a, dynamic b) => unchecked(a * b);
    private static object DynamicDivideFunc(dynamic a, dynamic b) => unchecked(a / b);
    private static object DynamicModuloFunc(dynamic a, dynamic b) => unchecked(a % b);

    private static object DynamicAddPercentFunc(dynamic a, dynamic b) => unchecked(a * (100 + b) / 100); // a / (a * b/100);
    private static object DynamicSubtractPercentFunc(dynamic a, dynamic b) => unchecked(a * (100 - b) / 100); //a - (a * b / 100);
    private static object DynamicMultiplyPercentFunc(dynamic a, dynamic b) => unchecked(a * b / 100);
    private static object DynamicDividePercentFunc(dynamic a, dynamic b) => unchecked(a * 100 / b);
#endif

#if AOT_COMPILATION
    public static T AddGeneric<T>(T left, T right) where T : IAdditionOperators<T, T, T>
    {
        return left + right;
    }
#endif

    private static object? AddFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = AddFuncChecked(a, b);
            else
                result = DynamicAddFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca + cb);
            else
                result = unchecked(ca + cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba + bb);
            else
                result = unchecked(ba + bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa + sb);
            else
                result = unchecked(sa + sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha + shb);
            else
                result = unchecked(sha + shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha + ushb);
            else
                result = unchecked(usha + ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia + ib);
            else
                result = unchecked(ia + ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia + uib);
            else
                result = unchecked(uia + uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la + lb);
            else
                result = unchecked(la + lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula + ulb);
            else
                result = unchecked(ula + ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa + fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa + fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da + db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da + db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca + dcb);
            }
            else
            {
                result = unchecked(dca + dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Addition");
            if (method is null)
                throw new InvalidOperationException($"No overloaded addition function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded addition function found for operands of type '{a.GetType()}'");
#endif
        }

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> AddFuncChecked = (a, b) =>
    {
        var res = checked(a + b);
        CheckOverflow(res);

        return res;
    };
#endif

    private static object AddPercentFunc(object a, object b, MathHelperOptions options)
    {
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            return options.OverflowProtection ? AddPercentFuncChecked(a, b) : DynamicAddPercentFunc(a, b);
        }
#endif
        object? im1 = Add(100, b, false, options);
        if (im1 is null)
            throw new InvalidOperationException($"No addition was possible for a number and an object of type '{b.GetType()}'");

        object? im2 = Multiply(a, im1, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for objects of types '{a.GetType()}' and '{im1.GetType()}'");

        object? result = Divide(im2, 100, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for an object of type '{im2.GetType()}' and a number");

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> AddPercentFuncChecked = (a, b) =>
    {
        var res = checked(a * (100 + b) / 100); //checked(a + (a * b / 100));
        CheckOverflow(res);

        return res;
    };
#endif

    private static object? SubtractFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = SubtractFuncChecked(a, b);
            else
                result = DynamicSubtractFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca - cb);
            else
                result = unchecked(ca - cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba - bb);
            else
                result = unchecked(ba - bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa - sb);
            else
                result = unchecked(sa - sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha - shb);
            else
                result = unchecked(sha - shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha - ushb);
            else
                result = unchecked(usha - ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia - ib);
            else
                result = unchecked(ia - ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia - uib);
            else
                result = unchecked(uia - uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la - lb);
            else
                result = unchecked(la - lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula - ulb);
            else
                result = unchecked(ula - ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa - fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa - fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da - db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da - db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca - dcb);
            }
            else
            {
                result = unchecked(dca - dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Subtraction");
            if (method is null)
                throw new InvalidOperationException($"No overloaded subtraction function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded subtraction function found for operands of type '{a.GetType()}'");
#endif
        }

            return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> SubtractFuncChecked = (a, b) =>
    {
        var res = checked(a - b);
        CheckOverflow(res);

        return res;
    };
#endif

    private static object SubtractPercentFunc(object a, object b, MathHelperOptions options)
    {
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            return options.OverflowProtection ? SubtractPercentFuncChecked(a, b) : DynamicSubtractPercentFunc(a, b);
        }
#endif
        object? im1 = Subtract(100, b, false, options);
        if (im1 is null)
            throw new InvalidOperationException($"No subtraction was possible for a number and an '{b.GetType()}' object");

        object? im2 = Multiply(a, im1, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for objects of types '{a.GetType()}' and '{im1.GetType()}'");

        object? result = Divide(im2, 100, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for an '{im2.GetType()}' object and a number");

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> SubtractPercentFuncChecked = (a, b) =>
    {
        var res = checked(a * (100 - b) / 100);
        CheckOverflow(res);

        return res;
    };
#endif
    private static object? MultiplyFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = MultiplyFuncChecked(a, b);
            else
                result = DynamicMultiplyFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca * cb);
            else
                result = unchecked(ca * cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba * bb);
            else
                result = unchecked(ba * bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa * sb);
            else
                result = unchecked(sa * sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha * shb);
            else
                result = unchecked(sha * shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha * ushb);
            else
                result = unchecked(usha * ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia * ib);
            else
                result = unchecked(ia * ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia * uib);
            else
                result = unchecked(uia * uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la * lb);
            else
                result = unchecked(la * lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula * ulb);
            else
                result = unchecked(ula * ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa * fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa * fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da * db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da * db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca * dcb);
            }
            else
            {
                result = unchecked(dca * dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Multiply");
            if (method is null)
                throw new InvalidOperationException($"No overloaded multiplication function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded multiplication function found for operands of type '{a.GetType()}'");
#endif
        }

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> MultiplyFuncChecked = (a, b) =>
    {
        var res = checked(a * b);
        CheckOverflow(res);

        return res;
    };
#endif
    private static object MultiplyPercentFunc(object a, object b, MathHelperOptions options)
    {
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            return options.OverflowProtection ? MultiplyPercentFuncChecked(a, b) : DynamicMultiplyPercentFunc(a, b);
        }
#endif
        object? im2 = Multiply(a, b, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for objects of types '{a.GetType()}' and '{b.GetType()}'");

        object? result = Divide(im2, 100, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for an '{im2.GetType()}' object and a number");

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> MultiplyPercentFuncChecked = (a, b) =>
    {
        var res = checked(a * b / 100);
        CheckOverflow(res);

        return res;
    };
#endif

    private static object? DivideFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = DivideFuncChecked(a, b);
            else
                result = DynamicDivideFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca / cb);
            else
                result = unchecked(ca / cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba / bb);
            else
                result = unchecked(ba / bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa / sb);
            else
                result = unchecked(sa / sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha / shb);
            else
                result = unchecked(sha / shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha / ushb);
            else
                result = unchecked(usha / ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia / ib);
            else
                result = unchecked(ia / ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia / uib);
            else
                result = unchecked(uia / uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la / lb);
            else
                result = unchecked(la / lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula / ulb);
            else
                result = unchecked(ula / ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa / fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa / fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da / db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da / db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca / dcb);
            }
            else
            {
                result = unchecked(dca / dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Division");
            if (method is null)
                throw new InvalidOperationException($"No overloaded division function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded division function found for operands of type '{a.GetType()}'");
#endif
        }

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> DivideFuncChecked = (a, b) =>
    {
        var res = checked(a / b);
        CheckOverflow(res);

        return res;
    };
#endif
    private static object DividePercentFunc(object a, object b, MathHelperOptions options)
    {
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            return options.OverflowProtection ? DividePercentFuncChecked(a, b) : DynamicDividePercentFunc(a, b);
        }
#endif
        object? im2 = Multiply(a, 100, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for an '{a.GetType()}' object and a number");

        object? result = Divide(im2, b, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for objects of types '{im2.GetType()}' and '{b.GetType()}'");

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> DividePercentFuncChecked = (a, b) =>
    {
        var res = checked(a * 100 / b);
        CheckOverflow(res);

        return res;
    };
#endif

    private static object? ModuloFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
#if !AOT_COMPILATION
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = ModuloFuncChecked(a, b);
            else
                result = DynamicModuloFunc(a, b);
        }
        else
#endif
        if (a is char ca && b is char cb)
        {
            if (options.OverflowProtection)
                result = checked(ca % cb);
            else
                result = unchecked(ca % cb);
        }
        else
        if (a is byte ba && b is byte bb)
        {
            if (options.OverflowProtection)
                result = checked(ba % bb);
            else
                result = unchecked(ba % bb);
        }
        else
        if (a is sbyte sa && b is sbyte sb)
        {
            if (options.OverflowProtection)
                result = checked(sa % sb);
            else
                result = unchecked(sa % sb);
        }
        else
        if (a is short sha && b is short shb)
        {
            if (options.OverflowProtection)
                result = checked(sha % shb);
            else
                result = unchecked(sha % shb);
        }
        else
        if (a is ushort usha && b is ushort ushb)
        {
            if (options.OverflowProtection)
                result = checked(usha % ushb);
            else
                result = unchecked(usha % ushb);
        }
        else
        if (a is int ia && b is int ib)
        {
            if (options.OverflowProtection)
                result = checked(ia % ib);
            else
                result = unchecked(ia % ib);
        }
        else
        if (a is uint uia && b is uint uib)
        {
            if (options.OverflowProtection)
                result = checked(uia % uib);
            else
                result = unchecked(uia % uib);
        }
        else
        if (a is long la && b is long lb)
        {
            if (options.OverflowProtection)
                result = checked(la % lb);
            else
                result = unchecked(la % lb);
        }
        else
        if (a is ulong ula && b is ulong ulb)
        {
            if (options.OverflowProtection)
                result = checked(ula % ulb);
            else
                result = unchecked(ula % ulb);
        }
        else
        if (a is float fa && b is float fb)
        {
            if (options.OverflowProtection)
            {
                result = checked(fa % fb);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(fa % fb);
            }
        }
        else
        if (a is double da && b is double db)
        {
            if (options.OverflowProtection)
            {
                result = checked(da % db);
                CheckOverflow(result);
            }
            else
            {
                result = unchecked(da % db);
            }
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca % dcb);
            }
            else
            {
                result = unchecked(dca % dcb);
            }
        }
        else
        {
#if !AOT_COMPILATION
            var method = FindOperator(a.GetType(), "op_Modulus");
            if (method is null)
                throw new InvalidOperationException($"No overloaded modulus function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
#else
            throw new InvalidOperationException($"No overloaded modulus function found for operands of type '{a.GetType()}'");
#endif
        }

        return result;
    }

#if !AOT_COMPILATION
    private static readonly Func<dynamic, dynamic, object> ModuloFuncChecked = (a, b) =>
    {
        var res = checked(a % b);
        CheckOverflow(res);

        return res;
    };
#endif

    public static object? AddPercent(object? a, object? b)
    {
        return AddPercent(a, b, CultureInfo.CurrentCulture);
    }

    public static object? Add(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "+", options);
        b = ConvertIfNeeded(b, "+", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options, out var typesWereExpanded);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Addition is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            if (a is BigDecimal bdA)
            {
                return ReduceNumericType(Add(bdA, b), reduceTypes ? null : t, options);
            }
            else
            if (b is BigDecimal bdB)
            {
                return ReduceNumericType(Add(bdB, a), reduceTypes ? null : t, options);
            }
            else
            if (a is BigInteger biA)
            {
                return ReduceNumericType(Add(biA, b), reduceTypes ? null : t, options);
            }
            else
            if (b is BigInteger biB)
            {
                return ReduceNumericType(Add(biB, a), reduceTypes ? null : t, options);
            }
            else
            if (a is long || a is ulong || b is long || b is ulong)
            {
                BigInteger result;
                if (a is long || a is ulong)
                {
                    if (a is long la)
                        result = new BigInteger(la);
                    else
                        result = new BigInteger((ulong)a);

                    result = Add(result, b);
                }
                else
                {
                    if (b is long lb)
                        result = new BigInteger(lb);
                    else
                        result = new BigInteger((ulong)b);

                    result = Add(result, a);
                }
                return ReduceNumericType(result, reduceTypes ? null : t, options);
            }
        }

        try
        {
            object result = ExecuteOperation(a, b, '+', ArithmeticOperation.Add, options, typeCode);
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            object? result = Add(a, b, reduceTypes, options);
            if (result is null)
                return null;
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
    }

    public static object? AddPercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, "+", options);
        b = ConvertIfNeeded(b, "+", options);

        //var func = options.OverflowProtection ? AddPercentFuncChecked : AddPercentFunc;
        return ExecuteOperation(a, b, '+', ArithmeticOperation.AddPercent, options);
    }

   /* public static object? Subtract(object? a, object? b)
    {
        return Subtract(a, b, CultureInfo.CurrentCulture);
    }*/

    public static object? SubtractPercent(object? a, object? b)
    {
        return SubtractPercent(a, b, CultureInfo.CurrentCulture);
    }

    public static object? Subtract(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "-", options);
        b = ConvertIfNeeded(b, "-", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options, out var typesWereExpanded);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Subtraction is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            BigInteger? result = null;
            BigDecimal? bdResult = null;

            // If any of the arguments are BigDecimal, calculate the result and either reduce it to minimal size
            if (a is BigDecimal bdA)
            {
                bdResult = Subtract(bdA, b);
            }
            else
            if (b is BigDecimal bdB)
            {
                bdResult = Subtract(a, bdB);
            }

            if (bdResult is not null)
            {
                if (bdResult.Value.GetFractionalPart().IsZero())
                {
                    result = bdResult.Value.WholeValue;
                    return ReduceNumericType(result, reduceTypes ? null : t, options);
                }
                else
                {
                    return ReduceNumericType(bdResult, reduceTypes ? null : t, options);
                }
            }

            // If there was no BigDecimal calculation performed, proceed with the operation
            if (result is null)
            {
                if (a is BigInteger biA)
                {
                    result = Subtract(biA, b);
                }
                else
                if (b is BigInteger biB)
                {
                    result = Subtract(a, biB);
                }
                else
                if (a is long || a is ulong || b is long || b is ulong)
                {
                    if (a is long || a is ulong)
                    {
                        if (a is long la)
                            result = new BigInteger(la);
                        else
                            result = new BigInteger((ulong)a);

                        result = Subtract(result.Value, b);
                    }
                    else
                    {
                        if (b is long lb)
                            result = new BigInteger(lb);
                        else
                            result = new BigInteger((ulong)b);

                        result = Subtract(a, result.Value);
                    }
                }
            }

            if (result is not null)
                return ReduceNumericType(result, reduceTypes ? null : t, options);
        }

        //var func = options.OverflowProtection ? SubtractFuncChecked : SubtractFunc;
        try
        {
            object result = ExecuteOperation(a, b, '-', ArithmeticOperation.Subtract, options, typeCode);
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            object? result = Subtract(a, b, reduceTypes, options);
            if (result is null)
                return result;
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
    }

    public static object? SubtractPercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, "-", options);
        b = ConvertIfNeeded(b, "-", options);

        //var func = options.OverflowProtection ? SubtractPercentFuncChecked : SubtractPercentFunc;
        return ExecuteOperation(a, b, '-', ArithmeticOperation.SubtractPercent, options);
    }

    public static object? MultiplyPercent(object? a, object? b)
    {
        return MultiplyPercent(a, b, CultureInfo.CurrentCulture);
    }

    public static object? Multiply(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "*", options);
        b = ConvertIfNeeded(b, "*", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options, out var typesWereExpanded);

        if (!options.OverflowProtection)
        {
            // For multiplication, we must up the number of bits to avoid a possible overflow.
            // We can't just call ConvertToHighestPrecision(...,...,true,...) because ConvertToHighestPrecision will do unexpected things.
            typeCode = TypeCodeExpandBits(typeCode, ref a, ref b, options);
            typesWereExpanded = true;
        }

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Multiplication is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            if (a is BigDecimal bdA)
            {
                return ReduceNumericType(Multiply(bdA, b), reduceTypes ? null : t, options);
            }
            else
            if (b is BigDecimal bdB)
            {
                return ReduceNumericType(Multiply(bdB, a), reduceTypes ? null : t, options);
            }
            else
            if (a is BigInteger biA)
            {
                return ReduceNumericType(Multiply(biA, b), reduceTypes ? null : t, options);
            }
            else
            if (b is BigInteger biB)
            {
                return ReduceNumericType(Multiply(biB, a), reduceTypes ? null : t, options);
            }
            else
            if (a is long || a is ulong || b is long || b is ulong)
            {
                BigInteger result;
                if (a is long || a is ulong)
                {
                    if (a is long la)
                        result = new BigInteger(la);
                    else
                        result = new BigInteger((ulong)a);

                    result = Multiply(result, b);
                }
                else
                {
                    if (b is long lb)
                        result = new BigInteger(lb);
                    else
                        result = new BigInteger((ulong)b);

                    result = Multiply(result, a);
                }
                return ReduceNumericType(result, reduceTypes ? null : t, options);
            }
        }

        //var func = options.OverflowProtection ? MultiplyFuncChecked : MultiplyFunc;
        try
        {
            object result = ExecuteOperation(a, b, '*', ArithmeticOperation.Multiply, options, typeCode);
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            object? result = Multiply(a, b, reduceTypes, options);
            if (result is null)
                return null;
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
    }

    public static object? MultiplyPercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, "*", options);
        b = ConvertIfNeeded(b, "*", options);

        //var func = options.OverflowProtection ? MultiplyPercentFuncChecked : MultiplyPercentFunc;
        return ExecuteOperation(a, b, '*', ArithmeticOperation.MultiplyPercent, options);
    }

   /* public static object? Divide(object? a, object? b)
    {
        return Divide(a, b, CultureInfo.CurrentCulture);
    }
*/
    public static object? Divide(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type typeA = a.GetType(); //Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "/", options);
        b = ConvertIfNeeded(b, "/", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Division is not implemented for operands of types {a.GetType()} and {b.GetType()}");

        // Convert types to floating-point ones because otherwise, we get an integer division
        if (options.UseBigNumbers)
        {
            if (typeCode != TypeCode.Object)
            {
                a = ConvertToBigDecimal(a);
                b = ConvertToBigDecimal(b);
                typeCode = TypeCode.Object;
            }
        }
        else
        {
            if (IsBoxedIntegerNumber(a))
            {
                long? tl = GetBoxedIntegerNumberAsLong(a);
                if (tl is null)
                    return null;

                a = (decimal)tl;
                typeCode = TypeCode.Decimal;
            }
            else
            if (a is decimal)
            {
                typeCode = TypeCode.Decimal;
            }
            else
            if (a is float)
            {
                double? da = GetBoxedNumberAsDouble(a);
                if (da is null)
                    return null;
                a = da.Value;
                typeCode = TypeCode.Double;
            }

            if (IsBoxedIntegerNumber(b))
            {
                long? tl = GetBoxedIntegerNumberAsLong(b);
                if (tl is null)
                    return null;

                b = (decimal)tl;
                typeCode = TypeCode.Decimal;
            }
            else
            if (b is decimal)
            {
                typeCode = TypeCode.Decimal;
            }
            else
            if (b is float)
            {
                double? db = GetBoxedNumberAsDouble(b);
                if (db is null)
                    return null;
                b = db.Value;
                typeCode = TypeCode.Double;
            }
        }

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            BigDecimal? bdResult = null;

            if (a is BigDecimal bdA)
            {
                bdResult = Divide(bdA, b);
            }
            else
            if (b is BigDecimal bdB)
            {
                bdResult = Divide(a, bdB);
            }
            else
            if (a is BigInteger biA)
            {
                bdResult = Divide(new BigDecimal(biA), b);
            }
            else
            if (b is BigInteger biB)
            {
                bdResult = Divide(a, new BigDecimal(biB));
            }
            /*else
            if (a is long || a is ulong || b is long || b is ulong)
            {
                if (a is long || a is ulong)
                {
                    if (a is long la)
                        bdResult = new BigDecimal(new BigInteger(la));
                    else
                        bdResult = new BigDecimal(new BigInteger((ulong)a));

                    bdResult = Divide(bdResult.Value, b);
                }
                else
                {
                    if (b is long lb)
                        bdResult = new BigDecimal(new BigInteger(lb));
                    else
                        bdResult = new BigDecimal(new BigInteger((ulong)b));

                    bdResult = Divide(a, bdResult.Value);
                }
            }*/

            if (bdResult is not null)
            {
                if (reduceTypes)
                {
                    if ((IsBoxedIntegerNumberOrBigNumber(typeA) || options.ReduceDivResultToInteger) && bdResult.Value.GetFractionalPart().IsZero())
                    {
                        BigInteger biResult = bdResult.Value.WholeValue;

                        if (biResult >= long.MinValue && biResult <= long.MaxValue)
                            return (long)biResult;
                        else
                        if (biResult >= ulong.MinValue && biResult <= ulong.MaxValue)
                            return (ulong)biResult;

                        return biResult;
                    }
                    else
                    if ((options.DecimalAsDefault || typeA == typeof(decimal)) && (bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue))
                    {
                        return (decimal)bdResult;
                    }
                    else
                    /*if (bdResult >= float.MinValue && bdResult <= float.MaxValue)
                    {
                        return (float)bdResult;
                    }
                    else*/
                    if (bdResult >= double.MinValue && bdResult <= double.MaxValue)
                    {
                        return (double)bdResult;
                    }
                    else
                    if (bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue)
                    {
                        return (decimal)bdResult;
                    }
                }
                else // try to keep the original type of a
                {
                    if (IsBoxedIntegerNumberOrBigNumber(typeA) && bdResult.Value.GetFractionalPart().IsZero())
                    {
                        BigInteger biResult = bdResult.Value.WholeValue;

                        if ((typeA == typeof(short)) && biResult >= short.MinValue && biResult <= short.MaxValue)
                            return (short)biResult;
                        else
                        if ((typeA == typeof(ushort)) && biResult >= ushort.MinValue && biResult <= ushort.MaxValue)
                            return (ushort)biResult;
                        else
                        if ((typeA == typeof(int)) && biResult >= int.MinValue && biResult <= int.MaxValue)
                            return (int)biResult;
                        else
                        if ((typeA == typeof(uint)) && biResult >= uint.MinValue && biResult <= uint.MaxValue)
                            return (uint)biResult;
                        else
                        if ((typeA == typeof(long)) && biResult >= long.MinValue && biResult <= long.MaxValue)
                            return (long)biResult;
                        else
                        if ((typeA == typeof(ulong)) && biResult >= ulong.MinValue && biResult <= ulong.MaxValue)
                            return (ulong)biResult;

                        return biResult;
                    }

                    if ((options.DecimalAsDefault || typeA == typeof(decimal)) && (bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue))
                        return (decimal)bdResult;
                    else
                    if ((typeA == typeof(float)) && bdResult >= float.MinValue && bdResult <= float.MaxValue)
                        return (float)bdResult;
                    else
                    if ((typeA == typeof(double)) && bdResult >= double.MinValue && bdResult <= double.MaxValue)
                        return (double)bdResult;
                }

                return bdResult;
            }

            typeCode = TypeCode.Empty;
        }

        if (a is null || b is null)
            return null;

        //var func = options.OverflowProtection ? DivideFuncChecked : DivideFunc;

        object? result = null;

        try
        {
            result = ExecuteOperation(a, b, '/', ArithmeticOperation.Divide, options, typeCode);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            result = Divide(a, b, reduceTypes, options);
        }
        if (result is null)
            return result;

        if (reduceTypes || options.ReduceDivResultToInteger)
        {
            if ((IsBoxedIntegerNumberOrBigNumber(typeA) || options.ReduceDivResultToInteger) && IsBoxedFloatingNumberInteger(result) == true)
            {
                long? iResult = GetBoxedIntegerNumberAsLong(result);
                if (iResult is null)
                    return result;

                if (iResult >= int.MinValue && iResult <= int.MaxValue)
                    return (int)iResult;
                else
                if (iResult >= uint.MinValue && iResult <= uint.MaxValue)
                    return (uint)iResult;

                return iResult;
            }
            else
            if ((options.DecimalAsDefault || typeA == typeof(decimal)) && (result is not decimal))
            {
                try
                {
                    if (result is BigDecimal)
                        return (decimal)result;

                    return ConvertToDecimal(result, options);
                }
                catch
                {
                }
            }
            else
            {
                double? dResult = null;
                if (result is BigDecimal bdResult)
                {
                    if (bdResult >= double.MinValue && bdResult <= double.MaxValue)
                        dResult = (double)bdResult;
                }
                else
                if (result is double)
                {
                    dResult = (double)result;
                }

                if (dResult is not null)
                {
                    /*if (dResult >= float.MinValue && dResult <= float.MaxValue)
                      return (float)dResult;*/

                    return dResult;
                }
            }

            return result;
        }
        else // try to keep the original type of a
        {
            if (IsBoxedIntegerNumberOrBigNumber(typeA) && IsBoxedFloatingNumberInteger(result) == true)
            {
                long? iResult = GetBoxedIntegerNumberAsLong(result);
                if (iResult is null)
                    return result;

                if ((typeA == typeof(short)) && iResult >= short.MinValue && iResult <= short.MaxValue)
                    return (short)iResult;
                else
                if ((typeA == typeof(ushort)) && iResult >= ushort.MinValue && iResult <= ushort.MaxValue)
                    return (ushort)iResult;
                else
                if ((typeA == typeof(int)) && iResult >= int.MinValue && iResult <= int.MaxValue)
                    return (int)iResult;
                else
                if ((typeA == typeof(uint)) && iResult >= uint.MinValue && iResult <= uint.MaxValue)
                    return (uint)iResult;
                else
                if ((typeA == typeof(ulong)) && iResult >= (long) ulong.MinValue)
                    return (ulong)iResult;
                else
                if ((typeA == typeof(long)) && iResult >= long.MinValue && iResult <= long.MaxValue)
                    return (long)iResult;

                return iResult;
            }

            if ((typeA == typeof(float)) && (double)result >= float.MinValue && (double)result <= float.MaxValue)
            {
                double dResult = (double)result;
                return (float)dResult;
            }
            else
            if ((typeA == typeof(decimal)) && (result is not decimal))
            {
                try
                {
                    return ConvertToDecimal(result, options);
                }
                catch
                {
                }
            }

            return result;
        }
    }

    public static object? IntegerDivide(object? a, object? b, bool truncateFirst, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "/", options);
        b = ConvertIfNeeded(b, "/", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Integer division is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            BigInteger? biResult = null;
            if (a is BigDecimal bdA)
            {
                if (truncateFirst)
                {
                    BigInteger biA = bdA.WholeValue;
                    if (!((b is BigInteger) || (b is BigDecimal)))
                        b = ConvertToLong(b, options);
                    biResult = IntegerDivide(biA, b);
                }
                else
                {
                    biResult = IntegerDivide(bdA, b);
                }
            }
            else
            if (b is BigDecimal bdB)
            {
                if (truncateFirst)
                {
                    BigInteger biB = bdB.WholeValue;
                    if (!((a is BigInteger) || (a is BigDecimal)))
                        a = ConvertToLong(a, options);
                    biResult = IntegerDivide(a, biB);
                }
                else
                {
                    biResult = IntegerDivide(a, bdB);
                }
            }
            else
            if (a is BigInteger biA)
            {
                if (truncateFirst)
                    b = ConvertToLong(b, options);

                biResult = IntegerDivide(biA, b);
            }
            else
            if (b is BigInteger biB)
            {
                if (truncateFirst)
                    a = ConvertToLong(a, options);

                biResult = IntegerDivide(a, biB);
            }
            else
            if (a is long || a is ulong || b is long || b is ulong)
            {
                if (a is long || a is ulong)
                {
                    if (a is long la)
                        biResult = new BigInteger(la);
                    else
                        biResult = new BigInteger((ulong)a);

                    biResult = IntegerDivide(biResult.Value, b);
                }
                else
                {
                    if (b is long lb)
                        biResult = new BigInteger(lb);
                    else
                        biResult = new BigInteger((ulong)b);

                    biResult = IntegerDivide(a, biResult.Value);
                }
            }

            if (biResult is not null)
            {
                return ReduceNumericType(biResult, reduceTypes ? null : t, options);
            }
        }

        if (truncateFirst)
        {
            a = ConvertToLong(a, options);
            b = ConvertToLong(b, options);
        }

        //var func = options.OverflowProtection ? DivideFuncChecked : DivideFunc;
        object? result = null;

        try
        {
            result = ExecuteOperation(a, b, '/', ArithmeticOperation.Divide, options);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            result = Divide(a, b, reduceTypes, options);
        }

        if (result is null)
            return result;

        if (IsBoxedIntegerNumber(result))
            return ReduceNumericType(result, reduceTypes ? null : t, options);

        if (reduceTypes)
        {
            if (result is decimal decResult)
            {
                long lResult = (long)Math.Truncate(decResult);
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;

                return lResult;
            }
            if (result is double dResult)
            {
                long lResult = (long)Math.Truncate(dResult);
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;

                return lResult;
            }
            if (result is float fResult)
            {
                long lResult = (long)Math.Truncate(fResult);
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;

                return lResult;
            }
        }
        else
        {
            if (result is decimal decResult)
            {
                long lResult = (long)Math.Truncate(decResult);
                if (t == typeof(long) || lResult < Int32.MinValue || lResult > Int32.MaxValue)
                    return lResult;

                return (int)lResult;
            }
            if (result is double dResult)
            {
                long lResult = (long)Math.Truncate(dResult);
                if (t == typeof(long) || lResult < Int32.MinValue || lResult > Int32.MaxValue)
                    return lResult;

                return (int)lResult;
            }
            if (result is float fResult)
            {
                long lResult = (long)Math.Truncate(fResult);
                if (t == typeof(long) || lResult < Int32.MinValue || lResult > Int32.MaxValue)
                    return lResult;

                return (int)lResult;
            }
            else
            if (result is long lResult)
            {
                if (t == typeof(long) || lResult < Int32.MinValue || lResult > Int32.MaxValue)
                    return lResult;

                return (int)lResult;
            }
        }

        return result;
    }

    public static object? DividePercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, "/", options);
        b = ConvertIfNeeded(b, "/", options);

        //var func = options.OverflowProtection ? DividePercentFuncChecked : DividePercentFunc;
        return ExecuteOperation(a, b, '/', ArithmeticOperation.DividePercent, options);
    }

    /*public static object? Modulo(object? a, object? b)
    {
        return Modulo(a, b, CultureInfo.CurrentCulture);
    }*/

    public static object? Modulo(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        Type t = GetBroaderType(a.GetType(), b.GetType());

        a = ConvertIfNeeded(a, "%", options);
        b = ConvertIfNeeded(b, "%", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options, out var typesWereExpanded);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Modulo operation is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            BigInteger? biResult = null;
            if (a is BigDecimal bdA)
            {
                BigInteger biA = bdA.WholeValue;
                if (b is BigDecimal bdB)
                    b = bdB.WholeValue;
                else
                if (!(b is BigInteger))
                    b = ConvertToBigInteger(b);
                biResult = BigInteger.Remainder(biA, (BigInteger) b);
            }
            else
            if (b is BigDecimal bdB)
            {
                BigInteger biB = bdB.WholeValue;
                if (a is BigDecimal)
                    a = ((BigDecimal)a).WholeValue;
                else
                if (!(a is BigInteger))
                    a = ConvertToBigInteger(a);
                biResult = BigInteger.Remainder((BigInteger)a, biB);
            }
            else
            if (a is BigInteger biA)
            {
                if (!(b is BigInteger))
                    b = ConvertToBigInteger(b);
                biResult = BigInteger.Remainder(biA, (BigInteger)b);
            }
            else
            if (b is BigInteger biB)
            {
                if (!(a is BigInteger))
                    a = ConvertToBigInteger(a);
                biResult = BigInteger.Remainder((BigInteger)a, biB);
            }
            else
            if (a is long || a is ulong || b is long || b is ulong)
            {
                if (a is long || a is ulong)
                {
                    if (a is long la)
                        biResult = new BigInteger(la);
                    else
                        biResult = new BigInteger((ulong)a);

                    biResult = IntegerDivide(biResult.Value, b);
                }
                else
                {
                    if (b is long lb)
                        biResult = new BigInteger(lb);
                    else
                        biResult = new BigInteger((ulong)b);

                    biResult = IntegerDivide(a, biResult.Value);
                }
            }
            if (biResult is not null)
                return ReduceNumericType(biResult, reduceTypes ? null : t, options);
        }

        try
        {
            object result = ExecuteOperation(a, b, '%', ArithmeticOperation.Modulo, options, typeCode);
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            object? result = Modulo(a, b, reduceTypes, options);
            if (result is null)
                return null;
            return ReduceNumericType(result, reduceTypes ? null : t, options);
        }
    }

    public static object? Max(object? a, object? b, MathHelperOptions options)
    {
        int cmpResult = Compare(a, b, options, options);
        if (cmpResult == -1)
            return b;

        return a;

        /*
        if (a is null && b is null)
        {
            return null;
        }

        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        a = ConvertIfNeeded(a, "Max", options);
        b = ConvertIfNeeded(b, "Max", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Maximum is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            if (a is BigInteger biA)
            {
                return Max(b, biA);
            }
            else
            if (b is BigInteger biB)
            {
                return Max(a, biB);
            }
        }

        return typeCode switch
        {
            TypeCode.Byte => Math.Max((byte)a!, (byte)b!),
            TypeCode.SByte => Math.Max((sbyte)a!, (sbyte)b!),
            TypeCode.Int16 => Math.Max((short)a!, (short)b!),
            TypeCode.UInt16 => Math.Max((ushort)a!, (ushort)b!),
            TypeCode.Int32 => Math.Max((int)a!, (int)b!),
            TypeCode.UInt32 => Math.Max((uint)a!, (uint)b!),
            TypeCode.Int64 => Math.Max((long)a!, (long)b!),
            TypeCode.UInt64 => Math.Max((ulong)a!, (ulong)b!),
            TypeCode.Single => Math.Max((float)a!, (float)b!),
            TypeCode.Double => Math.Max((double)a!, (double)b!),
            TypeCode.Decimal => Math.Max((decimal)a!, (decimal)b!),
            _ => null,
        };*/
    }

    public static object? Min(object? a, object? b, MathHelperOptions options)
    {
        int cmpResult = Compare(a, b, options, options);
        if (cmpResult == 1)
            return b;

        return a;

        /*if (a is null && b is null)
        {
            return null;
        }

        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        a = ConvertIfNeeded(a, "Min", options);
        b = ConvertIfNeeded(b, "Min", options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Minimum is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            if (a is BigInteger biA)
            {
                return Min(b, biA);
            }
            else
            if (b is BigInteger biB)
            {
                return Min(a, biB);
            }
        }

        return typeCode switch
        {
            TypeCode.Byte => Math.Min((byte)a!, (byte)b!),
            TypeCode.SByte => Math.Min((sbyte)a!, (sbyte)b!),
            TypeCode.Int16 => Math.Min((short)a!, (short)b!),
            TypeCode.UInt16 => Math.Min((ushort)a!, (ushort)b!),
            TypeCode.Int32 => Math.Min((int)a!, (int)b!),
            TypeCode.UInt32 => Math.Min((uint)a!, (uint)b!),
            TypeCode.Int64 => Math.Min((long)a!, (long)b!),
            TypeCode.UInt64 => Math.Min((ulong)a!, (ulong)b!),
            TypeCode.Single => Math.Min((float)a!, (float)b!),
            TypeCode.Double => Math.Min((double)a!, (double)b!),
            TypeCode.Decimal => Math.Min((decimal)a!, (decimal)b!),
            _ => null
        };*/
    }

    /// <summary>
    /// The method attempts to raise the number of bits in <paramref name="a"/> and <paramref name="b"/>, update the arguments to have a new type, and return the type code of the resulting type.
    /// </summary>
    /// <param name="a">The first argument to expand.</param>
    /// <param name="b">The second argument to expand.</param>
    /// <param name="forceExpandBits">Specifies that the arguments should be expanded even when they are of the same type. This parameter is used only when handling an overflow exception, where it is known that the original type does not work.</param>
    /// <param name="options">Calculation options.</param>
    /// <returns>The type of the converted variables or <seealso cref="TypeCode.Empty"/> if arguments are not numbers or if there is nowhere more to expand the arguments (e.g., when the original type is <see langword="double"/> and big numbers are disabled). The latter case is used to indicate that attempts to expand the arguments should be stopped.</returns>
    public static TypeCode ConvertToHighestPrecision(ref object a, ref object b, bool forceExpandBits, MathHelperOptions options)
    {
        return ConvertToHighestPrecision(ref a, ref b, forceExpandBits, options, out _);
    }

    public static TypeCode ConvertToHighestPrecision(ref object a, ref object b, bool forceExpandBits, MathHelperOptions options, out bool typesWereExpanded)
    {
        typesWereExpanded = false;

        if (options.AllowCharValues)
        {
            if (a is char)
                a = Convert.ChangeType(a, TypeCode.UInt16, options.CultureInfo);
            if (b is char)
                b = Convert.ChangeType(b, TypeCode.UInt16, options.CultureInfo);
        }

        var typeCodeA = Type.GetTypeCode(a.GetType());
        var typeCodeB = Type.GetTypeCode(b.GetType());

        if (typeCodeA == typeCodeB && !forceExpandBits)
            return typeCodeA;

        if (TypeCodeBitSize(typeCodeA, out var floatingPointA) is not { } bitSizeA)
            return TypeCode.Empty;

        if (TypeCodeBitSize(typeCodeB, out var floatingPointB) is not { } bitSizeB)
            return TypeCode.Empty;

        if (options.UseBigNumbers && (typeCodeA == TypeCode.Object || typeCodeB == TypeCode.Object))
        {
            if (a is BigInteger)
            {
                if (b is BigDecimal)
                {
                    a = ConvertToBigDecimal(a);
                }
                else
                if (IsBoxedFloatingNumber(b))
                {
                    a = ConvertToBigDecimal(a);
                    b = ConvertToBigDecimal(b);
                }
                else
                if (b is not BigInteger)
                {
                    b = ConvertToBigInteger(b);
                }
                return TypeCode.Object;
            }
            if (b is BigInteger)
            {
                if (a is BigDecimal)
                {
                    b = ConvertToBigDecimal(b);
                }
                else
                if (IsBoxedFloatingNumber(a))
                {
                    a = ConvertToBigDecimal(a);
                    b = ConvertToBigDecimal(b);
                }
                else
                {
                    a = ConvertToBigInteger(a);
                }

                return TypeCode.Object;
            }
            if (a is BigDecimal)
            {
                if (b is not BigDecimal)
                b = ConvertToBigDecimal(b);
                return TypeCode.Object;
            }
            if (b is BigDecimal)
            {
                a = ConvertToBigDecimal(a);
                return TypeCode.Object;
            }
        }

        if ((floatingPointA && !floatingPointB) || (bitSizeA > bitSizeB))
        {
            try
            {
                b = Convert.ChangeType(b, typeCodeA, options.CultureInfo);
                typesWereExpanded = true;
                return typeCodeA;
            }
            catch (OverflowException)
            {
                typesWereExpanded = true;
                // the code below is used to upgrade both variables
                return TypeCodeExpandBits(typeCodeA, ref a, ref b, options);
            }
        }
        else
        if ((!floatingPointA && floatingPointB) || bitSizeB > bitSizeA)
        {
            try
            {
                a = Convert.ChangeType(a, typeCodeB, options.CultureInfo);
                typesWereExpanded = true;
                return typeCodeB;
            }
            catch (OverflowException)
            {
                typesWereExpanded = true;
                // the code below is used to upgrade both variables
                return TypeCodeExpandBits(typeCodeB, ref a, ref b, options);
            }
        }
        else // same size, different types
        {
            // the code below is used to upgrade both variables
            TypeCode resultTypeCode = TypeCodeExpandBits(typeCodeB, ref a, ref b, options);
            if (typeCodeA == typeCodeB && typeCodeA == resultTypeCode)
            {
                return TypeCode.Empty; // nowhere else to expand
            }
            else
            {
                typesWereExpanded = true;
                return resultTypeCode;
            }
        }
    }

    private static TypeCode TypeCodeExpandBits(TypeCode typeCode, ref object a, ref object b, MathHelperOptions options)
    {
        TypeCode result = TypeCode.Empty;
        switch (typeCode)
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
                result = TypeCode.Int16;
                break;
            case TypeCode.Int16:
            case TypeCode.UInt16:
                result = TypeCode.Int32;
                break;
            case TypeCode.Int32:
            case TypeCode.UInt32:
                result = TypeCode.Int64;
                break;
            case TypeCode.Int64:
            case TypeCode.UInt64:
                if (options.UseBigNumbers)
                {
                    a = ConvertToBigInteger(a);
                    b = ConvertToBigInteger(b);
                    return TypeCode.Object;
                }
                else
                {
                    result = TypeCode.Int64;
                }

                break;
            case TypeCode.Single:
                        result = TypeCode.Double;
                        break;
            case TypeCode.Double:
            case TypeCode.Decimal:
                if (options.UseBigNumbers)
                {
                    a = ConvertToBigDecimal(a);
                    b = ConvertToBigDecimal(b);
                    return TypeCode.Object;
                }
                else
                {
                    result = typeCode;
                }

                break;
            case TypeCode.Object:
                return options.UseBigNumbers ? TypeCode.Object : TypeCode.Empty;
            default:
                return TypeCode.Empty;
        }
        if (result != TypeCode.Empty && result != TypeCode.Object)
        {
            a = Convert.ChangeType(a, result, options.CultureInfo);
            b = Convert.ChangeType(b, result, options.CultureInfo);
        }
        return result;
    }

    private static int? TypeCodeBitSize(TypeCode typeCode, out bool floatingPoint)
    {
        floatingPoint = false;
        switch (typeCode)
        {
            case TypeCode.SByte:
            case TypeCode.Byte:
                return 8;
            case TypeCode.Int16:
            case TypeCode.UInt16:
                return 16;
            case TypeCode.Int32:
            case TypeCode.UInt32:
                return 32;
            case TypeCode.Int64:
            case TypeCode.UInt64:
                return 64;
            case TypeCode.Single:
                floatingPoint = true;
                return 32;
            case TypeCode.Double:
                floatingPoint = true;
                return 64;
            case TypeCode.Decimal:
                floatingPoint = true;
                return 128;
            case TypeCode.Object:
                return int.MaxValue;
            default: return null;
        }
    }

    public static Type GetBroaderType(Type typeA, Type typeB)
    {
        if (typeA == typeB)
            return typeA;

        if (typeA == typeof(BigDecimal))
            return typeA;

        if (typeB == typeof(BigDecimal))
            return typeA;

        if (typeA == typeof(BigInteger))
            return typeA;

        if (typeB == typeof(BigInteger))
            return typeB;

        if (typeA == typeof(decimal))
            return typeA;

        if (typeB == typeof(decimal))
            return typeB;

        if (typeA == typeof(double))
            return typeA;

        if (typeB == typeof(double))
            return typeB;

        if (typeA == typeof(float))
            return typeA;

        if (typeB == typeof(float))
            return typeB;

        if (typeA == typeof(long))
            return typeA;

        if (typeB == typeof(long))
            return typeB;

        if (typeA == typeof(ulong))
            return typeA;

        if (typeB == typeof(ulong))
            return typeB;

        if (typeA == typeof(int))
            return typeA;

        if (typeB == typeof(int))
            return typeB;

        if (typeA == typeof(uint))
            return typeA;

        if (typeB == typeof(uint))
            return typeB;

        if (typeA == typeof(short))
            return typeA;

        if (typeB == typeof(short))
            return typeB;

        if (typeA == typeof(ushort))
            return typeA;

        if (typeB == typeof(ushort))
            return typeB;

        if (typeA == typeof(char))
            return typeA;

        if (typeB == typeof(char))
            return typeB;

        if (typeA == typeof(sbyte))
            return typeA;

        if (typeB == typeof(sbyte))
            return typeB;

        if (typeA == typeof(byte))
            return typeA;

        if (typeB == typeof(byte))
            return typeB;

        return typeA;
    }

    public static object Abs(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                if (biA.Sign < 0)
                    return -biA;
                else
                    return biA;
            }
            if (a is BigDecimal bdA)
            {
                if (bdA.Sign < 0)
                    return -bdA;
                else
                    return bdA;
            }
        }

        if (options.DecimalAsDefault)
            return Math.Abs(ConvertToDecimal(a, options));

        return Math.Abs(ConvertToDouble(a, options));
    }

    public static object Acos(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Arccos(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Arccos(bdA);
            }
        }
        return Math.Acos(ConvertToDouble(a, options));
    }

    public static object Asin(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Arcsin(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Arcsin(bdA);
            }
        }
        return Math.Asin(ConvertToDouble(a, options));
    }

    public static object Atan(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Arctan(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Arctan(bdA);
            }
        }
        return Math.Atan(ConvertToDouble(a, options));
    }

    public static object Atan2(object? a, object? b, MathHelperOptions options)
    {
        return Math.Atan2(ConvertToDouble(a, options), ConvertToDouble(b, options));
    }

    public static object Ceiling(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return biA;
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Ceiling(bdA);
            }
        }
        if (options.DecimalAsDefault)
            return Math.Ceiling(ConvertToDecimal(a, options));

        return Math.Ceiling(ConvertToDouble(a, options));
    }

    public static object Cos(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Cos(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Cos(bdA);
            }
        }
        return Math.Cos(ConvertToDouble(a, options));
    }

    public static object Exp(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Exp(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Exp(bdA);
            }
        }
        return Math.Exp(ConvertToDouble(a, options));
    }

    public static object Floor(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Floor(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Floor(bdA);
            }
        }
        if (options.DecimalAsDefault)
            return Math.Floor(ConvertToDecimal(a, options));

        return Math.Floor(ConvertToDouble(a, options));
    }

    // ReSharper disable once InconsistentNaming
    public static object IEEERemainder(object? a, object? b, MathHelperOptions options)
    {
        return Math.IEEERemainder(ConvertToDouble(a, options), ConvertToDouble(b, options));
    }

    public static object Ln(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Ln(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Ln(bdA);
            }
        }
        return Math.Log(ConvertToDouble(a, options));
    }

    public static object Log(object? a, object? b, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Log(new BigDecimal(biA), ConvertToInt(b, options));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Log(bdA, ConvertToInt(b, options));
            }
        }
        return Math.Log(ConvertToDouble(a, options), ConvertToDouble(b, options));
    }

#if NET8_0_OR_GREATER
    public static object Log2(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Log2(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Log2(bdA);
            }
        }
        return Math.Log2(ConvertToDouble(a, options));
    }
#endif

    public static object Log10(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Log10(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Log10(bdA);
            }
        }
        return Math.Log10(ConvertToDouble(a, options));
    }

    public static object Pow(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null)
            throw new ArgumentNullException(nameof(a));
        if (b is null)
            throw new ArgumentNullException(nameof(b));

        Type typeA = a.GetType();
        if ((options.DecimalAsDefault || options.UseBigNumbers) && (IsBoxedFloatingNumberInteger(b) == true))
        {
            var @base = new BigDecimal(ConvertToDecimal(a, options));
            var exponent = new BigInteger(ConvertToDecimal(b, options));

            BigDecimal bdResult = BigDecimal.Pow(@base, exponent);
            return ReduceNumericType(bdResult, reduceTypes ? null : typeA, options) ?? throw new NCalcEvaluationException("Pow result could not be reduced to a smaller type");
        }

        double result = Math.Pow(ConvertToDouble(a, options), ConvertToDouble(b, options));
        if (typeA == typeof(decimal))
            return (decimal)result;

        return result;
    }

    public static object Factorial(object a, object b, MathHelperOptions options)
    {
        if (!options.UseBigNumbers)
        {
            return Factorial(ConvertToLong(a, options), ConvertToLong(b, options), options);
        }

        BigInteger value;

        if (a is BigInteger)
            value = (BigInteger)a;
        else
            value = ConvertToBigInteger(a);

        long step = ConvertToLong(b, options);
        if (value <= 0)
            throw new NotImplementedException("Factorial operation is not implemented for negative or zero values");
        if (step <= 0)
            throw new NotImplementedException("Factorial operation cannot be done with a negative or zero step");

        if (value.IsZero)
            return 1;
        else
        if (value.IsOne)
            return 1;
        else
        if (value == 2)
            return 2;

        var result = value;

        value = value - step;
        while (value > 1)
        {
            result = result * value;
            value = value - step;
        }

        if (result >= int.MinValue && result <= int.MaxValue)
            return (int)result;

        if (options.UseBigNumbers)
        {
            if (result >= long.MinValue && result <= long.MaxValue)
                return (long)result;
            else
                return result;
        }
        else
        {
            return (long)result;
        }
    }

    public static long Factorial(long a, long b, MathHelperOptions options)
    {
        if (a <= 0)
            throw new NotImplementedException("Factorial operation is not implemented for negative or zero values");
        if (b <= 0)
            throw new NotImplementedException("Factorial operation cannot be done with a negative or zero step");

        switch (a)
        {
            case 0:
            case 1: return 1;
            case 2: return 2;
        }

        long value = a;
        var step = b;

        var result = value;

        value = value - step;
        while (value > 1)
        {
            try
            {
                result = result * value;
            }
            catch (OverflowException)
            {
                return -1;
            }
            value = value - step;
        }

        return result;
    }

    public static object Round(object? a, object? b, MidpointRounding rounding, MathHelperOptions options)
    {
        if (a is not null && (IsBoxedIntegerNumber(a) || a is BigInteger))
            return a;

        if (a is BigDecimal bdA)
            return BigDecimal.Round(bdA, ConvertToInt(b, options), (rounding == MidpointRounding.AwayFromZero) ? RoundingStrategy.AwayFromZero : RoundingStrategy.TowardZero);

        if (options.DecimalAsDefault)
            return Math.Round(ConvertToDecimal(a, options), ConvertToInt(b, options), rounding);

        return Math.Round(ConvertToDouble(a, options), ConvertToInt(b, options), rounding);
    }

    public static object Sign(object? a, MathHelperOptions options)
    {
        if (a is BigInteger biA)
        {
            return biA.Sign;
        }
        else
        if (a is BigDecimal bdA)
        {
            return bdA.Sign;
        }

        if (options.DecimalAsDefault)
            return Math.Sign(ConvertToDecimal(a, options));

        return Math.Sign(ConvertToDouble(a, options));
    }

    public static object Sin(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Sin(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Sin(bdA);
            }
        }
        return Math.Sin(ConvertToDouble(a, options));
    }

    public static object? Sqrt(object? a, MathHelperOptions options)
    {
        if (a is null)
            return null;

        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.SquareRoot(new BigDecimal(biA), BigDecimal.Precision);
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.SquareRoot(bdA, BigDecimal.Precision);
            }
        }

        var d = ConvertToDouble(a, options);

        return Math.Sqrt(d);
    }

    public static object? Fthrt(object? a, MathHelperOptions options)
    {
        if (a is null)
            return null;

        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.SquareRoot(BigDecimal.SquareRoot(new BigDecimal(biA), BigDecimal.Precision), BigDecimal.Precision);
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.SquareRoot(BigDecimal.SquareRoot(bdA, BigDecimal.Precision), BigDecimal.Precision);
            }
        }

        var d = ConvertToDouble(a, options);

        return Math.Sqrt(Math.Sqrt(d));
    }

#if NET8_0_OR_GREATER
    public static object? Cbrt(object? a, MathHelperOptions options)
    {
        if (a is null)
            return null;

        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.NthRoot(new BigDecimal(biA), 3, BigDecimal.Precision);
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.NthRoot(bdA, 3, BigDecimal.Precision);
            }
        }

        var d = ConvertToDouble(a, options);

        return Math.Cbrt(d);
    }
#endif

    public static object Tan(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.Tan(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.Tan(bdA);
            }
        }
        return Math.Tan(ConvertToDouble(a, options));
    }

    public static object Cot(object? a, MathHelperOptions options)
    {
        if (options.UseBigNumbers)
        {
            if (a is BigInteger biA)
            {
                return BigDecimal.One / BigDecimal.Tan(new BigDecimal(biA));
            }
            if (a is BigDecimal bdA)
            {
                return BigDecimal.One /  BigDecimal.Tan(bdA);
            }
        }
        return 1 / Math.Tan(ConvertToDouble(a, options));
    }

    public static object Truncate(object? a, MathHelperOptions options)
    {
        if (options.SupportTimeOperations)
        {
            if (a is TimeSpan ts)
                return new TimeSpan((int) ts.TotalDays, 0, 0, 0);
            else
            if (a is DateTime dt)
                return dt.Date;
        }
        if (a is not null && (IsBoxedIntegerNumber(a) || a is BigInteger))
        {
            return a;
        }
        else
        if (a is BigDecimal bdA)
        {
            return bdA.WholeValue;
        }

        if (options.DecimalAsDefault)
            return Math.Truncate(ConvertToDecimal(a, options));

        return Math.Truncate(ConvertToDouble(a, options));
    }

    private static object ConvertIfNeeded(object value, string operationName, MathHelperOptions options)
    {
        try
        {
            return value switch
            {
                char ch when options is { DecimalAsDefault: true, AllowCharValues: false } => decimal.Parse(ch.ToString(), options.CultureInfo),
                string s when options is { DecimalAsDefault: true } => decimal.Parse(s, options.CultureInfo),
                char ch when options is { AllowCharValues: false } => double.Parse(ch.ToString(), options.CultureInfo),
                string s => double.Parse(s, options.CultureInfo),
                bool boolean when options.AllowBooleanCalculation => boolean ? 1 : 0,
                _ => value
            };
        }
        catch (FormatException)
        {
            if (value is string s)
            {
                string valueStr = s;
                Type targetType = options.DecimalAsDefault ? typeof(decimal) : typeof(double);
                throw new NCalcConversionException($"Conversion of a string value '{valueStr}' to type '{targetType.Name}' in the '{operationName}' operation failed as a string could not be parsed.", valueStr, typeof(string), targetType);
            }

            if (value is char c)
            {
                string valueStr = c.ToString();
                Type targetType = options.DecimalAsDefault ? typeof(decimal) : typeof(double);
                throw new NCalcConversionException($"Conversion of a string value '{valueStr}' to type '{targetType.Name}' in the '{operationName}' operation failed as a string could not be parsed.", valueStr, typeof(char), targetType);
            }

            throw;
        }
    }

    public static double ConvertToDouble(object? value, MathHelperOptions options)
    {
        return value switch
        {
            BigInteger bigI => ((double)bigI),
            BigDecimal bigD => ((double)bigD),
            double @double => @double,
            char ch => Convert.ToDouble(ch.ToString(), options.CultureInfo),
            _ => Convert.ToDouble(value, options.CultureInfo)
        };
    }

    public static decimal ConvertToDecimal(object? value, MathHelperOptions options)
    {
        return value switch
        {
            BigInteger bigI => ((decimal)bigI),
            BigDecimal bigD => ((decimal)bigD),
            decimal @decimal => @decimal,
            char ch => Convert.ToDecimal(ch.ToString(), options.CultureInfo),
            _ => Convert.ToDecimal(value, options.CultureInfo)
        };
    }

    public static int ConvertToInt(object? value, MathHelperOptions options)
    {
        return value switch
        {
            BigInteger bigI => ((int)bigI),
            BigDecimal bigD => ((int)bigD),

            int i => i,
            char ch => Convert.ToInt32(ch.ToString(), options.CultureInfo),
            _ => Convert.ToInt32(value, options.CultureInfo)
        };
    }

    public static long ConvertToLong(object? value, MathHelperOptions options)
    {
        return value switch
        {
            BigInteger bigI => ((long)bigI),
            BigDecimal bigD => ((long)(decimal)bigD),

            long l => l,
            int i => i,
            short s => s,
            sbyte sb => sb,
            char ch => Convert.ToInt64(ch.ToString(), options.CultureInfo),
            _ => Convert.ToInt64(value, options.CultureInfo)
        };
    }

    public static ulong ConvertToULong(object? value, MathHelperOptions options)
    {
        return value switch
        {
            BigInteger bigI => bigI.Sign >= 0 ? ((ulong)bigI) : throw new NCalcConversionException("A negative number cannot be converted to ulong", value.ToString() ?? string.Empty, typeof(BigInteger), typeof(ulong)),
            BigDecimal bigD => bigD.Sign >= 0 ? ((ulong)(decimal)bigD) : throw new NCalcConversionException("A negative number cannot be converted to ulong", value.ToString() ?? string.Empty, typeof(BigDecimal), typeof(ulong)),

            ulong ul => ul,
            uint ui => ui,
            ushort us => us,
            byte b => b,
            char ch => Convert.ToUInt64(ch.ToString(), options.CultureInfo),
            _ => Convert.ToUInt64(value, options.CultureInfo)
        };
    }

    private static object ExecuteOperation(object a, object b, char operatorName, ArithmeticOperation operation, MathHelperOptions options, TypeCode typeCode = TypeCode.Empty)
    {
        object origA = a, origB = b;
        if (typeCode == TypeCode.Empty)
            typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        /*
        We permit math operations on any parameters and let the runtime blow up on unsupported types
        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException($"Operator '{operatorName}' is not implemented for operands of types {origA.GetType()} and {origB.GetType()}");

        if (!IsBoxedNumber(a) && !(typeCode is TypeCode.Char))
        {
            throw new InvalidOperationException($"Operator '{operatorName}' is not implemented for operands of type '{typeCode.ToString()}'");
        }
        else
        */
        try
        {
            object? result = null;
            switch (operation)
            {
                case ArithmeticOperation.Add:
                    result = AddFunc(a, b, options);
                    break;
                case ArithmeticOperation.Subtract:
                    result = SubtractFunc(a, b, options);
                    break;
                case ArithmeticOperation.Multiply:
                    result = MultiplyFunc(a, b, options);
                    break;
                case ArithmeticOperation.Divide:
                    result = DivideFunc(a, b, options);
                    break;
                case ArithmeticOperation.Modulo:
                    result = ModuloFunc(a, b, options);
                    break;
                case ArithmeticOperation.AddPercent:
                    result = AddPercentFunc(a, b, options);
                    break;

                case ArithmeticOperation.SubtractPercent:
                    result = SubtractPercentFunc(a, b, options);
                    break;
                case ArithmeticOperation.MultiplyPercent:
                    result = MultiplyPercentFunc(a, b, options);
                    break;
                case ArithmeticOperation.DividePercent:
                    result = DividePercentFunc(a, b, options);
                    break;
            }
            if (result is null)
                throw new InvalidOperationException($"Operation '{operatorName}' is not implemented for operands of type '{typeCode.ToString()}'");

            return result;
        }
        catch(Exception ex) when (ex is not OverflowException)
        {
            throw new InvalidOperationException($"Operator '{operatorName}' is not implemented for operands of types {origA.GetType()} and {origB.GetType()}", ex);
        }
    }

    private static void CheckOverflow(dynamic value)
    {
        switch (value)
        {
            case double doubleVal when double.IsInfinity(doubleVal):
            case float floatValue when float.IsInfinity(floatValue):
                throw new OverflowException("Arithmetic operation resulted in an overflow");
        }
    }

    public static BigInteger Multiply(BigInteger a, object b)
    {
        switch (b)
        {
            case byte value: return a * value;
            case sbyte value: return a * value;
            case short value: return a * value;
            case ushort value: return a * value;
            case int value: return a * value;
            case uint value: return a * value;
            case long value: return a * value;
            case ulong value: return a * value;
            case BigInteger value: return a * value;
            default:
                throw new ArgumentException("When multiplying BigInteger, the second parameter must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", nameof(b));
        }
    }

    public static BigDecimal Multiply(BigDecimal a, object b)
    {
        switch (b)
        {
            case byte value: return a * value;
            case sbyte value: return a * value;
            case short value: return a * value;
            case ushort value: return a * value;
            case int value: return a * value;
            case uint value: return a * value;
            case long value: return a * value;
            case ulong value: return a * value;
            case float value: return a * value;
            case double value: return a * value;
            case decimal value: return a * value;
            case BigInteger value: return a * new BigDecimal(value);
            case BigDecimal value: return a * value;
            default:
                throw new ArgumentException("When multiplying BigInteger, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", nameof(b));
        }
    }

    public static BigDecimal Multiply(BigInteger a, BigDecimal b)
    {
        return new BigDecimal(a) * b;
    }

    public static BigInteger Add(BigInteger a, object b)
    {
        switch (b)
        {
            case byte value: return a + value;
            case sbyte value: return a + value;
            case short value: return a + value;
            case ushort value: return a + value;
            case int value: return a + value;
            case uint value: return a + value;
            case long value: return a + value;
            case ulong value: return a + value;
            case BigInteger value: return a + value;
            default:
                throw new ArgumentException("When adding to a BigInteger, the second parameter must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", nameof(b));
        }
    }

    public static BigDecimal Add(BigInteger a, BigDecimal b)
    {
        return new BigDecimal(a) + b;
    }

    public static BigDecimal Add(BigDecimal a, object b)
    {
        switch (b)
        {
            case byte value: return a + value;
            case sbyte value: return a + value;
            case short value: return a + value;
            case ushort value: return a + value;
            case int value: return a + value;
            case uint value: return a + value;
            case long value: return a + value;
            case ulong value: return a + value;
            case float value: return a + value;
            case double value: return a + value;
            case decimal value: return a + value;
            case BigInteger value: return a + new BigDecimal(value);
            case BigDecimal value: return a + value;
            default:
                throw new ArgumentException("When adding to a BigDecimal, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", nameof(b));
        }
    }

    public static BigInteger Subtract(BigInteger a, object b)
    {
        switch (b)
        {
            case byte value: return a - value;
            case sbyte value: return a - value;
            case short value: return a - value;
            case ushort value: return a - value;
            case int value: return a - value;
            case uint value: return a - value;
            case long value: return a - value;
            case ulong value: return a - value;
            case BigInteger value: return a - value;
            default:
                throw new ArgumentException("When subtracting from a BigInteger, the second parameter must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", nameof(b));
        }
    }

    public static BigDecimal Subtract(BigDecimal a, object b)
    {
        switch (b)
        {
            case byte value: return a - value;
            case sbyte value: return a - value;
            case short value: return a - value;
            case ushort value: return a - value;
            case int value: return a - value;
            case uint value: return a - value;
            case long value: return a - value;
            case ulong value: return a - value;
            case float value: return a - value;
            case double value: return a - value;
            case decimal value: return a - value;
            case BigInteger value: return a - new BigDecimal(value);
            case BigDecimal value: return a - value;
            default:
                throw new ArgumentException("When subtracting from a BigDecimal, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", nameof(b));
        }
    }

    public static BigInteger IntegerDivide(BigInteger a, object b)
    {
        switch (b)
        {
            case byte value: return a / value;
            case sbyte value: return a / value;
            case short value: return a / value;
            case ushort value: return a / value;
            case int value: return a / value;
            case uint value: return a / value;
            case long value: return a / value;
            case ulong value: return a / value;
            case BigInteger value: return a / value;
            default:
                throw new ArgumentException("When dividing a BigInteger, the second parameter must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", nameof(b));
        }
    }

    public static BigInteger IntegerDivide(BigDecimal a, object b)
    {
        BigDecimal result;
        switch (b)
        {
            case byte value: result = a / value; break;
            case sbyte value: result = a / value; break;
            case short value: result = a / value; break;
            case ushort value: result = a / value; break;
            case int value: result = a / value; break;
            case uint value: result = a / value; break;
            case long value: result = a / value; break;
            case ulong value: result = a / value; break;
            case BigInteger value: result = a / new BigDecimal(value); break;
            case BigDecimal value: result = a / value; break;
            default:
                throw new ArgumentException("When dividing a BigInteger, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger, BigDecimal).", nameof(b));
        }
        return result.WholeValue;
    }

    public static BigDecimal Divide(BigDecimal a, object b)
    {
        switch (b)
        {
            case byte value: return a / value;
            case sbyte value: return a / value;
            case short value: return a / value;
            case ushort value: return a / value;
            case int value: return a / value;
            case uint value: return a / value;
            case long value: return a / value;
            case ulong value: return a / value;
            case float value: return a / value;
            case double value: return a / value;
            case decimal value: return a / value;
            case BigInteger value: return a / new BigDecimal(value);
            case BigDecimal value: return a / value;
            default:
                throw new ArgumentException("When dividing a BigDecimal, the second parameter must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", nameof(b));
        }
    }

    public static BigInteger Subtract(object a, BigInteger b)
    {
        var value = ConvertToBigInteger(a);
        return value - b;
    }

    public static BigDecimal Subtract(object a, BigDecimal b)
    {
        var value = ConvertToBigDecimal(a);
        return value - b;
    }

    public static BigInteger Divide(object a, BigInteger b)
    {
        var value = ConvertToBigInteger(a);
        return value / b;
    }

    public static BigInteger IntegerDivide(object a, BigInteger b)
    {
        var value = ConvertToBigInteger(a);
        return value / b;
    }

    public static BigInteger IntegerDivide(object a, BigDecimal b)
    {
        var value = ConvertToBigDecimal(a);
        return (value / b).WholeValue;
    }

    public static BigDecimal Divide(object a, BigDecimal b)
    {
        var value = ConvertToBigDecimal(a);
        return value / b;
    }

    public static object Max(object a, BigInteger b)
    {
        BigInteger aValue = ConvertToBigInteger(a);
        return aValue > b ? a : b;
    }

    public static object Min(object a, BigInteger b)
    {
        BigInteger aValue = ConvertToBigInteger(a);
        return aValue < b ? a: b;
    }

    public static object Max(object a, BigDecimal b)
    {
        BigDecimal aValue = ConvertToBigDecimal(a);
        return aValue > b ? a : b;
    }

    public static object Min(object a, BigDecimal b)
    {
        BigDecimal aValue = ConvertToBigDecimal(a);
        return aValue < b ? a: b;
    }

    // Helper to convert boxed integer types to BigInteger
    public static BigInteger ConvertToBigInteger(object a)
    {
        switch (a)
        {
            case byte value: return value;
            case sbyte value: return value;
            case short value: return value;
            case ushort value: return value;
            case int value: return value;
            case uint value: return value;
            case long value: return value;
            case ulong value: return value;
            case BigInteger value: return value;
            default:
                throw new ArgumentException("The source of the conversion to a BigInteger must be a boxed integer type (byte, sbyte, short, ushort, int, uint, long, ulong, BigInteger).", nameof(a));
        }
    }

    public static BigDecimal ConvertToBigDecimal(object a)
    {
        switch (a)
        {
            case byte value: return new BigDecimal(value);
            case sbyte value: return new BigDecimal(value);
            case short value: return new BigDecimal(value);
            case ushort value: return new BigDecimal(value);
            case int value: return new BigDecimal(value);
            case uint value: return new BigDecimal(new BigInteger(value));
            case long value: return new BigDecimal(new BigInteger(value));
            case ulong value: return new BigDecimal(new BigInteger(value));
            case float value: return new BigDecimal(value);
            case double value: return new BigDecimal(value);
            case decimal value: return new BigDecimal(value);
            case BigInteger value: return new BigDecimal(value);
            case BigDecimal value: return value;
            default:
                throw new ArgumentException("The source of the conversion to a BigDecimal must be a boxed numeric type (byte, sbyte, short, ushort, int, uint, long, ulong, float, double, decimal, BigInteger, BigDecimal).", nameof(a));
        }
    }

    public static BigInteger? BitwiseAnd(object? a, object? b)
    {
        if (a is null || b is null)
            return null;
        BigInteger valueA = ConvertToBigInteger(a);
        BigInteger valueB = ConvertToBigInteger(b);
        return valueA & valueB;
    }

    public static BigInteger? BitwiseOr(object? a, object? b)
    {
        if (a is null || b is null)
            return null;
        BigInteger valueA = ConvertToBigInteger(a);
        BigInteger valueB = ConvertToBigInteger(b);
        return valueA | valueB;
    }

    public static BigInteger? BitwiseXOr(object? a, object? b)
    {
        if (a is null || b is null)
            return null;
        BigInteger valueA = ConvertToBigInteger(a);
        BigInteger valueB = ConvertToBigInteger(b);
        return valueA ^ valueB;
    }

    public static BigInteger? BitwiseNot(object? a)
    {
        if (a is null)
            return null;
        BigInteger valueA = ConvertToBigInteger(a);
        return ~valueA;
    }

    public static BigInteger? LeftShift(BigInteger a, object? b, MathHelperOptions options)
    {
        if (b is null)
            return null;
        int intB = ConvertToInt(b, options);
        return a << intB;
    }

    public static object? LeftShift(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is BigInteger ba)
        {
            return LeftShift(ba, b, options);
        }

        if (!MathHelper.IsBoxedIntegerNumber(a))
            throw new NCalcEvaluationException($"The left operand {a} cannot be bit-shifted");

        long step;

        if (b is BigInteger)
        {
            step = (long)b;
        }
        else
        {
            if (!MathHelper.IsBoxedIntegerNumber(b))
                throw new NCalcEvaluationException($"The right operand {b} does not define the number of bits to shift {a}");

            step = MathHelper.GetBoxedIntegerNumberAsLong(b) ?? throw new NCalcEvaluationException("");
        }

        if (step > int.MaxValue || step < 0)
            throw new NCalcEvaluationException($"The value {step} cannot be used as a number of bits to shift {a}");

        int stepInt = (int)step;

        ulong ula = Convert.ToUInt64(a, options.CultureInfo);

        if (options.UseBigNumbers && (ula > UInt32.MaxValue || step > 32))
        {
            if (reduceTypes)
                return ReduceNumericType((new BigInteger(ula)) << stepInt);

            return ((new BigInteger(ula)) << stepInt);
        }
        else
        {
            if (reduceTypes)
                return ReduceNumericType(ula << stepInt);

            return ula << stepInt;
        }
    }

    public static BigInteger? RightShift(BigInteger a, object? b, MathHelperOptions options)
    {
        if (b is null)
            return null;
        int intB = ConvertToInt(b, options);
        return a >> intB;
    }

    public static object? RightShift(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is BigInteger ba)
        {
            return RightShift(ba, b, options);
        }

        if (!MathHelper.IsBoxedIntegerNumber(a))
            throw new NCalcEvaluationException($"The left operand {a} cannot be bit-shifted");

        long step;

        if (b is BigInteger)
        {
            step = (long)b;
        }
        else
        {
            if (!MathHelper.IsBoxedIntegerNumber(b))
                throw new NCalcEvaluationException($"The right operand {b} does not define the number of bits to shift {a}");

            step = MathHelper.GetBoxedIntegerNumberAsLong(b) ?? throw new NCalcEvaluationException("");
        }

        if (step > int.MaxValue || step < 0)
            throw new NCalcEvaluationException($"The value {step} cannot be used as a number of bits to shift {a}");

        int stepInt = (int)step;

        ulong ula = Convert.ToUInt64(a, options.CultureInfo);

        if (reduceTypes)
            return ReduceNumericType(ula >> stepInt);

        return ula << stepInt;
    }

    public static int Compare(object? a, object? b, ComparisonOptions comparisonOptions, MathHelperOptions mathHelperOptions)
    {
        int result;

        // Handle possible NaN
        if (a is double dA)
        {
            if (Double.IsNaN(dA))
                return -1;
        }
        else
        if (a is float fA)
        {
            if (float.IsNaN(fA))
                return -1;
        }
        if (b is double dB)
        {
            if (Double.IsNaN(dB))
                return 1;
        }
        else
        if (b is float fB)
        {
            if (float.IsNaN(fB))
                return 1;
        }

        // Handle null
        if (a is null || b is null)
        {
            if (a is null && b is null)
                return 0;
            else
            if (a is null)
                return -1;
            else
                return 1;
        }

        // Handle bug numbers
        if (a is BigDecimal || b is BigDecimal)
        {
            if (a is BigDecimal bdA)
            {
                if (b is BigDecimal bdB)
                    result = bdA.CompareTo(bdB);
                else
                    result = bdA.CompareTo(MathHelper.ConvertToBigDecimal(b));
            }
            else
            {
                result = ((BigDecimal)b).CompareTo(MathHelper.ConvertToBigDecimal(a));
            }

            return result;
        }
        else
        if (a is BigInteger || b is BigInteger)
        {
            if (a is BigInteger biA)
            {
                if (b is BigInteger biB)
                    result = biA.CompareTo(biB);
                else
                    result = biA.CompareTo(MathHelper.ConvertToBigInteger(b));
            }
            else
            {
                result = ((BigInteger)b).CompareTo(MathHelper.ConvertToBigInteger(a));
            }

            return result;
        }

        // Handle everything else. We are not interested in the value returned by CompareUsingMostPreciseType because it would return false only when comparison of non-compatible types is allowed and in this case, the result is returned based on object hash codes.
        TypeHelper.CompareUsingMostPreciseType(a, b, comparisonOptions, mathHelperOptions, out result);
        return result;
    }

    public static long? GetBoxedIntegerNumberAsLong(object? obj)
    {
        if (obj is null)
            return null;

        try
        {
            switch (obj)
            {
                case byte b: return (long)b;
                case sbyte sb: return (long)sb;
                case short s: return (long)s;
                case ushort us: return (long)us;
                case int i: return (long)i;
                case uint ui: return (long)ui;
                case long l: return l;
                case ulong ul: if (ul < Int64.MaxValue) return (long)ul; else return null;
                case float fl: if (fl == Math.Truncate(fl)) return (long)fl; else return null;
                case double db: if (db == Math.Truncate(db)) return (long)db; else return null;
                case decimal dd: if (dd == Math.Truncate(dd)) return (long)dd; else return null;
                case BigInteger bi: return (long)bi;
                case BigDecimal bd: if (bd.GetFractionalPart().IsZero()) return (long)(bd.GetWholePart()); else return null;
                default:
                    throw new ArgumentException("Provided object is not a supported numeric type.");
            }
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    public static ulong? GetBoxedIntegerNumberAsULong(object? obj)
    {
        if (obj is null)
            return null;

        try
        {
            switch (obj)
            {
                case byte b: return (ulong)(long)b;
                case sbyte sb: if (sb > 0) return (ulong)sb; else return null;
                case short s: if (s > 0) return (ulong)s; else return null;
                case ushort us: return (ulong)(long)us;
                case int i: if (i > 0) return (ulong)i; else return null;
                case uint ui: return (ulong)ui;
                case long l: if (l > 0) return (ulong)l; else return null;
                case ulong ul: return ul;
                case float fl: if (fl == Math.Truncate(fl)) return (ulong)fl; else return null;
                case double db: if (db == Math.Truncate(db)) return (ulong)db; else return null;
                case decimal dd: if (dd == Math.Truncate(dd)) return (ulong)dd; else return null;
                case BigInteger bi: return (ulong)bi;
                case BigDecimal bd: if (bd.GetFractionalPart().IsZero()) return (ulong)(bd.GetWholePart()); else return null;
                default:
                    throw new ArgumentException("Provided object is not a supported numeric type.");
            }
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    public static double? GetBoxedNumberAsDouble(object? obj)
    {
        if (obj is null)
            return null;

        switch (obj)
        {
            case byte b: return (double)b;
            case sbyte sb: return (double)sb;
            case short s: return (double)s;
            case ushort us: return (double)us;
            case int i: return (double)i;
            case uint ui: return (double)ui;
            case long l: return (double)l;
            case ulong ul: return (double)ul;
            case float fl: return (double)fl;
            case double db: return db;
            case decimal dd: return (double)dd;
            case BigInteger bi: return (double)(long)bi;
            case BigDecimal bd: return (double)bd;
            default:
                throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static decimal? GetBoxedNumberAsDecimal(object? obj)
    {
        if (obj is null)
            return null;

        switch (obj)
        {
            case byte b: return (decimal)b;
            case sbyte sb: return (decimal)sb;
            case short s: return (decimal)s;
            case ushort us: return (decimal)us;
            case int i: return (decimal)i;
            case uint ui: return (decimal)ui;
            case long l: return (decimal)l;
            case ulong ul: return (decimal)ul;
            case float fl: return (decimal)fl;
            case double db: return (decimal)db;
            case decimal dd: return dd;
            case BigInteger bi: return (decimal)(long)bi;
            case BigDecimal bd: return (decimal)bd;
            default:
                throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool IsBoxedIntegerNumber(object? obj)
    {
        if (obj is null)
            return false;

        var t = obj.GetType();
        return t == typeof(byte) || t == typeof(sbyte) ||
               t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) ||
               t == typeof(long) || t == typeof(ulong);
    }

    public static bool IsBoxedIntegerNumberOrBigNumber(object? obj)
    {
        if (obj is null)
            return false;

        var t = obj.GetType();
        if (t == typeof(BigInteger))
        {
            return true;
        }
        else
        if (t == typeof(BigDecimal))
        {
            if (((BigDecimal)obj).GetFractionalPart() == 0)
            {
                return true;
            }
        }

        return t == typeof(byte) || t == typeof(sbyte) ||
                   t == typeof(short) || t == typeof(ushort) ||
                   t == typeof(int) || t == typeof(uint) ||
                   t == typeof(long) || t == typeof(ulong);
    }

    public static bool IsBoxedIntegerNumberOrBigNumber(Type t)
    {
        if (t == typeof(BigInteger))
        {
            return true;
        }

        return t == typeof(byte) || t == typeof(sbyte) ||
                   t == typeof(short) || t == typeof(ushort) ||
                   t == typeof(int) || t == typeof(uint) ||
                   t == typeof(long) || t == typeof(ulong);
    }

    public static bool IsBoxedNumber(object? obj)
    {
        if (obj is null)
            return false;

        var t = obj.GetType();
        return t == typeof(byte) || t == typeof(sbyte) ||
               t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) ||
               t == typeof(long) || t == typeof(ulong) ||
               t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    public static bool IsBoxedFloatingNumber(object? obj)
    {
        if (obj is null)
            return false;

        var t = obj.GetType();
        return t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    /// <summary>
    /// Checks if the given number is a floating point number and if it is, whether it contains only the whole part
    /// </summary>
    /// <param name="obj">the number to check</param>
    /// <returns><see langword="null"/> if <paramref name="obj"/> is not a floating point number, <see langword="true"/> if <paramref name="obj"/> is a floating point number without a fractional part, and <see langword="false"/> if it has a non-zero fractional part.</returns>
    public static bool? IsBoxedFloatingNumberInteger(object? obj)
    {
        if (obj is null)
            return null;

        if (obj is float ft)
        {
            return ft == Math.Truncate(ft);
        }

        if (obj is double dt)
        {
            return dt == Math.Truncate(dt);
        }

        if (obj is decimal dct)
        {
            return dct == Math.Truncate(dct);
        }

        return null;
    }

    public static bool IsBoxedNumberOrBigNumber(object? obj)
    {
        if (obj is null)
            return false;

        if (obj is BigInteger || obj is BigDecimal)
            return true;

        var t = obj.GetType();
        return t == typeof(byte) || t == typeof(sbyte) ||
               t == typeof(short) || t == typeof(ushort) ||
               t == typeof(int) || t == typeof(uint) ||
               t == typeof(long) || t == typeof(ulong) ||
               t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    public static bool IsBoxedFloatingNumberOrBigNumber(object? obj)
    {
        if (obj is null)
            return false;

        if (obj is BigDecimal)
            return true;

        var t = obj.GetType();
        return t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    public static bool? IsBoxedPositiveNumber(object? number)
    {
        if (number is null)
            return null; //throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return b > 0;
            case sbyte sb: return sb > 0;
            case short s: return s > 0;
            case ushort us: return us > 0;
            case int i: return i > 0;
            case uint ui: return ui > 0;
            case long l: return l > 0;
            case ulong ul: return ul > 0;
            case float f: return f > 0f;
            case double d: return d > 0.0;
            case decimal dec: return dec > 0m;
            case BigInteger bi: return bi > 0;
            case BigDecimal bd: return bd > 0;
            default:
                return null; //throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool? IsBoxedNegativeNumber(object? number)
    {
        if (number is null)
            return null; //throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return false;
            case sbyte sb: return sb < 0;
            case short s: return s < 0;
            case ushort us: return false;
            case int i: return i < 0;
            case uint ui: return false;
            case long l: return l < 0;
            case ulong ul: return false;
            case float f: return f < 0f;
            case double d: return d < 0.0;
            case decimal dec: return dec < 0m;
            case BigInteger bi: return bi < 0;
            case BigDecimal bd: return bd < 0;
            default:
                return null; //throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool? IsBoxedNumberZero(object? number)
    {
        if (number is null)
            return null; // throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return b == 0;
            case sbyte sb: return sb == 0;
            case short s: return s == 0;
            case ushort us: return us == 0;
            case int i: return i == 0;
            case uint ui: return ui == 0;
            case long l: return l == 0;
            case ulong ul: return ul == 0;
            case float f: return f == 0f;
            case double d: return d == 0.0;
            case decimal dec: return dec == 0m;
            case BigInteger bi: return bi.IsZero;
            case BigDecimal bd: return bd.IsZero();
            default:
                return null; //throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool? IsBoxedNumberOne(object? number)
    {
        if (number is null)
            return null; // throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return b == 1;
            case sbyte sb: return sb == 1;
            case short s: return s == 1;
            case ushort us: return us == 1;
            case int i: return i == 1;
            case uint ui: return ui == 1;
            case long l: return l == 1;
            case ulong ul: return ul == 1;
            case float f: return f == 1f;
            case double d: return d == 1d;
            case decimal dec: return dec == 1m;
            case BigInteger bi: return bi.IsOne;
            case BigDecimal bd: return (double) bd == 1;
            default:
                return null; // throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static bool? IsBoxedNumberZeroOrOne(object? number)
    {
        if (number is null)
            return null; // throw new ArgumentNullException(nameof(number));

        switch (number)
        {
            case byte b: return b == 1 || b == 0;
            case sbyte sb: return sb == 1 || sb == 0;
            case short s: return s == 1 || s == 0;
            case ushort us: return us == 1 || us == 0;
            case int i: return i == 1 || i == 0;
            case uint ui: return ui == 1 || ui == 0;
            case long l: return l == 1 || l == 0;
            case ulong ul: return ul == 1 || ul == 0;
            case float f: return f == 1f || f == 0f;
            case double d: return d == 1d || d == 0d;
            case decimal dec: return dec == 1m || dec == 0m;
            case BigInteger bi: return bi.IsOne || bi.IsZero;
            case BigDecimal bd: return bd.IsZero() || ((double) bd == 1);
            default:
                return null; // throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static object? ReduceNumericType(object value, Type? restrictToType = null, MathHelperOptions options = default)
    {
        object? lValue = value;
        if (value is BigDecimal bdValue)
        {
            lValue = MathHelper.ReduceBigDecimal(bdValue, restrictToType, false, options);
        }
        else
        if (value is BigInteger biValue)
        {
            lValue = MathHelper.ReduceBigInteger(biValue, restrictToType, options);
        }
        if (MathHelper.IsBoxedIntegerNumber(lValue))
        {
            if (restrictToType == typeof(ulong) || restrictToType == typeof(uint) || restrictToType == typeof(ushort) || restrictToType == typeof(byte) || restrictToType == typeof(char))
            {
                ulong candidate = MathHelper.ConvertToULong(lValue, options);

                if ((restrictToType is null || restrictToType == typeof(uint) || restrictToType == typeof(ushort)) && candidate <= uint.MaxValue)
                    return (uint)candidate;
                else
                if ((restrictToType is null || restrictToType == typeof(int) || restrictToType == typeof(short)) && candidate <= int.MaxValue)
                    return (int)candidate;

                return candidate;
            }

            if ((restrictToType is null || restrictToType == typeof(long) || restrictToType == typeof(int) || restrictToType == typeof(short) || restrictToType == typeof(sbyte)))
            {
                long candidate = MathHelper.ConvertToLong(lValue, options);
                if ((restrictToType is null || restrictToType == typeof(int) || restrictToType == typeof(short)) && candidate >= int.MinValue && candidate <= int.MaxValue)
                    return (int)candidate;
                else
                if ((restrictToType is null || restrictToType == typeof(uint) || restrictToType == typeof(ushort)) && candidate >= uint.MinValue && candidate <= uint.MaxValue)
                    return (uint)(ulong)candidate;

                return candidate;
            }
        }
        else
        if (IsBoxedFloatingNumberInteger(lValue) == true)
        {
            if ((restrictToType == typeof(ulong) || restrictToType == typeof(uint) || restrictToType == typeof(ushort) || restrictToType == typeof(byte) || restrictToType == typeof(char)))
            {
                ulong? candidate = GetBoxedIntegerNumberAsULong(lValue);
                if (candidate is not null)
                {
                    if (restrictToType != typeof(ulong) && candidate >= uint.MinValue && candidate <= uint.MaxValue)
                        return (uint)candidate;
                    return candidate;
                }
            }
            if ((restrictToType is null || restrictToType == typeof(long) || restrictToType == typeof(int) || restrictToType == typeof(short) || restrictToType == typeof(sbyte)))
            {
                long? candidate = GetBoxedIntegerNumberAsLong(lValue);
                if (candidate is not null)
                {
                    if (restrictToType != typeof(long) && candidate >= int.MinValue && candidate <= int.MaxValue)
                        return (int)candidate;
                    return candidate;
                }
            }
        }

        if (lValue is double dValue)
        {
            if ((/*restrictToType is null || */restrictToType == typeof(float)) && dValue >= float.MinValue && dValue <= float.MaxValue)
                return (float)dValue;
            if (restrictToType is null || restrictToType == typeof(double))
                return dValue;
        }

        // here, float and decimal are returned as is
        return lValue;
    }

    public static object? ReduceBigDecimal(BigDecimal value, Type? restrictToType = null, bool forceInteger = false, MathHelperOptions? options = default)
    {
        if (options is null)
            options = MathHelperOptions.Empty;

        BigInteger? biResult = null;

        if (forceInteger || value.GetFractionalPart().IsZero())
        {
            biResult = value.WholeValue;

            if ((restrictToType is null || restrictToType == typeof(long) || restrictToType == typeof(int) || restrictToType == typeof(short)) && biResult >= long.MinValue && biResult <= long.MaxValue)
                return (long)biResult;
            else
            if ((restrictToType is null || restrictToType == typeof(ulong) || restrictToType == typeof(uint) || restrictToType == typeof(ushort)) && biResult >= ulong.MinValue && biResult <= ulong.MaxValue)
                return (ulong)biResult;

            //return biResult;
        }

        if (((restrictToType is null && (options.Value.DecimalAsDefault)) || restrictToType == typeof(decimal)) && (value >= decimal.MinValue && value <= decimal.MaxValue))
        {
            return (decimal)value;
        }

        if (((/*restrictToType is null && */(options.Value.DecimalAsDefault != true)) || restrictToType == typeof(float)) && (value >= float.MinValue && value <= float.MaxValue))
        {
            return (float)value;
        }

        if (((restrictToType is null && (options.Value.DecimalAsDefault != true)) || restrictToType == typeof(double)) && value >= double.MinValue && value <= double.MaxValue)
        {
            return (double)value;
        }

        if (((restrictToType is null && (options.Value.DecimalAsDefault != true)) || restrictToType == typeof(decimal)) && value >= decimal.MinValue && value <= decimal.MaxValue)
        {
            return (decimal)value;
        }

        if (biResult is not null)
            return biResult;

        return value;
    }

    public static object? ReduceBigInteger(BigInteger value, Type? restrictToType = null, MathHelperOptions? options = default)
    {
        if ((restrictToType is null || restrictToType == typeof(long) || restrictToType == typeof(int) || restrictToType == typeof(short)) && value >= long.MinValue && value <= long.MaxValue)
            return (long)value;
        else
        if ((restrictToType is null || restrictToType == typeof(ulong) || restrictToType == typeof(uint) || restrictToType == typeof(ushort)) && value >= ulong.MinValue && value <= ulong.MaxValue)
            return (ulong)value;

        return value;
    }

    public static object? TryReduceToUInt64(BigInteger? value)
    {
        if (value is null)
            return null;

        if (value >= ulong.MinValue && value <= ulong.MaxValue)
            return (ulong)value;

        return value;
    }

#if !AOT_COMPILATION
    private static MethodInfo? FindOperator(
        Type opType,
        string opName)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        return opType
            .GetMethods(flags)
            .FirstOrDefault(m =>
                m.Name == opName &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType.IsAssignableFrom(opType) &&
                m.GetParameters()[1].ParameterType.IsAssignableFrom(opType)
            );
    }
#endif
}
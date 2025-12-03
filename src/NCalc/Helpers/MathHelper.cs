using System.Numerics;
using System.Reflection;

using ExtendedNumerics;

using Parlot.Fluent;

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

    private static readonly Func<dynamic, dynamic, object> DynamicAddFunc = (a, b) => unchecked(a + b);
    private static readonly Func<dynamic, dynamic, object> DynamicSubtractFunc = (a, b) => unchecked(a - b);
    private static readonly Func<dynamic, dynamic, object> DynamicMultiplyFunc = (a, b) => unchecked(a * b);
    private static readonly Func<dynamic, dynamic, object> DynamicDivideFunc = (a, b) => unchecked(a / b);
    private static readonly Func<dynamic, dynamic, object> DynamicModuloFunc = (a, b) => unchecked(a % b);

    private static readonly Func<dynamic, dynamic, object> DynamicAddPercentFunc = (a, b) => unchecked(a * (100 + b) / 100); // a / (a * b/100);
    private static readonly Func<dynamic, dynamic, object> DynamicSubtractPercentFunc = (a, b) => unchecked(a * (100 - b) / 100); //a - (a * b / 100);
    private static readonly Func<dynamic, dynamic, object> DynamicMultiplyPercentFunc = (a, b) => unchecked(a * b / 100);
    private static readonly Func<dynamic, dynamic, object> DynamicDividePercentFunc = (a, b) => unchecked(a * 100 / b);

    private static object? AddFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = AddFuncChecked(a, b);
            else
                result = DynamicAddFunc(a, b);
        }
        else
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
                result = unchecked(fa + fb);
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
                result = unchecked(da + db);
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca + dcb);
            }
            else
                result = unchecked(dca + dcb);
        }
        else
        {
            var method = FindOperator(a.GetType(), "op_Addition");
            if (method is null)
                throw new InvalidOperationException($"No overloaded addition function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
        }

        return result;
    }

    private static readonly Func<dynamic, dynamic, object> AddFuncChecked = (a, b) =>
    {
        var res = checked(a + b);
        CheckOverflow(res);

        return res;
    };

    private static object AddPercentFunc(object a, object b, MathHelperOptions options)
    {
        if (!options.AvoidDynamicFunctions)
        {
            return options.OverflowProtection ? AddPercentFuncChecked(a, b) : DynamicAddPercentFunc(a, b);
        }

        object? im1 = Add(100, b, false, options);
        if (im1 is null)
            throw new InvalidOperationException($"No addition was possible for a number and an '{b.GetType()}' object");

        object? im2 = Multiply(a, im1, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for objects of types '{a.GetType()}' and '{im1.GetType()}'");

        object? result = Divide(im2, 100, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for an '{im2.GetType()}' object and a number");

        return result;
    }

    private static readonly Func<dynamic, dynamic, object> AddPercentFuncChecked = (a, b) =>
    {
        var res = checked(a * (100 + b) / 100); //checked(a + (a * b / 100));
        CheckOverflow(res);

        return res;
    };

    private static object? SubtractFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = SubtractFuncChecked(a, b);
            else
                result = DynamicSubtractFunc(a, b);
        }
        else
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
                result = unchecked(fa - fb);
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
                result = unchecked(da - db);
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca - dcb);
            }
            else
                result = unchecked(dca - dcb);
        }
        else
        {
            var method = FindOperator(a.GetType(), "op_Subtraction");
            if (method is null)
                throw new InvalidOperationException($"No overloaded subtraction function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
        }

        return result;
    }

    private static readonly Func<dynamic, dynamic, object> SubtractFuncChecked = (a, b) =>
    {
        var res = checked(a - b);
        CheckOverflow(res);

        return res;
    };

    private static object SubtractPercentFunc(object a, object b, MathHelperOptions options)
    {
        if (!options.AvoidDynamicFunctions)
        {
            return options.OverflowProtection ? SubtractPercentFuncChecked(a, b) : DynamicSubtractPercentFunc(a, b);
        }

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

    private static readonly Func<dynamic, dynamic, object> SubtractPercentFuncChecked = (a, b) =>
    {
        var res = checked(a * (100 - b) / 100);
        CheckOverflow(res);

        return res;
    };

    private static object? MultiplyFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = MultiplyFuncChecked(a, b);
            else
                result = DynamicMultiplyFunc(a, b);
        }
        else
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
                result = unchecked(fa * fb);
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
                result = unchecked(da * db);
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca * dcb);
            }
            else
                result = unchecked(dca * dcb);
        }
        else
        {
            var method = FindOperator(a.GetType(), "op_Multiply");
            if (method is null)
                throw new InvalidOperationException($"No overloaded multiplication function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
        }

        return result;
    }

    private static readonly Func<dynamic, dynamic, object> MultiplyFuncChecked = (a, b) =>
    {
        var res = checked(a * b);
        CheckOverflow(res);

        return res;
    };

    private static object MultiplyPercentFunc(object a, object b, MathHelperOptions options)
    {
        if (!options.AvoidDynamicFunctions)
        {
            return options.OverflowProtection ? MultiplyPercentFuncChecked(a, b) : DynamicMultiplyPercentFunc(a, b);
        }

        object? im2 = Multiply(a, b, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for objects of types '{a.GetType()}' and '{b.GetType()}'");

        object? result = Divide(im2, 100, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for an '{im2.GetType()}' object and a number");

        return result;
    }

    private static readonly Func<dynamic, dynamic, object> MultiplyPercentFuncChecked = (a, b) =>
    {
        var res = checked(a * b / 100);
        CheckOverflow(res);

        return res;
    };

    private static object? DivideFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = DivideFuncChecked(a, b);
            else
                result = DynamicDivideFunc(a, b);
        }
        else
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
                result = unchecked(fa / fb);
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
                result = unchecked(da / db);
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca / dcb);
            }
            else
                result = unchecked(dca / dcb);
        }
        else
        {
            var method = FindOperator(a.GetType(), "op_Division");
            if (method is null)
                throw new InvalidOperationException($"No overloaded division function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
        }

        return result;
    }

    private static readonly Func<dynamic, dynamic, object> DivideFuncChecked = (a, b) =>
    {
        var res = checked(a / b);
        CheckOverflow(res);

        return res;
    };

    private static object DividePercentFunc(object a, object b, MathHelperOptions options)
    {
        if (!options.AvoidDynamicFunctions)
        {
            return options.OverflowProtection ? DividePercentFuncChecked(a, b) : DynamicDividePercentFunc(a, b);
        }

        object? im2 = Multiply(a, 100, false, options);
        if (im2 is null)
            throw new InvalidOperationException($"No multiplication was possible for an '{a.GetType()}' object and a number");

        object? result = Divide(im2, b, true, options);
        if (result is null)
            throw new InvalidOperationException($"No division was possible for objects of types '{im2.GetType()}' and '{b.GetType()}'");

        return result;
    }

    private static readonly Func<dynamic, dynamic, object> DividePercentFuncChecked = (a, b) =>
    {
        var res = checked(a * 100 / b);
        CheckOverflow(res);

        return res;
    };

    private static object? ModuloFunc(object a, object b, MathHelperOptions options)
    {
        object? result = null;
        if (!options.AvoidDynamicFunctions)
        {
            if (options.OverflowProtection)
                result = ModuloFuncChecked(a, b);
            else
                result = DynamicModuloFunc(a, b);
        }
        else
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
                result = unchecked(fa % fb);
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
                result = unchecked(da % db);
        }
        else
        if (a is decimal dca && b is decimal dcb)
        {
            if (options.OverflowProtection)
            {
                result = checked(dca % dcb);
            }
            else
                result = unchecked(dca % dcb);
        }
        else
        {
            var method = FindOperator(a.GetType(), "op_Modulus");
            if (method is null)
                throw new InvalidOperationException($"No overloaded modulus function found for operands of type '{a.GetType()}'");
            return method.Invoke(null, new[] { a, b });
        }

        return result;
    }

    private static readonly Func<dynamic, dynamic, object> ModuloFuncChecked = (a, b) =>
    {
        var res = checked(a % b);
        CheckOverflow(res);

        return res;
    };

    public static object? AddPercent(object? a, object? b)
    {
        return AddPercent(a, b, CultureInfo.CurrentCulture);
    }

    public static object? Add(object? a, object? b, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Addition is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            if (a is BigDecimal bdA)
            {
                return Add(bdA, b);
            }
            else
            if (b is BigDecimal bdB)
            {
                return Add(bdB, a);
            }
            else
            if (a is BigInteger biA)
            {
                return Add(biA, b);
            }
            else
            if (b is BigInteger biB)
            {
                return Add(biB, a);
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
                if (reduceTypes)
                {
                    if (result >= long.MinValue && result <= long.MaxValue)
                        return (long)result;
                    else
                    if (result >= ulong.MinValue && result <= ulong.MaxValue)
                        return (ulong)result;
                }
                return result;
            }
        }

        //var func = options.OverflowProtection ? AddFuncChecked : AddFunc;
        try
        {
            return ExecuteOperation(a, b, '+', ArithmeticOperation.Add, options, typeCode);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            return Add(a, b, reduceTypes, options);
        }
    }

    public static object? AddPercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

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

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

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

            if (bdResult != null)
            {
                if (bdResult.Value.GetFractionalPart().IsZero())
                {
                    result = bdResult.Value.WholeValue;
                }
                else
                {
                    if (reduceTypes)
                    {
                        if (options.DecimalAsDefault && (bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue))
                            return (decimal)bdResult;
                        else
                        if (bdResult >= float.MinValue && bdResult <= float.MaxValue)
                            return (float)bdResult;
                        else
                        if (bdResult >= double.MinValue && bdResult <= double.MaxValue)
                            return (double)bdResult;
                        else
                        if (bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue)
                            return (decimal)bdResult;
                    }
                    return bdResult;
                }
            }

            // If there was no BigDecimal calculation performed, proceed with the operation
            if (result == null)
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

            if (result != null && reduceTypes)
            {
                if (result >= long.MinValue && result <= long.MaxValue)
                    return (long)result;
                else
                if (result >= ulong.MinValue && result <= ulong.MaxValue)
                    return (ulong)result;

                return result;
            }
        }

        //var func = options.OverflowProtection ? SubtractFuncChecked : SubtractFunc;
        try
        {
            return ExecuteOperation(a, b, '-', ArithmeticOperation.Subtract, options, typeCode);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            return Subtract(a, b, reduceTypes, options);
        }
    }

    public static object? SubtractPercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

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

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Multiplication is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

        if (options.UseBigNumbers && typeCode == TypeCode.Object)
        {
            if (a is BigDecimal bdA)
            {
                return Multiply(bdA, b);
            }
            else
            if (b is BigDecimal bdB)
            {
                return Multiply(bdB, a);
            }
            else
            if (a is BigInteger biA)
            {
                return Multiply(biA, b);
            }
            else
            if (b is BigInteger biB)
            {
                return Multiply(biB, a);
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
                if (reduceTypes)
                {
                    if (result >= long.MinValue && result <= long.MaxValue)
                        return (long)result;
                    else
                    if (result >= ulong.MinValue && result <= ulong.MaxValue)
                        return (ulong)result;
                }
                return result;
            }
        }

        //var func = options.OverflowProtection ? MultiplyFuncChecked : MultiplyFunc;
        try
        {
            return ExecuteOperation(a, b, '*', ArithmeticOperation.Multiply, options, typeCode);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            return Multiply(a, b, reduceTypes, options);
        }
    }

    public static object? MultiplyPercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

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

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

        bool useInteger = false;

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

        if (typeCode == TypeCode.Empty)
            throw new InvalidOperationException(
                $"Division is not implemented for operands of types {a.GetType().ToString()} and {b.GetType().ToString()}");

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
            else
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
            }

            if (bdResult != null)
            {
                if (reduceTypes)
                {
                    if ((a is BigInteger || IsBoxedIntegerNumber(a) || options.ReduceDivResultToInteger) && bdResult.Value.GetFractionalPart().IsZero())
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
                    if (options.DecimalAsDefault && (bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue))
                        return (decimal)bdResult;
                    else
                    if (bdResult >= float.MinValue && bdResult <= float.MaxValue)
                        return (float)bdResult;
                    else
                    if (bdResult >= double.MinValue && bdResult <= double.MaxValue)
                        return (double)bdResult;
                    else
                    if (bdResult >= decimal.MinValue && bdResult <= decimal.MaxValue)
                        return (decimal)bdResult;
                }

                return bdResult;
            }
        }

        if (IsBoxedIntegerNumber(a) || options.ReduceDivResultToInteger)
        {
            if (a is decimal || b is decimal)
            {
                if (a != null && a.GetType() != typeof(decimal))
                    a = Convert.ChangeType(a, TypeCode.Decimal);
                else
                if (b != null && b.GetType() != typeof(decimal))
                    b = Convert.ChangeType(b, TypeCode.Decimal);

                if (a != null && b != null)
                {
                    object? modObj = Modulo(a, b, false, options);
                    if (modObj is decimal mod && mod == 0)
                        useInteger = true;
                }
            }
            else
            if (a is double || b is double)
            {
                if (a != null && a.GetType() != typeof(double))
                    a = Convert.ChangeType(a, TypeCode.Double);
                else
                if (b != null && b.GetType() != typeof(double))
                    b = Convert.ChangeType(b, TypeCode.Double);

                if (a != null && b != null)
                {
                    object? modObj = Modulo(a, b, false, options);
                    if (modObj is double mod && mod == 0)
                        useInteger = true;
                }
            }
        }

        if (a is null || b is null)
            return null;

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
        if (result == null || !useInteger)
            return result;

        if (reduceTypes)
        {
            if (result is decimal decResult)
            {
                long lResult = (long)decResult;
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;
                else
                    return lResult;
            }
            if (result is double dResult)
            {
                long lResult = (long)dResult;
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;
                else
                    return lResult;
            }
            if (result is float fResult)
            {
                long lResult = (long)fResult;
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;
                else
                    return lResult;
            }
        }

        return result;
    }

    public static object? IntegerDivide(object? a, object? b, bool truncateFirst, bool reduceTypes, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

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

            if (biResult != null)
            {
                if (reduceTypes)
                {
                    if (biResult >= long.MinValue && biResult <= long.MaxValue)
                        return (long)biResult;
                    else
                    if (biResult >= ulong.MinValue && biResult <= ulong.MaxValue)
                        return (ulong)biResult;
                }
                return biResult;
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

        if (result == null || IsBoxedIntegerNumber(result))
            return result;

        if (reduceTypes)
        {
            if (result is decimal decResult)
            {
                long lResult = (long)Math.Truncate(decResult);
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;
                else
                    return lResult;
            }
            if (result is double dResult)
            {
                long lResult = (long)Math.Truncate(dResult);
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;
                else
                    return lResult;
            }
            if (result is float fResult)
            {
                long lResult = (long)Math.Truncate(fResult);
                if (lResult >= Int32.MinValue && lResult <= Int32.MaxValue)
                    return (int)lResult;
                else
                    return lResult;
            }
        }
        return result;
    }

    public static object? DividePercent(object? a, object? b, MathHelperOptions options)
    {
        if (a is null || b is null)
            return null;

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

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

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

        TypeCode typeCode = ConvertToHighestPrecision(ref a, ref b, false, options);

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

            if (reduceTypes)
            {
                if (biResult != null)
                {
                    if (biResult >= long.MinValue && biResult <= long.MaxValue)
                        return (long)biResult;
                    else
                    if (biResult >= ulong.MinValue && biResult <= ulong.MaxValue)
                        return (ulong)biResult;

                    return biResult;
                }
            }
        }

        try
        {
            return ExecuteOperation(a, b, '%', ArithmeticOperation.Modulo, options, typeCode);
        }
        catch (OverflowException)
        {
            TypeCode newTypeCode = ConvertToHighestPrecision(ref a, ref b, true, options);
            if (newTypeCode == TypeCode.Empty)
                throw;
            return Modulo(a, b, reduceTypes, options);
        }
    }

    public static object? Max(object? a, object? b, MathHelperOptions options)
    {
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

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

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
        };
    }

    public static object? Min(object? a, object? b, MathHelperOptions options)
    {
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

        a = ConvertIfNeeded(a, options);
        b = ConvertIfNeeded(b, options);

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
        };
    }

    private static TypeCode ConvertToHighestPrecision(ref object a, ref object b, bool forceExpandBits, MathHelperOptions options)
    {
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
                    a = ConvertToBigDecimal(a);
                else
                if (b is not BigInteger)
                    b = ConvertToBigInteger(b);
                return TypeCode.Object;
            }
            if (b is BigInteger)
            {
                if (a is BigDecimal)
                    b = ConvertToBigDecimal(b);
                else
                    a = ConvertToBigInteger(a);
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
                return typeCodeA;
            }
            catch (OverflowException)
            {
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
                return typeCodeB;
            }
            catch (OverflowException)
            {
                // the code below is used to upgrade both variables
                return TypeCodeExpandBits(typeCodeB, ref a, ref b, options);
            }
        }
        else // same size, different types
        {
            // the code below is used to upgrade both variables
            TypeCode resultTypeCode = TypeCodeExpandBits(typeCodeB, ref a, ref b, options);
            if (typeCodeA == typeCodeB && typeCodeA == resultTypeCode)
                return TypeCode.Empty; // nowhere else to expand
            else
                return resultTypeCode;
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
                    result = TypeCode.Int64;
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
                    result = typeCode;
                break;
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
        if (options.DecimalAsDefault || options.UseBigNumbers)
        {
            var @base = new BigDecimal(ConvertToDecimal(a, options));
            var exponent = new BigInteger(ConvertToDecimal(b, options));

            BigDecimal result = BigDecimal.Pow(@base, exponent);
            if (result.GetFractionalPart().IsZero())
            {
                BigInteger bi = result.WholeValue;
                if (reduceTypes)
                {
                    if (bi >= int.MinValue && bi <= int.MaxValue)
                        return (int)bi;
                    if (bi >= long.MinValue && bi <= long.MaxValue)
                        return (long)bi;
                    if (bi >= ulong.MinValue && bi <= ulong.MaxValue)
                        return (ulong)bi;
                }
                if (options.UseBigNumbers)
                    return bi;
            }
            else
            if (reduceTypes)
            {
                if (options.DecimalAsDefault && (result >= decimal.MinValue && result <= decimal.MaxValue))
                    return (decimal)result;
                else
                if (result >= float.MinValue && result <= float.MaxValue)
                    return (float)result;
                else
                if (result >= double.MinValue && result <= double.MaxValue)
                    return (double)result;
                else
                if (result >= decimal.MinValue && result <= decimal.MaxValue)
                    return (decimal)result;
            }
            return result;
        }

        return Math.Pow(ConvertToDouble(a, options), ConvertToDouble(b, options));
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
            return (long)result;
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
        if (a != null && (IsBoxedIntegerNumber(a) || a is BigInteger))
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
        if (a != null && (IsBoxedIntegerNumber(a) || a is BigInteger))
            return a;
        else
        if (a is BigDecimal bdA)
        {
            return bdA.WholeValue;
        }

        if (options.DecimalAsDefault)
            return Math.Truncate(ConvertToDecimal(a, options));

        return Math.Truncate(ConvertToDouble(a, options));
    }

    private static object ConvertIfNeeded(object value, MathHelperOptions options)
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

            int i => i,
            char ch => Convert.ToInt64(ch.ToString(), options.CultureInfo),
            _ => Convert.ToInt64(value, options.CultureInfo)
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

    public static BigInteger? RightShift(BigInteger a, object? b, MathHelperOptions options)
    {
        if (b is null)
            return null;
        int intB = ConvertToInt(b, options);
        return a >> intB;
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
            return true;
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

    public static bool IsBoxedNumberZero(object? number)
    {
        if (number == null)
            throw new ArgumentNullException(nameof(number));

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
                throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }
    public static bool IsBoxedNumberOne(object? number)
    {
        if (number == null)
            throw new ArgumentNullException(nameof(number));

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
            case double d: return d == 1.1;
            case decimal dec: return dec == 1m;
            case BigInteger bi: return bi.IsOne;
            case BigDecimal bd: return (double) bd == 1;
            default:
                throw new ArgumentException("Provided object is not a supported numeric type.");
        }
    }

    public static object? ReduceNumericType(object value, bool forceInteger = false, MathHelperOptions options = default)
    {
        if (value is BigDecimal bdValue)
            return MathHelper.ReduceToSaneNumber(bdValue, false, options);
        else
        if (value is BigInteger biValue)
            return MathHelper.ReduceToSaneNumber(biValue, options);
        else
        if (MathHelper.IsBoxedIntegerNumber(value))
        {
            if (value is ulong ulValue)
            {
                if (ulValue <= int.MaxValue)
                    return (int)value;
                else
                if (ulValue <= uint.MaxValue)
                    return (uint)ulValue;
                return ulValue;
            }
            else
            {
                long candidate = MathHelper.ConvertToLong(value, options);
                if (candidate >= int.MinValue && candidate <= int.MaxValue)
                    return (int)candidate;
                else
                if (candidate >= uint.MinValue && candidate <= uint.MaxValue)
                    return (uint)candidate;
                return candidate;
            }
        }
        else
        if (options.DecimalAsDefault == true)
            return MathHelper.ConvertToDecimal(value, options);
        else
            return MathHelper.ConvertToDouble(value, options);
    }

    public static object? ReduceToSaneNumber(BigDecimal value, bool forceInteger = false, MathHelperOptions? options = default)
    {
        if (forceInteger || value.GetFractionalPart().IsZero())
        {
            BigInteger biResult = value.WholeValue;

            if (biResult >= long.MinValue && biResult <= long.MaxValue)
                return (long)biResult;
            else
            if (biResult >= ulong.MinValue && biResult <= ulong.MaxValue)
                return (ulong)biResult;

            return biResult;
        }
        else
        if ((options?.DecimalAsDefault  == true) && (value >= decimal.MinValue && value <= decimal.MaxValue))
            return (decimal)value;
        else
            if (value >= float.MinValue && value <= float.MaxValue)
            return (float)value;
        else
            if (value >= double.MinValue && value <= double.MaxValue)
            return (double)value;
        else
            if (value >= decimal.MinValue && value <= decimal.MaxValue)
            return (decimal)value;
        return value;
    }

    public static object? ReduceToSaneNumber(BigInteger value, MathHelperOptions? options = default)
    {
        if (value >= long.MinValue && value <= long.MaxValue)
            return (long)value;
        else
        if (value >= ulong.MinValue && value <= ulong.MaxValue)
            return (ulong)value;

        return value;
    }

    public static object? TryReduceToUInt64(BigInteger? value)
    {
        if (value is null)
            return null;
        if (value >= ulong.MinValue && value <= ulong.MaxValue)
            return (ulong)value;
        else
            return value;
    }

    private static MethodInfo? FindOperator(Type opType, string opName)
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
}
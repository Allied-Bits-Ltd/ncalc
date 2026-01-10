using System.Collections.Frozen;
using System.Numerics;
using System.Runtime.CompilerServices;

using ExtendedNumerics;

using NCalc.Exceptions;

namespace NCalc.Helpers;

public static class TypeHelper
{
    private static readonly Type[] BuiltInTypes =
    [
        typeof(decimal),
        typeof(double),
        typeof(float),
        typeof(long),
        typeof(ulong),
        typeof(int),
        typeof(uint),
        typeof(short),
        typeof(ushort),
        typeof(byte),
        typeof(sbyte),
        typeof(char),
        typeof(bool),
        typeof(string),
        typeof(object)
    ];

    private static readonly Type[] NumbersPrecedence =
    [
        typeof(decimal),
        typeof(double),
        typeof(float),
        typeof(ulong),
        typeof(long),
        typeof(uint),
        typeof(int),
        typeof(ushort),
        typeof(short),
        typeof(byte),
        typeof(sbyte)
    ];

    private static readonly FrozenDictionary<Type, int> NumbersPrecedenceIndex = NumbersPrecedence
        .Select((type, index) => new { type, index })
        .ToFrozenDictionary(x => x.type, x => x.index);

    public static readonly FrozenDictionary<Type, Type[]> ImplicitPrimitiveConversionTable =
        new Dictionary<Type, Type[]>
        {
            {
                typeof(sbyte),
                [typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal)]
            },
            {
                typeof(byte),
                [
                    typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
                    typeof(float),
                    typeof(double), typeof(decimal)
                ]
            },
            { typeof(short), [typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal)] },
            {
                typeof(ushort),
                [typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)]
            },
            { typeof(int), [typeof(long), typeof(float), typeof(double), typeof(decimal)] },
            { typeof(uint), [typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)] },
            { typeof(long), [typeof(float), typeof(double), typeof(decimal)] },
            {
                typeof(char),
                [
                    typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float),
                    typeof(double),
                    typeof(decimal)
                ]
            },
            { typeof(float), [typeof(double)] },
            { typeof(ulong), [typeof(float), typeof(double), typeof(decimal)] }
        }.ToFrozenDictionary();

    /// <summary>
    /// Gets the most precise type.
    /// </summary>
    /// <param name="a">Type a.</param>
    /// <param name="b">Type b.</param>
    /// <returns></returns>
    private static Type GetMostPreciseType(Type? a, Type? b)
    {
        foreach (var t in BuiltInTypes)
        {
            if (a == t || b == t)
            {
                return t;
            }
        }

        return a ?? typeof(object);
    }

    /// <summary>
    /// Gets the most precise number type.
    /// </summary>
    /// <param name="a">Type a.</param>
    /// <param name="b">Type b.</param>
    /// <returns></returns>
    public static Type? GetMostPreciseNumberType(Type a, Type b)
    {
        if (NumbersPrecedenceIndex.TryGetValue(a, out var l) && NumbersPrecedenceIndex.TryGetValue(b, out var r))
        {
            return NumbersPrecedence[Math.Min(l, r)];
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsReal(object? value) => value is decimal or double or float;

    public static StringComparer GetStringComparer(ComparisonOptions options)
    {
        return options.IsOrdinal switch
        {
            true when options.IsCaseInsensitive => StringComparer.OrdinalIgnoreCase,
            true => StringComparer.Ordinal,
            false when options.IsCaseInsensitive => StringComparer.CurrentCultureIgnoreCase,
            _ => StringComparer.CurrentCulture
        };
    }

    public static bool CompareUsingMostPreciseType(object? a, object? b, ExpressionContextBase context, out int outcome)
    {
        ComparisonOptions cmpOptions = context;
        MathHelperOptions mhOptions = context;
        return CompareUsingMostPreciseType(a, b, cmpOptions, mhOptions, out outcome);
    }

    public static bool CompareUsingMostPreciseType(object? a, object? b, ComparisonOptions comparisonOptions, MathHelperOptions mathHelperOptions, out int outcome)
    {
        bool result = false;
        object? aValue;
        object? bValue;

        try
        {
            if (MathHelper.IsBoxedIntegerNumberOrBigNumber(a) && MathHelper.IsBoxedIntegerNumberOrBigNumber(b))
            {
                object aVal = a!;
                object bVal = b!;
                TypeCode typeCode = MathHelper.ConvertToHighestPrecision(ref aVal, ref bVal, false, mathHelperOptions);
                if (typeCode == TypeCode.Empty)
                {
                    outcome = -1;
                    return false;
                }

                if (typeCode == TypeCode.Object)
                {
                    outcome = 0;

                    if (aVal is BigDecimal || bVal is BigDecimal)
                    {
                        if (aVal is BigDecimal bdA)
                        {
                            if (bVal is BigDecimal bdB)
                                outcome = bdA.CompareTo(bdB);
                            else
                                outcome = bdA.CompareTo(MathHelper.ConvertToBigDecimal(bVal));
                        }
                        else
                        {
                            outcome = ((BigDecimal)bVal).CompareTo(MathHelper.ConvertToBigDecimal(aVal));
                        }
                    }
                    else
                    if (aVal is BigInteger || bVal is BigInteger)
                    {
                        if (aVal is BigInteger biA)
                        {
                            if (bVal is BigInteger biB)
                                outcome = biA.CompareTo(biB);
                            else
                                outcome = biA.CompareTo(MathHelper.ConvertToBigInteger(bVal));
                        }
                        else
                        {
                            outcome = ((BigInteger)bVal).CompareTo(MathHelper.ConvertToBigInteger(aVal));
                        }
                    }
                    return true;
                }

                aValue = aVal;
                bValue = bVal;
            }
            else
            {
                Type mpt = GetMostPreciseType(a?.GetType(), b?.GetType());

                aValue = a is not null ? Convert.ChangeType(a, mpt, comparisonOptions.CultureInfo) : null;
                bValue = b is not null ? Convert.ChangeType(b, mpt, comparisonOptions.CultureInfo) : null;
            }

            result = true;
        }
        catch (Exception ex) // Must be InvalidCastException, but maybe some conversion throws a different one ...
        {
            if (!comparisonOptions.CompareIncompatibleTypes)
                throw new NCalcEvaluationException($"Comparison of incomparable type was attempted. The types of the operands are {a?.GetType().Name ?? "null"} and {b?.GetType().Name ?? "null"}.", ex);

            aValue = a is not null ? a.GetHashCode() : null;
            bValue = b is not null ? b.GetHashCode() : null;
        }
        var comparer = GetStringComparer(comparisonOptions);

        outcome = comparer.Compare(aValue, bValue);
        return result;
    }
}
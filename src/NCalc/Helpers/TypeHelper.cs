using System.Collections.Frozen;
using System.Numerics;
using System.Runtime.CompilerServices;

using ExtendedNumerics;

using Microsoft.Extensions.Options;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Parser;

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
            // Culture-sensitive comparison uses the culture of the expression, not the culture of the current thread
            false => StringComparer.Create(options.CultureInfo ?? CultureInfo.CurrentCulture, options.IsCaseInsensitive)
        };
    }

    /// <summary>
    /// Converts a string that is compared with a value of another type. The string is accepted only when it is a single literal
    /// in NCalc syntax, optionally with a sign, so a value that comes from the outside is never evaluated as an expression.
    /// A dedicated literal-only parser is used, and the parsed-expression cache is bypassed, as such strings are data.
    /// </summary>
    private static bool TryConvertLiteral(string value, MathHelperOptions options, out object? result)
    {
        result = null;
        try
        {
            var parser = LogicalExpressionParser.GetLiteralParser(options.CultureInfo, options.AllOptions, null);
            var context = new LogicalExpressionParserContext(value, options.AllOptions, options.CultureInfo);
            if (!parser.TryParse(context, out var parsed, out _))
                return false;

            switch (parsed)
            {
                case ValueExpression literal:
                    result = literal.Value;
                    return true;
                case UnaryExpression { Type: UnaryExpressionType.Negate, Expression: ValueExpression negated }:
                    result = MathHelper.Negate(negated.Value, options.AllOptions.HasFlag(ExpressionOptions.ReduceArithmeticResultType), options);
                    return true;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Compares two numbers exactly when both are integers or at least one of them is a big number. Integers of any size and sign are
    /// compared by value; a big number is compared as a BigDecimal when either operand has a fractional part, and infinity is greater
    /// (or smaller) than any finite number. Other pairs are left to the caller.
    /// </summary>
    internal static bool TryCompareNumbers(object? a, object? b, out int outcome)
    {
        outcome = 0;
        if (a is null || b is null)
            return false;

        bool bigA = a is BigInteger or BigDecimal, bigB = b is BigInteger or BigDecimal;
        if (MathHelper.IsBoxedIntegerNumber(a) && MathHelper.IsBoxedIntegerNumber(b))
        {
            bool unsignedA = a is byte or ushort or uint or ulong, unsignedB = b is byte or ushort or uint or ulong;
            if (!unsignedA && !unsignedB)
                outcome = Convert.ToInt64(a, CultureInfo.InvariantCulture).CompareTo(Convert.ToInt64(b, CultureInfo.InvariantCulture));
            else
            if (unsignedA && unsignedB)
                outcome = Convert.ToUInt64(a, CultureInfo.InvariantCulture).CompareTo(Convert.ToUInt64(b, CultureInfo.InvariantCulture));
            else
            {
                // A negative signed value is smaller than any unsigned value; otherwise both fit in ulong
                long signedValue = Convert.ToInt64(unsignedA ? b : a, CultureInfo.InvariantCulture);
                ulong unsignedValue = Convert.ToUInt64(unsignedA ? a : b, CultureInfo.InvariantCulture);
                int signedComparedToUnsigned = signedValue < 0 ? -1 : ((ulong)signedValue).CompareTo(unsignedValue);
                outcome = unsignedA ? -signedComparedToUnsigned : signedComparedToUnsigned;
            }
            return true;
        }

        if (!(bigA || bigB) || !MathHelper.IsBoxedNumberOrBigNumber(a) || !MathHelper.IsBoxedNumberOrBigNumber(b))
            return false;

        if (GetInfinitySign(a) is { } infinityA)
        {
            outcome = infinityA;
            return true;
        }
        if (GetInfinitySign(b) is { } infinityB)
        {
            outcome = -infinityB;
            return true;
        }
        if (a is double or float && double.IsNaN(Convert.ToDouble(a, CultureInfo.InvariantCulture)))
        {
            outcome = -1;
            return true;
        }
        if (b is double or float && double.IsNaN(Convert.ToDouble(b, CultureInfo.InvariantCulture)))
        {
            outcome = 1;
            return true;
        }

        if (a is BigInteger || MathHelper.IsBoxedIntegerNumber(a))
        {
            if (b is BigInteger || MathHelper.IsBoxedIntegerNumber(b))
            {
                outcome = MathHelper.ConvertToBigInteger(a).CompareTo(MathHelper.ConvertToBigInteger(b));
                return true;
            }
        }

        outcome = MathHelper.ConvertToBigDecimal(a).CompareTo(MathHelper.ConvertToBigDecimal(b));
        return true;
    }

    private static int? GetInfinitySign(object? value) => value switch
    {
        double d when double.IsInfinity(d) => Math.Sign(d),
        float f when float.IsInfinity(f) => Math.Sign(f),
        _ => null
    };

    private static bool IsOutsideDecimalRange(object? value) => value switch
    {
        double d => double.IsInfinity(d) || d < (double)decimal.MinValue || d > (double)decimal.MaxValue,
        float f => float.IsInfinity(f) || f < (float)decimal.MinValue || f > (float)decimal.MaxValue,
        _ => false
    };

    public static bool CompareUsingMostPreciseType(object? a, object? b, ExpressionContextBase context, out int outcome)
    {
        ComparisonOptions cmpOptions = context;
        MathHelperOptions mhOptions = context;
        return CompareUsingMostPreciseType(a, b, cmpOptions, mhOptions, out outcome);
    }

    public static bool CompareUsingMostPreciseType(object? a, object? b, ComparisonOptions comparisonOptions, MathHelperOptions mathHelperOptions, out int outcome)
    {
        bool result = false;
        object? aValue = null;
        object? bValue = null;

        try
        {
            if (TryCompareNumbers(a, b, out outcome))
            {
                return true;
            }
            else
            {
                Type? mpt = null;
                bool compareStrings = false;

                if (a is string aa && b is string bb)
                {
                    aValue = aa;
                    bValue = bb;
                    compareStrings = true;
                }
                else
                if (a is string sa)
                {
                    bool aConverted = false;
                    if (!comparisonOptions.AllOptions.HasFlag(ExpressionOptions.NoStringTypeCoercion))
                    {
                        if (TryConvertLiteral(sa, mathHelperOptions, out var aLiteral))
                        {
                            a = aLiteral;
                            aConverted = true;
                        }

                        if (!aConverted)
                        {
                            aValue = sa;
                            if (b is string stb)
                                bValue = stb;
                            else
                                bValue = Convert.ToString(b, comparisonOptions.CultureInfo) ?? "null";

                            compareStrings = true;
                        }
                    }
                    else
                    {
                        outcome = -1;
                        return false;
                    }
                }
                else
                if (b is string sb)
                {
                    bool bConverted = false;
                    if (!comparisonOptions.AllOptions.HasFlag(ExpressionOptions.NoStringTypeCoercion))
                    {
                        if (TryConvertLiteral(sb, mathHelperOptions, out var bLiteral))
                        {
                            b = bLiteral;
                            bConverted = true;
                        }

                        if (!bConverted)
                        {
                            bValue = sb;
                            if (a is string sta)
                                aValue = sta;
                            else
                                aValue = Convert.ToString(a, comparisonOptions.CultureInfo) ?? "null";

                            compareStrings = true;
                        }
                    }
                    else
                    {
                        outcome = -1;
                        return false;
                    }
                }

                if (!compareStrings)
                {
                    // A string literal converted above may now be compared exactly with a number
                    if (TryCompareNumbers(a, b, out outcome))
                        return true;

                    // A big number compared with a value of another kind (e.g. a boolean) is compared as a double
                    if (a is BigInteger or BigDecimal && !MathHelper.IsBoxedNumberOrBigNumber(b))
                        a = MathHelper.ConvertToDouble(a, mathHelperOptions);
                    if (b is BigInteger or BigDecimal && !MathHelper.IsBoxedNumberOrBigNumber(a))
                        b = MathHelper.ConvertToDouble(b, mathHelperOptions);

                    mpt = GetMostPreciseType(a?.GetType(), b?.GetType());

                    // A double beyond the range of decimal (or infinity) cannot be converted to decimal, but it can be compared with one as a double
                    if (mpt == typeof(decimal) && (IsOutsideDecimalRange(a) || IsOutsideDecimalRange(b)))
                        mpt = typeof(double);

                    aValue = a is not null ? Convert.ChangeType(a, mpt, comparisonOptions.CultureInfo) : null;
                    bValue = b is not null ? Convert.ChangeType(b, mpt, comparisonOptions.CultureInfo) : null;
                }
            }

            result = true;
        }
        catch (Exception ex) // Must be InvalidCastException, but maybe some conversion throws a different one ...
        {
            if (!comparisonOptions.CompareIncompatibleTypes)
                throw new NCalcEvaluationException($"Comparison of incomparable types was attempted. The types of the operands are {a?.GetType().Name ?? "null"} and {b?.GetType().Name ?? "null"}.", ex);

            // The values cannot be ordered; the caller treats them as unequal, so every comparison except "!=" is false
            outcome = -1;
            return false;
        }
        var comparer = GetStringComparer(comparisonOptions);

        outcome = comparer.Compare(aValue, bValue);
        return result;
    }
}
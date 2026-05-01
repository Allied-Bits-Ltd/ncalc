using System.Numerics;
using System.Text.RegularExpressions;
using ExtendedNumerics;
using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Handlers;

using NCalcVector = NCalc.Domain.Vector;

namespace NCalc.Helpers;

/// <summary>
/// Provides helper methods for evaluating expressions.
/// </summary>
public static class EvaluationHelper
{
    /// <summary>
    /// Adds two values, with special handling for string concatenation based on the context options.
    /// </summary>
    /// <param name="leftValue">The left operand.</param>
    /// <param name="rightValue">The right operand.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The result of the addition or string concatenation.</returns>
    public static object? Plus(object? leftValue, object? rightValue, ExpressionContextBase context)
    {
        if (context.Options.HasFlag(ExpressionOptions.StringConcat))
            return string.Concat(
                Convert.ToString(leftValue, context.CultureInfo),
                Convert.ToString(rightValue, context.CultureInfo));

        if (context.Options.HasFlag(ExpressionOptions.NoStringTypeCoercion) &&
            (leftValue is string || rightValue is string))
        {
            return string.Concat(Convert.ToString(leftValue, context.CultureInfo), Convert.ToString(rightValue, context.CultureInfo));
        }
        else
        if (context.Options.HasFlag(ExpressionOptions.SupportTimeOperations))
        {
            if ((leftValue is DateTime) && (MathHelper.IsBoxedIntegerNumberOrBigNumber(rightValue)))
            {
                long? newTicks = MathHelper.GetBoxedIntegerNumberAsLong(MathHelper.Add(((DateTime)leftValue).Ticks, rightValue, true, context));
                if (newTicks.HasValue)
                    return new DateTime(newTicks.Value, ((DateTime)leftValue).Kind);
            }
            else
            if ((leftValue is DateTime) && (rightValue is TimeSpan))
            {
                return ((DateTime)leftValue).Add((TimeSpan)rightValue);
            }
            else
            if ((leftValue is TimeSpan) && (MathHelper.IsBoxedIntegerNumberOrBigNumber(rightValue)))
            {
                long? newTicks = MathHelper.GetBoxedIntegerNumberAsLong(MathHelper.Add(((TimeSpan)leftValue).Ticks, rightValue, true, context));
                if (newTicks.HasValue)
                    return new TimeSpan(newTicks.Value);
            }
            else
            if ((leftValue is TimeSpan) && (rightValue is TimeSpan))
            {
                return ((TimeSpan)leftValue).Add((TimeSpan)rightValue);
            }
            else
            if ((rightValue is DateTime) && (leftValue is TimeSpan))
            {
                return ((DateTime)rightValue).Add((TimeSpan)leftValue);
            }
        }

        try
        {
            return MathHelper.Add(leftValue, rightValue, context.Options.HasFlag(ExpressionOptions.ReduceArithmeticResultType), context);
        }
        catch (NCalcConversionException) when (leftValue is string && rightValue is string)
        {
            return string.Concat(
                Convert.ToString(leftValue, context.CultureInfo),
                Convert.ToString(rightValue, context.CultureInfo));
        }
    }

    /// <summary>
    /// Subtracts the second value from the first one, with support for datetime and timespans
    /// </summary>
    /// <param name="leftValue">The left operand.</param>
    /// <param name="rightValue">The right operand.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The result of the subtraction.</returns>
    public static object? Minus(object? leftValue, object? rightValue, ExpressionContextBase context)
    {
        if (context.Options.HasFlag(ExpressionOptions.SupportTimeOperations))
        {
            if ((leftValue is DateTime) && (MathHelper.IsBoxedIntegerNumberOrBigNumber(rightValue)))
            {
                long? newTicks = MathHelper.GetBoxedIntegerNumberAsLong(MathHelper.Subtract(((DateTime)leftValue).Ticks, rightValue, true, context));
                if (newTicks.HasValue)
                    return new DateTime(newTicks.Value, ((DateTime)leftValue).Kind);
            }
            else
            if ((leftValue is TimeSpan) && (MathHelper.IsBoxedIntegerNumberOrBigNumber(rightValue)))
            {
                long? newTicks = MathHelper.GetBoxedIntegerNumberAsLong(MathHelper.Subtract(((TimeSpan)leftValue).Ticks, rightValue, true, context));
                if (newTicks.HasValue)
                    return new TimeSpan(newTicks.Value);
            }
            else
            if (leftValue is DateTime && (rightValue is DateTime))
            {
                return ((DateTime)leftValue).Subtract((DateTime)rightValue);
            }
            else
            if (leftValue is DateTime && (rightValue is TimeSpan))
            {
                return ((DateTime)leftValue).Subtract((TimeSpan)rightValue);
            }
            else
            if ((leftValue is TimeSpan) && (rightValue is TimeSpan))
            {
                return ((TimeSpan)leftValue).Subtract((TimeSpan)rightValue);
            }
        }
        return MathHelper.Subtract(leftValue, rightValue, context.Options.HasFlag(ExpressionOptions.ReduceArithmeticResultType), context);
    }

    /// <summary>
    /// Multiplies the first value and the second one, with support for timespans
    /// </summary>
    /// <param name="leftValue">The left operand.</param>
    /// <param name="rightValue">The right operand.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The result of the multiplication.</returns>
    public static object? Multiply(object? leftValue, object? rightValue, ExpressionContextBase context)
    {
        if (context.Options.HasFlag(ExpressionOptions.SupportTimeOperations))
        {
            object? ticks = null;
            if (MathHelper.IsBoxedNumberOrBigNumber(leftValue) && (rightValue is TimeSpan rts))
            {
                ticks = MathHelper.Multiply(leftValue, (object) rts.Ticks, true, context);
            }
            else
            if ((leftValue is TimeSpan lts) && MathHelper.IsBoxedNumberOrBigNumber(rightValue))
            {
                ticks = MathHelper.Multiply((object)lts.Ticks, rightValue, true, context);
            }
            if (ticks is not null)
            {
                if (ticks is int it)
                    return new TimeSpan(it);
                else
                if (ticks is uint uit)
                    return new TimeSpan(uit);
                else
                if (ticks is long lt)
                    return new TimeSpan(lt);
                else
                if (ticks is ulong ult)
                    return new TimeSpan((long)ult);
                else
                if (ticks is float ft)
                    return new TimeSpan((long)Math.Round(ft));
                else
                if (ticks is double dt)
                    return new TimeSpan((long)Math.Round(dt));
            }
        }
        return MathHelper.Multiply(leftValue, rightValue, context.Options.HasFlag(ExpressionOptions.ReduceArithmeticResultType), context);
    }

    /// <summary>
    /// Divides the first value by the second one, with support for timespans as left values.
    /// </summary>
    /// <param name="leftValue">The left operand.</param>
    /// <param name="rightValue">The right operand.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The result of the division.</returns>
    public static object? Divide(object? leftValue, object? rightValue, ExpressionContextBase context)
    {
        if (context.Options.HasFlag(ExpressionOptions.SupportTimeOperations))
        {
            if ((leftValue is TimeSpan lts) && MathHelper.IsBoxedNumberOrBigNumber(rightValue))
            {
                var ticks = MathHelper.Divide((object)lts.Ticks, rightValue, true, context);
                if (ticks is not null)
                {
                    if (ticks is int it)
                        return new TimeSpan(it);
                    else
                    if (ticks is long lt)
                        return new TimeSpan(lt);
                    else
                    if (ticks is float ft)
                        return new TimeSpan((long)Math.Round(ft));
                    else
                    if (ticks is double dt)
                        return new TimeSpan((long)Math.Round(dt));
                }
            }
        }
        return MathHelper.Divide(leftValue, rightValue, context.Options.HasFlag(ExpressionOptions.ReduceArithmeticResultType), context);
    }

    /// <summary>
    /// Determines if the left value is contained within the right value, which must be either an enumerable or a string.
    /// </summary>
    /// <param name="rightValue">The right operand.</param>
    /// <param name="leftValue">The left operand.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>True if the left value is contained within the right value, otherwise false.</returns>
    /// <exception cref="NCalcEvaluationException">Thrown when the right value is not an enumerable or a string.</exception>
    public static bool In(object? rightValue, object? leftValue, ExpressionContextBase context)
    {
        return rightValue switch
        {
            string rightValueString => Contains(leftValue, rightValueString, context),
            IEnumerable<object?> rightValueEnumerableOfObj => Contains(leftValue, rightValueEnumerableOfObj, context),
            IEnumerable rightValueEnumerable => Contains(leftValue, rightValueEnumerable, context),
            { } rightValueObject => Contains(leftValue, [rightValueObject], context),
            _ => throw new NCalcEvaluationException(
                "'in' operator right value must implement IEnumerable, be a string or an object.")
        };
    }

    private static bool Contains(object? leftValue, string rightValue, ExpressionContextBase context)
    {
        if (leftValue is not string && context.Options.HasFlag(ExpressionOptions.NoStringTypeCoercion))
        {
            return false;
        }

        var leftValueString = Convert.ToString(leftValue, CultureInfo.InvariantCulture);

        if (string.IsNullOrEmpty(leftValueString) && string.IsNullOrEmpty(rightValue))
            return true;

        if (string.IsNullOrEmpty(leftValueString))
            return false;

        return rightValue.Contains(leftValueString);
    }

    private static bool Contains(object? leftValue, IEnumerable<object?> rightValue, ExpressionContextBase context)
    {
        var rightArray = rightValue as object[] ?? rightValue.ToArray();

        var noStringTypeCoercion = context.Options.HasFlag(ExpressionOptions.NoStringTypeCoercion);

        if (rightArray.All(v => v is string))
        {
            if (noStringTypeCoercion && leftValue is not string)
            {
                return false;
            }

            return rightArray.OfType<string>().Contains(Convert.ToString(leftValue, context.CultureInfo) ?? string.Empty,
                TypeHelper.GetStringComparer(context));
        }

        return rightArray.Contains(leftValue,
            noStringTypeCoercion ? EqualityComparer<object?>.Default : StringCoercionComparer.Default);
    }

    private static bool Contains(object? leftValue, IEnumerable rightValue, ExpressionContextBase context)
    {
        // Null rightValue means nothing to iterate
        if (rightValue is null)
            return false;

        // Null leftValue means check if collection contains any null
        if (leftValue is null)
        {
            foreach (var item in rightValue)
            {
                if (item is null)
                    return true;
            }
            return false;
        }

        // Cache the runtime type of leftValue once
        var leftType = leftValue.GetType();

        //ComparisonOptions options = context;

        foreach (var item in rightValue)
        {
            if (item is null)
                continue;
            var rightType = item.GetType();
            // If the element type matches, Equals is fast and precise
            if (rightType == leftType)
            {
                if (leftValue.Equals(item))
                    return true;
            }
            else
            if (Compare(leftValue, item, ComparisonType.Equal, context))
            {
                return true;
            }
        }

        return false;
    }

    public static int Compare(object? a, object? b, ComparisonOptions comparisonOptions, MathHelperOptions mathHelperOptions)
    {
        return MathHelper.Compare(a, b, comparisonOptions, mathHelperOptions);
    }

    public static bool Compare(object? a, object? b, ComparisonType comparisonType, ExpressionContextBase context)
    {
        ComparisonOptions cmpOptions = context;
        MathHelperOptions mhOptions = context;
        return Compare(a, b, comparisonType, cmpOptions, mhOptions);
    }

    public static bool Compare(object? a, object? b, ComparisonType comparisonType, ComparisonOptions comparisonOptions, MathHelperOptions mathHelperOptions)
    {
        int result;
        if (a is double dA)
        {
            if (Double.IsNaN(dA))
                return comparisonType == ComparisonType.NotEqual;
        }
        else
        if (a is float fA)
        {
            if (float.IsNaN(fA))
                return comparisonType == ComparisonType.NotEqual;
        }
        if (b is double dB)
        {
            if (Double.IsNaN(dB))
                return comparisonType == ComparisonType.NotEqual;
        }
        else
        if (b is float fB)
        {
            if (float.IsNaN(fB))
                return comparisonType == ComparisonType.NotEqual;
        }

        if (a is null || b is null)
        {
            if (comparisonOptions.CompareNullValues)
            {
                if (a is null && b is null)
                    result = 0;
                else
                if (a is null)
                    result = -1;
                else
                    result = 1;
            }
            else
            {
                return comparisonType switch
                {
                    ComparisonType.Equal => a == b, // true if null is null
                    ComparisonType.NotEqual => a != b,
                    _ => false
                };
            }
        }
        else
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
                result = MathHelper.ConvertToBigDecimal(a).CompareTo((BigDecimal)b);
            }
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
                result = MathHelper.ConvertToBigInteger(a).CompareTo((BigInteger)b);
            }
        }
        else
        {
            TypeHelper.CompareUsingMostPreciseType(a, b, comparisonOptions, mathHelperOptions, out result);
        }

        return comparisonType switch
        {
            ComparisonType.Equal => result == 0,
            ComparisonType.Greater => result > 0,
            ComparisonType.GreaterOrEqual => result >= 0,
            ComparisonType.Less => result < 0,
            ComparisonType.LessOrEqual => result <= 0,
            ComparisonType.NotEqual => result != 0,
            _ => throw new ArgumentOutOfRangeException(nameof(comparisonType), comparisonType, null)
        };
    }

    /// <summary>
    /// Evaluates a unary expression.
    /// </summary>
    /// <param name="expression">The unary expression to evaluate.</param>
    /// <param name="result">The result of evaluating the operand of the unary expression.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The result of the unary operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the unary expression type is unknown.</exception>
    public static object? Unary(UnaryExpression expression, object? result, ExpressionContextBase context)
    {
        return expression.Type switch
        {
            UnaryExpressionType.Not => !MathHelper.ConvertToBoolean(result, "Not", context.CultureInfo, expression.Location),
            UnaryExpressionType.Negate =>
                /*(result is BigDecimal)
                    ? MathHelper.Subtract((object)(long)0, (BigDecimal)result)
                    : ((result is BigInteger)
                        ? MathHelper.TryReduceToUInt64(MathHelper.Subtract((object)(long)0, (BigInteger)result))
                        : MathHelper.Subtract(0, result, true, context)),*/
                MathHelper.Negate(result, context.Options.HasFlag(ExpressionOptions.ReduceArithmeticResultType), context),
            UnaryExpressionType.FromEnd => new NCalc.Domain.Index(MathHelper.ConvertToInt(result, "From End", context.CultureInfo, expression.Location), true),
            UnaryExpressionType.BitwiseNot =>
                (result is NCalcVector vResult) ? ~vResult :
                (result is BigInteger biResult) ? MathHelper.TryReduceToUInt64(~biResult) : ~MathHelper.ConvertToULong(result, "Bitwise Not", context.CultureInfo, expression.Location),
            UnaryExpressionType.SqRoot => MathHelper.Sqrt(result, context.CultureInfo),
#if NET8_0_OR_GREATER
            UnaryExpressionType.CbRoot => MathHelper.Cbrt(result, context.CultureInfo),
#endif
            UnaryExpressionType.FourthRoot => MathHelper.Fthrt(result, context.CultureInfo),
            UnaryExpressionType.Positive => result,
            UnaryExpressionType.Return => throw new NCalcFlowControl(result, expression.Location),
            _ => throw new InvalidOperationException("Unknown UnaryExpressionType")
        };
    }

    /// <summary>
    /// Determines whether a specified string matches a pattern using an event or SQL-like wildcards.
    /// </summary>
    /// <param name="value">The string to be compared against the pattern.</param>
    /// <param name="pattern">The pattern to match. If a default Regex-based matcher is used, '%' matches zero or more characters, and '_' matches exactly one character.</param>
    /// <param name="context">The context containing options for the comparison.</param>
    /// <returns>
    /// <c>true</c> if the <paramref name="value"/> matches the <paramref name="pattern"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The comparison is case-insensitive if the <see cref="ExpressionOptions.CaseInsensitiveStringComparer"/> flag is set in the <paramref name="context"/>.
    /// </remarks>
    public static bool Like(string value, string pattern, ExpressionContextBase context)
    {
        bool? outcome = null;
        if (context is ExpressionContext actualCtx)
        {
            if (actualCtx.MatchStringHandler is not null)
            {
                MatchStringArgs args = new MatchStringArgs(value, pattern, context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer));
                actualCtx.MatchStringHandler.Invoke(args);
                outcome = args.Matches;
            }
        }

        if (outcome.HasValue)
            return outcome.Value;

        var regexPattern = Regex.Escape(pattern)
            .Replace("%", ".*") // % matches zero or more characters
            .Replace("_", "."); // _ matches exactly one character

        var options = context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer)
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;

        // Use ^ and $ to match the start and end of the string
        return Regex.IsMatch(value, $"^{regexPattern}$", options);
    }

    /// <summary>
    /// Determines whether a specified string matches a pattern using an event or SQL-like wildcards.
    /// </summary>
    /// <param name="value">The string to be compared against the pattern.</param>
    /// <param name="pattern">The pattern to match. If a default Regex-based matcher is used, '%' matches zero or more characters, and '_' matches exactly one character.</param>
    /// <param name="context">The context containing options for the comparison.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the <paramref name="value"/> matches the <paramref name="pattern"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The comparison is case-insensitive if the <see cref="ExpressionOptions.CaseInsensitiveStringComparer"/> flag is set in the <paramref name="context"/>.
    /// </remarks>
    public static async Task<bool> LikeAsync(string value, string pattern, ExpressionContextBase context, CancellationToken cancellationToken = default)
    {
        bool? outcome = null;
        if (context is AsyncExpressionContext actualCtx)
        {
            if (actualCtx.AsyncMatchStringHandler != null)
            {
                MatchStringArgs args = new MatchStringArgs(value, pattern, context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer));

                ValueTask? task = actualCtx.AsyncMatchStringHandler?.Invoke(args, cancellationToken);
                if (task.HasValue)
                    await task.Value;

                outcome = args.Matches;
            }
        }

        if (outcome.HasValue)
            return outcome.Value;

        var regexPattern = Regex.Escape(pattern)
            .Replace("%", ".*") // % matches zero or more characters
            .Replace("_", "."); // _ matches exactly one character

        var options = context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer)
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;

        // Use ^ and $ to match the start and end of the string
        return Regex.IsMatch(value, $"^{regexPattern}$", options);
    }

    /// <summary>
    /// Determines whether a specified string matches a pattern using an event or SQL-like wildcards.
    /// </summary>
    /// <param name="value">The string to be compared against the pattern.</param>
    /// <param name="pattern">The pattern to match. If a default Regex-based matcher is used, '%' matches zero or more characters, and '_' matches exactly one character.</param>
    /// <param name="context">The context containing options for the comparison.</param>
    /// <returns>
    /// <c>true</c> if the <paramref name="value"/> matches the <paramref name="pattern"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The comparison is case-insensitive if the <see cref="ExpressionOptions.CaseInsensitiveStringComparer"/> flag is set in the <paramref name="context"/>.
    /// </remarks>
    public static bool Like(object value, object pattern, ExpressionContextBase context)
    {
        if (context.Options.HasFlag(ExpressionOptions.StrictTypeMatching))
        {
            if (pattern is not string)
                throw new NCalcEvaluationException("A pattern in LIKE and NOTLIKE operations must be a string");

            if (value is not string && value is not char)
                throw new NCalcEvaluationException("A value in LIKE and NOTLIKE operations must be a char or a string");
        }

        string? lValue = value.ToString();
        string? lPattern = pattern.ToString();

        if (lValue is null || lPattern is null)
        {
            return (lValue is null && lPattern is null);
        }

        bool? outcome = null;
        if (context is ExpressionContext actualCtx)
        {
            if (actualCtx.MatchStringHandler != null)
            {
                MatchStringArgs args = new MatchStringArgs(lValue, lPattern, context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer));
                actualCtx.MatchStringHandler?.Invoke(args);
                outcome = args.Matches;
            }
        }

        if (outcome.HasValue)
            return outcome.Value;

        var regexPattern = Regex.Escape(lPattern)
            .Replace("%", ".*") // % matches zero or more characters
            .Replace("_", "."); // _ matches exactly one character

        var options = context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer)
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;

        // Use ^ and $ to match the start and end of the string
        return Regex.IsMatch(lValue, $"^{regexPattern}$", options);
    }

    /// <summary>
    /// Determines whether a specified string matches a pattern using an event or SQL-like wildcards.
    /// </summary>
    /// <param name="value">The string to be compared against the pattern.</param>
    /// <param name="pattern">The pattern to match. If a default Regex-based matcher is used, '%' matches zero or more characters, and '_' matches exactly one character.</param>
    /// <param name="context">The context containing options for the comparison.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>
    /// <c>true</c> if the <paramref name="value"/> matches the <paramref name="pattern"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The comparison is case-insensitive if the <see cref="ExpressionOptions.CaseInsensitiveStringComparer"/> flag is set in the <paramref name="context"/>.
    /// </remarks>
    public static async Task<bool> LikeAsync(object value, object pattern, ExpressionContextBase context, CancellationToken cancellationToken = default)
    {
        if (context.Options.HasFlag(ExpressionOptions.StrictTypeMatching))
        {
            if (pattern is not string)
                throw new NCalcEvaluationException("A pattern in LIKE and NOTLIKE operations must be a string");

            if (value is not string && value is not char)
                throw new NCalcEvaluationException("A value in LIKE and NOTLIKE operations must be a char or a string");
        }

        string? lValue = value.ToString();
        string? lPattern = pattern.ToString();

        if (lValue is null || lPattern is null)
        {
            return (lValue is null && lPattern is null);
        }

        bool? outcome = null;
        if (context is AsyncExpressionContext actualCtx)
        {
            if (actualCtx.AsyncMatchStringHandler != null)
            {
                MatchStringArgs args = new MatchStringArgs(lValue, lPattern, context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer));

                ValueTask? task = actualCtx.AsyncMatchStringHandler?.Invoke(args, cancellationToken);
                if (task.HasValue)
                    await task.Value;

                outcome = args.Matches;
            }
        }

        if (outcome.HasValue)
            return outcome.Value;

        var regexPattern = Regex.Escape(lPattern)
            .Replace("%", ".*") // % matches zero or more characters
            .Replace("_", "."); // _ matches exactly one character

        var options = context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer)
            ? RegexOptions.IgnoreCase
            : RegexOptions.None;

        // Use ^ and $ to match the start and end of the string
        return Regex.IsMatch(lValue, $"^{regexPattern}$", options);
    }

    internal static void EnsureProperParamNumInFunctionCall(Function userFunction, FunctionCall functionCall)
    {
        int argsCount = functionCall.Parameters.Count;

        if (argsCount > userFunction.Parameters.Count)
        {
            if (userFunction.MandatoryParamCount < userFunction.Parameters.Count)
                throw new NCalcEvaluationException($"Too many arguments in a call to '{userFunction.Name}' which expects between {userFunction.MandatoryParamCount} and {userFunction.Parameters.Count} arguments", functionCall.Location);
            else
                throw new NCalcEvaluationException($"Too many arguments in a call to '{userFunction.Name}' which expects {userFunction.MandatoryParamCount} arguments", functionCall.Location);
        }
        if (argsCount < userFunction.MandatoryParamCount)
        {
            if (userFunction.MandatoryParamCount < userFunction.Parameters.Count)
                throw new NCalcEvaluationException($"Too few arguments in a call to '{userFunction.Name}' which expects between {userFunction.MandatoryParamCount} and {userFunction.Parameters.Count} arguments", functionCall.Location);
            else
                throw new NCalcEvaluationException($"Too few arguments in a call to '{userFunction.Name}' which expects {userFunction.MandatoryParamCount} arguments", functionCall.Location);
        }
    }

    internal static void PopulateArgumentStates(Function userFunction, FunctionCall functionCall, Dictionary<string, ArgumentStateBase> argumentStates, ExpressionContextBase context, Func<LogicalExpression, ExpressionContextBase, ArgumentStateBase> stateFactory)
    {
        ArgumentStateBase? state;
        string paramName;
        bool ignoreCase = context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup);
        for (int i = 0; i < userFunction.Parameters.Count; i++)
        {
            if (i < functionCall.Parameters.Count)
                state = stateFactory(functionCall.Parameters[i], context);
            else
            if (userFunction.Parameters[i].IsOptional)
                state = stateFactory(userFunction.Parameters[i].DefaultValue ?? new ValueExpression(), context);
            else
                state = stateFactory(new ValueExpression(), context);
            paramName = ignoreCase ? userFunction.Parameters[i].Name.ToLowerInvariant() : userFunction.Parameters[i].Name;
            argumentStates.Add(paramName, state);
        }
    }

    internal static bool GetFunctionFromListNoCase(IDictionary<string, Function> dictionary, string functionName, out Function? function)
    {
        functionName = functionName.ToUpperInvariant();
        KeyValuePair<string, Function>? functionPair = dictionary.FirstOrDefault((f) => f.Key.ToUpperInvariant() == functionName);
        if (functionPair.HasValue && !string.IsNullOrEmpty(functionPair.Value.Key))
        {
            function = functionPair.Value.Value;
            return true;
        }

        function = null;
        return false;
    }

    internal static bool GetParameterValueFromListNoCase(IDictionary<string, object?> dictionary, string parameterName, out object? value)
    {
        parameterName = parameterName.ToUpperInvariant();
        KeyValuePair<string, object?>? paramPair = dictionary.FirstOrDefault((f) => f.Key.ToUpperInvariant() == parameterName);
        if (paramPair.HasValue && !string.IsNullOrEmpty(paramPair.Value.Key))
        {
            value = paramPair.Value.Value;
            return true;
        }

        value = null;
        return false;
    }

    internal static void SetParameterValueFromListNoCase(IDictionary<string, object?> dictionary, string parameterName, object? value)
    {
        var lParameterName = parameterName.ToUpperInvariant();
        string? paramKey = dictionary.Keys.FirstOrDefault((k) => k.ToUpperInvariant() == lParameterName);
        if (!string.IsNullOrEmpty(paramKey))
            dictionary[paramKey] = value;
        else
            dictionary[parameterName] = value;
    }
}

internal class ArgumentStateBase
{
    protected object? _value = null;
    protected bool _valueSet = false;
}
using System.Numerics;
using System.Runtime.CompilerServices;
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
    private const string ExpressionTooDeepMessage = "The expression is nested too deeply to be evaluated recursively. Use ExpressionOptions.UseNonRecursiveEvaluator to evaluate deeply nested or very long expressions.";

    /// <summary>
    /// Throws an <see cref="NCalcEvaluationException"/> when the remaining stack space is too small to evaluate the given node recursively,
    /// instead of letting the process terminate with an uncatchable StackOverflowException.
    /// </summary>
    /// <param name="expression">The node about to be evaluated.</param>
    internal static void EnsureSufficientExecutionStack(LogicalExpression expression)
    {
#if NET
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            throw new NCalcEvaluationException(ExpressionTooDeepMessage, expression.Location);
#else
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
        }
        catch (InsufficientExecutionStackException ex)
        {
            throw new NCalcEvaluationException(ExpressionTooDeepMessage, expression.Location, ex);
        }
#endif
    }

    private const string ExpressionTooDeepNonRecursiveMessage = "The expression is nested too deeply to be evaluated: function calls or expressions used as parameter values are nested too deeply for the stack of the thread. The non-recursive evaluator continues them on threads with a larger stack unless LogicalExpressionParser.DeepExpressionStackSize is 0.";

    internal static bool HasSufficientExecutionStack()
    {
#if NET
        return RuntimeHelpers.TryEnsureSufficientExecutionStack();
#else
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            return true;
        }
        catch (InsufficientExecutionStackException)
        {
            return false;
        }
#endif
    }

    /// <summary>
    /// Runs a part of a non-recursive evaluation that needs the stack (a function call, whose arguments are evaluated as separate
    /// expressions, or a parameter whose value is evaluated as an expression) on a new thread with a stack of
    /// <see cref="Parser.LogicalExpressionParser.DeepExpressionStackSize"/> bytes, once the stack of the current thread is nearly used up.
    /// Each such thread provides room for thousands of further levels of nested function calls. The calling thread waits meanwhile, and
    /// the handlers called during that part run on the new thread. When the size is 0 or the platform cannot start a thread,
    /// throws an <see cref="NCalcEvaluationException"/>.
    /// </summary>
    internal static T EvaluateOnLargeStack<T>(Func<T> evaluate, LogicalExpression expression)
    {
        var stackSize = Parser.LogicalExpressionParser.DeepExpressionStackSize;
#if NET
        if (OperatingSystem.IsBrowser())
            stackSize = 0;
#endif
        if (stackSize <= 0)
            throw new NCalcEvaluationException(ExpressionTooDeepNonRecursiveMessage, expression.Location);

        // The values may be formatted or parsed with the current culture, which a new thread would not have
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        T result = default!;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? error = null;

        var thread = new Thread(() =>
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            try
            {
                result = evaluate();
            }
            catch (Exception ex)
            {
                error = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex);
            }
        }, stackSize);

        try
        {
            thread.Start();
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or OutOfMemoryException)
        {
            throw new NCalcEvaluationException(ExpressionTooDeepNonRecursiveMessage, expression.Location, ex);
        }
        thread.Join();

        error?.Throw();
        return result;
    }

    /// <summary>
    /// Like <see cref="EvaluateOnLargeStack{T}(Func{T}, LogicalExpression)"/>, for the asynchronous non-recursive evaluator: continues
    /// the part on a thread pool thread, whose stack is empty, while the calling one waits without using a thread. The handlers called
    /// during that part run on the thread pool thread, without the synchronization context of the caller. When
    /// <see cref="Parser.LogicalExpressionParser.DeepExpressionStackSize"/> is 0, throws an <see cref="NCalcEvaluationException"/>.
    /// </summary>
    internal static async ValueTask<object?> EvaluateOnThreadPoolAsync(Func<ValueTask<object?>> evaluate, LogicalExpression expression)
    {
        if (Parser.LogicalExpressionParser.DeepExpressionStackSize <= 0)
            throw new NCalcEvaluationException(ExpressionTooDeepNonRecursiveMessage, expression.Location);

        return await Task.Run(() => evaluate().AsTask()).ConfigureAwait(false);
    }

    // The number of asynchronous evaluations that an exception has come out of
    private static readonly ConditionalWeakTable<Exception, StrongBox<int>> AsyncEvaluationExits = new();

    // An exception that comes out of this many nested asynchronous evaluations keeps its whole stack trace
    private const int EvaluationsWithFullStackTrace = 32;

    /// <summary>
    /// Called when an exception comes out of an asynchronous evaluation. Each await on the way of an exception appends the stack trace
    /// so far to the exception once more, so an exception coming out of deeply nested evaluations (function arguments, bodies of user
    /// functions, expressions used as parameters) would take time that grows with the square of the depth. Returns true when the
    /// exception has come out of more than 32 evaluations, for every 4th one: the evaluation throws the exception anew, which starts a new
    /// stack trace. The stack trace of such an exception shows the outer evaluations only.
    /// </summary>
    internal static bool ShouldRestartStackTrace(Exception exception)
    {
        var exits = Interlocked.Increment(ref AsyncEvaluationExits.GetOrCreateValue(exception).Value);
        return exits > EvaluationsWithFullStackTrace && exits % 4 == 0;
    }

    private const string ExpressionTooDeepToVisitMessage = "The expression is nested too deeply to be processed";

    /// <summary>
    /// Throws an <see cref="NCalcException"/> when the remaining stack space is too small for a recursive visitor other than the evaluators
    /// (serialization, extraction of parameters and functions, lambda compilation) to visit one more level of the tree,
    /// instead of letting the process terminate with an uncatchable StackOverflowException.
    /// </summary>
    internal static void EnsureSufficientExecutionStackToVisit()
    {
#if NET
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            throw new NCalcException(ExpressionTooDeepToVisitMessage);
#else
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
        }
        catch (InsufficientExecutionStackException)
        {
            throw new NCalcException(ExpressionTooDeepToVisitMessage);
        }
#endif
    }

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
            return ConcatStrings(leftValue, rightValue, context);

        if (context.Options.HasFlag(ExpressionOptions.NoStringTypeCoercion) &&
            (leftValue is string || rightValue is string))
        {
            return ConcatStrings(leftValue, rightValue, context);
        }
        else
        if (context.Options.HasFlag(ExpressionOptions.SupportTimeOperations))
        {
            if ((leftValue is DateTime) && (MathHelper.IsBoxedIntegerNumberOrBigNumber(rightValue)))
            {
                return ToDateTime(AddTicks(((DateTime)leftValue).Ticks, rightValue!, false), ((DateTime)leftValue).Kind);
            }
            else
            if ((leftValue is DateTime) && (rightValue is TimeSpan))
            {
                return TimeOperation(() => ((DateTime)leftValue).Add((TimeSpan)rightValue), nameof(DateTime));
            }
            else
            if ((leftValue is TimeSpan) && (MathHelper.IsBoxedIntegerNumberOrBigNumber(rightValue)))
            {
                return ToTimeSpan(AddTicks(((TimeSpan)leftValue).Ticks, rightValue!, false));
            }
            else
            if ((leftValue is TimeSpan) && (rightValue is TimeSpan))
            {
                return TimeOperation(() => ((TimeSpan)leftValue).Add((TimeSpan)rightValue), nameof(TimeSpan));
            }
            else
            if ((rightValue is DateTime) && (leftValue is TimeSpan))
            {
                return TimeOperation(() => ((DateTime)rightValue).Add((TimeSpan)leftValue), nameof(DateTime));
            }
        }

        try
        {
            return MathHelper.Add(leftValue, rightValue, context.Options.HasFlag(ExpressionOptions.ReduceArithmeticResultType), context);
        }
        catch (NCalcConversionException) when (leftValue is string && rightValue is string)
        {
            return ConcatStrings(leftValue, rightValue, context);
        }
    }

    /// <summary>
    /// Concatenates the text of two values. The result may have at most <see cref="BuiltInFunctionHelper.MaxMakeResultLength"/>
    /// characters, as the results of MakeStr(), so that doubling a string in a loop (s = s + s) cannot exhaust the memory.
    /// </summary>
    private static string ConcatStrings(object? leftValue, object? rightValue, ExpressionContextBase context)
    {
        string left = Convert.ToString(leftValue, context.CultureInfo) ?? string.Empty;
        string right = Convert.ToString(rightValue, context.CultureInfo) ?? string.Empty;
        long length = (long)left.Length + right.Length;
        if (length > BuiltInFunctionHelper.MaxMakeResultLength)
            throw new NCalcEvaluationException($"The concatenated string would have {length} characters, which exceeds the limit of {BuiltInFunctionHelper.MaxMakeResultLength} (BuiltInFunctionHelper.MaxMakeResultLength)");
        return string.Concat(left, right);
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
                return ToDateTime(AddTicks(((DateTime)leftValue).Ticks, rightValue!, true), ((DateTime)leftValue).Kind);
            }
            else
            if ((leftValue is TimeSpan) && (MathHelper.IsBoxedIntegerNumberOrBigNumber(rightValue)))
            {
                return ToTimeSpan(AddTicks(((TimeSpan)leftValue).Ticks, rightValue!, true));
            }
            else
            if (leftValue is DateTime && (rightValue is DateTime))
            {
                return ((DateTime)leftValue).Subtract((DateTime)rightValue);
            }
            else
            if (leftValue is DateTime && (rightValue is TimeSpan))
            {
                return TimeOperation(() => ((DateTime)leftValue).Subtract((TimeSpan)rightValue), nameof(DateTime));
            }
            else
            if ((leftValue is TimeSpan) && (rightValue is TimeSpan))
            {
                return TimeOperation(() => ((TimeSpan)leftValue).Subtract((TimeSpan)rightValue), nameof(TimeSpan));
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
            // The product is computed exactly (or in double for a floating-point factor) and rounded to the nearest tick
            if (MathHelper.IsBoxedNumberOrBigNumber(leftValue) && (rightValue is TimeSpan rts))
            {
                return ToTimeSpan(MultiplyTicks(rts.Ticks, leftValue!));
            }
            else
            if ((leftValue is TimeSpan lts) && MathHelper.IsBoxedNumberOrBigNumber(rightValue))
            {
                return ToTimeSpan(MultiplyTicks(lts.Ticks, rightValue!));
            }
        }
        return MathHelper.Multiply(leftValue, rightValue, context.Options.HasFlag(ExpressionOptions.ReduceArithmeticResultType), context);
    }

    /// <summary>
    /// Tells whether the left operand of a division is converted to double before dividing, so that dividing two integers gives a fractional result.
    /// Floating-point and big numbers, complex numbers, vectors and timespans keep their type. All evaluators use this check.
    /// </summary>
    /// <param name="leftValue">The left operand.</param>
    /// <param name="rightValue">The right operand.</param>
    /// <returns><see langword="true"/> if the left operand is converted to double.</returns>
    public static bool ShouldConvertDividendToDouble(object? leftValue, object? rightValue)
        => !(TypeHelper.IsReal(leftValue) || TypeHelper.IsReal(rightValue)
            || leftValue is BigInteger || rightValue is BigInteger
            || leftValue is BigDecimal || rightValue is BigDecimal
            || leftValue is ComplexNumber || rightValue is ComplexNumber
            || leftValue is NCalcVector || rightValue is NCalcVector
            || leftValue is TimeSpan);

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
            // The quotient is rounded to the nearest tick, as TimeSpan / double does, whatever the type of the divisor
            if ((leftValue is TimeSpan lts) && MathHelper.IsBoxedNumberOrBigNumber(rightValue))
            {
                return ToTimeSpan(DivideTicks(lts.Ticks, rightValue!));
            }
        }
        return MathHelper.Divide(leftValue, rightValue, context.Options.HasFlag(ExpressionOptions.ReduceArithmeticResultType), context);
    }

    // Time arithmetic: the number of ticks is computed exactly or in floating point, rounded to the nearest tick (midpoints to even,
    // as TimeSpan / double does), and a result outside the range of TimeSpan or DateTime throws NCalcEvaluationException
    // instead of wrapping around or throwing a .NET exception.
    private static object AddTicks(long ticks, object amount, bool subtract)
    {
        if (amount is BigDecimal bdAmount)
            return subtract ? MathHelper.ConvertToBigDecimal(ticks) - bdAmount : MathHelper.ConvertToBigDecimal(ticks) + bdAmount;

        BigInteger integerAmount = MathHelper.ConvertToBigInteger(amount);
        return subtract ? ticks - integerAmount : ticks + integerAmount;
    }

    private static object MultiplyTicks(long ticks, object factor)
    {
        return factor switch
        {
            double d => (object)(ticks * d),
            float f => (object)(ticks * (double)f),
            decimal or BigDecimal => (object)(MathHelper.ConvertToBigDecimal(ticks) * MathHelper.ConvertToBigDecimal(factor)),
            _ => (object)(ticks * MathHelper.ConvertToBigInteger(factor))
        };
    }

    private static object DivideTicks(long ticks, object divisor)
    {
        switch (divisor)
        {
            case double d:
                return ticks / d;
            case float f:
                return ticks / (double)f;
            case decimal or BigDecimal:
                BigDecimal bdDivisor = MathHelper.ConvertToBigDecimal(divisor);
                if (bdDivisor.IsZero())
                    throw new DivideByZeroException();
                return MathHelper.Divide(MathHelper.ConvertToBigDecimal(ticks), (object)bdDivisor);
            default:
                // Integers are divided exactly, and the remainder decides the rounding
                BigInteger integerDivisor = MathHelper.ConvertToBigInteger(divisor);
                BigInteger quotient = BigInteger.DivRem(ticks, integerDivisor, out BigInteger remainder);
                BigInteger twiceRemainder = BigInteger.Abs(remainder) * 2;
                BigInteger absDivisor = BigInteger.Abs(integerDivisor);
                if (twiceRemainder > absDivisor || (twiceRemainder == absDivisor && !quotient.IsEven))
                    quotient += (ticks < 0) == (integerDivisor.Sign < 0) ? 1 : -1;
                return quotient;
        }
    }

    private static long ToTicks(object ticks, long minTicks, long maxTicks, string typeName)
    {
        BigInteger wholeTicks = ticks switch
        {
            double d when double.IsNaN(d) || double.IsInfinity(d) => throw new NCalcEvaluationException($"The result of the time operation is outside the range of {typeName}"),
            double d => new BigInteger(Math.Round(d)),
            BigDecimal bd => MathHelper.GetWholeValue(MathHelper.RoundBigDecimal(bd, 0, MidpointRounding.ToEven)),
            _ => MathHelper.ConvertToBigInteger(ticks)
        };

        if (wholeTicks < minTicks || wholeTicks > maxTicks)
            throw new NCalcEvaluationException($"The result of the time operation is outside the range of {typeName}");
        return (long)wholeTicks;
    }

    private static TimeSpan ToTimeSpan(object ticks)
        => new(ToTicks(ticks, TimeSpan.MinValue.Ticks, TimeSpan.MaxValue.Ticks, nameof(TimeSpan)));

    private static DateTime ToDateTime(object ticks, DateTimeKind kind)
        => new(ToTicks(ticks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks, nameof(DateTime)), kind);

    private static T TimeOperation<T>(Func<T> operation, string typeName)
    {
        try
        {
            return operation();
        }
        catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
        {
            throw new NCalcEvaluationException($"The result of the time operation is outside the range of {typeName}", ex);
        }
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

        var leftValueString = Convert.ToString(leftValue, context.CultureInfo);

        if (string.IsNullOrEmpty(leftValueString) && string.IsNullOrEmpty(rightValue))
            return true;

        if (string.IsNullOrEmpty(leftValueString))
            return false;

        // A substring is looked up with the same case and culture rules as the string comparison
        ComparisonOptions options = context;
        if (options.IsOrdinal)
            return rightValue.IndexOf(leftValueString, options.IsCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) >= 0;

        var compareInfo = (options.CultureInfo ?? CultureInfo.CurrentCulture).CompareInfo;
        return compareInfo.IndexOf(rightValue, leftValueString, options.IsCaseInsensitive ? CompareOptions.IgnoreCase : CompareOptions.None) >= 0;
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

            // A string is looked up with the string comparer; any other value is compared as with "=", so that "x in (...)" and "x = ..." agree
            if (leftValue is string leftString)
                return rightArray.OfType<string>().Contains(leftString, TypeHelper.GetStringComparer(context));
        }

        return rightArray.Contains(leftValue, new ObjectEqualityComparer(context));
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

        // The same equality as the "=" operator (Equals would compare strings case-sensitively and find NaN)
        foreach (var item in rightValue)
        {
            if (item is null)
                continue;
            if (Compare(leftValue, item, ComparisonType.Equal, context))
                return true;
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

        // Percentages are compared by their values; a complex number with no imaginary part is a real number
        if (a is Percent percentA && b is Percent percentB)
            return Compare(percentA.Value, percentB.Value, comparisonType, comparisonOptions, mathHelperOptions);
        if (a is ComplexNumber realA && realA.IsReal)
            a = MathHelper.ReduceComplexToReal(realA, mathHelperOptions);
        if (b is ComplexNumber realB && realB.IsReal)
            b = MathHelper.ReduceComplexToReal(realB, mathHelperOptions);

        // Complex numbers and vectors have no order, but they can be compared for equality
        if (a is ComplexNumber || b is ComplexNumber || a is NCalcVector || b is NCalcVector)
        {
            if (comparisonType is not (ComparisonType.Equal or ComparisonType.NotEqual))
                throw new NCalcEvaluationException($"{(a is NCalcVector || b is NCalcVector ? "Vectors" : "Complex numbers")} can only be compared for equality");

            bool equal = (a, b) switch
            {
                (ComplexNumber complexA, ComplexNumber complexB) => complexA.Real.CompareTo(complexB.Real) == 0 && complexA.Imaginary.CompareTo(complexB.Imaginary) == 0,
                (NCalcVector vectorA, NCalcVector vectorB) => vectorA.Dimensions == vectorB.Dimensions
                    && Enumerable.Range(0, vectorA.Dimensions).All(i => vectorA[i].CompareTo(vectorB[i]) == 0),
                _ => false
            };
            return (comparisonType == ComparisonType.Equal) == equal;
        }
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
        {
            // Values that cannot be compared (a string and a number with NoStringTypeCoercion, or incompatible types with
            // CompareIncompatibleTypes) are unequal and unordered
            if (!TypeHelper.CompareUsingMostPreciseType(a, b, comparisonOptions, mathHelperOptions, out result))
                return comparisonType == ComparisonType.NotEqual;
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

    private static NCalc.Domain.Index FromEndIndex(int value, UnaryExpression expression)
    {
        if (value < 0)
            throw new NCalcParameterIndexException($"An index from the end (^{value}) must be zero or positive", expression.Location);

        return new NCalc.Domain.Index(value, true);
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
            UnaryExpressionType.FromEnd => FromEndIndex(MathHelper.ConvertToInt(result, "From End", context.CultureInfo, expression.Location), expression),
            UnaryExpressionType.BitwiseNot =>
                (result is NCalcVector vResult) ? ~vResult :
                MathHelper.BitwiseNot(result, "Bitwise Not", context.CultureInfo, expression.Location),
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
    /// <param name="pattern">The pattern to match. If the default matcher is used, '%' matches zero or more characters, and '_' matches exactly one character.</param>
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

        return MatchesLikePattern(value, pattern, context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer), context.CultureInfo);
    }

    /// <summary>
    /// Determines whether a specified string matches a pattern using an event or SQL-like wildcards.
    /// </summary>
    /// <param name="value">The string to be compared against the pattern.</param>
    /// <param name="pattern">The pattern to match. If the default matcher is used, '%' matches zero or more characters, and '_' matches exactly one character.</param>
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

                await InvokeAllAsync(actualCtx.AsyncMatchStringHandler, args, cancellationToken).ConfigureAwait(false);

                outcome = args.Matches;
            }
        }

        if (outcome.HasValue)
            return outcome.Value;

        return MatchesLikePattern(value, pattern, context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer), context.CultureInfo);
    }

    /// <summary>
    /// Determines whether a specified string matches a pattern using an event or SQL-like wildcards.
    /// </summary>
    /// <param name="value">The string to be compared against the pattern.</param>
    /// <param name="pattern">The pattern to match. If the default matcher is used, '%' matches zero or more characters, and '_' matches exactly one character.</param>
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

        string? lValue = Convert.ToString(value, context.CultureInfo);
        string? lPattern = Convert.ToString(pattern, context.CultureInfo);

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

        return MatchesLikePattern(lValue, lPattern, context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer), context.CultureInfo);
    }

    /// <summary>
    /// Determines whether a specified string matches a pattern using an event or SQL-like wildcards.
    /// </summary>
    /// <param name="value">The string to be compared against the pattern.</param>
    /// <param name="pattern">The pattern to match. If the default matcher is used, '%' matches zero or more characters, and '_' matches exactly one character.</param>
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

        string? lValue = Convert.ToString(value, context.CultureInfo);
        string? lPattern = Convert.ToString(pattern, context.CultureInfo);

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

                await InvokeAllAsync(actualCtx.AsyncMatchStringHandler, args, cancellationToken).ConfigureAwait(false);

                outcome = args.Matches;
            }
        }

        if (outcome.HasValue)
            return outcome.Value;

        return MatchesLikePattern(lValue, lPattern, context.Options.HasFlag(ExpressionOptions.CaseInsensitiveStringComparer), context.CultureInfo);
    }

    /// <summary>
    /// Matches a value against a LIKE pattern, where '%' matches zero or more characters and '_' matches exactly one character.
    /// </summary>
    /// <remarks>
    /// A regular expression would backtrack exponentially on patterns such as '%a%a%a%b'; this matcher only returns to the
    /// last '%' on a mismatch, so it takes at most O(value length × pattern length) steps.
    /// </remarks>
    internal static bool MatchesLikePattern(string value, string pattern, bool ignoreCase, CultureInfo cultureInfo)
    {
        var textInfo = cultureInfo.TextInfo;
        bool CharsMatch(char patternChar, char valueChar)
            => patternChar == valueChar || (ignoreCase && textInfo.ToUpper(patternChar) == textInfo.ToUpper(valueChar));

        int valueIndex = 0, patternIndex = 0;
        int lastWildcard = -1, valueIndexAtWildcard = 0;
        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == '%')
            {
                lastWildcard = patternIndex++;
                valueIndexAtWildcard = valueIndex;
            }
            else
            if (patternIndex < pattern.Length && (pattern[patternIndex] == '_' || CharsMatch(pattern[patternIndex], value[valueIndex])))
            {
                patternIndex++;
                valueIndex++;
            }
            else
            if (lastWildcard >= 0)
            {
                // Let the last '%' absorb one more character and retry the rest of the pattern
                patternIndex = lastWildcard + 1;
                valueIndex = ++valueIndexAtWildcard;
            }
            else
                return false;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '%')
            patternIndex++;
        return patternIndex == pattern.Length;
    }

    // A multicast delegate returns only the ValueTask of its last handler, so the other handlers would not be awaited
    // (their results lost and their exceptions unobserved); these methods await every handler in order.
    internal static async ValueTask InvokeAllAsync(AsyncEvaluateFunctionHandler? handler, string name, AsyncFunctionArgs args, CancellationToken cancellationToken)
    {
        if (handler is null)
            return;
        foreach (AsyncEvaluateFunctionHandler single in handler.GetInvocationList())
            await single(name, args, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask InvokeAllAsync(AsyncEvaluateParameterHandler? handler, string name, ParameterArgs args, CancellationToken cancellationToken)
    {
        if (handler is null)
            return;
        foreach (AsyncEvaluateParameterHandler single in handler.GetInvocationList())
            await single(name, args, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask InvokeAllAsync(AsyncUpdateParameterHandler? handler, string name, UpdateParameterArgs args, CancellationToken cancellationToken)
    {
        if (handler is null)
            return;
        foreach (AsyncUpdateParameterHandler single in handler.GetInvocationList())
            await single(name, args, cancellationToken).ConfigureAwait(false);
    }

    internal static async ValueTask InvokeAllAsync(AsyncMatchStringHandler? handler, MatchStringArgs args, CancellationToken cancellationToken)
    {
        if (handler is null)
            return;
        foreach (AsyncMatchStringHandler single in handler.GetInvocationList())
            await single(args, cancellationToken).ConfigureAwait(false);
    }

    private sealed class NestedExpressionLink(object expression, NestedExpressionLink? parent)
    {
        public object Expression { get; } = expression;
        public NestedExpressionLink? Parent { get; } = parent;
    }

    private sealed class NestedExpressionScope(NestedExpressionLink? previous) : IDisposable
    {
        public void Dispose() => NestedExpressions.Value = previous;
    }

    // The expressions that are being evaluated as values of parameters, innermost first (per thread and async flow)
    private static readonly AsyncLocal<NestedExpressionLink?> NestedExpressions = new();

    /// <summary>
    /// Marks the start of the evaluation of an expression that is the value of a parameter. An expression that is already being
    /// evaluated this way refers to itself through its parameters, which would otherwise recurse until the process crashes.
    /// </summary>
    internal static IDisposable EnterNestedExpression(object expression)
    {
        var current = NestedExpressions.Value;
        for (var link = current; link is not null; link = link.Parent)
        {
            if (ReferenceEquals(link.Expression, expression))
                throw new NCalcEvaluationException("An expression used as the value of a parameter refers to itself through its parameters.");
        }

        NestedExpressions.Value = new NestedExpressionLink(expression, current);
        return new NestedExpressionScope(current);
    }

    /// <summary>
    /// The handlers of an expression evaluated as the value of a parameter: its own ones, then those of the outer expression.
    /// A handler that both have is called once.
    /// </summary>
    internal static THandler? CombineHandlers<THandler>(THandler? own, THandler? outer) where THandler : Delegate
    {
        if (own is null)
            return outer;
        if (outer is null || own.Equals(outer))
            return own;

        // Each handler of this expression is removed from the outer ones, also when the outer expression has other handlers
        Delegate? remaining = outer;
        foreach (var handler in own.GetInvocationList())
        {
            while (remaining is not null)
            {
                var reduced = Delegate.Remove(remaining, handler);
                if (ReferenceEquals(reduced, remaining) || Equals(reduced, remaining))
                    break;
                remaining = reduced;
            }
        }

        return remaining is null ? own : (THandler)Delegate.Combine(own, remaining);
    }

    /// <summary>
    /// Assigns a value to an element of a list or to a character of a string by index. A list is changed in place, unless
    /// <paramref name="copyList"/> is set, in which case a copy of the list is changed. Returns the new value of the parameter:
    /// the list (or its copy), or a new string.
    /// </summary>
    internal static object UpdateElement(object container, string identifierName, int index, object? value, bool copyList, CultureInfo cultureInfo,
        Parser.ExpressionLocation containerLocation, Parser.ExpressionLocation indexLocation)
    {
        if (container is string strParam)
        {
            if (!(value is char || (value is string && ((string)value).Length == 1)))
                throw new NCalcParameterIndexException(identifierName, $"When updating a string in '{identifierName}' via the index, the value must be a character or a one-character string", indexLocation);

            if (strParam.Length <= index)
                throw new NCalcParameterIndexException(identifierName, $"A character in the '{identifierName}' string cannot be updated by index: the string has the length of {strParam.Length}, while the index is {index}", indexLocation);

            char charValue = value is string strValue ? strValue[0] : (char)value;

            return strParam[0..index] + charValue + strParam[(index + 1)..];
        }

        if (container is not IList list)
            throw new NCalcParameterIndexException(identifierName, $"'{identifierName}' is not a list or a string and cannot be assigned to by index", containerLocation);

        if (copyList)
            list = list is Array array ? (IList)array.Clone() : new List<object?>(list.Cast<object?>());
        else
        if (list.IsReadOnly)
            throw new NCalcParameterIndexException(identifierName, $"'{identifierName}' is read-only and cannot be assigned to by index", containerLocation);

        if (list.Count <= index)
            throw new NCalcParameterIndexException(identifierName, $"'{identifierName}' cannot be assigned to by index: it has {list.Count} elements, while the index to update is {index}", indexLocation);

        SetListElement(list, identifierName, index, value, cultureInfo, containerLocation);
        return list;
    }

    // A typed list or array gets the value converted to its element type; a value that cannot be converted is reported as an NCalc exception
    private static void SetListElement(IList list, string identifierName, int index, object? value, CultureInfo cultureInfo, Parser.ExpressionLocation location)
    {
        var listType = list.GetType();
        var elementType = list is Array ? listType.GetElementType() : (listType.IsGenericType && listType.GetGenericArguments() is { Length: 1 } arguments ? arguments[0] : null);
        if (elementType is not null)
            elementType = Nullable.GetUnderlyingType(elementType) ?? elementType;

        try
        {
            if (value is not null && elementType is not null && !elementType.IsInstanceOfType(value))
                value = ConvertElement(value, elementType, cultureInfo, location);

            list[index] = value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new NCalcParameterIndexException($"The value cannot be assigned to an element of '{identifierName}'" + (elementType is null ? string.Empty : $", whose elements are of type {elementType.Name}"), location, ex);
        }
    }

    private static object? ConvertElement(object value, Type elementType, CultureInfo cultureInfo, Parser.ExpressionLocation location)
    {
        const string operation = "Indexed assignment";

        if (elementType == typeof(double))
            return MathHelper.ConvertToDouble(value, operation, cultureInfo, location);
        if (elementType == typeof(float))
            return (float)MathHelper.ConvertToDouble(value, operation, cultureInfo, location);
        if (elementType == typeof(decimal))
            return MathHelper.ConvertToDecimal(value, operation, cultureInfo, location);
        if (elementType == typeof(int))
            return MathHelper.ConvertToInt(value, operation, cultureInfo, location);
        if (elementType == typeof(long))
            return MathHelper.ConvertToLong(value, operation, cultureInfo, location);
        if (elementType == typeof(ulong))
            return MathHelper.ConvertToULong(value, operation, cultureInfo, location);
        if (elementType == typeof(bool))
            return MathHelper.ConvertToBoolean(value, operation, cultureInfo, location);
        if (elementType == typeof(BigDecimal))
            return MathHelper.ConvertToBigDecimal(value);
        if (elementType == typeof(BigInteger))
            return MathHelper.ConvertToBigInteger(value);
        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(elementType))
            return Convert.ChangeType(value, elementType, cultureInfo);

        // The list reports the mismatch
        return value;
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

    internal sealed class ObjectEqualityComparer : IEqualityComparer<object?>
    {
        private readonly ComparisonOptions _comparisonOptions;
        private readonly MathHelperOptions _mathHelperOptions;

        public ObjectEqualityComparer(ExpressionContextBase context)
        {
            _comparisonOptions = context;
            _mathHelperOptions = context;
        }

        public new bool Equals(object? x, object? y)
        {
            if (x is null || y is null)
                return x == y;

            // The same equality as the "=" operator: a string literal is converted to the type of the other value,
            // and numbers are not compared through their culture-dependent text
            return Compare(x, y, ComparisonType.Equal, _comparisonOptions, _mathHelperOptions);
        }

        public int GetHashCode(object? obj)
            => obj?.GetHashCode() ?? 0;
    }
}

internal class ArgumentStateBase
{
    protected object? _value = null;
    protected bool _valueSet = false;

    // Set when _value is a copy of a list made by this state, which may be changed in place
    private bool _ownsValue;

    // Identifies the function call that the arguments belong to
    internal object? Owner { get; set; }

    protected void ReplaceValue(object? value)
    {
        _value = value;
        _valueSet = true;
        _ownsValue = false;
    }

    /// <summary>
    /// An indexed assignment to an argument (<c>a[1] := 5</c>) changes that element of the argument. Assignments to arguments are local
    /// to the function, so the list of the caller is copied first (once) and the copy is changed.
    /// </summary>
    protected void UpdateElement(object? current, string name, int index, object? value, CultureInfo cultureInfo)
    {
        if (current is null)
            throw new NCalcParameterIndexException(name, $"{name} is not set and cannot be assigned to by index");

        _value = EvaluationHelper.UpdateElement(current, name, index, value, !_ownsValue, cultureInfo, Parser.ExpressionLocation.Empty, Parser.ExpressionLocation.Empty);
        _valueSet = true;
        _ownsValue = _value is not string;
    }
}

/// <summary>
/// The loop iterations that an evaluation may still run. The MaxLoopIterations limit applies to all the loops of one evaluation together,
/// including nested loops and the loops in function arguments, in the bodies of user functions and in expressions used as parameters.
/// Any other evaluation, also one started by a handler or by work that an evaluation started (a task, a timer), has a budget of its own.
/// </summary>
internal sealed class LoopIterationBudget
{
    // The budget of the evaluation in progress in this thread and async flow, for its visitor. Work started during an evaluation captures
    // it with the execution context, so the parts of an evaluation get the budget explicitly (OuterLoopBudget of the expressions created
    // for them), and a budget that has ended is never used again
    private static readonly AsyncLocal<LoopIterationBudget?> CurrentBudget = new();

    private int _remaining;
    private volatile bool _ended;

    private LoopIterationBudget(int limit)
    {
        _remaining = limit;
    }

    /// <summary>
    /// The budget of the evaluation in progress in this thread and async flow, or null.
    /// </summary>
    internal static LoopIterationBudget? Current => CurrentBudget.Value is { _ended: false } budget ? budget : null;

    /// <summary>
    /// Enters an evaluation, which uses the budget of the evaluation that it is a part of (<paramref name="outer"/>), or, when it has
    /// none or that one has ended, a new budget of <paramref name="limit"/> iterations if <paramref name="useLoops"/> is set.
    /// </summary>
    internal static Scope Enter(bool useLoops, int limit, LoopIterationBudget? outer)
    {
        if (outer is { _ended: true })
            outer = null;
        var owned = outer is null && useLoops ? new LoopIterationBudget(limit) : null;
        var budget = outer ?? owned;
        var previous = CurrentBudget.Value;
        if (!ReferenceEquals(previous, budget))
            CurrentBudget.Value = budget;
        return new Scope(previous, owned);
    }

    internal readonly struct Scope
    {
        private readonly LoopIterationBudget? _previous;
        private readonly LoopIterationBudget? _owned;

        internal Scope(LoopIterationBudget? previous, LoopIterationBudget? owned)
        {
            _previous = previous;
            _owned = owned;
        }

        /// <summary>
        /// Leaves the evaluation: the budget that it started ends, and the budget that was current before is current again.
        /// </summary>
        internal void Exit()
        {
            if (_owned is not null)
                _owned._ended = true;
            if (!ReferenceEquals(CurrentBudget.Value, _previous))
                CurrentBudget.Value = _previous;
        }
    }

    /// <summary>
    /// The budget for a loop: that of the evaluation, or, for a visitor used without an expression, one for this loop alone.
    /// </summary>
    internal static LoopIterationBudget ForLoop(int limit) => Current ?? new LoopIterationBudget(limit);

    /// <summary>
    /// Takes one iteration; returns false when the evaluation has run all the iterations it may run.
    /// The parts of an evaluation may run in parallel (e.g. arguments that a function evaluates on several threads).
    /// </summary>
    internal bool TryTake()
    {
        if (Volatile.Read(ref _remaining) <= 0)
            return false;

        return Interlocked.Decrement(ref _remaining) >= 0;
    }
}

/// <summary>
/// Carries flow control (<c>return</c>, <c>break</c>, <c>continue</c>) that happened in an argument of a user function through the body of
/// that function, which would otherwise handle it as its own; the function call rethrows the original flow control in the calling expression.
/// </summary>
internal sealed class ArgumentFlowControl(NCalcFlowControl flowControl, object? owner) : Exception(flowControl.Message, flowControl)
{
    internal NCalcFlowControl FlowControl { get; } = flowControl;

    internal object? Owner { get; } = owner;
}

/// <summary>
/// The parameters of an expression that is evaluated as the value of a parameter of another expression: its own parameters come first,
/// then those of the outer expression. Values written during the evaluation are kept in a local layer, so neither dictionary is changed
/// (both may be read-only).
/// </summary>
internal sealed class ParameterOverlay<TValue>(IDictionary<string, TValue> own, IDictionary<string, TValue> outer) : IDictionary<string, TValue>
{
    private readonly Dictionary<string, TValue> _local = new(StringComparer.Ordinal);
    private readonly IDictionary<string, TValue> _own = own;
    private readonly IDictionary<string, TValue> _outer = outer;

    public TValue this[string key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"The parameter '{key}' is not defined.");
        set => _local[key] = value;
    }

    public ICollection<string> Keys
    {
        get
        {
            var keys = new List<string>();
            foreach (var pair in this)
                keys.Add(pair.Key);
            return keys;
        }
    }

    public ICollection<TValue> Values
    {
        get
        {
            var values = new List<TValue>();
            foreach (var pair in this)
                values.Add(pair.Value);
            return values;
        }
    }

    // Not Enumerable.Count(), which would call this property
    public int Count
    {
        get
        {
            var count = 0;
            using var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
                count++;
            return count;
        }
    }

    public bool IsReadOnly => false;

    public void Add(string key, TValue value)
    {
        if (ContainsKey(key))
            throw new ArgumentException($"The parameter '{key}' is already defined.", nameof(key));
        _local.Add(key, value);
    }

    public void Add(KeyValuePair<string, TValue> item) => Add(item.Key, item.Value);

    public void Clear() => _local.Clear();

    public bool Contains(KeyValuePair<string, TValue> item)
        => TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

    public bool ContainsKey(string key) => TryGetValue(key, out _);

    public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
    {
        foreach (var pair in this)
            array[arrayIndex++] = pair;
    }

    // Only the values written during the evaluation can be removed
    public bool Remove(string key) => _local.Remove(key);

    public bool Remove(KeyValuePair<string, TValue> item) => Contains(item) && _local.Remove(item.Key);

    // The overlays of deeply nested evaluations form long chains, which are walked in a loop, not by recursion
    public bool TryGetValue(string key, out TValue value)
    {
        var overlay = this;
        while (true)
        {
            if (overlay._local.TryGetValue(key, out value!) || overlay._own.TryGetValue(key, out value!))
                return true;
            if (overlay._outer is not ParameterOverlay<TValue> next)
                break;
            overlay = next;
        }
        if (overlay._outer.TryGetValue(key, out value!))
            return true;
        value = default!;
        return false;
    }

    // Each name once, from the layer that provides its value
    public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
    {
        foreach (var pair in _local)
            yield return pair;
        foreach (var pair in _own)
        {
            if (!_local.ContainsKey(pair.Key))
                yield return pair;
        }
        foreach (var pair in _outer)
        {
            if (!_local.ContainsKey(pair.Key) && !_own.ContainsKey(pair.Key))
                yield return pair;
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
using System.Collections.Concurrent;
using System.Numerics;
using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Handlers;
using NCalc.Helpers;
using NCalc.Parser;

using LinqExpression = System.Linq.Expressions.Expression;
using NCalcVector = NCalc.Domain.Vector;

namespace NCalc.Visitors;

/// <summary>
/// The operations that the lambdas compiled by <see cref="LambdaExpressionVisitor"/> call. They do what the evaluators do
/// (<see cref="EvaluationVisitor"/>), with the same helper methods and the options of the expression, so that a lambda
/// computes the same value as Evaluate().
/// </summary>
internal static partial class LambdaRuntime
{
    /// <summary>Returned by <see cref="LogicalLeft"/> when the result depends on the right operand.</summary>
    internal static readonly object Undecided = new();

    /// <summary>Returned by <see cref="CompoundAssignment"/> when, as in the evaluators, the parameter is left unchanged and the result is null.</summary>
    internal static readonly object NoUpdate = new();

    // An operand that is null makes the result null, unless TreatNullAsZero is set
    internal static bool IsNull(object? value, ExpressionContext context)
        => value is null && !context.Options.HasFlag(ExpressionOptions.TreatNullAsZero);

    // The value of an operand as the evaluators use it: null is 0 with TreatNullAsZero
    internal static object? ValueOrNull(object? value, ExpressionContext context)
        => value is null && context.Options.HasFlag(ExpressionOptions.TreatNullAsZero) ? 0 : value;

    internal static bool ToBoolean(object? value, string operation, LogicalExpression expression, ExpressionContext context)
        => MathHelper.ConvertToBoolean(ValueOrNull(value, context), operation, context.CultureInfo, expression.Location);

    // A condition of if(), iff() and ifs(), which the built-in functions convert without the null check
    internal static bool ToBooleanRaw(object? value, string operation, LogicalExpression expression, ExpressionContext context)
        => MathHelper.ConvertToBoolean(value, operation, context.CultureInfo, expression.Location);

    // A binary operation whose result is null when an operand is null; the caller checks the left operand first,
    // so that the right one is not evaluated when the left one is null
    internal static object? Binary(object? left, object? right, BinaryExpression expression, ExpressionContext context)
    {
        try
        {
            if (IsNull(right, context))
                return null;

            return Compute(expression.Type, ValueOrNull(left, context), ValueOrNull(right, context), expression, context);
        }
        catch (NCalcEvaluationException ex) when (ex.Location == ExpressionLocation.Empty)
        {
            ex.Location = expression.Location;
            throw;
        }
    }

    internal static bool Compare(object? a, object? b, BinaryExpression expression, ExpressionContext context)
    {
        var comparisonType = expression.Type switch
        {
            BinaryExpressionType.Equal => ComparisonType.Equal,
            BinaryExpressionType.NotEqual => ComparisonType.NotEqual,
            BinaryExpressionType.Greater => ComparisonType.Greater,
            BinaryExpressionType.GreaterOrEqual => ComparisonType.GreaterOrEqual,
            BinaryExpressionType.Less => ComparisonType.Less,
            BinaryExpressionType.LessOrEqual => ComparisonType.LessOrEqual,
            _ => throw new ArgumentOutOfRangeException(nameof(expression), expression.Type, null)
        };

        try
        {
            // Values of different types are unequal and unordered
            if (context.Options.HasFlag(ExpressionOptions.StrictTypeMatching) && a?.GetType() != b?.GetType())
                return comparisonType == ComparisonType.NotEqual;

            if (!context.Options.HasFlag(ExpressionOptions.CompareNullValues) && (a is null || b is null) && !(a is null && b is null))
                return comparisonType == ComparisonType.NotEqual;

            return EvaluationHelper.Compare(a, b, comparisonType, context);
        }
        catch (NCalcEvaluationException ex) when (ex.Location == ExpressionLocation.Empty)
        {
            ex.Location = expression.Location;
            throw;
        }
    }

    // and, or, xor: the result when the left operand decides it, otherwise Undecided
    internal static object? LogicalLeft(object? left, BinaryExpression expression, ExpressionContext context)
    {
        try
        {
            if (context.Options.HasFlag(ExpressionOptions.UseTernaryLogic))
            {
                return expression.Type switch
                {
                    BinaryExpressionType.And => left is false ? false : Undecided,
                    BinaryExpressionType.Or => left is true ? true : Undecided,
                    _ => left is bool ? Undecided : null
                };
            }

            if (IsNull(left, context))
                return null;

            return expression.Type switch
            {
                BinaryExpressionType.And => ToBoolean(left, "And", expression.LeftExpression, context) ? Undecided : false,
                BinaryExpressionType.Or => ToBoolean(left, "Or", expression.LeftExpression, context) ? true : Undecided,
                _ => Undecided
            };
        }
        catch (NCalcEvaluationException ex) when (ex.Location == ExpressionLocation.Empty)
        {
            ex.Location = expression.Location;
            throw;
        }
    }

    internal static object? LogicalRight(object? left, object? right, BinaryExpression expression, ExpressionContext context)
    {
        try
        {
            if (context.Options.HasFlag(ExpressionOptions.UseTernaryLogic))
            {
                return expression.Type switch
                {
                    BinaryExpressionType.And => K3LogicHelper.And(left, right),
                    BinaryExpressionType.Or => K3LogicHelper.Or(left, right),
                    _ => right is bool ? K3LogicHelper.Xor(left, right) : null
                };
            }

            if (IsNull(right, context))
                return null;

            return expression.Type switch
            {
                BinaryExpressionType.And => ToBoolean(right, "And", expression.RightExpression, context),
                BinaryExpressionType.Or => ToBoolean(right, "Or", expression.RightExpression, context),
                _ => ToBoolean(left, "XOr", expression.LeftExpression, context) ^ ToBoolean(right, "XOr", expression.RightExpression, context)
            };
        }
        catch (NCalcEvaluationException ex) when (ex.Location == ExpressionLocation.Empty)
        {
            ex.Location = expression.Location;
            throw;
        }
    }

    // The value to assign by a compound assignment (+=, -=, *=, /=, &=, |=, ^=); the caller checks that the left operand is not null
    internal static object? CompoundAssignment(object? left, object? right, BinaryExpression expression, ExpressionContext context)
    {
        try
        {
            if (IsNull(right, context))
                return null;

            object? leftValue = ValueOrNull(left, context);
            object? rightValue = ValueOrNull(right, context);

            switch (expression.Type)
            {
                case BinaryExpressionType.AndAssignment:
                    if (leftValue is BigInteger || rightValue is BigInteger)
                        return MathHelper.BitwiseAnd(leftValue, rightValue);
                    return MathHelper.BitwiseAnd(leftValue, rightValue, "And with assignment", context.CultureInfo, expression.LeftExpression.Location, expression.RightExpression.Location);

                case BinaryExpressionType.OrAssignment:
                    if (leftValue is BigInteger || rightValue is BigInteger)
                        return MathHelper.BitwiseOr(leftValue, rightValue);
                    return MathHelper.BitwiseOr(leftValue, rightValue, "Or with assignment", context.CultureInfo, expression.LeftExpression.Location, expression.RightExpression.Location);

                case BinaryExpressionType.XOrAssignment:
                    if (leftValue is BigInteger || rightValue is BigInteger)
                        return MathHelper.BitwiseXOr(leftValue, rightValue);
                    return MathHelper.BitwiseXOr(leftValue, rightValue, "Xor with assignment", context.CultureInfo, expression.LeftExpression.Location, expression.RightExpression.Location);
            }

            var operation = expression.Type switch
            {
                BinaryExpressionType.PlusAssignment => BinaryExpressionType.Plus,
                BinaryExpressionType.MinusAssignment => BinaryExpressionType.Minus,
                BinaryExpressionType.MultiplyAssignment => BinaryExpressionType.Times,
                BinaryExpressionType.DivAssignment => BinaryExpressionType.Div,
                _ => throw new ArgumentOutOfRangeException(nameof(expression), expression.Type, null)
            };

            // A null result of the operation leaves the parameter unchanged
            return Compute(operation, leftValue, rightValue, expression, context) ?? NoUpdate;
        }
        catch (NCalcEvaluationException ex) when (ex.Location == ExpressionLocation.Empty)
        {
            ex.Location = expression.Location;
            throw;
        }
    }

    // Whether an assigned value is written to the member of the context: as in the evaluators, null is not assigned unless
    // AllowNullParameter or UseTernaryLogic is set, and the UpdateParameter handler can cancel the update
    internal static bool ShouldUpdate(object? value, Identifier identifier, ExpressionContext context)
    {
        if (value is null && !(context.Options.HasFlag(ExpressionOptions.AllowNullParameter) || context.Options.HasFlag(ExpressionOptions.UseTernaryLogic)))
            return false;

        var parameterArgs = new UpdateParameterArgs(identifier.Name, identifier.Id, value);
        context.UpdateParameterHandler?.Invoke(identifier.Name, parameterArgs);
        return parameterArgs.UpdateParameterLists;
    }

    internal static object? Unary(object? value, UnaryExpression expression, ExpressionContext context)
    {
        if (expression.Type == UnaryExpressionType.Not && context.Options.HasFlag(ExpressionOptions.UseTernaryLogic))
            return K3LogicHelper.Not(value);

        if (IsNull(value, context))
            return null;

        return EvaluationHelper.Unary(expression, ValueOrNull(value, context), context);
    }

    internal static object? Percent(object? value, ExpressionContext context)
    {
        if (IsNull(value, context))
            return null;

        return new Percent(ValueOrNull(value, context)!);
    }

    internal static object? Imaginary(object? value, ImaginaryNumberExpression expression, ExpressionContext context)
    {
        if (IsNull(value, context))
            return null;

        return new ComplexNumber(0, ValueOrNull(value, context)!, new MathHelperOptions(expression.CultureInfo ?? CultureInfo.CurrentCulture, expression.Options));
    }

    internal static object Vector(object?[] components, VectorExpression expression)
    {
        var options = new MathHelperOptions(expression.CultureInfo ?? CultureInfo.CurrentCulture, expression.Options);
        var values = new List<BigDecimal>(components.Length);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is not { } component)
                throw new NCalcEvaluationException($"Vector component at index {i} evaluated to null.", expression.Expressions[i].Location);
            values.Add(MathHelper.ConvertToBigDecimal(component));
        }

        return new NCalcVector(values, options);
    }

    // One comparison of the in() function
    internal static bool InFunctionItem(object? value, object? item, LogicalExpression itemExpression, ExpressionContext context)
    {
        try
        {
            return EvaluationHelper.Compare(value, item, ComparisonType.Equal, context);
        }
        catch (NCalcConversionException ex)
        {
            ex.Location = itemExpression.Location;
            throw;
        }
    }

    // The value of a parameter that is an expression
    internal static object? EvaluateParameterExpression(Expression parameter, ExpressionContext context)
    {
        using (EvaluationHelper.EnterNestedExpression(parameter))
            return parameter.EvaluateAsParameter(context, CancellationToken.None);
    }

    internal static object? EvaluateDynamicParameter(ExpressionParameter parameter, Identifier identifier, ExpressionContext context)
        => parameter(new ExpressionParameterData(identifier.Id, context));

    // A statement of a sequence other than the last one, whose value is not used. Compiling it as the argument of a void call
    // instead of as a statement of the block avoids invalid IL from FastExpressionCompiler for lists and in() (NCALC-210).
    internal static void Discard(object? value)
    {
        _ = value;
    }
    internal static long MakeSize(object? size, string what, string functionName, FunctionCall function)
        => BuiltInFunctionHelper.GetMakeSize(size, what, functionName, function.Location);

    internal static object MakeList(long size, object? value)
        => BuiltInFunctionHelper.MakeList(size, value);

    internal static string MakeString(long size, object? value, FunctionCall function)
        => BuiltInFunctionHelper.MakeString(size, value, function.Location);

    internal static object? PercentOf(object? arg1, object? arg2, ExpressionContext context)
        => arg1 is null || arg2 is null ? null : BuiltInFunctionHelper.PercentOf(arg1, arg2, context, ExpressionLocation.Empty);

    internal static object? PercentDiff(object? arg1, object? arg2, ExpressionContext context)
        => arg1 is null || arg2 is null ? null : BuiltInFunctionHelper.PercentDiff(arg1, arg2, context, ExpressionLocation.Empty);

    internal static object? Round(object arg0, object arg1, ExpressionContext context)
    {
        var rounding = context.Options.HasFlag(ExpressionOptions.RoundAwayFromZero)
            ? MidpointRounding.AwayFromZero
            : MidpointRounding.ToEven;
        return MathHelper.Round(arg0, arg1, rounding, context);
    }

    /// <summary>The built-in functions with one argument, called as BuiltInFunctionHelper calls them.</summary>
    internal static readonly KeyValuePair<string, Func<object, ExpressionContext, object?>>[] UnaryFunctions =
    [
        new("Abs", (a, c) => MathHelper.Abs(a, c)),
        new("Acos", (a, c) => MathHelper.Acos(a, c)),
        new("Asin", (a, c) => MathHelper.Asin(a, c)),
        new("Atan", (a, c) => MathHelper.Atan(a, c)),
        new("Ceiling", (a, c) => MathHelper.Ceiling(a, c)),
        new("Cos", (a, c) => MathHelper.Cos(a, c)),
        new("Exp", (a, c) => MathHelper.Exp(a, c)),
        new("Floor", (a, c) => MathHelper.Floor(a, c)),
        new("Ln", (a, c) => MathHelper.Ln(a, c)),
#if NET8_0_OR_GREATER
        new("Log2", (a, c) => MathHelper.Log2(a, c)),
#endif
        new("Log10", (a, c) => MathHelper.Log10(a, c)),
        new("Sign", (a, c) => MathHelper.Sign(a, c)),
        new("Sin", (a, c) => MathHelper.Sin(a, c)),
        new("Sqrt", (a, c) => MathHelper.Sqrt(a, c)),
        new("Tan", (a, c) => MathHelper.Tan(a, c)),
        new("Trunc", (a, c) => MathHelper.Truncate(a, c)),
        new("Truncate", (a, c) => MathHelper.Truncate(a, c)),
    ];

    /// <summary>The built-in functions with two arguments that evaluate both arguments before checking them for null.</summary>
    internal static readonly KeyValuePair<string, Func<object, object, ExpressionContext, object?>>[] BinaryFunctions =
    [
        new("Atan2", (a, b, c) => MathHelper.Atan2(a, b, c)),
        new("IEEERemainder", (a, b, c) => MathHelper.IEEERemainder(a, b, c)),
        new("Log", (a, b, c) => MathHelper.Log(a, b, c)),
        new("Pow", (a, b, c) => MathHelper.Pow(a, b, true, c)),
        new("Round", Round),
        new("Max", (a, b, c) => MathHelper.Max(a, b, c)),
        new("Min", (a, b, c) => MathHelper.Min(a, b, c)),
    ];

    /// <summary>
    /// Converts the result of a compiled expression to the result type of the lambda: a number of another type is converted as a cast
    /// converts it (checked with OverflowProtection), and null gives the default value.
    /// </summary>
    internal static T ConvertResult<T>(object? value, ExpressionContext context)
    {
        if (value is T result)
            return result;
        if (value is null)
            return default!;

        var converter = Converters.GetOrAdd(Tuple.Create(value.GetType(), typeof(T), context.Options.HasFlag(ExpressionOptions.OverflowProtection)), BuildConverter);
        return (T)converter(value)!;
    }

    private static readonly ConcurrentDictionary<Tuple<Type, Type, bool>, Func<object, object?>> Converters = new();

    private static Func<object, object?> BuildConverter(Tuple<Type, Type, bool> key)
    {
        var from = key.Item1;
        var to = key.Item2;
        var isChecked = key.Item3;
        var parameter = LinqExpression.Parameter(typeof(object), "value");
        LinqExpression value = LinqExpression.Convert(parameter, from);

        // The explicit conversions of BigDecimal depend on the current culture
        var target = Nullable.GetUnderlyingType(to) ?? to;
        if (from == typeof(BigDecimal))
        {
            if (target == typeof(double))
                value = LinqExpression.Call(typeof(MathHelper).GetMethod(nameof(MathHelper.BigDecimalToDouble), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!, value);
            else if (target == typeof(float))
                value = LinqExpression.Call(typeof(MathHelper).GetMethod(nameof(MathHelper.BigDecimalToSingle), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!, value);
            else if (target == typeof(decimal))
                value = LinqExpression.Call(typeof(MathHelper).GetMethod(nameof(MathHelper.BigDecimalToDecimal), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!, value);
            else if ((target.IsEnum ? Enum.GetUnderlyingType(target) : target) is var integerType && IsIntegerType(integerType))
            {
                // To an integer type, the whole part, as a cast of a double gives it (NCALC-320); a value out of the range of the type
                // throws OverflowException
                value = LinqExpression.Call(typeof(LambdaRuntime).GetMethod(nameof(TruncateBigDecimal), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!, value);
                if (integerType != typeof(BigInteger))
                    value = LinqExpression.Convert(value, integerType);
            }
        }

        LinqExpression body;
        try
        {
            body = value.Type == to ? value : isChecked ? LinqExpression.ConvertChecked(value, to) : LinqExpression.Convert(value, to);
        }
        catch (InvalidOperationException)
        {
            return v => throw new InvalidCastException($"A value of type {v.GetType().Name} cannot be converted to {to.Name}");
        }

        return LinqExpression.Lambda<Func<object, object?>>(LinqExpression.Convert(body, typeof(object)), parameter).Compile();
    }

    private static bool IsIntegerType(Type type)
        => type == typeof(sbyte) || type == typeof(byte) || type == typeof(short) || type == typeof(ushort) || type == typeof(int)
            || type == typeof(uint) || type == typeof(long) || type == typeof(ulong) || type == typeof(BigInteger);

    // The whole part of a BigDecimal; one too large for any integer type throws OverflowException without being computed
    private static BigInteger TruncateBigDecimal(BigDecimal value)
    {
        if (value.Exponent > MathHelper.MaxPowResultDigits)
            throw new OverflowException("The value is too large for an integer type.");

        // A value with fewer digits than decimal places is below 1
        if (value.Exponent < -MathHelper.MaxPowResultDigits && (value.Mantissa.IsZero || BigInteger.Log10(BigInteger.Abs(value.Mantissa)) < -(double)value.Exponent - 1))
            return BigInteger.Zero;

        return MathHelper.GetWholeValue(value);
    }

    // The arithmetic, bitwise and other operations of the evaluators (EvaluationVisitor.Visit(BinaryExpression)) on non-null operands
    private static object? Compute(BinaryExpressionType type, object? leftValue, object? rightValue, BinaryExpression expression, ExpressionContext context)
    {
        var handlePercent = context.AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.CalculatePercent) == true;
        var leftIsPercent = leftValue is Percent;
        var rightIsPercent = rightValue is Percent;

        switch (type)
        {
            case BinaryExpressionType.Plus:
            case BinaryExpressionType.Minus:
            {
                var plus = type == BinaryExpressionType.Plus;
                if (handlePercent)
                {
                    if (leftValue is Percent lValPercent)
                        leftValue = lValPercent.Value;
                    if (rightValue is Percent rValPercent)
                        rightValue = rValPercent.Value;

                    if (leftIsPercent && rightIsPercent)
                    {
                        object? result = plus ? MathHelper.Add(leftValue, rightValue, true, context) : MathHelper.Subtract(leftValue, rightValue, true, context);
                        return result is null ? null : new Percent(result);
                    }
                    if (rightIsPercent)
                        return plus ? MathHelper.AddPercent(leftValue, rightValue, context) : MathHelper.SubtractPercent(leftValue, rightValue, context);
                    if (leftIsPercent)
                        throw new NCalcEvaluationException(plus
                            ? "The left side of an addition operation cannot be a percent unless the right side is a percent as well"
                            : "The left side of a subtraction operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                }

                return plus ? EvaluationHelper.Plus(leftValue, rightValue, context) : EvaluationHelper.Minus(leftValue, rightValue, context);
            }

            case BinaryExpressionType.Times:
            {
                if (handlePercent)
                {
                    if (leftValue is Percent lValPerc && rightValue is Percent rValPerc)
                    {
                        object? result = MathHelper.MultiplyPercent(lValPerc.Value, rValPerc.Value, context);
                        return result is null ? null : new Percent(result);
                    }
                    if (leftValue is Percent lValPercent)
                    {
                        object? result = MathHelper.Multiply(lValPercent.Value, rightValue, true, context);
                        return result is null ? null : new Percent(result);
                    }
                    if (rightValue is Percent rValPercent)
                        return MathHelper.MultiplyPercent(leftValue, rValPercent.Value, context);
                }

                return EvaluationHelper.Multiply(leftValue, rightValue, context);
            }

            case BinaryExpressionType.Div:
            {
                bool convertToDouble = EvaluationHelper.ShouldConvertDividendToDouble(leftValue, rightValue);

                if (handlePercent)
                {
                    if (leftValue is Percent lValPerc && rightValue is Percent rValPerc)
                    {
                        leftValue = lValPerc.Value;
                        if (convertToDouble)
                            leftValue = MathHelper.ConvertToDouble(leftValue, "Div", context.CultureInfo, expression.Location);
                        object? result = MathHelper.DividePercent(leftValue, rValPerc.Value, context);
                        return result is null ? null : new Percent(result);
                    }
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        if (convertToDouble)
                            leftValue = MathHelper.ConvertToDouble(leftValue, "Div", context.CultureInfo, expression.Location);
                        object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                        return result is null ? null : new Percent(result);
                    }
                    if (rightValue is Percent rValPercent)
                    {
                        if (convertToDouble)
                            leftValue = MathHelper.ConvertToDouble(leftValue, "Div", context.CultureInfo, expression.Location);
                        return MathHelper.DividePercent(leftValue, rValPercent.Value, context);
                    }
                }

                if (convertToDouble)
                    leftValue = MathHelper.ConvertToDouble(leftValue, "Div", context.CultureInfo, expression.Location);

                return EvaluationHelper.Divide(leftValue, rightValue, context);
            }

            case BinaryExpressionType.IntDivB:
            case BinaryExpressionType.IntDivP:
                return MathHelper.IntegerDivide(leftValue, rightValue, type == BinaryExpressionType.IntDivB, true, context);

            case BinaryExpressionType.Modulo:
                return MathHelper.Modulo(leftValue, rightValue, true, context);

            case BinaryExpressionType.BitwiseAnd:
            case BinaryExpressionType.BitwiseOr:
            case BinaryExpressionType.BitwiseXOr:
            {
                var name = type switch
                {
                    BinaryExpressionType.BitwiseAnd => "AND",
                    BinaryExpressionType.BitwiseOr => "OR",
                    _ => "XOR"
                };
                if (leftValue is NCalcVector lv && rightValue is NCalcVector rv)
                    return type switch
                    {
                        BinaryExpressionType.BitwiseAnd => lv & rv,
                        BinaryExpressionType.BitwiseOr => lv | rv,
                        _ => lv ^ rv
                    };
                if (leftValue is NCalcVector || rightValue is NCalcVector)
                    throw new NCalcEvaluationException($"Bitwise {name} between a vector and a non-vector is not supported.", expression.Location);

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return type switch
                    {
                        BinaryExpressionType.BitwiseAnd => MathHelper.BitwiseAnd(leftValue, rightValue),
                        BinaryExpressionType.BitwiseOr => MathHelper.BitwiseOr(leftValue, rightValue),
                        _ => MathHelper.BitwiseXOr(leftValue, rightValue)
                    };

                var operation = type switch
                {
                    BinaryExpressionType.BitwiseAnd => "Bitwise And",
                    BinaryExpressionType.BitwiseOr => "Bitwise Or",
                    _ => "Bitwise XOr"
                };
                return type switch
                {
                    BinaryExpressionType.BitwiseAnd => MathHelper.BitwiseAnd(leftValue, rightValue, operation, context.CultureInfo, expression.LeftExpression.Location, expression.RightExpression.Location),
                    BinaryExpressionType.BitwiseOr => MathHelper.BitwiseOr(leftValue, rightValue, operation, context.CultureInfo, expression.LeftExpression.Location, expression.RightExpression.Location),
                    _ => MathHelper.BitwiseXOr(leftValue, rightValue, operation, context.CultureInfo, expression.LeftExpression.Location, expression.RightExpression.Location)
                };
            }

            case BinaryExpressionType.LeftShift:
                return MathHelper.LeftShift(leftValue, rightValue, true, context);

            case BinaryExpressionType.RightShift:
                return MathHelper.RightShift(leftValue, rightValue, true, context);

            case BinaryExpressionType.Exponentiation:
                return MathHelper.Pow(leftValue, rightValue, true, context);

            case BinaryExpressionType.Factorial:
                return MathHelper.Factorial(leftValue!, rightValue!, context);

            case BinaryExpressionType.In:
                return EvaluationHelper.In(rightValue, leftValue, context);

            case BinaryExpressionType.NotIn:
                return !EvaluationHelper.In(rightValue, leftValue, context);

            case BinaryExpressionType.Like:
                return EvaluationHelper.Like(leftValue!, rightValue!, context);

            case BinaryExpressionType.NotLike:
                return !EvaluationHelper.Like(leftValue!, rightValue!, context);

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}

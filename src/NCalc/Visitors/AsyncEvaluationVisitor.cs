using System.Numerics;
using ExtendedNumerics;
using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Handlers;
using NCalc.Helpers;
using static NCalc.Helpers.TypeHelper;
using BinaryExpression = NCalc.Domain.BinaryExpression;
using UnaryExpression = NCalc.Domain.UnaryExpression;

namespace NCalc.Visitors;

/// <summary>
/// Class responsible to asynchronous evaluating <see cref="LogicalExpression"/> objects into CLR objects.
/// </summary>
/// <param name="context">Contextual parameters of the <see cref="LogicalExpression"/>, like custom functions and parameters.</param>
public class AsyncEvaluationVisitor(AsyncExpressionContext context) : ILogicalExpressionVisitor<ValueTask<object?>>
{
    private bool TryGetValueOrNull(object? candidate, out object? value)
    {
        if (candidate is null)
        {
            if (context.Options.HasFlag(ExpressionOptions.TreatNullAsZero))
            {
                value = 0;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
        else
        {
            value = candidate;
            return true;
        }
    }

    public virtual async ValueTask<object?> Visit(TernaryExpression expression, CancellationToken cancellationToken = default)
    {
        object? value;
        if (!TryGetValueOrNull(await expression.LeftExpression.Accept(this, cancellationToken).ConfigureAwait(false), out value))
            return null;

        return await (Convert.ToBoolean(value, context.CultureInfo) ? expression.MiddleExpression : expression.RightExpression).Accept(this, cancellationToken).ConfigureAwait(false);
    }

    private async Task<object?> UpdateParameterAsync(LogicalExpression leftExpression, object? value, CancellationToken cancellationToken = default)
    {
        if (leftExpression is Identifier identifier)
        {
            var identifierName = identifier.Name;

            var parameterArgs = new UpdateParameterArgs(identifierName, identifier.Id, value);

            await OnUpdateParameterAsync(identifierName, parameterArgs, cancellationToken).ConfigureAwait(false);

            if (!parameterArgs.UpdateParameterLists)
            {
                return value;
            }

            context.StaticParameters[context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? identifierName.ToLowerInvariant() : identifierName] = value;
        }
        return value;
    }

    public virtual async ValueTask<object?> Visit(BinaryExpression expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var left = new Lazy<ValueTask<object?>>(() => EvaluateAsync(expression.LeftExpression, cancellationToken),
            LazyThreadSafetyMode.None);
        var right = new Lazy<ValueTask<object?>>(() => EvaluateAsync(expression.RightExpression, cancellationToken),
            LazyThreadSafetyMode.None);

        var handlePercent = context.AdvancedOptions != null && context.AdvancedOptions.Flags.HasFlag(AdvExpressionOptions.CalculatePercent);

        object? leftValue = null;
        object? rightValue = null;

        switch (expression.Type)
        {
            case BinaryExpressionType.StatementSequence:
            {
                _ = await left.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                if (handlePercent && rightValue is Percent rValPercent)
                    rightValue = rValPercent.Value;
                return rightValue;
            }
            case BinaryExpressionType.Assignment:
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                return await UpdateParameterAsync(expression.LeftExpression, rightValue, cancellationToken).ConfigureAwait(false);

            case BinaryExpressionType.PlusAssignment:
            {
                var lval = await left.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(lval, out leftValue))
                    return null;
                var rval = await right.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(rval, out rightValue))
                    return null;

                if (handlePercent)
                {
                    if (lval is Percent lValPercent)
                        leftValue = lValPercent.Value;

                    if (rval is Percent rValPercent)
                        rightValue = rValPercent.Value;

                    if (lval is Percent && rval is Percent)
                    {
                        object? result = MathHelper.Add(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;
                        return await UpdateParameterAsync(expression.LeftExpression, new Percent(result), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    if (rval is Percent)
                        return await UpdateParameterAsync(expression.LeftExpression, MathHelper.AddPercent(leftValue, rightValue, context), cancellationToken).ConfigureAwait(false);
                    else
                    if (lval is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of a += operation cannot be a percent unless the right side is a percent as well");
                    }
                }

                return await UpdateParameterAsync(expression.LeftExpression, EvaluationHelper.Plus(leftValue, rightValue, context), cancellationToken).ConfigureAwait(false);
            }

            case BinaryExpressionType.MinusAssignment:
            {
                var lval = await left.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(lval, out leftValue))
                    return null;
                var rval = await right.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(rval, out rightValue))
                    return null;

                if (handlePercent)
                {
                    if (lval is Percent lValPercent)
                        leftValue = lValPercent.Value;

                    if (rval is Percent rValPercent)
                        rightValue = rValPercent.Value;

                    if (lval is Percent && rval is Percent)
                    {
                        object? result = MathHelper.Subtract(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;
                        return await UpdateParameterAsync(expression.LeftExpression, new Percent(result), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    if (rval is Percent)
                        return await UpdateParameterAsync(expression.LeftExpression, MathHelper.SubtractPercent(leftValue, rightValue, context), cancellationToken).ConfigureAwait(false);
                    else
                    if (lval is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of a -= operation cannot be a percent unless the right side is a percent as well");
                    }
                }

                return await UpdateParameterAsync(expression.LeftExpression, EvaluationHelper.Minus(leftValue, rightValue, context), cancellationToken).ConfigureAwait(false);
            }

            case BinaryExpressionType.MultiplyAssignment:
            {
                var lval = await left.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(lval, out leftValue))
                    return null;
                var rval = await right.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(rval, out rightValue))
                    return null;

                if (handlePercent)
                {
                    if (lval is Percent lValPerc && rval is Percent rValPerc)
                    {
                        object? result = MathHelper.MultiplyPercent(lValPerc.Value, rValPerc.Value, context);
                        if (result is null)
                            return null;
                        return await UpdateParameterAsync(expression.LeftExpression, new Percent(result), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    if (lval is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        object? result = MathHelper.Multiply(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;

                        return await UpdateParameterAsync(expression.LeftExpression, new Percent(result), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    if (rval is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;

                        object? result = MathHelper.MultiplyPercent(leftValue, rightValue, context);
                        if (result is null)
                            return null;

                        return await UpdateParameterAsync(expression.LeftExpression, result, cancellationToken).ConfigureAwait(false);
                    }
                }

                return await UpdateParameterAsync(expression.LeftExpression, MathHelper.Multiply(leftValue, rightValue, true, context), cancellationToken).ConfigureAwait(false);
            }

            case BinaryExpressionType.DivAssignment:
            {
                var lval = await left.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(lval, out leftValue))
                    return null;
                var rval = await right.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(rval, out rightValue))
                    return null;

                bool noConvertToDouble = IsReal(leftValue) || IsReal(rightValue) || leftValue is BigInteger || rightValue is BigInteger || leftValue is BigDecimal || rightValue is BigDecimal;

                if (handlePercent)
                {
                    if (lval is Percent lValPerc && rval is Percent rValPerc)
                    {
                        leftValue = lValPerc.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        object? result = MathHelper.DividePercent(leftValue, rValPerc.Value, context);
                        if (result is null)
                            return null;
                        return await UpdateParameterAsync(expression.LeftExpression, new Percent(result), cancellationToken).ConfigureAwait(false);
                    }
                    if (lval is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);
                        object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;

                        return await UpdateParameterAsync(expression.LeftExpression, new Percent(result), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    if (rval is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        object? result = MathHelper.DividePercent(leftValue, rightValue, context);
                        if (result is null)
                            return null;

                        return await UpdateParameterAsync(expression.LeftExpression, result, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (!noConvertToDouble)
                    leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                {
                    object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                    if (result is null)
                        return null;
                    return await UpdateParameterAsync(expression.LeftExpression, result, cancellationToken).ConfigureAwait(false);
                }
            }

            case BinaryExpressionType.AndAssignment:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                if (leftValue is BigInteger || rightValue is BigInteger)
                    return await UpdateParameterAsync(expression.LeftExpression,
                        MathHelper.BitwiseAnd(leftValue, rightValue), cancellationToken).ConfigureAwait(false);
                return await UpdateParameterAsync(expression.LeftExpression,
                    Convert.ToUInt64(leftValue, context.CultureInfo) &
                    Convert.ToUInt64(rightValue, context.CultureInfo)
                    , cancellationToken).ConfigureAwait(false);
            }
            case BinaryExpressionType.OrAssignment:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                if (leftValue is BigInteger || rightValue is BigInteger)
                    return await UpdateParameterAsync(expression.LeftExpression,
                        MathHelper.BitwiseOr(leftValue, rightValue), cancellationToken).ConfigureAwait(false);
                return await UpdateParameterAsync(expression.LeftExpression,
                    Convert.ToUInt64(leftValue, context.CultureInfo) |
                    Convert.ToUInt64(rightValue, context.CultureInfo)
                    , cancellationToken).ConfigureAwait(false);
            }
            case BinaryExpressionType.XOrAssignment:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                if (leftValue is BigInteger || rightValue is BigInteger)
                    return await UpdateParameterAsync(expression.LeftExpression,
                        MathHelper.BitwiseXOr(leftValue, rightValue), cancellationToken).ConfigureAwait(false);

                return await UpdateParameterAsync(expression.LeftExpression,
                    Convert.ToUInt64(leftValue, context.CultureInfo) ^
                    Convert.ToUInt64(rightValue, context.CultureInfo)
                    , cancellationToken).ConfigureAwait(false);
            }
            case BinaryExpressionType.And:
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!Convert.ToBoolean(leftValue, context.CultureInfo))
                    return false;

                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                return Convert.ToBoolean(rightValue, context.CultureInfo);

            case BinaryExpressionType.Or:
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (Convert.ToBoolean(leftValue, context.CultureInfo))
                    return true;

                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                return Convert.ToBoolean(rightValue, context.CultureInfo);

            case BinaryExpressionType.XOr:
                return Convert.ToBoolean(await left.Value.ConfigureAwait(false), context.CultureInfo) ^
                        Convert.ToBoolean(await right.Value.ConfigureAwait(false), context.CultureInfo);

            case BinaryExpressionType.Div:
            {
                var lval = await left.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(lval, out leftValue))
                    return null;
                var rval = await right.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(rval, out rightValue))
                    return null;

                bool noConvertToDouble = IsReal(leftValue) || IsReal(rightValue) || leftValue is BigInteger || rightValue is BigInteger || leftValue is BigDecimal || rightValue is BigDecimal;

                if (handlePercent)
                {
                    if (lval is Percent lValPerc && rval is Percent rValPerc)
                    {
                        leftValue = lValPerc.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        object? result = MathHelper.DividePercent(leftValue, rValPerc.Value, context);
                        if (result is null)
                            return null;
                        return new Percent(result);
                    }
                    if (lval is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);
                        object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;

                        return new Percent(result);
                    }
                    else
                    if (rval is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        object? result = MathHelper.DividePercent(leftValue, rightValue, context);
                        if (result is null)
                            return null;

                        return result;
                    }
                }

                if (!noConvertToDouble)
                    leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                {
                    object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                    if (result is null)
                        return null;
                    return result;
                }
            }
            case BinaryExpressionType.IntDivB:
            case BinaryExpressionType.IntDivP:
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                return MathHelper.IntegerDivide(leftValue, rightValue, (expression.Type == BinaryExpressionType.IntDivB), true, context);

            case BinaryExpressionType.Equal:
                return Compare(await left.Value.ConfigureAwait(false), await right.Value.ConfigureAwait(false), ComparisonType.Equal);

            case BinaryExpressionType.Greater:
                return Compare(await left.Value.ConfigureAwait(false), await right.Value.ConfigureAwait(false), ComparisonType.Greater);

            case BinaryExpressionType.GreaterOrEqual:
                return Compare(await left.Value.ConfigureAwait(false), await right.Value.ConfigureAwait(false), ComparisonType.GreaterOrEqual);

            case BinaryExpressionType.Less:
                return Compare(await left.Value.ConfigureAwait(false), await right.Value.ConfigureAwait(false), ComparisonType.Less);

            case BinaryExpressionType.LessOrEqual:
                return Compare(await left.Value.ConfigureAwait(false), await right.Value.ConfigureAwait(false), ComparisonType.LessOrEqual);

            case BinaryExpressionType.NotEqual:
                return Compare(await left.Value.ConfigureAwait(false), await right.Value.ConfigureAwait(false), ComparisonType.NotEqual);

            case BinaryExpressionType.Minus:
            {
                var lval = await left.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(lval, out leftValue))
                    return null;
                var rval = await right.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(rval, out rightValue))
                    return null;

                if (handlePercent)
                {
                    if (lval is Percent lValPercent)
                        leftValue = lValPercent.Value;

                    if (rval is Percent rValPercent)
                        rightValue = rValPercent.Value;

                    if (lval is Percent && rval is Percent)
                    {
                        object? result = MathHelper.Subtract(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;
                        return new Percent(result);
                    }
                    else
                    if (rval is Percent)
                        return MathHelper.SubtractPercent(leftValue, rightValue, context);
                    else
                    if (lval is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of a subtraction operation cannot be a percent unless the right side is a percent as well");
                    }
                }

                return EvaluationHelper.Minus(leftValue, rightValue, context);
            }

            case BinaryExpressionType.Modulo:
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                return MathHelper.Modulo(leftValue, rightValue, true, context);

            case BinaryExpressionType.Plus:
            {
                var lval = await left.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(lval, out leftValue))
                    return null;
                var rval = await right.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(rval, out rightValue))
                    return null;

                if (handlePercent)
                {
                    if (lval is Percent lValPercent)
                        leftValue = lValPercent.Value;

                    if (rval is Percent rValPercent)
                        rightValue = rValPercent.Value;

                    if (lval is Percent && rval is Percent)
                    {
                        object? result = MathHelper.Add(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;
                        return new Percent(result);
                    }
                    else
                    if (rval is Percent)
                        return MathHelper.AddPercent(leftValue, rightValue, context);
                    else
                    if (lval is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of an addition operation cannot be a percent unless the right side is a percent as well");
                    }
                }

                return EvaluationHelper.Plus(leftValue, rightValue, context);
            }

            case BinaryExpressionType.Times:
            {
                var lval = await left.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(lval, out leftValue))
                    return null;
                var rval = await right.Value.ConfigureAwait(false);
                if (!TryGetValueOrNull(rval, out rightValue))
                    return null;

                if (handlePercent)
                {
                    if (lval is Percent lValPerc && rval is Percent rValPerc)
                    {
                        object? result = MathHelper.MultiplyPercent(lValPerc.Value, rValPerc.Value, context);
                        if (result is null)
                            return null;
                        return new Percent(result);
                    }
                    else
                    if (lval is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        object? result = MathHelper.Multiply(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;

                        return new Percent(result);
                    }
                    else
                    if (rval is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;

                        object? result = MathHelper.MultiplyPercent(leftValue, rightValue, context);
                        if (result is null)
                            return null;

                        return result;
                    }
                }

                return MathHelper.Multiply(leftValue, rightValue, true, context);
            }

            case BinaryExpressionType.BitwiseAnd:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return MathHelper.BitwiseAnd(leftValue, rightValue);
                return Convert.ToUInt64(leftValue, context.CultureInfo) &
                    Convert.ToUInt64(rightValue, context.CultureInfo);
            }
            case BinaryExpressionType.BitwiseOr:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return MathHelper.BitwiseOr(leftValue, rightValue);
                return Convert.ToUInt64(leftValue, context.CultureInfo) |
                        Convert.ToUInt64(rightValue, context.CultureInfo);
            }
            case BinaryExpressionType.BitwiseXOr:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return MathHelper.BitwiseXOr(leftValue, rightValue);
                return Convert.ToUInt64(leftValue, context.CultureInfo) ^
                        Convert.ToUInt64(rightValue, context.CultureInfo);
            }
            case BinaryExpressionType.LeftShift:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                if (leftValue is BigInteger)
                    return MathHelper.LeftShift((BigInteger)leftValue, rightValue, context);
                return Convert.ToUInt64(leftValue, context.CultureInfo) <<
                        Convert.ToInt32(rightValue, context.CultureInfo);
            }
            case BinaryExpressionType.RightShift:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                if (leftValue is BigInteger)
                    return MathHelper.RightShift((BigInteger)leftValue, rightValue, context);
                return Convert.ToUInt64(leftValue, context.CultureInfo) >>
                        Convert.ToInt32(rightValue, context.CultureInfo);
            }
            case BinaryExpressionType.Exponentiation:
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;
                return MathHelper.Pow(leftValue, rightValue, true, context);

            case BinaryExpressionType.Factorial:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                return MathHelper.Factorial(leftValue!, rightValue!, context);
            }
            case BinaryExpressionType.In:
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                return EvaluationHelper.In(leftValue, rightValue, context);

            case BinaryExpressionType.NotIn:
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                return !EvaluationHelper.In(leftValue, rightValue, context);

            case BinaryExpressionType.Like:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                return await EvaluationHelper.LikeAsync(leftValue!, rightValue!, context, cancellationToken).ConfigureAwait(false);
            }

            case BinaryExpressionType.NotLike:
            {
                if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                    return null;
                if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                    return null;

                return !(await EvaluationHelper.LikeAsync(leftValue!, rightValue!, context, cancellationToken).ConfigureAwait(false));
            }
        }
        return null;
    }

    public virtual async ValueTask<object?> Visit(UnaryExpression expression, CancellationToken cancellationToken = default)
    {
        // Recursively evaluates the underlying expression
        var result = await expression.Expression.Accept(this, cancellationToken).ConfigureAwait(false);

        return EvaluationHelper.Unary(expression, result, context);
    }

    public virtual async ValueTask<object?> Visit(PercentExpression expression, CancellationToken cancellationToken = default)
    {
        object? result = await expression.Expression.Accept(this, cancellationToken).ConfigureAwait(false);
        if (result == null)
            return result;
        return new Percent(result);
    }

    public virtual async ValueTask<object?> Visit(Function function, CancellationToken cancellationToken = default)
    {
        var argsCount = function.Parameters.Count;
        var args = new AsyncExpression[argsCount];

        // Don't call parameters right now, instead let the function do it as needed.
        // Some parameters shouldn't be called, for instance, in a if(), the "not" value might be a division by zero
        // Evaluating every value could produce unexpected behavior
        for (var i = 0; i < argsCount; i++)
        {
            args[i] = new AsyncExpression(function.Parameters[i], context);
        }

        var functionName = function.Identifier.Name;
        var functionArgs = new AsyncFunctionArgs(function.Identifier.Id, args);

        await OnEvaluateFunctionAsync(functionName, functionArgs, cancellationToken).ConfigureAwait(false);

        if (functionArgs.HasResult)
        {
            return functionArgs.Result;
        }

        if (context.Functions.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? functionName.ToLowerInvariant() : functionName, out var expressionFunction))
        {
            return await expressionFunction(new AsyncExpressionFunctionData(function.Identifier.Id, args, context), cancellationToken).ConfigureAwait(false);
        }

        return await AsyncBuiltInFunctionHelper.EvaluateAsync(functionName, args, context, cancellationToken).ConfigureAwait(false);
    }

    public virtual async ValueTask<object?> Visit(Identifier identifier, CancellationToken cancellationToken = default)
    {
        var identifierName = identifier.Name;

        var parameterArgs = new ParameterArgs(identifier.Id);

        await OnEvaluateParameterAsync(identifierName, parameterArgs, cancellationToken).ConfigureAwait(false);

        if (parameterArgs.HasResult)
        {
            return parameterArgs.Result;
        }

        if (context.StaticParameters.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? identifierName.ToLowerInvariant() : identifierName, out var parameter))
        {
            if (parameter is AsyncExpression expression)
            {
                //Share the parameters with child expression.
                foreach (var p in context.StaticParameters)
                    expression.Parameters[p.Key] = p.Value;

                foreach (var p in context.DynamicParameters)
                    expression.DynamicParameters[p.Key] = p.Value;

                expression.EvaluateFunctionAsync += context.AsyncEvaluateFunctionHandler;
                expression.EvaluateParameterAsync += context.AsyncEvaluateParameterHandler;
                expression.UpdateParameterAsync += context.AsyncUpdateParameterHandler;
                expression.MatchStringAsync += context.AsyncMatchStringHandler;

                return await expression.EvaluateAsync(cancellationToken).ConfigureAwait(false);
            }

            return parameter;
        }

        if (context.DynamicParameters.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? identifierName.ToLowerInvariant() : identifierName, out var dynamicParameter))
        {
            return await dynamicParameter(new AsyncExpressionParameterData(identifier.Id, context), cancellationToken).ConfigureAwait(false);
        }

        throw new NCalcParameterNotDefinedException(identifierName);
    }

    public virtual ValueTask<object?> Visit(ValueExpression expression, CancellationToken cancellationToken = default) => new(expression.Value);

    public virtual async ValueTask<object?> Visit(LogicalExpressionList list, CancellationToken cancellationToken = default)
    {
        List<object?> result = [];

        foreach (var value in list)
        {
            result.Add(await EvaluateAsync(value, cancellationToken).ConfigureAwait(false));
            cancellationToken.ThrowIfCancellationRequested();
        }

        return result;
    }

    protected bool Compare(object? a, object? b, ComparisonType comparisonType)
    {
        if (context.Options.HasFlag(ExpressionOptions.StrictTypeMatching) && a?.GetType() != b?.GetType())
            return false;

        if (!context.Options.HasFlag(ExpressionOptions.CompareNullValues))
        {
            if ((a == null || b == null) && !(a == null && b == null))
                return false;
        }
        return EvaluationHelper.Compare(a, b, comparisonType, context);
    }

    protected ValueTask OnEvaluateFunctionAsync(string name, AsyncFunctionArgs args, CancellationToken cancellationToken = default)
    {
        return context.AsyncEvaluateFunctionHandler?.Invoke(name, args, cancellationToken) ?? default;
    }

    protected ValueTask OnEvaluateParameterAsync(string name, ParameterArgs args, CancellationToken cancellationToken = default)
    {
        return context.AsyncEvaluateParameterHandler?.Invoke(name, args, cancellationToken) ?? default;
    }
    protected ValueTask OnUpdateParameterAsync(string name, UpdateParameterArgs args, CancellationToken cancellationToken = default)
    {
        return context.AsyncUpdateParameterHandler?.Invoke(name, args, cancellationToken) ?? default;
    }

    protected ValueTask<object?> EvaluateAsync(LogicalExpression expression, CancellationToken cancellationToken = default)
    {
        return expression.Accept(this, cancellationToken);
    }
}
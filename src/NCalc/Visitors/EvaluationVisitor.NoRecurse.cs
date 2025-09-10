using System.Numerics;
using ExtendedNumerics;
using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Helpers;

using static NCalc.Helpers.TypeHelper;

namespace NCalc.Visitors;

/// <summary>
/// Class responsible to evaluating <see cref="LogicalExpression"/> objects into CLR objects.
/// </summary>
public partial class EvaluationVisitor : ILogicalExpressionVisitor<object?>, ILogicalExpressionNoRecurseVisitor<object?>
{
    protected object? SetTaskValue(ExpressionTask<object?> task, object? value)
    {
        return task.State.SetValue(value);
    }

    private bool ExpressionEvaluated(ExpressionTask<object?> task, int index, LogicalExpression expression)
    {
        if (task.ChildStates.Count <= index)
        {
            task.ChildStates.Add(new ExpressionState<object?>(expression));
            return false;
        }
        else
            return task.ChildStates[index].ValueSet;
    }

    private bool ExpressionsEvaluated(ExpressionTask<object?> task, LogicalExpression expression1, LogicalExpression expression2)
    {
        if (task.ChildStates.Count == 0)
        {
            task.ChildStates.Add(new ExpressionState<object?>(expression1));
        }
        else
        if (task.ChildStates.Count == 1)
        {
            task.ChildStates.Add(new ExpressionState<object?>(expression2));
        }
        else
        if (task.ChildStates[0].ValueSet && task.ChildStates[1].ValueSet)
            return true;

        return false;
    }

    public virtual object? Visit(TernaryExpression expression, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        object? value;

        // Request the value of the condition
        if (!ExpressionEvaluated(task, 0, expression.LeftExpression))
            return null;

        // Request the value of the middle or right expression
        if (task.ChildStates.Count == 1)
        {
            if (!TryGetValueOrNull(task.ChildStates[0].Value, out value))
                return null;

            task.ChildStates.Add(new ExpressionState<object?>(Convert.ToBoolean(value, context.CultureInfo) ? expression.MiddleExpression : expression.RightExpression));
            return null;
        }
        else
        if (!task.ChildStates[1].ValueSet)
            return null;

        return SetTaskValue(task, task.ChildStates[1].Value);
    }

    public virtual object? Visit(BinaryExpression expression, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        var handlePercent = context.AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.CalculatePercent) == true;

        object? leftValue = null;
        object? rightValue = null;

        switch (expression.Type)
        {
            /*case BinaryExpressionType.StatementSequence:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;

                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                if (handlePercent && rightValue is Percent rValPercent)
                    rightValue = rValPercent.Value;
                return SetTaskValue(task, rightValue);
            }*/

            case BinaryExpressionType.Assignment:
            {
                if (!ExpressionEvaluated(task, 0, expression.RightExpression))
                    return null;

                if (!TryGetValueOrNull(task.ChildStates[0].Value, out rightValue))
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, null, cancellationToken));

                return SetTaskValue(task, UpdateParameter(expression.LeftExpression, rightValue, cancellationToken));
            }

            case BinaryExpressionType.PlusAssignment:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, null, cancellationToken));

                if (handlePercent)
                {
                    bool leftPercent = false;
                    bool rightPercent = false;

                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        leftPercent = true;
                    }
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        rightPercent = true;
                    }

                    if (leftPercent && rightPercent)
                    {
                        object? result = MathHelper.Add(leftValue, rightValue, true, context);
                        if (result is null)
                            return SetTaskValue(task, null);
                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken));
                    }
                    else
                    if (rightPercent)
                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, MathHelper.AddPercent(leftValue, rightValue, context), cancellationToken));
                    else
                    if (leftPercent)
                    {
                        throw new NCalcEvaluationException("The left side of a += operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return SetTaskValue(task, UpdateParameter(expression.LeftExpression, EvaluationHelper.Plus(leftValue, rightValue, context), cancellationToken));
            }
            case BinaryExpressionType.MinusAssignment:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, null, cancellationToken));

                if (handlePercent)
                {
                    bool leftPercent = false;
                    bool rightPercent = false;

                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        leftPercent = true;
                    }
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        rightPercent = true;
                    }

                    if (leftPercent && rightPercent)
                    {
                        object? result = MathHelper.Subtract(leftValue, rightValue, true, context);
                        if (result is null)
                            return SetTaskValue(task, null);
                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken));
                    }
                    else
                    if (rightPercent)
                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, MathHelper.SubtractPercent(leftValue, rightValue, context), cancellationToken));
                    else
                    if (leftPercent)
                    {
                        throw new NCalcEvaluationException("The left side of a -= operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return SetTaskValue(task, UpdateParameter(expression.LeftExpression, EvaluationHelper.Minus(leftValue, rightValue, context), cancellationToken));
            }
            case BinaryExpressionType.MultiplyAssignment:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, null, cancellationToken));

                if (handlePercent)
                {
                    if (leftValue is Percent lValPerc && rightValue is Percent rValPerc)
                    {
                        object? result = MathHelper.MultiplyPercent(lValPerc.Value, rValPerc.Value, context);
                        if (result is null)
                            return SetTaskValue(task, null);
                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken));
                    }
                    else
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        object? result = MathHelper.Multiply(leftValue, rightValue, true, context);
                        if (result is null)
                            return SetTaskValue(task, null);

                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken));
                    }
                    else
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;

                        object? result = MathHelper.MultiplyPercent(leftValue, rightValue, context);
                        if (result is null)
                            return SetTaskValue(task, null);

                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, result, cancellationToken));
                    }
                }

                return SetTaskValue(task, UpdateParameter(expression.LeftExpression, MathHelper.Multiply(leftValue, rightValue, true, context), cancellationToken));
            }

            case BinaryExpressionType.DivAssignment:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, null, cancellationToken));

                bool noConvertToDouble = IsReal(leftValue) || IsReal(rightValue) || leftValue is BigInteger || rightValue is BigInteger || leftValue is BigDecimal || rightValue is BigDecimal;

                if (handlePercent)
                {
                    if (leftValue is Percent lValPerc && rightValue is Percent rValPerc)
                    {
                        leftValue = lValPerc.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        object? result = MathHelper.DividePercent(leftValue, rValPerc.Value, context);
                        if (result is null)
                            return SetTaskValue(task, null);
                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken));
                    }
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);
                        object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                        if (result is null)
                            return SetTaskValue(task, null);

                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken));
                    }
                    else
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        object? result = MathHelper.DividePercent(leftValue, rightValue, context);
                        if (result is null)
                            return SetTaskValue(task, null);

                        return SetTaskValue(task, UpdateParameter(expression.LeftExpression, result, cancellationToken));
                    }
                }

                if (!noConvertToDouble)
                    leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                {
                    object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                    if (result is null)
                        return SetTaskValue(task, null);
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, result, cancellationToken));
                }
            }

            case BinaryExpressionType.AndAssignment:

                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, null, cancellationToken));

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, MathHelper.BitwiseAnd(leftValue, rightValue), cancellationToken));

                return SetTaskValue(task, UpdateParameter(expression.LeftExpression, Convert.ToUInt64(leftValue, context.CultureInfo) &
                    Convert.ToUInt64(rightValue, context.CultureInfo), cancellationToken));

            case BinaryExpressionType.OrAssignment:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, null, cancellationToken));

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, MathHelper.BitwiseOr(leftValue, rightValue), cancellationToken));
                return SetTaskValue(task, UpdateParameter(expression.LeftExpression, Convert.ToUInt64(leftValue, context.CultureInfo) |
                    Convert.ToUInt64(rightValue, context.CultureInfo), cancellationToken));

            case BinaryExpressionType.XOrAssignment:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, null, cancellationToken));

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return SetTaskValue(task, UpdateParameter(expression.LeftExpression, MathHelper.BitwiseXOr(leftValue, rightValue), cancellationToken));
                return SetTaskValue(task, UpdateParameter(expression.LeftExpression, Convert.ToUInt64(leftValue, context.CultureInfo) ^
                    Convert.ToUInt64(rightValue, context.CultureInfo), cancellationToken));

            case BinaryExpressionType.And:
                if (!ExpressionEvaluated(task, 0, expression.LeftExpression))
                    return null;

                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!Convert.ToBoolean(leftValue, context.CultureInfo))
                    return SetTaskValue(task, false);

                if (!ExpressionEvaluated(task, 1, expression.RightExpression))
                    return null;

                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                return SetTaskValue(task, Convert.ToBoolean(rightValue, context.CultureInfo));

            case BinaryExpressionType.Or:
                if (!ExpressionEvaluated(task, 0, expression.LeftExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (Convert.ToBoolean(leftValue, context.CultureInfo))
                    return SetTaskValue(task, true);

                if (!ExpressionEvaluated(task, 1, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);
                return SetTaskValue(task, Convert.ToBoolean(rightValue, context.CultureInfo));

            case BinaryExpressionType.XOr:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);
                return SetTaskValue(task, Convert.ToBoolean(leftValue, context.CultureInfo) ^
                        Convert.ToBoolean(rightValue, context.CultureInfo));

            case BinaryExpressionType.Div:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                bool noConvertToDouble = IsReal(leftValue) || IsReal(rightValue) || leftValue is BigInteger || rightValue is BigInteger || leftValue is BigDecimal || rightValue is BigDecimal;

                if (handlePercent)
                {
                    if (leftValue is Percent lValPerc && rightValue is Percent rValPerc)
                    {
                        leftValue = lValPerc.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        object? result = MathHelper.DividePercent(leftValue, rValPerc.Value, context);
                        if (result is null)
                            return SetTaskValue(task, null);
                        return SetTaskValue(task, new Percent(result));
                    }
                    else
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);
                        object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                        if (result is null)
                            return SetTaskValue(task, null);

                        return SetTaskValue(task, new Percent(result));
                    }
                    else
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        object? result = MathHelper.DividePercent(leftValue, rightValue, context);
                        if (result is null)
                            return SetTaskValue(task, null);

                        return SetTaskValue(task, result);
                    }
                }

                if (!noConvertToDouble)
                    leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                {
                    object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                    if (result is null)
                        return SetTaskValue(task, null);
                    return SetTaskValue(task, result);
                }
            }

            case BinaryExpressionType.IntDivB:
            case BinaryExpressionType.IntDivP:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                return SetTaskValue(task, MathHelper.IntegerDivide(leftValue, rightValue, (expression.Type == BinaryExpressionType.IntDivB), true, context));

            case BinaryExpressionType.Equal:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;

                return SetTaskValue(task, Compare(task.ChildStates[0].Value, task.ChildStates[1].Value, ComparisonType.Equal));

            case BinaryExpressionType.Greater:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                return SetTaskValue(task, Compare(task.ChildStates[0].Value, task.ChildStates[1].Value, ComparisonType.Greater));

            case BinaryExpressionType.GreaterOrEqual:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                return SetTaskValue(task, Compare(task.ChildStates[0].Value, task.ChildStates[1].Value, ComparisonType.GreaterOrEqual));

            case BinaryExpressionType.Less:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                return SetTaskValue(task, Compare(task.ChildStates[0].Value, task.ChildStates[1].Value, ComparisonType.Less));

            case BinaryExpressionType.LessOrEqual:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                return SetTaskValue(task, Compare(task.ChildStates[0].Value, task.ChildStates[1].Value, ComparisonType.LessOrEqual));

            case BinaryExpressionType.NotEqual:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                return SetTaskValue(task, Compare(task.ChildStates[0].Value, task.ChildStates[1].Value, ComparisonType.NotEqual));

            case BinaryExpressionType.Minus:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                if (handlePercent)
                {
                    bool leftPercent = false;
                    bool rightPercent = false;

                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        leftPercent = true;
                    }
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        rightPercent = true;
                    }

                    if (leftPercent && rightPercent)
                    {
                        object? result = MathHelper.Subtract(leftValue, rightValue, true, context);
                        if (result is null)
                            return SetTaskValue(task, null);
                        return SetTaskValue(task, new Percent(result));
                    }
                    else
                    if (rightPercent)
                        return SetTaskValue(task, MathHelper.SubtractPercent(leftValue, rightValue, context));
                    else
                    if (leftPercent)
                    {
                        throw new NCalcEvaluationException("The left side of a subtraction operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return SetTaskValue(task, EvaluationHelper.Minus(leftValue, rightValue, context));
            }

            case BinaryExpressionType.Modulo:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);
                return SetTaskValue(task, MathHelper.Modulo(leftValue, rightValue, true, context));

            case BinaryExpressionType.Plus:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                if (handlePercent)
                {
                    bool leftPercent = false;
                    bool rightPercent = false;

                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        leftPercent = true;
                    }
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        rightPercent = true;
                    }

                    if (leftPercent && rightPercent)
                    {
                        object? result = MathHelper.Add(leftValue, rightValue, true, context);
                        if (result is null)
                            return SetTaskValue(task, null);
                        return SetTaskValue(task, new Percent(result));
                    }
                    else
                    if (rightPercent)
                        return SetTaskValue(task, MathHelper.AddPercent(leftValue, rightValue, context));
                    else
                    if (leftPercent)
                    {
                        throw new NCalcEvaluationException("The left side of an addition operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return SetTaskValue(task, EvaluationHelper.Plus(leftValue, rightValue, context));
            }

            case BinaryExpressionType.Times:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                if (handlePercent)
                {
                    if (leftValue is Percent lValPerc && rightValue is Percent rValPerc)
                    {
                        object? result = MathHelper.MultiplyPercent(lValPerc.Value, rValPerc.Value, context);
                        if (result is null)
                            return SetTaskValue(task, null);
                        return SetTaskValue(task, new Percent(result));
                    }
                    else
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        object? result = MathHelper.Multiply(leftValue, rightValue, true, context);
                        if (result is null)
                            return SetTaskValue(task, null);
                        return SetTaskValue(task, new Percent(result));
                    }
                    else
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;

                        object? result = MathHelper.MultiplyPercent(leftValue, rightValue, context);
                        if (result is null)
                            return SetTaskValue(task, null);

                        return SetTaskValue(task, result);
                    }
                }

                return SetTaskValue(task, MathHelper.Multiply(leftValue, rightValue, true, context));
            }

            case BinaryExpressionType.BitwiseAnd:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return SetTaskValue(task, MathHelper.BitwiseAnd(leftValue, rightValue));
                return SetTaskValue(task, Convert.ToUInt64(leftValue, context.CultureInfo) &
                        Convert.ToUInt64(rightValue, context.CultureInfo));

            case BinaryExpressionType.BitwiseOr:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return SetTaskValue(task, MathHelper.BitwiseOr(leftValue, rightValue));
                return SetTaskValue(task, Convert.ToUInt64(leftValue, context.CultureInfo) |
                        Convert.ToUInt64(rightValue, context.CultureInfo));

            case BinaryExpressionType.BitwiseXOr:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return SetTaskValue(task, MathHelper.BitwiseXOr(leftValue, rightValue));
                return SetTaskValue(task, Convert.ToUInt64(leftValue, context.CultureInfo) ^
                        Convert.ToUInt64(rightValue, context.CultureInfo));

            case BinaryExpressionType.LeftShift:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                if (leftValue is BigInteger)
                    return SetTaskValue(task, MathHelper.LeftShift((BigInteger)leftValue, rightValue, context));

                return SetTaskValue(task, Convert.ToUInt64(leftValue, context.CultureInfo) << Convert.ToInt32(rightValue, context.CultureInfo));

            case BinaryExpressionType.RightShift:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                if (leftValue is BigInteger)
                    return SetTaskValue(task, MathHelper.RightShift((BigInteger)leftValue, rightValue, context));

                return SetTaskValue(task, Convert.ToUInt64(leftValue, context.CultureInfo) >> Convert.ToInt32(rightValue, context.CultureInfo));

            case BinaryExpressionType.Exponentiation:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                return SetTaskValue(task, MathHelper.Pow(leftValue, rightValue, true, context));

            case BinaryExpressionType.Factorial:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                return SetTaskValue(task, MathHelper.Factorial(leftValue!, rightValue!, context));

            case BinaryExpressionType.In:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                return SetTaskValue(task, EvaluationHelper.In(rightValue, leftValue, context));

            case BinaryExpressionType.NotIn:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                return SetTaskValue(task, !EvaluationHelper.In(rightValue, leftValue, context));

            case BinaryExpressionType.Like:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                return SetTaskValue(task, EvaluationHelper.Like(leftValue!, rightValue!, context));
            }

            case BinaryExpressionType.NotLike:
            {
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;
                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    return SetTaskValue(task, null);
                if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                    return SetTaskValue(task, null);

                return SetTaskValue(task, !EvaluationHelper.Like(leftValue!, rightValue!, context));
            }

            case BinaryExpressionType.RangeIndex:
                if (!ExpressionsEvaluated(task, expression.LeftExpression, expression.RightExpression))
                    return null;

                leftValue = task.ChildStates[0].Value;
                rightValue = task.ChildStates[1].Value;

                if (leftValue is not null && leftValue is not NCalc.Domain.Index && !MathHelper.IsBoxedNumberOrBigNumber(leftValue))
                    throw new NCalcParameterIndexException("The lower boundary, unless omitted, should evaluate to zero or an integer number", expression.LeftExpression.Location);
                if (rightValue is not null && rightValue is not NCalc.Domain.Index && !MathHelper.IsBoxedNumberOrBigNumber(rightValue))
                    throw new NCalcParameterIndexException("The upper boundary, unless omitted, should evaluate to zero or an integer number", expression.RightExpression.Location);

                int? leftInt = (leftValue is null) ? null : (leftValue is NCalc.Domain.Index leftIdx) ? leftIdx.Value : MathHelper.ConvertToInt(leftValue, context);
                int? rightInt = (rightValue is null) ? null : (rightValue is NCalc.Domain.Index rightIdx) ? rightIdx.Value : MathHelper.ConvertToInt(rightValue, context);

                if (leftInt.HasValue && leftInt < 0)
                    throw new NCalcParameterIndexException("The lower boundary should be zero or a positive number", expression.LeftExpression.Location);

                if (rightInt.HasValue && rightInt < 0)
                    throw new NCalcParameterIndexException("The upper boundary should be zero or a positive number", expression.RightExpression.Location);

                return SetTaskValue(task, new RangeValue
                    {
                        LowerBound = leftInt is null ? null : (leftValue is NCalc.Domain.Index leftIdx2) ? leftIdx2 : new NCalc.Domain.Index(leftInt.Value),
                        UpperBound = rightInt is null ? null : (rightValue is NCalc.Domain.Index rightIdx2) ? rightIdx2 : new NCalc.Domain.Index(rightInt.Value),
                    });

            case BinaryExpressionType.IndexAccess:
            {
                if (!ExpressionEvaluated(task, 0, expression.LeftExpression))
                    return null;

                if (!TryGetValueOrNull(task.ChildStates[0].Value, out leftValue))
                    throw new NCalcParameterIndexException("An expression, if used with an index, must denote a list or a string", expression.LeftExpression.Location);

                IList? identList = null;
                string? identString = null;

                if (leftValue is IList)
                    identList = (IList)leftValue;
                else
                if (leftValue is string)
                    identString = (string)leftValue;
                else
                    throw new NCalcParameterIndexException("An expression, if used with an index, must denote a list or a string", expression.LeftExpression.Location);

                object? result = null;

                if (expression.RightExpression is BinaryExpression binExpr && binExpr.Type == BinaryExpressionType.RangeIndex)
                {
                    if (!ExpressionEvaluated(task, 1, binExpr))
                        return null;

                    RangeValue? range = (RangeValue?)task.ChildStates[1].Value;
                    if (range is null)
                        return SetTaskValue(task, null);

                    int lowerBound;
                    int upperBound;

                    if (identList is not null)
                    {
                        lowerBound = (range.LowerBound?.IsFromEnd == true) ? (identList.Count - range.LowerBound.Value.Value) : (range.LowerBound?.Value ?? 0);
                        upperBound = (range.UpperBound?.IsFromEnd == true) ? (identList.Count - range.UpperBound.Value.Value) : (range.UpperBound?.Value ?? identList.Count);

                        if (lowerBound > upperBound)
                            throw new NCalcParameterIndexException("The upper boundary (the actual value is {upperBound}) should be equal to or larger than the lower boundary (the actual value is {lowerBound})", expression.RightExpression.Location);

                        if (lowerBound >= identList.Count || upperBound > identList.Count)
                            throw new NCalcParameterIndexException($"The index range [{lowerBound}..{upperBound}] goes out out of the list bounds [0; {identList.Count - 1}]", expression.RightExpression.Location);

                        if (upperBound == lowerBound)
                            return SetTaskValue(task, new object?[0]);

                        object?[] resultArr = new object?[upperBound - lowerBound];
                        for (int i = 0; i < resultArr.Length; i++)
                        {
                            result = resultArr[lowerBound + i];
                            if (result is LogicalExpression expr)
                                result = expr.AcceptNoRecurse(this, new ExpressionTask<object?>(null, new ExpressionState<object?>(expr)), cancellationToken);
                            resultArr[i] = result;
                        }
                        result = resultArr;
                    }
                    else
                    if (identString is not null)
                    {
                        lowerBound = (range.LowerBound?.IsFromEnd == true) ? (identString.Length - range.LowerBound.Value.Value) : (range.LowerBound?.Value ?? 0);
                        upperBound = (range.UpperBound?.IsFromEnd == true) ? (identString.Length - range.UpperBound.Value.Value) : (range.UpperBound?.Value ?? identString.Length);

                        if (lowerBound > upperBound)
                            throw new NCalcParameterIndexException("The upper boundary (the actual value is {upperBound}) should be equal to or larger than the lower boundary (the actual value is {lowerBound})", expression.RightExpression.Location);

                        if (lowerBound >= identString.Length || upperBound > identString.Length)
                            throw new NCalcParameterIndexException($"The range [{lowerBound}..{upperBound}] is out of bounds [0; {identString.Length - 1}]", expression.RightExpression.Location);

                        if (upperBound == lowerBound)
                            return SetTaskValue(task, string.Empty);

                        result = identString[lowerBound..upperBound];
                    }

                    return SetTaskValue(task, result);
                }
                else
                {
                    if (!ExpressionEvaluated(task, 1, expression.RightExpression))
                        return null;

                    if (!TryGetValueOrNull(task.ChildStates[1].Value, out rightValue))
                        throw new NCalcParameterIndexException("The index does not evaluate to a number", expression.RightExpression.Location);

                    int index;
                    try
                    {
                        index = MathHelper.ConvertToInt(rightValue, context);
                    }
                    catch
                    {
                        throw new NCalcParameterIndexException("The index does not evaluate to a number", expression.RightExpression.Location);
                    }

                    if (identList is not null)
                    {
                        if (index < 0 || index >= identList.Count)
                            throw new NCalcParameterIndexException($"The index is out of bounds [0; {identList.Count - 1}]", expression.RightExpression.Location);
                        result = identList[index];
                    }
                    else
                    if (identString is not null)
                    {
                        if (index < 0 || index >= identString.Length)
                            throw new NCalcParameterIndexException($"The index is out of bounds [0; {identString.Length - 1}]", expression.RightExpression.Location);
                        result = identString[index];
                    }
                    if (result is LogicalExpression expr)
                        result = expr.Accept(this, cancellationToken);
                }
                return SetTaskValue(task, result);
            }

            case BinaryExpressionType.WhileLoop:
            {
                object? result = null;

                int ctr = 0;

                while (ctr < Expression.MaxLoopIterations)
                {
                    ctr++;

                    if (!TryGetValueOrNull(EvaluateNoRecurse(expression.LeftExpression, cancellationToken), out leftValue))
                        break;

                    if (!Convert.ToBoolean(leftValue, context.CultureInfo))
                        break;

                    try
                    {
                        TryGetValueOrNull(EvaluateNoRecurse(expression.RightExpression, cancellationToken), out result);
                    }
                    catch (NCalcFlowControl fc)
                    {
                        if (fc.Type == NCalcFlowControl.FlowControlType.Break)
                            break;
                        /*else
                        if (fc.Type == NCalcFlowControl.FlowControlType.Continue)
                            continue;*/
                    }
                }
                return SetTaskValue(task, result);
            }
        }

        return SetTaskValue(task, null);
    }

    public virtual object? Visit(UnaryExpression expression, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        // Request the value of the backing expression
        if (!ExpressionEvaluated(task, 0, expression.Expression))
            return null;

        object? result = null;
        if (!TryGetValueOrNull(task.ChildStates[0].Value, out result))
            return SetTaskValue(task, null);

        return SetTaskValue(task, EvaluationHelper.Unary(expression, result, context));
    }

    public virtual object? Visit(PercentExpression expression, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        // Request the value of the backing expression
        if (!ExpressionEvaluated(task, 0, expression.Expression))
            return null;

        object? result = null;
        if (!TryGetValueOrNull(task.ChildStates[0].Value, out result))
            return SetTaskValue(task, null);

        return SetTaskValue(task, new Percent(result!));
    }

    public virtual object? Visit(ValueExpression expression, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        return SetTaskValue(task, expression.Value);
    }

    public virtual object? Visit(FunctionExpression expression, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        return SetTaskValue(task, null);
    }

    public virtual object? Visit(FunctionCall function, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        return SetTaskValue(task, Visit(function, cancellationToken));
    }

    public virtual object? Visit(Identifier identifier, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        return SetTaskValue(task, Visit(identifier, cancellationToken));
    }

    public virtual object? Visit(LogicalExpressionList list, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        if (task.ChildStates.Count < list.Count)
        {
            foreach(LogicalExpression expression in list)
                if (task.ChildStates.Any(e => e.Expression == expression) == false)
                    task.ChildStates.Add(new ExpressionState<object?>(expression));
            return null;
        }
        foreach (var state in task.ChildStates)
        {
            if (!state.ValueSet)
                return null;
        }

        List<object?> result = [];

        foreach (var state in task.ChildStates)
        {
            result.Add(state.Value);
        }

        return SetTaskValue(task, result);
    }

    public virtual object? Visit(ExpressionGroup group, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        // Request the value of the backing expression
        if (!ExpressionEvaluated(task, 0, group.Expression))
            return null;

        return SetTaskValue(task, task.ChildStates[0].Value);
    }

    public object? Visit(StatementSequence seq, ExpressionTask<object?> task, CancellationToken cancellationToken = default)
    {
        object? result = null;

        if (seq.Count == 0)
            return SetTaskValue(task, null);

        if (task.ChildStates.Count < seq.Count)
        {
            foreach (LogicalExpression expression in seq)
                if (task.ChildStates.Any(e => e.Expression == expression) == false)
                    task.ChildStates.Add(new ExpressionState<object?>(expression));
            return null;
        }
        foreach (var state in task.ChildStates)
        {
            if (!state.ValueSet)
                return null;
        }
        result = task.ChildStates[^1].Value;

        /*if ((context.AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.CalculatePercent) == true) && result is Percent valPercent)
            result = valPercent.Value;*/

        return SetTaskValue(task, result);
    }
}
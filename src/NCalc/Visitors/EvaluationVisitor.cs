using System.Numerics;
using ExtendedNumerics;
using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Handlers;
using NCalc.Helpers;

using static NCalc.Helpers.TypeHelper;

namespace NCalc.Visitors;

/// <summary>
/// Class responsible to evaluating <see cref="LogicalExpression"/> objects into CLR objects.
/// </summary>
public partial class EvaluationVisitor : ILogicalExpressionVisitor<object?>, ILogicalExpressionNoRecurseVisitor<object?>
{
    private readonly ExpressionContext context;

    public EvaluationVisitor(ExpressionContext context)
    {
        this.context = context;
    }

    public EvaluationVisitor(Expression parentExpression)
    {
        this.context = parentExpression.Context;
    }

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

    public virtual object? Visit(TernaryExpression expression, CancellationToken cancellationToken = default)
    {
        if (!TryGetValueOrNull(expression.LeftExpression.Accept(this, cancellationToken), out object? value))
            return null;

        return (Convert.ToBoolean(value, context.CultureInfo) ? expression.MiddleExpression : expression.RightExpression).Accept(this, cancellationToken);
    }

    private object? UpdateParameter(LogicalExpression leftExpression, object? value, CancellationToken cancellationToken = default)
    {
        if (value is null && !context.Options.HasFlag(ExpressionOptions.AllowNullParameter))
        {
            return value;
        }

        if (leftExpression is BinaryExpression binExpr && binExpr.Type == BinaryExpressionType.IndexAccess)
        {
            if (binExpr.LeftExpression is Identifier ident)
            {
                var identifierName = ident.Name;

                var indexObj = binExpr.RightExpression.Accept(this, cancellationToken);
                if (!MathHelper.IsBoxedIntegerNumberOrBigNumber(indexObj))
                    throw new NCalcParameterIndexException(identifierName, $"The index of {identifierName} does not evaluate to a number", binExpr.RightExpression.Location);
                var index = MathHelper.ConvertToInt(indexObj, context);

                var parameterArgs = new UpdateParameterArgs(identifierName, ident.Id, index, value);

                OnUpdateParameter(identifierName, parameterArgs);

                if (!parameterArgs.UpdateParameterLists)
                {
                    return value;
                }

                if (!context.StaticParameters.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? identifierName.ToLowerInvariant() : identifierName, out object? staticParam) || staticParam is null)
                    throw new NCalcParameterIndexException(identifierName, $"{identifierName} is not set and cannot be assigned to by index", binExpr.LeftExpression.Location);

                if (staticParam is string strParam)
                {
                    if (value is char || (value is string && ((string)value).Length == 1))
                    {
                        if (strParam.Length <= index)
                            throw new NCalcParameterIndexException(identifierName, $"A character in the '{identifierName}' string cannot be updated by index: the string has the length of {strParam.Length}, while the index is {index}", binExpr.RightExpression.Location);

                        char charValue;

                        if (value is string strValue)
                            charValue = strValue[0];
                        else
                            charValue = (char)value;

                        strParam = strParam[0..index] + charValue + strParam[(index + 1)..];
                        context.StaticParameters[context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? identifierName.ToLowerInvariant() : identifierName] = strParam;
                    }
                    else
                        throw new NCalcParameterIndexException(identifierName, $"When updating a string in '{identifierName}' via the index, the value must be a character or a one-character string", binExpr.RightExpression.Location);
                }
                else
                {
                    if (staticParam is not IList list)
                        throw new NCalcParameterIndexException(identifierName, $"'{identifierName}' is not a list or a string and cannot be assigned to by index", binExpr.LeftExpression.Location);

                    if (list.IsReadOnly)
                        throw new NCalcParameterIndexException(identifierName, $"'{identifierName}' is read-only and cannot be assigned to by index", binExpr.LeftExpression.Location);

                    if (list.Count <= index)
                        throw new NCalcParameterIndexException(identifierName, $"'{identifierName}' cannot be assigned to by index: it has {list.Count} elements, while the index to update is {index}", binExpr.RightExpression.Location);

                    list[index] = value;
                }
            }
            else
                throw new NCalcEvaluationException("The expression should evaluate to an identifier", binExpr.Location);
        }
        else
        if (leftExpression is Identifier identifier)
        {
            var identifierName = identifier.Name;

            var parameterArgs = new UpdateParameterArgs(identifierName, identifier.Id, value);

            OnUpdateParameter(identifierName, parameterArgs);

            if (!parameterArgs.UpdateParameterLists)
            {
                return value;
            }

            context.StaticParameters[context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? identifierName.ToLowerInvariant() : identifierName] = value;
        }
        return value;
    }

    public virtual object? Visit(BinaryExpression expression, CancellationToken cancellationToken = default)
    {
        var left = new Lazy<object?>(() => Evaluate(expression.LeftExpression, cancellationToken), LazyThreadSafetyMode.None);
        var right = new Lazy<object?>(() => Evaluate(expression.RightExpression, cancellationToken), LazyThreadSafetyMode.None);

        var handlePercent = context.AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.CalculatePercent) == true;

        object? leftValue = null;
        object? rightValue = null;

        switch (expression.Type)
        {
            case BinaryExpressionType.Assignment:
            {
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return UpdateParameter(expression.LeftExpression, null, cancellationToken);

                return UpdateParameter(expression.LeftExpression, rightValue, cancellationToken);
            }

            case BinaryExpressionType.PlusAssignment:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return UpdateParameter(expression.LeftExpression, null, cancellationToken);

                if (handlePercent)
                {
                    if (leftValue is Percent lValPercent)
                        leftValue = lValPercent.Value;

                    if (rightValue is Percent rValPercent)
                        rightValue = rValPercent.Value;

                    if (left.Value is Percent && right.Value is Percent)
                    {
                        object? result = MathHelper.Add(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;
                        return UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken);
                    }
                    else
                    if (right.Value is Percent)
                        return UpdateParameter(expression.LeftExpression, MathHelper.AddPercent(leftValue, rightValue, context), cancellationToken);
                    else
                    if (left.Value is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of a += operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return UpdateParameter(expression.LeftExpression, EvaluationHelper.Plus(leftValue, rightValue, context), cancellationToken);
            }
            case BinaryExpressionType.MinusAssignment:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return UpdateParameter(expression.LeftExpression, null, cancellationToken);

                if (handlePercent)
                {
                    if (leftValue is Percent lValPercent)
                        leftValue = lValPercent.Value;

                    if (rightValue is Percent rValPercent)
                        rightValue = rValPercent.Value;

                    if (left.Value is Percent && right.Value is Percent)
                    {
                        object? result = MathHelper.Subtract(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;
                        return UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken);
                    }
                    else
                    if (right.Value is Percent)
                        return UpdateParameter(expression.LeftExpression, MathHelper.SubtractPercent(leftValue, rightValue, context), cancellationToken);
                    else
                    if (left.Value is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of a -= operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return UpdateParameter(expression.LeftExpression, EvaluationHelper.Minus(leftValue, rightValue, context), cancellationToken);
            }
            case BinaryExpressionType.MultiplyAssignment:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return UpdateParameter(expression.LeftExpression, null, cancellationToken);

                if (handlePercent)
                {
                    if (leftValue is Percent lValPerc && rightValue is Percent rValPerc)
                    {
                        object? result = MathHelper.MultiplyPercent(lValPerc.Value, rValPerc.Value, context);
                        if (result is null)
                            return null;
                        return UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken);
                    }
                    else
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        object? result = MathHelper.Multiply(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;

                        return UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken);
                    }
                    else
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;

                        object? result = MathHelper.MultiplyPercent(leftValue, rightValue, context);
                        if (result is null)
                            return null;

                        return UpdateParameter(expression.LeftExpression, result, cancellationToken);
                    }
                }

                return UpdateParameter(expression.LeftExpression, MathHelper.Multiply(leftValue, rightValue, true, context), cancellationToken);
            }

            case BinaryExpressionType.DivAssignment:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return UpdateParameter(expression.LeftExpression, null, cancellationToken);

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
                            return null;
                        return UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken);
                    }
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);
                        object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;

                        return UpdateParameter(expression.LeftExpression, new Percent(result), cancellationToken);
                    }
                    else
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        object? result = MathHelper.DividePercent(leftValue, rightValue, context);
                        if (result is null)
                            return null;

                        return UpdateParameter(expression.LeftExpression, result, cancellationToken);
                    }
                }

                if (!noConvertToDouble)
                    leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                {
                    object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                    if (result is null)
                        return null;
                    return UpdateParameter(expression.LeftExpression, result, cancellationToken);
                }
            }

            case BinaryExpressionType.AndAssignment:

                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return UpdateParameter(expression.LeftExpression, null, cancellationToken);

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return UpdateParameter(expression.LeftExpression, MathHelper.BitwiseAnd(leftValue, rightValue), cancellationToken);
                return UpdateParameter(expression.LeftExpression, Convert.ToUInt64(leftValue, context.CultureInfo) &
                    Convert.ToUInt64(rightValue, context.CultureInfo)
, cancellationToken);

            case BinaryExpressionType.OrAssignment:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;

                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return UpdateParameter(expression.LeftExpression, null, cancellationToken);

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return UpdateParameter(expression.LeftExpression, MathHelper.BitwiseOr(leftValue, rightValue), cancellationToken);
                return UpdateParameter(expression.LeftExpression, Convert.ToUInt64(leftValue, context.CultureInfo) |
                    Convert.ToUInt64(rightValue, context.CultureInfo)
, cancellationToken);

            case BinaryExpressionType.XOrAssignment:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return UpdateParameter(expression.LeftExpression, null, cancellationToken);

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return UpdateParameter(expression.LeftExpression, MathHelper.BitwiseXOr(leftValue, rightValue), cancellationToken);
                return UpdateParameter(expression.LeftExpression, Convert.ToUInt64(leftValue, context.CultureInfo) ^
                    Convert.ToUInt64(rightValue, context.CultureInfo)
, cancellationToken);

            case BinaryExpressionType.And:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!Convert.ToBoolean(leftValue, context.CultureInfo))
                    return false;

                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                return Convert.ToBoolean(rightValue, context.CultureInfo);

            case BinaryExpressionType.Or:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (Convert.ToBoolean(leftValue, context.CultureInfo))
                    return true;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;
                return Convert.ToBoolean(rightValue, context.CultureInfo);

            case BinaryExpressionType.XOr:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;
                return Convert.ToBoolean(leftValue, context.CultureInfo) ^
                        Convert.ToBoolean(rightValue, context.CultureInfo);

            case BinaryExpressionType.Div:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

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
                            return null;
                        return new Percent(result);
                    }
                    else
                    if (leftValue is Percent lValPercent)
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
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                        return MathHelper.DividePercent(leftValue, rightValue, context);
                    }
                }

                if (!noConvertToDouble)
                    leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                return MathHelper.Divide(leftValue, rightValue, true, context);
            }

            case BinaryExpressionType.IntDivB:
            case BinaryExpressionType.IntDivP:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;
                return MathHelper.IntegerDivide(leftValue, rightValue, (expression.Type == BinaryExpressionType.IntDivB), true, context);

            case BinaryExpressionType.Equal:
                return Compare(left.Value, right.Value, ComparisonType.Equal);

            case BinaryExpressionType.Greater:
                return Compare(left.Value, right.Value, ComparisonType.Greater);

            case BinaryExpressionType.GreaterOrEqual:
                return Compare(left.Value, right.Value, ComparisonType.GreaterOrEqual);

            case BinaryExpressionType.Less:
                return Compare(left.Value, right.Value, ComparisonType.Less);

            case BinaryExpressionType.LessOrEqual:
                return Compare(left.Value, right.Value, ComparisonType.LessOrEqual);

            case BinaryExpressionType.NotEqual:
                return Compare(left.Value, right.Value, ComparisonType.NotEqual);

            case BinaryExpressionType.Minus:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                if (handlePercent)
                {
                    if (leftValue is Percent lValPercent)
                        leftValue = lValPercent.Value;

                    if (rightValue is Percent rValPercent)
                        rightValue = rValPercent.Value;

                    if (left.Value is Percent && right.Value is Percent)
                    {
                        object? result = MathHelper.Subtract(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;
                        return new Percent(result);
                    }
                    else
                    if (right.Value is Percent)
                        return MathHelper.SubtractPercent(leftValue, rightValue, context);
                    else
                    if (left.Value is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of a subtraction operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return EvaluationHelper.Minus(leftValue, rightValue, context);
            }

            case BinaryExpressionType.Modulo:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                return MathHelper.Modulo(leftValue, rightValue, true, context);

            case BinaryExpressionType.Plus:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                if (handlePercent)
                {
                    if (leftValue is Percent lValPercent)
                        leftValue = lValPercent.Value;

                    if (rightValue is Percent rValPercent)
                        rightValue = rValPercent.Value;

                    if (left.Value is Percent && right.Value is Percent)
                    {
                        object? result = MathHelper.Add(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;
                        return new Percent(result);
                    }
                    else
                    if (right.Value is Percent)
                        return MathHelper.AddPercent(leftValue, rightValue, context);
                    else
                    if (left.Value is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of an addition operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return EvaluationHelper.Plus(leftValue, rightValue, context);
            }

            case BinaryExpressionType.Times:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                if (handlePercent)
                {
                    if (leftValue is Percent lValPerc && rightValue is Percent rValPerc)
                    {
                        object? result = MathHelper.MultiplyPercent(lValPerc.Value, rValPerc.Value, context);
                        if (result is null)
                            return null;
                        return new Percent(result);
                    }
                    else
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        object? result = MathHelper.Multiply(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;

                        return new Percent(result);
                    }
                    else
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;

                        return MathHelper.MultiplyPercent(leftValue, rightValue, context);
                    }
                }

                return MathHelper.Multiply(leftValue, rightValue, true, context);
            }

            case BinaryExpressionType.BitwiseAnd:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return MathHelper.BitwiseAnd(leftValue, rightValue);
                return Convert.ToUInt64(leftValue, context.CultureInfo) &
                        Convert.ToUInt64(rightValue, context.CultureInfo);

            case BinaryExpressionType.BitwiseOr:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return MathHelper.BitwiseOr(leftValue, rightValue);
                return Convert.ToUInt64(leftValue, context.CultureInfo) |
                        Convert.ToUInt64(rightValue, context.CultureInfo);

            case BinaryExpressionType.BitwiseXOr:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;
                if (leftValue is BigInteger || rightValue is BigInteger)
                    return MathHelper.BitwiseXOr(leftValue, rightValue);
                return Convert.ToUInt64(leftValue, context.CultureInfo) ^
                        Convert.ToUInt64(rightValue, context.CultureInfo);

            case BinaryExpressionType.LeftShift:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;
                if (leftValue is BigInteger)
                    return MathHelper.LeftShift((BigInteger) leftValue, rightValue, context);
                return Convert.ToUInt64(leftValue, context.CultureInfo) <<
                        Convert.ToInt32(rightValue, context.CultureInfo);

            case BinaryExpressionType.RightShift:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                if (leftValue is BigInteger)
                    return MathHelper.RightShift((BigInteger)leftValue, rightValue, context);
                return Convert.ToUInt64(leftValue, context.CultureInfo) >>
                        Convert.ToInt32(rightValue, context.CultureInfo);

            case BinaryExpressionType.Exponentiation:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                return MathHelper.Pow(leftValue, rightValue, true, context);

            case BinaryExpressionType.Factorial:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                return MathHelper.Factorial(leftValue!, rightValue!, context);

            case BinaryExpressionType.In:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;
                return EvaluationHelper.In(rightValue, leftValue, context);

            case BinaryExpressionType.NotIn:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                return !EvaluationHelper.In(rightValue, leftValue, context);

            case BinaryExpressionType.Like:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                return EvaluationHelper.Like(leftValue!, rightValue!, context);
            }

            case BinaryExpressionType.NotLike:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                return !EvaluationHelper.Like(leftValue!, rightValue!, context);
            }

            case BinaryExpressionType.RangeIndex:
                leftValue = left.Value;
                rightValue = right.Value;

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

                return new RangeValue
                {
                    LowerBound = leftInt is null ? null : (leftValue is NCalc.Domain.Index leftIdx2) ? leftIdx2 : new NCalc.Domain.Index(leftInt.Value),
                    UpperBound = rightInt is null ? null : (rightValue is NCalc.Domain.Index rightIdx2) ? rightIdx2 : new NCalc.Domain.Index(rightInt.Value),
                };

            case BinaryExpressionType.IndexAccess:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
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
                    RangeValue? range = (RangeValue?) binExpr.Accept(this, cancellationToken);
                    if (range is null)
                        return null;

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
                            return Array.Empty<object?>();

                        object?[] resultArr = new object?[upperBound - lowerBound];
                        for (int i = 0; i < resultArr.Length; i++)
                        {
                            result = resultArr[lowerBound + i];
                            if (result is LogicalExpression expr)
                                result = expr.Accept(this, cancellationToken);
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
                            return string.Empty;

                        result = identString[lowerBound..upperBound];
                    }

                    return result;
                }
                else
                {
                    if (!TryGetValueOrNull(right.Value, out rightValue))
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
                return result;
            }

            case BinaryExpressionType.WhileLoop:
            {
                object? result = null;

                int ctr = 0;

                while (ctr < Expression.MaxLoopIterations)
                {
                    ctr++;

                    if (!TryGetValueOrNull(Evaluate(expression.LeftExpression, cancellationToken), out leftValue))
                        break;

                    if (!Convert.ToBoolean(leftValue, context.CultureInfo))
                        break;

                    try
                    {
                        TryGetValueOrNull(Evaluate(expression.RightExpression, cancellationToken), out result);
                    }
                    catch (NCalcFlowControl fc)
                    {
                        if (fc.Type == NCalcFlowControl.FlowControlType.Break)
                            break;
                        else
                        if (fc.Type == NCalcFlowControl.FlowControlType.Continue)
                            continue;
                    }
                }
                return result;
            }
        }

        return null;
    }

    public virtual object? Visit(UnaryExpression expression, CancellationToken cancellationToken = default)
    {
        // Recursively evaluates the underlying expression
        if (!TryGetValueOrNull(expression.Expression.Accept(this, cancellationToken), out object? result))
            return null;

        return EvaluationHelper.Unary(expression, result, context);
    }

    public virtual object? Visit(PercentExpression expression, CancellationToken cancellationToken = default)
    {
        // Recursively evaluates the underlying expression
        if (!TryGetValueOrNull(expression.Expression.Accept(this, cancellationToken), out object? result))
            return null;

        return new Percent(result!);
    }

    public virtual object? Visit(ValueExpression expression, CancellationToken cancellationToken = default) => expression.Value;
    public virtual object? Visit(FunctionExpression expression, CancellationToken cancellationToken = default) => null;

    public virtual object? Visit(FunctionCall functionCall, CancellationToken cancellationToken = default)
    {
        var argsCount = functionCall.Parameters.Count;

        var functionName = functionCall.Identifier.Name;

        if (context.UserFunctions.Count > 0)
        {
            if (context.UserFunctions.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? functionName.ToLowerInvariant() : functionName, out Function? userFunction) && userFunction is not null)
            {
                EvaluationHelper.EnsureProperParamNumInFunctionCall(userFunction, functionCall);
                return ExecuteUserFunctionCall(userFunction, functionCall, context, cancellationToken);
            }
        }

        Expression[] args = new Expression[argsCount];

        // Don't call parameters right now, instead let the function do it as needed.
        // Some parameters shouldn't be called, for instance, in a if(), the "not" value might be a division by zero
        // Evaluating every value could produce unexpected behavior
        for (var i = 0; i < argsCount; i++)
        {
            args[i] = new Expression(functionCall.Parameters[i], context);
        }

        var functionArgs = new FunctionArgs(functionCall.Identifier.Id, args);

        OnEvaluateFunction(functionName, functionArgs);

        if (functionArgs.HasResult)
            return functionArgs.Result;

        if (context.Functions.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? functionName.ToLowerInvariant() : functionName, out var expressionFunction))
        {
            return expressionFunction(new ExpressionFunctionData(functionCall.Identifier.Id, args, context));
        }

        return BuiltInFunctionHelper.Evaluate(functionName, args, context, functionCall.Location);
    }

    public virtual object? Visit(Identifier identifier, CancellationToken cancellationToken = default)
    {
        object? result = null;
        var identifierName = identifier.Name;

        if (context.Options.HasFlag(ExpressionOptions.UseLoops))
        {
            if (identifierName.Equals("break", StringComparison.InvariantCultureIgnoreCase))
                throw new NCalcFlowControl(NCalcFlowControl.FlowControlType.Break, identifier.Location);
            if (identifierName.Equals("continue", StringComparison.InvariantCultureIgnoreCase))
                throw new NCalcFlowControl(NCalcFlowControl.FlowControlType.Continue, identifier.Location);
        }

        var parameterArgs = new ParameterArgs(identifier.Id);

        OnEvaluateParameter(identifierName, parameterArgs);

        if (parameterArgs.HasResult)
        {
            result = parameterArgs.Result;
            if (result is null)
            {
                return result;
            }
        }

        if (result == null)
        {
            if (context.StaticParameters.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? identifierName.ToLowerInvariant() : identifierName, out var parameter))
            {
                if (parameter is Expression expression)
                {
                    //Share the parameters with child expression.
                    foreach (var p in context.StaticParameters)
                        expression.Parameters[p.Key] = p.Value;

                    foreach (var p in context.DynamicParameters)
                        expression.DynamicParameters[p.Key] = p.Value;

                    expression.EvaluateFunction += context.EvaluateFunctionHandler;
                    expression.EvaluateParameter += context.EvaluateParameterHandler;
                    expression.UpdateParameter += context.UpdateParameterHandler;
                    expression.MatchString += context.MatchStringHandler;

                    result = expression.Evaluate(cancellationToken);
                    if (result is null)
                    {
                        return result;
                    }
                }
                else
                {
                    result = parameter;
                    if (result is null)
                    {
                        return result;
                    }
                }
            }
        }

        if (result == null)
        {
            if (context.DynamicParameters.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? identifierName.ToLowerInvariant() : identifierName, out var dynamicParameter))
            {
                result = dynamicParameter(new ExpressionParameterData(identifier.Id, context));
                if (result is null)
                {
                    return result;
                }
            }
        }

        if (result != null)
            return result;

        throw new NCalcParameterNotDefinedException(identifierName, identifier.Location);
    }

    public virtual object Visit(LogicalExpressionList list, CancellationToken cancellationToken = default)
    {
        List<object?> result = [];

        result.AddRange(list.Select(Evaluate));

        return result;
    }

    public object? Visit(ExpressionGroup group, CancellationToken cancellationToken = default)
    {
        return group.Expression.Accept(this, cancellationToken);
    }

    public object? Visit(StatementSequence seq, CancellationToken cancellationToken = default)
    {
        object? result = null;
        foreach (var expr in seq)
            result = Evaluate(expr, cancellationToken);

        if (!TryGetValueOrNull(result, out result))
            return null;

        /*if ((context.AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.CalculatePercent) == true) && result is Percent valPercent)
            result = valPercent.Value;*/

        return result;
    }

    protected bool Compare(object? a, object? b, ComparisonType comparisonType)
    {
        if (context.Options.HasFlag(ExpressionOptions.StrictTypeMatching) && a?.GetType() != b?.GetType())
            return false;

        if (!context.Options.HasFlag(ExpressionOptions.CompareNullValues))
        {
            if ((a is null || b is null) && !(a is null && b is null))
                return comparisonType switch
                {
                    ComparisonType.Equal => false, // true if null == null
                    ComparisonType.NotEqual => true,
                    _ => false
                };
        }

        return EvaluationHelper.Compare(a, b, comparisonType, context);
    }

    protected void OnEvaluateFunction(string name, FunctionArgs args)
    {
        context.EvaluateFunctionHandler?.Invoke(name, args);
    }

    protected void OnEvaluateParameter(string name, ParameterArgs args)
    {
        context.EvaluateParameterHandler?.Invoke(name, args);
    }

    protected void OnUpdateParameter(string name, UpdateParameterArgs args)
    {
        context.UpdateParameterHandler?.Invoke(name, args);
    }

    protected object? Evaluate(LogicalExpression expression)
    {
        return expression.Accept(this);
    }

    protected object? Evaluate(LogicalExpression expression, CancellationToken cancellationToken = default)
    {
        return expression.Accept(this, cancellationToken);
    }

    internal object? EvaluateNoRecurse(LogicalExpression expression, CancellationToken cancellationToken = default)
    {
        List<ExpressionTask<object?>> stack = [];
        ExpressionTask<object?> root = new(null, new ExpressionState<object?>(expression));
        stack.Add(root);
        ExpressionTask<object?> currentTask;
        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Ask visitor to visit the expression. Pass the task to it.
            // The visitor may
            // a) return the value
            // b) add an expression to the task that must be evaluated first.
            currentTask = stack[^1];
            _ = currentTask.State.Expression.AcceptNoRecurse(this, currentTask, cancellationToken);
            if (currentTask.State.ValueSet)
            {
                // Remove the current task from the stack
                stack.Remove(currentTask);
            }
            else
            {
                ExpressionState<object?> state;
                // We add child expressions starting from the end to let different expression types put the child expressions in the order in which those child expressions happen in the evaluated expression,
                // but the leftmost child must appear at the top of the stack so that it is evaluated first
                for (int i = currentTask.ChildStates.Count -1; i >= 0; i--)
                {
                    state = currentTask.ChildStates[i];
                    if (!state.ValueSet)
                    {
                        stack.Add(new ExpressionTask<object?>(currentTask, state));
                    }
                }
            }
        }

        return root.State.ValueSet ? root.State.Value : null;
    }

    internal static object? ExecuteUserFunctionCall(Function userFunction, FunctionCall functionCall, ExpressionContext context, CancellationToken cancellationToken = default)
    {
        Dictionary<string, ArgumentStateBase> argumentStates = [];

        EvaluationHelper.PopulateArgumentStates(userFunction, functionCall, argumentStates, context, (expression, context) => new ArgumentState(expression, context));

        var expression = new Expression(userFunction.Body, context.Options, context.CultureInfo);
        expression.AdvancedOptions = context.AdvancedOptions;

        bool ignoreCase = context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup);

        expression.EvaluateParameter += (name, args) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string paramName = ignoreCase ? name.ToLowerInvariant() : name;
                if (argumentStates.TryGetValue(paramName, out var state))
                {
                    args.Result = ((ArgumentState)state).Value;
                    return;
                }
                context.EvaluateParameterHandler?.Invoke(name, args);
            };

        expression.UpdateParameter += (name, args) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                string paramName = ignoreCase ? name.ToLowerInvariant() : name;
                if (argumentStates.TryGetValue(paramName, out var state))
                {
                    ((ArgumentState)state).Value = args.Value;
                    args.UpdateParameterLists = false;
                }
            };

        expression.EvaluateFunction += (name, args) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                context.EvaluateFunctionHandler?.Invoke(name, args);
            };

        expression.MatchString += (args) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                context.MatchStringHandler?.Invoke(args);
            };

        try
        {
            return expression.Evaluate(cancellationToken);
        }
        catch (NCalcFlowControl ex) when (ex.Type == NCalcFlowControl.FlowControlType.Return)
        {
            return ex.ReturnValue;
        }
    }
}

internal class ArgumentState(LogicalExpression expression, ExpressionContextBase context) : ArgumentStateBase
{
    internal object? Value
    {
        get
        {
            if (!_valueSet)
            {
                var expr = new Expression(expression, context as ExpressionContext);
                _value = expr.Evaluate();
                _valueSet = true;
            }
            return _value;
        }

        set
        {
            _value = value;
            _valueSet = true;
        }
    }
}
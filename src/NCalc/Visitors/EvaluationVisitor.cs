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
public class EvaluationVisitor(ExpressionContext context) : ILogicalExpressionVisitor<object?>
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

    public virtual object? Visit(TernaryExpression expression, CancellationToken cancellationToken = default)
    {
        object? value;
        if (!TryGetValueOrNull(expression.LeftExpression.Accept(this, cancellationToken), out value))
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

                object? staticParam = null;
                if (!context.StaticParameters.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? identifierName.ToLowerInvariant() : identifierName, out staticParam) || staticParam is null)
                    throw new NCalcParameterIndexException(identifierName, $"{identifierName} is not set and cannot be assigned to by index", binExpr.LeftExpression.Location);

                if (staticParam is not IList)
                {
                    throw new NCalcParameterIndexException(identifierName, $"{identifierName} is not a list and cannot be assigned to by index", binExpr.LeftExpression.Location);
                }
                ((IList)staticParam)[index] = value;
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

        var handlePercent = context.AdvancedOptions != null && context.AdvancedOptions.Flags.HasFlag(AdvExpressionOptions.CalculatePercent);

        object? leftValue = null;
        object? rightValue = null;

        switch (expression.Type)
        {
            case BinaryExpressionType.StatementSequence:
            {
                _ = left.Value;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;
                if (handlePercent && rightValue is Percent rValPercent)
                    rightValue = rValPercent.Value;
                return rightValue;
            }

            case BinaryExpressionType.Assignment:
            {
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                return UpdateParameter(expression.LeftExpression, rightValue);
            }

            case BinaryExpressionType.PlusAssignment:
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
                        return UpdateParameter(expression.LeftExpression, new Percent(result));
                    }
                    else
                    if (right.Value is Percent)
                        return UpdateParameter(expression.LeftExpression, MathHelper.AddPercent(leftValue, rightValue, context));
                    else
                    if (left.Value is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of a += operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return UpdateParameter(expression.LeftExpression, EvaluationHelper.Plus(leftValue, rightValue, context));
            }
            case BinaryExpressionType.MinusAssignment:
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
                        return UpdateParameter(expression.LeftExpression, new Percent(result));
                    }
                    else
                    if (right.Value is Percent)
                        return UpdateParameter(expression.LeftExpression, MathHelper.SubtractPercent(leftValue, rightValue, context));
                    else
                    if (left.Value is Percent)
                    {
                        throw new NCalcEvaluationException("The left side of a -= operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
                    }
                }

                return UpdateParameter(expression.LeftExpression, EvaluationHelper.Minus(leftValue, rightValue, context));
            }
            case BinaryExpressionType.MultiplyAssignment:
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
                        return UpdateParameter(expression.LeftExpression, new Percent(result));
                    }
                    else
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        object? result = MathHelper.Multiply(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;

                        return UpdateParameter(expression.LeftExpression, new Percent(result));
                    }
                    else
                    if (rightValue is Percent rValPercent)
                    {
                        rightValue = rValPercent.Value;

                        object? result = MathHelper.MultiplyPercent(leftValue, rightValue, context);
                        if (result is null)
                            return null;

                        return UpdateParameter(expression.LeftExpression, result);
                    }
                }

                return UpdateParameter(expression.LeftExpression, MathHelper.Multiply(leftValue, rightValue, true, context));
            }

            case BinaryExpressionType.DivAssignment:
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
                        return UpdateParameter(expression.LeftExpression, new Percent(result));
                    }
                    if (leftValue is Percent lValPercent)
                    {
                        leftValue = lValPercent.Value;
                        if (!noConvertToDouble)
                            leftValue = Convert.ToDouble(leftValue, context.CultureInfo);
                        object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                        if (result is null)
                            return null;

                        return UpdateParameter(expression.LeftExpression, new Percent(result));
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

                        return UpdateParameter(expression.LeftExpression, result);
                    }
                }

                if (!noConvertToDouble)
                    leftValue = Convert.ToDouble(leftValue, context.CultureInfo);

                {
                    object? result = MathHelper.Divide(leftValue, rightValue, true, context);
                    if (result is null)
                        return null;
                    return UpdateParameter(expression.LeftExpression, result);
                }
            }

            case BinaryExpressionType.AndAssignment:

                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return UpdateParameter(expression.LeftExpression, MathHelper.BitwiseAnd(leftValue, rightValue));
                return UpdateParameter(expression.LeftExpression,
                    Convert.ToUInt64(leftValue, context.CultureInfo) &
                    Convert.ToUInt64(rightValue, context.CultureInfo)
                    );

            case BinaryExpressionType.OrAssignment:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return UpdateParameter(expression.LeftExpression, MathHelper.BitwiseOr(leftValue, rightValue));
                return UpdateParameter(expression.LeftExpression,
                    Convert.ToUInt64(leftValue, context.CultureInfo) |
                    Convert.ToUInt64(rightValue, context.CultureInfo)
                    );

            case BinaryExpressionType.XOrAssignment:
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    return null;
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    return null;

                if (leftValue is BigInteger || rightValue is BigInteger)
                    return UpdateParameter(expression.LeftExpression, MathHelper.BitwiseXOr(leftValue, rightValue));
                return UpdateParameter(expression.LeftExpression,
                    Convert.ToUInt64(leftValue, context.CultureInfo) ^
                    Convert.ToUInt64(rightValue, context.CultureInfo)
                    );

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

                        object? result = MathHelper.MultiplyPercent(leftValue, rightValue, context);
                        if (result is null)
                            return null;

                        return result;
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

            case BinaryExpressionType.IndexAccess:
            {
                if (!TryGetValueOrNull(left.Value, out leftValue))
                    throw new NCalcParameterIndexException("An expression, if used with an index, must denote a list", expression.LeftExpression.Location);
                if (!TryGetValueOrNull(right.Value, out rightValue))
                    throw new NCalcParameterIndexException("The index does not evaluate to a number", expression.RightExpression.Location);

                if (leftValue is not IList identList)
                    throw new NCalcParameterIndexException("An expression, if used with an index, must denote a list", expression.LeftExpression.Location);

                int index;
                try
                {
                    index = MathHelper.ConvertToInt(rightValue, context);
                }
                catch
                {
                    throw new NCalcParameterIndexException("The index does not evaluate to a number", expression.RightExpression.Location);
                }
                if (index < 0 || index >= identList.Count)
                    throw new NCalcParameterIndexException($"The index is out of bounds [0; {identList.Count - 1}]", expression.RightExpression.Location);
                object? result = identList[index];
                if (result is LogicalExpression expr)
                    result = expr.Accept(this);
                return result;
            }
        }

        return null;
    }

    public virtual object? Visit(UnaryExpression expression, CancellationToken cancellationToken = default)
    {
        // Recursively evaluates the underlying expression
        object? result = null;
        if (!TryGetValueOrNull(expression.Expression.Accept(this, cancellationToken), out result))
            return null;

        return EvaluationHelper.Unary(expression, result, context);
    }

    public virtual object? Visit(PercentExpression expression, CancellationToken cancellationToken = default)
    {
        // Recursively evaluates the underlying expression
        object? result = null;
        if (!TryGetValueOrNull(expression.Expression.Accept(this, cancellationToken), out result))
            return null;

        return new Percent(result!);
    }

    public virtual object? Visit(ValueExpression expression, CancellationToken cancellationToken = default) => expression.Value;

    public virtual object? Visit(Function function, CancellationToken cancellationToken = default)
    {
        var argsCount = function.Parameters.Count;
        var args = new Expression[argsCount];

        // Don't call parameters right now, instead let the function do it as needed.
        // Some parameters shouldn't be called, for instance, in a if(), the "not" value might be a division by zero
        // Evaluating every value could produce unexpected behaviour
        for (var i = 0; i < argsCount; i++)
        {
            args[i] = new Expression(function.Parameters[i], context);
        }

        var functionName = function.Identifier.Name;
        var functionArgs = new FunctionArgs(function.Identifier.Id, args);

        OnEvaluateFunction(functionName, functionArgs);

        if (functionArgs.HasResult)
            return functionArgs.Result;

        if (context.Functions.TryGetValue(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? functionName.ToLowerInvariant() : functionName, out var expressionFunction))
        {
            return expressionFunction(new ExpressionFunctionData(function.Identifier.Id, args, context));
        }

        return BuiltInFunctionHelper.Evaluate(functionName, args, context, function.Location);
    }

    public virtual object? Visit(Identifier identifier, CancellationToken cancellationToken = default)
    {
        object? result = null;
        var identifierName = identifier.Name;

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

                    result = expression.Evaluate();
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

    protected bool Compare(object? a, object? b, ComparisonType comparisonType)
    {
        if (context.Options.HasFlag(ExpressionOptions.StrictTypeMatching) && a?.GetType() != b?.GetType())
            return false;

        if (!context.Options.HasFlag(ExpressionOptions.CompareNullValues))
        {
            if ((a is null || b is null) && !(a is null && b is null))
                return false;
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

    public object? Visit(ExpressionGroup group, CancellationToken cancellationToken = default)
    {
        return group.Expression.Accept(this, cancellationToken);
    }
}
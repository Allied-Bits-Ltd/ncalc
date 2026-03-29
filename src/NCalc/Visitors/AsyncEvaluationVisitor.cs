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
public partial class AsyncEvaluationVisitor : ILogicalExpressionVisitor<ValueTask<object?>>, ILogicalExpressionNoRecurseVisitor<ValueTask<object?>>
{
    private readonly AsyncExpressionContext context;

    public AsyncEvaluationVisitor(AsyncExpressionContext context)
    {
        this.context = context;
    }

    public AsyncEvaluationVisitor(AsyncExpression parentExpression)
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

    public virtual async ValueTask<object?> Visit(TernaryExpression expression, CancellationToken cancellationToken = default)
    {
        if (!TryGetValueOrNull(await expression.LeftExpression.Accept(this, cancellationToken).ConfigureAwait(false), out object? value))
            return null;

        return await (MathHelper.ConvertToBoolean(value, "Ternary", context.CultureInfo, expression.LeftExpression.Location) ? expression.MiddleExpression : expression.RightExpression).Accept(this, cancellationToken).ConfigureAwait(false);
    }

    private async Task<object?> UpdateParameterAsync(LogicalExpression leftExpression, object? value, CancellationToken cancellationToken = default)
    {
        if (value is null && !(context.Options.HasFlag(ExpressionOptions.AllowNullParameter) || context.Options.HasFlag(ExpressionOptions.UseTernaryLogic)))
            return value;

        switch (leftExpression)
        {
            case BinaryExpression binExpr when binExpr.Type == BinaryExpressionType.IndexAccess:
            {
                if (binExpr.LeftExpression is Identifier ident)
                {
                    var identifierName = ident.Name;

                    var indexObj = await binExpr.RightExpression.Accept(this, cancellationToken).ConfigureAwait(false);
                    if (!MathHelper.IsBoxedIntegerNumberOrBigNumber(indexObj))
                        throw new NCalcParameterIndexException(identifierName, $"The index of {identifierName} does not evaluate to a number", binExpr.RightExpression.Location);

                    var index = MathHelper.ConvertToInt(indexObj, "Indexed access", context.CultureInfo, binExpr.RightExpression.Location);
                    if (index < 0)
                        throw new NCalcParameterIndexException(identifierName, $"The index of {identifierName} is less than zero ({index}), which is not a valid value (an index must be zero or positive)", binExpr.RightExpression.Location);

                    var parameterArgs = new UpdateParameterArgs(identifierName, ident.Id, index, value);

                    await OnUpdateParameterAsync(identifierName, parameterArgs, cancellationToken).ConfigureAwait(false);

                    if (!parameterArgs.UpdateParameterLists)
                        return value;

                    object? staticParam = null;
                    if ((!(context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup)
                            ? EvaluationHelper.GetParameterValueFromListNoCase(context.StaticParameters, identifierName, out staticParam)
                            : context.StaticParameters.TryGetValue(identifierName, out staticParam)))
                        || staticParam is null)
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

                            if (context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup))
                                EvaluationHelper.SetParameterValueFromListNoCase(context.StaticParameters, identifierName, strParam);
                            else
                                context.StaticParameters[identifierName] = strParam;
                        }
                        else
                        {
                            throw new NCalcParameterIndexException(identifierName, $"When updating a string in '{identifierName}' via the index, the value must be a character or a one-character string", binExpr.RightExpression.Location);
                        }
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
                {
                    throw new NCalcEvaluationException("The expression should evaluate to an identifier", binExpr.Location);
                }

                break;
            }
            case Identifier identifier:
            {
                var identifierName = identifier.Name;

                var parameterArgs = new UpdateParameterArgs(identifierName, identifier.Id, value);

                await OnUpdateParameterAsync(identifierName, parameterArgs, cancellationToken).ConfigureAwait(false);

                if (!parameterArgs.UpdateParameterLists)
                    return value;

                if (context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup))
                    EvaluationHelper.SetParameterValueFromListNoCase(context.StaticParameters, identifierName, value);
                else
                    context.StaticParameters[identifierName] = value;

                break;
            }
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

        var handlePercent = context.AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.CalculatePercent) == true;

        object? leftValue = null;
        object? rightValue = null;

        try
        {
            switch (expression.Type)
            {
                case BinaryExpressionType.Assignment:
                    if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                        return await UpdateParameterAsync(expression.LeftExpression, null, cancellationToken).ConfigureAwait(false);

                    return await UpdateParameterAsync(expression.LeftExpression, rightValue, cancellationToken).ConfigureAwait(false);

                case BinaryExpressionType.PlusAssignment:
                {
                    var lval = await left.Value.ConfigureAwait(false);
                    if (!TryGetValueOrNull(lval, out leftValue))
                        return null;
                    var rval = await right.Value.ConfigureAwait(false);
                    if (!TryGetValueOrNull(rval, out rightValue))
                        return await UpdateParameterAsync(expression.LeftExpression, null, cancellationToken).ConfigureAwait(false);

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
                        {
                            return await UpdateParameterAsync(expression.LeftExpression, MathHelper.AddPercent(leftValue, rightValue, context), cancellationToken).ConfigureAwait(false);
                        }
                        else
                        if (lval is Percent)
                        {
                            throw new NCalcEvaluationException("The left side of a += operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
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
                        return await UpdateParameterAsync(expression.LeftExpression, null, cancellationToken).ConfigureAwait(false);

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
                        {
                            return await UpdateParameterAsync(expression.LeftExpression, MathHelper.SubtractPercent(leftValue, rightValue, context), cancellationToken).ConfigureAwait(false);
                        }
                        else
                        if (lval is Percent)
                        {
                            throw new NCalcEvaluationException("The left side of a -= operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
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
                        return await UpdateParameterAsync(expression.LeftExpression, null, cancellationToken).ConfigureAwait(false);

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
                        return await UpdateParameterAsync(expression.LeftExpression, null, cancellationToken).ConfigureAwait(false);

                    bool noConvertToDouble = IsReal(leftValue) || IsReal(rightValue) || leftValue is BigInteger || rightValue is BigInteger || leftValue is BigDecimal || rightValue is BigDecimal;

                    if (handlePercent)
                    {
                        if (lval is Percent lValPerc && rval is Percent rValPerc)
                        {
                            leftValue = lValPerc.Value;
                            if (!noConvertToDouble)
                                leftValue = MathHelper.ConvertToDouble(leftValue, "Div with assignment", context.CultureInfo, expression.LeftExpression.Location);

                            object? result = MathHelper.DividePercent(leftValue, rValPerc.Value, context);
                            if (result is null)
                                return null;

                            return await UpdateParameterAsync(expression.LeftExpression, new Percent(result), cancellationToken).ConfigureAwait(false);
                        }
                        if (lval is Percent lValPercent)
                        {
                            leftValue = lValPercent.Value;
                            if (!noConvertToDouble)
                                leftValue = MathHelper.ConvertToDouble(leftValue, "Div with assignment", context.CultureInfo, expression.LeftExpression.Location);

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
                                leftValue = MathHelper.ConvertToDouble(leftValue, "Div with assignment", context.CultureInfo, expression.LeftExpression.Location);

                            object? result = MathHelper.DividePercent(leftValue, rightValue, context);
                            if (result is null)
                                return null;

                            return await UpdateParameterAsync(expression.LeftExpression, result, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (!noConvertToDouble)
                        leftValue = MathHelper.ConvertToDouble(leftValue, "Div with assignment", context.CultureInfo, expression.LeftExpression.Location);

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
                        return await UpdateParameterAsync(expression.LeftExpression, null, cancellationToken).ConfigureAwait(false);

                    if (leftValue is BigInteger || rightValue is BigInteger)
                        return await UpdateParameterAsync(expression.LeftExpression,
                            MathHelper.BitwiseAnd(leftValue, rightValue), cancellationToken).ConfigureAwait(false);

                    return await UpdateParameterAsync(
                        expression.LeftExpression,
                        MathHelper.ConvertToULong(leftValue, "And with assignment", context.CultureInfo, expression.LeftExpression.Location) &
                            MathHelper.ConvertToULong(rightValue, "And with assignment", context.CultureInfo, expression.RightExpression.Location),
                        cancellationToken).ConfigureAwait(false);
                }
                case BinaryExpressionType.OrAssignment:
                {
                    if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                        return null;
                    if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                        return await UpdateParameterAsync(expression.LeftExpression, null, cancellationToken).ConfigureAwait(false);

                    if (leftValue is BigInteger || rightValue is BigInteger)
                        return await UpdateParameterAsync(expression.LeftExpression,
                            MathHelper.BitwiseOr(leftValue, rightValue), cancellationToken).ConfigureAwait(false);

                    return await UpdateParameterAsync(
                        expression.LeftExpression,
                        MathHelper.ConvertToULong(leftValue, "Or with assignment", context.CultureInfo, expression.LeftExpression.Location) |
                            MathHelper.ConvertToULong(rightValue, "Or with assignment", context.CultureInfo, expression.RightExpression.Location),
                        cancellationToken).ConfigureAwait(false);
                }
                case BinaryExpressionType.XOrAssignment:
                {
                    if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                        return null;
                    if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                        return await UpdateParameterAsync(expression.LeftExpression, null, cancellationToken).ConfigureAwait(false);

                    if (leftValue is BigInteger || rightValue is BigInteger)
                        return await UpdateParameterAsync(expression.LeftExpression,
                            MathHelper.BitwiseXOr(leftValue, rightValue), cancellationToken).ConfigureAwait(false);

                    return await UpdateParameterAsync(
                        expression.LeftExpression,
                        MathHelper.ConvertToULong(leftValue, "Xor with assignment", context.CultureInfo, expression.LeftExpression.Location) ^
                            MathHelper.ConvertToULong(rightValue, "XOr with assignment", context.CultureInfo, expression.RightExpression.Location),
                        cancellationToken).ConfigureAwait(false);
                }
                case BinaryExpressionType.And:
                    if (context.Options.HasFlag(ExpressionOptions.UseTernaryLogic))
                    {
                        leftValue = await left.Value.ConfigureAwait(false);
                        bool? leftBool = leftValue is bool lb ? lb : null;
                        if (leftBool == false)
                            return false;

                        rightValue = await right.Value.ConfigureAwait(false);

                        return K3LogicHelper.And(leftValue, rightValue);
                    }

                    if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                        return null;
                    if (!MathHelper.ConvertToBoolean(leftValue, "And", context.CultureInfo, expression.LeftExpression.Location))
                        return false;

                    if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                        return null;

                    return MathHelper.ConvertToBoolean(rightValue, "And", context.CultureInfo, expression.RightExpression.Location);

                case BinaryExpressionType.Or:
                    if (context.Options.HasFlag(ExpressionOptions.UseTernaryLogic))
                    {
                        leftValue = await left.Value.ConfigureAwait(false);
                        bool? leftBool = leftValue is bool lb ? lb : null;
                        if (leftBool == true)
                            return true;

                        rightValue = await right.Value.ConfigureAwait(false);

                        return K3LogicHelper.Or(leftValue, rightValue);
                    }

                    if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                        return null;
                    if (MathHelper.ConvertToBoolean(leftValue, "Or", context.CultureInfo, expression.LeftExpression.Location))
                        return true;

                    if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                        return null;

                    return MathHelper.ConvertToBoolean(rightValue, "Or", context.CultureInfo, expression.RightExpression.Location);

                case BinaryExpressionType.XOr:
                    if (context.Options.HasFlag(ExpressionOptions.UseTernaryLogic))
                    {
                        leftValue = await left.Value.ConfigureAwait(false);
                        bool? leftBool = leftValue is bool lb ? lb : null;
                        if (leftBool == null)
                            return null;

                        rightValue = await right.Value.ConfigureAwait(false);

                        return K3LogicHelper.Xor(leftValue, rightValue);
                    }
                    return MathHelper.ConvertToBoolean(await left.Value.ConfigureAwait(false), "XOr", context.CultureInfo, expression.LeftExpression.Location) ^
                           MathHelper.ConvertToBoolean(await right.Value.ConfigureAwait(false), "XOr", context.CultureInfo, expression.RightExpression.Location);

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
                                leftValue = MathHelper.ConvertToDouble(leftValue, "Div", context.CultureInfo, expression.Location);

                            object? result = MathHelper.DividePercent(leftValue, rValPerc.Value, context);
                            if (result is null)
                                return null;
                            return new Percent(result);
                        }
                        if (lval is Percent lValPercent)
                        {
                            leftValue = lValPercent.Value;
                            if (!noConvertToDouble)
                                leftValue = MathHelper.ConvertToDouble(leftValue, "Div", context.CultureInfo, expression.Location);

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
                                leftValue = MathHelper.ConvertToDouble(leftValue, "Div", context.CultureInfo, expression.Location);

                            return MathHelper.DividePercent(leftValue, rightValue, context);
                        }
                    }

                    if (!noConvertToDouble)
                        leftValue = MathHelper.ConvertToDouble(leftValue, "Div", context.CultureInfo, expression.Location);

                    return MathHelper.Divide(leftValue, rightValue, true, context);
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
                        {
                            return MathHelper.SubtractPercent(leftValue, rightValue, context);
                        }
                        else
                        if (lval is Percent)
                        {
                            throw new NCalcEvaluationException("The left side of a subtraction operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
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
                        {
                            return MathHelper.AddPercent(leftValue, rightValue, context);
                        }
                        else
                        if (lval is Percent)
                        {
                            throw new NCalcEvaluationException("The left side of an addition operation cannot be a percent unless the right side is a percent as well", expression.LeftExpression.Location);
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

                            return MathHelper.MultiplyPercent(leftValue, rightValue, context);
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

                    return MathHelper.ConvertToULong(leftValue, "Bitwise And", context.CultureInfo, expression.LeftExpression.Location) &
                            MathHelper.ConvertToULong(rightValue, "Bitwise And", context.CultureInfo, expression.RightExpression.Location);
                }
                case BinaryExpressionType.BitwiseOr:
                {
                    if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                        return null;
                    if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                        return null;

                    if (leftValue is BigInteger || rightValue is BigInteger)
                        return MathHelper.BitwiseOr(leftValue, rightValue);

                    return MathHelper.ConvertToULong(leftValue, "Bitwise Or", context.CultureInfo, expression.LeftExpression.Location) |
                        MathHelper.ConvertToULong(rightValue, "Bitwise Or", context.CultureInfo, expression.RightExpression.Location);
                }
                case BinaryExpressionType.BitwiseXOr:
                {
                    if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                        return null;
                    if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                        return null;

                    if (leftValue is BigInteger || rightValue is BigInteger)
                        return MathHelper.BitwiseXOr(leftValue, rightValue);

                    return MathHelper.ConvertToULong(leftValue, "Bitwise XOr", context.CultureInfo, expression.LeftExpression.Location) ^
                            MathHelper.ConvertToULong(rightValue, "Bitwise XOr", context.CultureInfo, expression.RightExpression.Location);
                }
                case BinaryExpressionType.LeftShift:
                {
                    if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                        return null;
                    if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                        return null;

                    return MathHelper.LeftShift(leftValue, rightValue, true, context);
                }
                case BinaryExpressionType.RightShift:
                {
                    if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
                        return null;
                    if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                        return null;

                    return MathHelper.RightShift(leftValue, rightValue, true, context);
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

                case BinaryExpressionType.RangeIndex:
                    leftValue = await left.Value.ConfigureAwait(false);
                    rightValue = await right.Value.ConfigureAwait(false);

                    if (leftValue is not null && leftValue is not NCalc.Domain.Index && !MathHelper.IsBoxedNumberOrBigNumber(leftValue))
                        throw new NCalcParameterIndexException("The lower boundary, unless omitted, should evaluate to zero or an integer number", expression.LeftExpression.Location);
                    if (rightValue is not null && rightValue is not NCalc.Domain.Index && !MathHelper.IsBoxedNumberOrBigNumber(rightValue))
                        throw new NCalcParameterIndexException("The upper boundary, unless omitted, should evaluate to zero or an integer number", expression.RightExpression.Location);

                    int? leftInt = (leftValue is null) ? null : (leftValue is NCalc.Domain.Index leftIdx) ? leftIdx.Value : MathHelper.ConvertToInt(leftValue, "Range Index", context.CultureInfo, expression.LeftExpression.Location);
                    int? rightInt = (rightValue is null) ? null : (rightValue is NCalc.Domain.Index rightIdx) ? rightIdx.Value : MathHelper.ConvertToInt(rightValue, "Range Index", context.CultureInfo, expression.RightExpression.Location);

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
                    if (!TryGetValueOrNull(await left.Value.ConfigureAwait(false), out leftValue))
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
                        RangeValue? range = (RangeValue?)await binExpr.Accept(this, cancellationToken).ConfigureAwait(false);
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
                                    result = await expr.Accept(this, cancellationToken).ConfigureAwait(false);

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
                        if (!TryGetValueOrNull(await right.Value.ConfigureAwait(false), out rightValue))
                            throw new NCalcParameterIndexException("The index does not evaluate to a number", expression.RightExpression.Location);

                        int index;
                        try
                        {
                            index = MathHelper.ConvertToInt(rightValue, "Index Access", context.CultureInfo, expression.RightExpression.Location);
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

                            if (result is LogicalExpression expr)
                                result = await expr.Accept(this, cancellationToken).ConfigureAwait(false);
                        }
                        else
                        if (identString is not null)
                        {
                            if (index < 0 || index >= identString.Length)
                                throw new NCalcParameterIndexException($"The index is out of bounds [0; {identString.Length - 1}]", expression.RightExpression.Location);
                            result = identString[index];
                        }
                    }
                    return result;
                }

                case BinaryExpressionType.WhileLoop:
                {
                    object? result = null;

                    int ctr = 0;

                    while (ctr < AsyncExpression.MaxLoopIterations)
                    {
                        ctr++;

                        if (!TryGetValueOrNull(await EvaluateAsync(expression.LeftExpression, cancellationToken).ConfigureAwait(false), out leftValue))
                            break;

                        if (!MathHelper.ConvertToBoolean(leftValue, "While loop", context.CultureInfo, expression.LeftExpression.Location))
                            break;

                        try
                        {
                            TryGetValueOrNull(await EvaluateAsync(expression.RightExpression, cancellationToken).ConfigureAwait(false), out result);
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
        }
        catch (NCalcEvaluationException ex)
        {
            if (ex.Location == Parser.ExpressionLocation.Empty)
                ex.Location = expression.Location;
            throw;
        }
        return null;
    }

    public virtual async ValueTask<object?> Visit(UnaryExpression expression, CancellationToken cancellationToken = default)
    {
        if (expression.Type == UnaryExpressionType.Not && context.Options.HasFlag(ExpressionOptions.UseTernaryLogic))
        {
            object? value = await expression.Expression.Accept(this, cancellationToken).ConfigureAwait(false);

            return K3LogicHelper.Not(value);
        }

        // Recursively evaluates the underlying expression
        var result = await expression.Expression.Accept(this, cancellationToken).ConfigureAwait(false);

        return EvaluationHelper.Unary(expression, result, context);
    }

    public virtual async ValueTask<object?> Visit(PercentExpression expression, CancellationToken cancellationToken = default)
    {
        object? result = await expression.Expression.Accept(this, cancellationToken).ConfigureAwait(false);
        if (result is null)
            return result;
        return new Percent(result);
    }

    public virtual async ValueTask<object?> Visit(ComplexNumberExpression expression, CancellationToken cancellationToken = default)
    {
        object? result = await expression.Expression.Accept(this, cancellationToken).ConfigureAwait(false);
        if (result is null)
            return result;
        return new ComplexNumber(0, result, new MathHelperOptions(expression.CultureInfo ?? CultureInfo.CurrentCulture, expression.Options));
    }

    public virtual async ValueTask<object?> Visit(FunctionCall functionCall, CancellationToken cancellationToken = default)
    {
        var argsCount = functionCall.Parameters.Count;

        var functionName = functionCall.Identifier.Name;

        if (context.UserFunctions.Count > 0)
        {
            Function? userFunction;
            if ((context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup)
                ? EvaluationHelper.GetFunctionFromListNoCase(context.UserFunctions, functionName, out userFunction)
                : context.UserFunctions.TryGetValue(functionName, out userFunction)) && userFunction is not null)
            {
                EvaluationHelper.EnsureProperParamNumInFunctionCall(userFunction, functionCall);
                return await ExecuteUserFunctionCallAsync(userFunction, functionCall, context, cancellationToken).ConfigureAwait(false);
            }
        }

        var args = new AsyncExpression[argsCount];

        // Don't call parameters right now, instead let the function do it as needed.
        // Some parameters shouldn't be called, for instance, in a if(), the "not" value might be a division by zero
        // Evaluating every value could produce unexpected behavior
        for (var i = 0; i < argsCount; i++)
        {
            args[i] = new AsyncExpression(functionCall.Parameters[i], context);
        }

        var functionArgs = new AsyncFunctionArgs(functionCall.Identifier.Id, args);

        await OnEvaluateFunctionAsync(functionName, functionArgs, cancellationToken).ConfigureAwait(false);

        if (functionArgs.HasResult)
        {
            return functionArgs.Result;
        }

        AsyncExpressionFunction? expressionFunction = null;
        if ((context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup)
                ? GetFunctionFromListNoCase(context.Functions, functionName, out expressionFunction)
                : context.Functions.TryGetValue(functionName, out expressionFunction))
            && expressionFunction is not null)
        {
            return await expressionFunction(new AsyncExpressionFunctionData(functionCall.Identifier.Id, args, context), cancellationToken).ConfigureAwait(false);
        }

        return await AsyncBuiltInFunctionHelper.EvaluateAsync(functionName, args, context, functionCall.Location, cancellationToken).ConfigureAwait(false);
    }

    public virtual async ValueTask<object?> Visit(Identifier identifier, CancellationToken cancellationToken = default)
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

        await OnEvaluateParameterAsync(identifierName, parameterArgs, cancellationToken).ConfigureAwait(false);

        if (parameterArgs.HasResult)
        {
            result = parameterArgs.Result;
            if (result is null)
            {
                return result;
            }
        }

        if (result is null)
        {
            object? parameter = null;
            if (context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup)
                    ? EvaluationHelper.GetParameterValueFromListNoCase(context.StaticParameters, identifierName, out parameter)
                    : context.StaticParameters.TryGetValue(identifierName, out parameter))
            {
                if (parameter is AsyncExpression expression)
                {
                    //Share the parameters with child expression.
                    foreach (var p in context.StaticParameters)
                    {
                        if (context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup))
                            EvaluationHelper.SetParameterValueFromListNoCase(expression.Parameters, p.Key, p.Value);
                        else
                            expression.Parameters[p.Key] = p.Value;
                    }

                    foreach (var p in context.DynamicParameters)
                    {
                        if (context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup))
                            SetParameterValueFromListNoCase(expression.DynamicParameters, p.Key, p.Value);
                        else
                            expression.DynamicParameters[p.Key] = p.Value;
                    }

                    expression.EvaluateFunctionAsync += context.AsyncEvaluateFunctionHandler;
                    expression.EvaluateParameterAsync += context.AsyncEvaluateParameterHandler;
                    expression.UpdateParameterAsync += context.AsyncUpdateParameterHandler;
                    expression.MatchStringAsync += context.AsyncMatchStringHandler;

                    result = await expression.EvaluateAsync(cancellationToken).ConfigureAwait(false);
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
        if (result is null)
        {
            AsyncExpressionParameter? dynamicParameter = null;
            if (context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup)
                    ? GetParameterValueFromListNoCase(context.DynamicParameters, identifierName, out dynamicParameter)
                    : context.DynamicParameters.TryGetValue(identifierName, out dynamicParameter))
            {
                result = dynamicParameter is null ? null : await dynamicParameter(new AsyncExpressionParameterData(identifier.Id, context), cancellationToken).ConfigureAwait(false);
                if (result is null)
                {
                    return result;
                }
            }
        }

        if (result != null)
        {
            return result;
        }

        throw new NCalcParameterNotDefinedException(identifierName, identifier.Location);
    }

    public virtual ValueTask<object?> Visit(ValueExpression expression, CancellationToken cancellationToken = default) => new(expression.Value);
    public virtual ValueTask<object?> Visit(FunctionExpression expression, CancellationToken cancellationToken = default) => new((object?)null);

    public virtual async ValueTask<object?> Visit(LogicalExpressionList list, CancellationToken cancellationToken = default)
    {
        List<object?> result = [];

        foreach (var expr in list)
        {
            result.Add(await EvaluateAsync(expr, cancellationToken).ConfigureAwait(false));
            cancellationToken.ThrowIfCancellationRequested();
        }

        return result;
    }

    public ValueTask<object?> Visit(ExpressionGroup group, CancellationToken cancellationToken = default)
    {
        return group.Expression.Accept(this, cancellationToken);
    }

    public virtual async ValueTask<object?> Visit(StatementSequence seq, CancellationToken cancellationToken = default)
    {
        object? result = null;

        foreach (var expr in seq)
        {
            result = await EvaluateAsync(expr, cancellationToken).ConfigureAwait(false);
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
            if ((a is null || b is null) && !(a is null && b is null))
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

    internal async ValueTask<object?> EvaluateNoRecurseAsync(LogicalExpression expression, CancellationToken cancellationToken = default)
    {
        List<ExpressionTask<ValueTask<object?>>> stack = [];
        ExpressionTask<ValueTask<object?>> root = new(null, new ExpressionState<ValueTask<object?>>(expression));
        stack.Add(root);
        ExpressionTask<ValueTask<object?>> currentTask;
        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Ask visitor to visit the expression. Pass the task to it.
            // The visitor may
            // a) return the value
            // b) add an expression to the task that must be evaluated first.
            currentTask = stack[^1];
            await currentTask.State.Expression.AcceptNoRecurse<ValueTask<object?>>(this, currentTask, cancellationToken).ConfigureAwait(false);
            if (currentTask.State.ValueSet)
            {
                // Remove the current task from the stack
                stack.Remove(currentTask);
            }
            else
            {
                ExpressionState<ValueTask<object?>> state;
                // We add child expressions starting from the end to let different expression types put the child expressions in the order in which those child expressions happen in the evaluated expression,
                // but the leftmost child must appear at the top of the stack so that it is evaluated first
                for (int i = currentTask.ChildStates.Count - 1; i >= 0; i--)
                {
                    state = currentTask.ChildStates[i];
                    if (!state.ValueSet)
                    {
                        stack.Add(new ExpressionTask<ValueTask<object?>>(currentTask, state));
                    }
                }
            }
        }

        return root.State.ValueSet ? root.State.Value : null;
    }

    internal static async ValueTask<object?> ExecuteUserFunctionCallAsync(Function userFunction, FunctionCall functionCall, AsyncExpressionContext context, CancellationToken cancellationToken = default)
    {
        Dictionary<string, ArgumentStateBase> argumentStates = [];

        EvaluationHelper.PopulateArgumentStates(userFunction, functionCall, argumentStates, context, (expression, context) => new AsyncArgumentState(expression, context));

        var expression = new AsyncExpression(userFunction.Body, context.Options, context.CultureInfo)
        {
            AdvancedOptions = context.AdvancedOptions
        };

        bool ignoreCase = context.Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup);

        expression.EvaluateParameterAsync += async (name, args, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string paramName = ignoreCase ? name.ToLowerInvariant() : name;
            if (argumentStates.TryGetValue(paramName, out var state))
            {
                args.Result = await ((AsyncArgumentState)state).GetValueAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            if (context.AsyncEvaluateParameterHandler != null)
                await context.AsyncEvaluateParameterHandler.Invoke(name, args, cancellationToken).ConfigureAwait(false);
        };

        expression.UpdateParameterAsync +=
            (name, args, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            string paramName = ignoreCase ? name.ToLowerInvariant() : name;
            if (argumentStates.TryGetValue(paramName, out var state))
            {
                ((AsyncArgumentState)state).SetValue(args.Value);
                args.UpdateParameterLists = false;
            }
#if NET8_0_OR_GREATER
            return ValueTask.CompletedTask;
#else
            return new ValueTask(Task.CompletedTask);
#endif
        };

        expression.EvaluateFunctionAsync += (name, args, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.AsyncEvaluateFunctionHandler != null)
                return context.AsyncEvaluateFunctionHandler.Invoke(name, args, cancellationToken);
            else
#if NET8_0_OR_GREATER
                return ValueTask.CompletedTask;
#else
                return new ValueTask(Task.CompletedTask);
#endif
        };

        expression.MatchStringAsync += (args, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.AsyncMatchStringHandler != null)
                return context.AsyncMatchStringHandler.Invoke(args, cancellationToken);
            else
#if NET8_0_OR_GREATER
                return ValueTask.CompletedTask;
#else
                return new ValueTask(Task.CompletedTask);
#endif
        };

        try
        {
            return await expression.EvaluateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (NCalcFlowControl ex) when (ex.Type == NCalcFlowControl.FlowControlType.Return)
        {
            return ex.ReturnValue;
        }
    }

    private static bool GetFunctionFromListNoCase(IDictionary<string, AsyncExpressionFunction> dictionary, string functionName, out AsyncExpressionFunction? function)
    {
        functionName = functionName.ToUpperInvariant();
        KeyValuePair<string, AsyncExpressionFunction>? functionPair = dictionary.FirstOrDefault((f) => f.Key.ToUpperInvariant() == functionName);
        if (functionPair.HasValue && !string.IsNullOrEmpty(functionPair.Value.Key))
        {
            function = functionPair.Value.Value;
            return true;
        }

        function = null;
        return false;
    }

    internal static bool GetParameterValueFromListNoCase(IDictionary<string, AsyncExpressionParameter> dictionary, string parameterName, out AsyncExpressionParameter? value)
    {
        parameterName = parameterName.ToUpperInvariant();
        KeyValuePair<string, AsyncExpressionParameter>? paramPair = dictionary.FirstOrDefault((f) => f.Key.ToUpperInvariant() == parameterName);
        if (paramPair.HasValue && !string.IsNullOrEmpty(paramPair.Value.Key))
        {
            value = paramPair.Value.Value;
            return true;
        }

        value = null;
        return false;
    }

    internal static void SetParameterValueFromListNoCase(IDictionary<string, AsyncExpressionParameter> dictionary, string parameterName, AsyncExpressionParameter value)
    {
        var lParameterName = parameterName.ToUpperInvariant();
        string? paramKey = dictionary.Keys.FirstOrDefault((k) => k.ToUpperInvariant() == lParameterName);
        if (!string.IsNullOrEmpty(paramKey))
            dictionary[paramKey] = value;
        else
            dictionary[parameterName] = value;
    }
}

internal class AsyncArgumentState(LogicalExpression expression, ExpressionContextBase context) : ArgumentStateBase
{
    internal async ValueTask<object?> GetValueAsync(CancellationToken cancellationToken = default)
    {
        if (!_valueSet)
        {
            var expr = new AsyncExpression(expression, context as AsyncExpressionContext);
            _value = await expr.EvaluateAsync(cancellationToken).ConfigureAwait(false);
            _valueSet = true;
        }
        return _value;
    }

    internal void SetValue(object? value)
    {
        _value = value;
        _valueSet = true;
    }
}
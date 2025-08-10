using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Parser;

namespace NCalc.Helpers;

public static class AsyncBuiltInFunctionHelper
{
    public static async ValueTask<object?> EvaluateAsync(string functionName, AsyncExpression[] arguments, AsyncExpressionContext context, ExpressionLocation location, CancellationToken cancellationToken = default)
    {
        var caseInsensitive = context.Options.HasFlag(ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
        var comparison = caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (functionName.Equals("%") || functionName.Equals("PercentOf", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("PercentOf() takes exactly 2 arguments", location);
            object? arg1 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            if (arg1 == null)
                return null;
            object? arg2 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            if (arg2 == null)
                return null;
            object? result = MathHelper.Divide(MathHelper.Multiply(100, arg1, false, context), arg2, true, context);
            if (result != null)
                return new Percent(result);
            else
                return null;
        }
        if (functionName.Equals("PercentDiff", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("PercentDiff() takes exactly 2 arguments", location);
            object? arg1 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            if (arg1 == null)
                return null;
            object? arg2 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            if (arg2 == null)
                return null;
            object? result = MathHelper.Divide(MathHelper.Multiply(MathHelper.Subtract(arg2, arg1, false, context), 100, false, context), arg1, true, context);
            if (result != null)
                return new Percent(result);
            else
                return null;
        }
        if (functionName.Equals("Abs", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Abs() takes exactly 1 argument", location);
            return MathHelper.Abs(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Acos", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Acos() takes exactly 1 argument", location);
            return MathHelper.Acos(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Asin", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Asin() takes exactly 1 argument", location);
            return MathHelper.Asin(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Atan", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Atan() takes exactly 1 argument", location);
            return MathHelper.Atan(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Atan2", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Atan2() takes exactly 2 arguments", location);
            return MathHelper.Atan2(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Ceiling", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Ceiling() takes exactly 1 argument", location);
            return MathHelper.Ceiling(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Cos", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Cos() takes exactly 1 argument", location);
            return MathHelper.Cos(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Exp", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Exp() takes exactly 1 argument", location);
            return MathHelper.Exp(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Floor", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Floor() takes exactly 1 argument", location);
            return MathHelper.Floor(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("IEEERemainder", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("IEEERemainder() takes exactly 2 arguments", location);
            return MathHelper.IEEERemainder(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Ln", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Ln() takes exactly 1 argument", location);
            return MathHelper.Ln(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Log", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Log() takes exactly 2 arguments", location);
            return MathHelper.Log(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Log10", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Log10() takes exactly 1 argument", location);
            return MathHelper.Log10(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Pow", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Pow() takes exactly 2 arguments", location);
            return MathHelper.Pow(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false), true, context);
        }
        if (functionName.Equals("Round", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Round() takes exactly 2 arguments", location);
            var rounding = context.Options.HasFlag(ExpressionOptions.RoundAwayFromZero)
                ? MidpointRounding.AwayFromZero
                : MidpointRounding.ToEven;
            return MathHelper.Round(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false), rounding, context);
        }
        if (functionName.Equals("Sign", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Sign() takes exactly 1 argument", location);
            return MathHelper.Sign(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Sin", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Sin() takes exactly 1 argument", location);
            return MathHelper.Sin(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Sqrt", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Sqrt() takes exactly 1 argument", location);
            return MathHelper.Sqrt(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Tan", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Tan() takes exactly 1 argument", location);
            return MathHelper.Tan(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Truncate", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Truncate() takes exactly 1 argument", location);
            return MathHelper.Truncate(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Max", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Max() takes exactly 2 arguments", location);
            return MathHelper.Max(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("Min", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Min() takes exactly 2 arguments", location);
            return MathHelper.Min(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false), context);
        }
        if (functionName.Equals("MakeList", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("MakeList() takes exactly 1 argument", location);
            var sizeObj = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            if (sizeObj is null)
                throw new NCalcEvaluationException("List size is evaluated to null in a call to MakeList()", location);
            if (!MathHelper.IsBoxedIntegerNumberOrBigNumber(sizeObj))
                throw new NCalcEvaluationException("List size is not evaluated to an integer number in a call to MakeList()", location);
            int size = MathHelper.ConvertToInt(sizeObj, context);
            if (size <= 0)
                throw new NCalcEvaluationException($"List size is {size}, and it must be positive in a call to MakeList()", location);

            return new object?[size];
        }
        if (functionName.Equals("ifs", comparison))
        {
            if (arguments.Length < 2)
            {
                throw new NCalcEvaluationException("ifs() takes at least 2 arguments", location);
            }

            for (int i = 0; i < arguments.Length; i += 2)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var argument = arguments[i];

                if (i == arguments.Length - 1)
                    return await argument.EvaluateAsync(cancellationToken).ConfigureAwait(false);

                var tf = Convert.ToBoolean(await argument.EvaluateAsync(cancellationToken).ConfigureAwait(false), context.CultureInfo);
                if (tf)
                    return await arguments[i + 1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            }
            if (arguments.Length % 2 == 1)
                return await arguments[^1].EvaluateAsync(cancellationToken).ConfigureAwait(false);

            return null;
        }
        if (functionName.Equals("iff", comparison) || functionName.Equals("if", comparison))
        {
            if (arguments.Length < 2 || arguments.Length > 3)
                throw new NCalcEvaluationException("iff() takes 2 or 3 arguments", location);
            var cond = Convert.ToBoolean(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), context.CultureInfo);
            return cond ? await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false) : ((arguments.Length == 3) ? await arguments[2].EvaluateAsync(cancellationToken).ConfigureAwait(false) : null);
        }
        if (functionName.Equals("in", comparison))
        {
            if (arguments.Length < 2)
                throw new NCalcEvaluationException("in() takes at least 2 arguments", location);
            var parameter = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            var evaluation = false;
            for (var i = 1; i < arguments.Length; i++)
            {
                if (TypeHelper.CompareUsingMostPreciseType(parameter, await arguments[i].EvaluateAsync(cancellationToken).ConfigureAwait(false), context) != 0) continue;
                evaluation = true;
                break;
            }

            return evaluation;
        }

        throw new NCalcFunctionNotFoundException(functionName, location);
    }
}

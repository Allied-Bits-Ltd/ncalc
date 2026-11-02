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
            if (arg1 is null)
                return null;
            object? arg2 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            if (arg2 is null)
                return null;
            return BuiltInFunctionHelper.PercentOf(arg1, arg2, context, location);
        }
        if (functionName.Equals("PercentDiff", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("PercentDiff() takes exactly 2 arguments", location);
            object? arg1 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            if (arg1 is null)
                return null;
            object? arg2 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            if (arg2 is null)
                return null;
            return BuiltInFunctionHelper.PercentDiff(arg1, arg2, context, location);
        }
        if (functionName.Equals("Abs", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Abs() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Abs(arg0, context);
        }
        if (functionName.Equals("Acos", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Acos() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Acos(arg0, context);
        }
        if (functionName.Equals("Asin", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Asin() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Asin(arg0, context);
        }
        if (functionName.Equals("Atan", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Atan() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Atan(arg0, context);
        }
        if (functionName.Equals("Atan2", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Atan2() takes exactly 2 arguments", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            var arg1 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.Atan2(arg0, arg1, context);
        }
        if (functionName.Equals("Ceiling", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Ceiling() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Ceiling(arg0, context);
        }
        if (functionName.Equals("Cos", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Cos() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Cos(arg0, context);
        }
        if (functionName.Equals("Exp", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Exp() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Exp(arg0, context);
        }
        if (functionName.Equals("Floor", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Floor() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Floor(arg0, context);
        }
        if (functionName.Equals("IEEERemainder", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("IEEERemainder() takes exactly 2 arguments", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            var arg1 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.IEEERemainder(arg0, arg1, context);
        }
        if (functionName.Equals("Ln", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Ln() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Ln(arg0, context);
        }
        if (functionName.Equals("Log", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Log() takes exactly 2 arguments", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            var arg1 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.Log(arg0, arg1, context);
        }
#if NET8_0_OR_GREATER
        if (functionName.Equals("Log2", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Log2() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Log2(arg0, context);
        }
#endif
        if (functionName.Equals("Log10", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Log10() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Log10(arg0, context);
        }
        if (functionName.Equals("Pow", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Pow() takes exactly 2 arguments", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            var arg1 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.Pow(arg0, arg1, true, context);
        }
        if (functionName.Equals("Round", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Round() takes exactly 2 arguments", location);
            var rounding = context.Options.HasFlag(ExpressionOptions.RoundAwayFromZero)
                ? MidpointRounding.AwayFromZero
                : MidpointRounding.ToEven;
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            var arg1 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.Round(arg0, arg1, rounding, context);
        }
        if (functionName.Equals("Sign", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Sign() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Sign(arg0, context);
        }
        if (functionName.Equals("Sin", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Sin() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Sin(arg0, context);
        }
        if (functionName.Equals("Sqrt", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Sqrt() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Sqrt(arg0, context);
        }
        if (functionName.Equals("Tan", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Tan() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Tan(arg0, context);
        }
        if (functionName.Equals("Trunc", comparison) || functionName.Equals("Truncate", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Truncate() takes exactly 1 argument", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Truncate(arg0, context);
        }
        if (functionName.Equals("Max", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Max() takes exactly 2 arguments", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            var arg1 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.Max(arg0, arg1, context);
        }
        if (functionName.Equals("Min", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Min() takes exactly 2 arguments", location);
            var arg0 = await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            var arg1 = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.Min(arg0, arg1, context);
        }
        if (functionName.Equals("MakeList", comparison))
        {
            if (arguments.Length != 1 && arguments.Length != 2)
                throw new NCalcEvaluationException("MakeList() takes 1 or 2 arguments", location);
            // The size is checked before the value is evaluated
            long size = BuiltInFunctionHelper.GetMakeSize(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), "List size", "MakeList", location);
            var value = arguments.Length == 2 ? await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false) : null;
            return BuiltInFunctionHelper.MakeList(size, value);
        }
        if ((functionName.Equals("MakeStr", comparison)) || (functionName.Equals("MakeString", comparison)))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("MakeStr() takes exactly 2 arguments", location);
            long size = BuiltInFunctionHelper.GetMakeSize(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), "String length", "MakeStr", location);
            var value = await arguments[1].EvaluateAsync(cancellationToken).ConfigureAwait(false);
            return BuiltInFunctionHelper.MakeString(size, value, location);
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

                var tf = MathHelper.ConvertToBoolean(await argument.EvaluateAsync(cancellationToken).ConfigureAwait(false), "ifs", context.CultureInfo, argument.LogicalExpression?.Location);
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

            var cond = MathHelper.ConvertToBoolean(await arguments[0].EvaluateAsync(cancellationToken).ConfigureAwait(false), "iff", context.CultureInfo, arguments[0].LogicalExpression?.Location);
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
                try
                {
                    // The same equality as the "=" operator
                    if (!EvaluationHelper.Compare(parameter, await arguments[i].EvaluateAsync(cancellationToken).ConfigureAwait(false), ComparisonType.Equal, context))
                        continue;
                }
                catch (NCalcConversionException cex)
                {
                    LogicalExpression? lex = arguments[i].LogicalExpression;
                    if (lex is not null)
                        cex.Location = lex.Location;

                    throw;
                }
                evaluation = true;
                break;
            }

            return evaluation;
        }

        throw new NCalcFunctionNotFoundException(functionName, location);
    }
}

using NCalc.Domain;
using NCalc.Exceptions;
using NCalc.Parser;

namespace NCalc.Helpers;

public static class BuiltInFunctionHelper
{
    public static object? Evaluate(string functionName, Expression[] arguments, ExpressionContext context, ExpressionLocation location)
    {
        var caseInsensitive = context.Options.HasFlag(ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
        var comparison = caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (functionName.Equals("%") || functionName.Equals("PercentOf", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("PercentOf() takes exactly 2 arguments", location);
            object? arg1 = arguments[0].Evaluate();
            if (arg1 is null)
                return null;
            object? arg2 = arguments[1].Evaluate();
            if (arg2 is null)
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
            object? arg1 = arguments[0].Evaluate();
            if (arg1 is null)
                return null;
            object? arg2 = arguments[1].Evaluate();
            if (arg2 is null)
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
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Abs(arg0, context);
        }
        if (functionName.Equals("Acos", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Acos() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Acos(arg0, context);
        }
        if (functionName.Equals("Asin", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Asin() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Asin(arg0, context);
        }
        if (functionName.Equals("Atan", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Atan() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Atan(arg0, context);
        }
        if (functionName.Equals("Atan2", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Atan2() takes exactly 2 arguments", location);
            var arg0 = arguments[0].Evaluate();
            var arg1 = arguments[1].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.Atan2(arg0, arg1, context);
        }
        if (functionName.Equals("Ceiling", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Ceiling() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Ceiling(arg0, context);
        }
        if (functionName.Equals("Cos", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Cos() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Cos(arg0, context);
        }
        if (functionName.Equals("Exp", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Exp() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Exp(arg0, context);
        }
        if (functionName.Equals("Floor", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Floor() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Floor(arg0, context);
        }
        if (functionName.Equals("IEEERemainder", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("IEEERemainder() takes exactly 2 arguments", location);
            var arg0 = arguments[0].Evaluate();
            var arg1 = arguments[1].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.IEEERemainder(arg0, arg1, context);
        }
        if (functionName.Equals("Ln", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Ln() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Ln(arg0, context);
        }
        if (functionName.Equals("Log", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Log() takes exactly 2 arguments", location);
            var arg0 = arguments[0].Evaluate();
            var arg1 = arguments[1].Evaluate();
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
            var arg0 = arguments[0].Evaluate();
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
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Log10(arg0, context);
        }
        if (functionName.Equals("Pow", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Pow() takes exactly 2 arguments", location);
            var arg0 = arguments[0].Evaluate();
            var arg1 = arguments[1].Evaluate();
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
            var arg0 = arguments[0].Evaluate();
            var arg1 = arguments[1].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.Round(arg0, arg1, rounding, context);
        }
        if (functionName.Equals("Sign", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Sign() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Sign(arg0, context);
        }
        if (functionName.Equals("Sin", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Sin() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Sin(arg0, context);
        }
        if (functionName.Equals("Sqrt", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Sqrt() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Sqrt(arg0, context);
        }
        if (functionName.Equals("Tan", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Tan() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Tan(arg0, context);
        }
        if (functionName.Equals("Trunc", comparison) || functionName.Equals("Truncate", comparison))
        {
            if (arguments.Length != 1)
                throw new NCalcEvaluationException("Truncate() takes exactly 1 argument", location);
            var arg0 = arguments[0].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null)
                return null;
            return MathHelper.Truncate(arg0, context);
        }
        if (functionName.Equals("Max", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Max() takes exactly 2 arguments", location);
            var arg0 = arguments[0].Evaluate();
            var arg1 = arguments[1].Evaluate();
            // A null argument gives a null result, as with the operators
            if (arg0 is null || arg1 is null)
                return null;
            return MathHelper.Max(arg0, arg1, context);
        }
        if (functionName.Equals("Min", comparison))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("Min() takes exactly 2 arguments", location);
            var arg0 = arguments[0].Evaluate();
            var arg1 = arguments[1].Evaluate();
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
            long size = GetMakeSize(arguments[0].Evaluate(), "List size", "MakeList", location);
            return MakeList(size, arguments.Length == 2 ? arguments[1].Evaluate() : null);
        }
        if ((functionName.Equals("MakeStr", comparison)) || (functionName.Equals("MakeString", comparison)))
        {
            if (arguments.Length != 2)
                throw new NCalcEvaluationException("MakeStr() takes exactly 2 arguments", location);
            long size = GetMakeSize(arguments[0].Evaluate(), "String length", "MakeStr", location);
            return MakeString(size, arguments[1].Evaluate(), location);
        }
        if (functionName.Equals("ifs", comparison))
        {
            if (arguments.Length < 2)
            {
                throw new NCalcEvaluationException(
                    "ifs() takes at least 2 arguments", location);
            }

            for (int i = 0; i < arguments.Length; i+=2)
            {
                var argument = arguments[i];

                if (i == arguments.Length - 1)
                    return argument.Evaluate();

                var tf = MathHelper.ConvertToBoolean(argument.Evaluate(), "ifs", context.CultureInfo, argument.LogicalExpression?.Location);
                if (tf)
                    return arguments[i + 1].Evaluate();
            }
            if (arguments.Length % 2 == 1)
                return arguments[^1].Evaluate();

            return null;
        }
        if (functionName.Equals("iff", comparison) || functionName.Equals("if", comparison))
        {
            if (arguments.Length < 2 || arguments.Length > 3)
                throw new NCalcEvaluationException("iff() takes 2 or 3 arguments", location);

            var cond = MathHelper.ConvertToBoolean(arguments[0].Evaluate(), "iff", context.CultureInfo, arguments[0].LogicalExpression?.Location);
            return cond ? arguments[1].Evaluate() : ((arguments.Length == 3) ? arguments[2].Evaluate() : null);
        }
        if (functionName.Equals("in", comparison))
        {
            if (arguments.Length < 2)
                throw new NCalcEvaluationException("in() takes at least 2 arguments", location);

            var parameter = arguments[0].Evaluate();
            var evaluation = false;
            for (var i = 1; i < arguments.Length; i++)
            {
                try
                {
                    // The same equality as the "=" operator
                    if (!EvaluationHelper.Compare(parameter, arguments[i].Evaluate(), ComparisonType.Equal, context))
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

    /// <summary>
    /// The maximum number of elements of a list created by MakeList() and of characters of a string created by MakeStr().
    /// A larger request throws <see cref="NCalcEvaluationException"/>, so that a short expression cannot allocate gigabytes. The default is 10,000,000.
    /// </summary>
    public static int MaxMakeResultLength { get; set; } = 10_000_000;

    internal static long GetMakeSize(object? sizeObj, string what, string function, ExpressionLocation location)
    {
        if (sizeObj is null)
            throw new NCalcEvaluationException($"{what} is evaluated to null in a call to {function}()", location);
        if (!MathHelper.IsBoxedIntegerNumberOrBigNumber(sizeObj) || sizeObj is ExtendedNumerics.BigDecimal)
            throw new NCalcEvaluationException($"{what} is not evaluated to an integer number in a call to {function}()", location);

        var size = MathHelper.ConvertToBigInteger(sizeObj);
        if (size.Sign <= 0)
            throw new NCalcEvaluationException($"{what} is {size}, and it must be positive in a call to {function}()", location);
        if (size > MaxMakeResultLength)
            throw new NCalcEvaluationException($"{what} is {size}, which exceeds the limit of {MaxMakeResultLength} (BuiltInFunctionHelper.MaxMakeResultLength) in a call to {function}()", location);
        return (long)size;
    }

    internal static object?[] MakeList(long size, object? value)
    {
        object?[] result = new object?[size];
        if (value is not null)
        {
            for (long i = 0; i < size; i++)
                result[i] = value;
        }
        return result;
    }

    internal static string MakeString(long size, object? value, ExpressionLocation location)
    {
        string valueStr;
        switch (value)
        {
            case null:
                throw new NCalcEvaluationException("The string element in `value` is evaluated to null in a call to MakeStr()", location);
            case string str:
                valueStr = str;
                break;
            case char ch:
                valueStr = ch.ToString();
                break;
            case not ExtendedNumerics.BigDecimal when MathHelper.IsBoxedIntegerNumberOrBigNumber(value):
            {
                var valueCode = MathHelper.ConvertToBigInteger(value);
                if (valueCode < 1 || valueCode > 65535)
                    throw new NCalcEvaluationException("If a string element in `value` in a call to MakeStr() is provided as a character code, it must be in the range of [1..65535]", location);
                valueStr = ((char)(int)valueCode).ToString();
                break;
            }
            default:
                throw new NCalcEvaluationException("The string element in `value` in a call to MakeStr() must be a character, a character code (an integer number), or a string", location);
        }

        long length = size * valueStr.Length;
        if (length > MaxMakeResultLength)
            throw new NCalcEvaluationException($"The resulting string would have {length} characters, which exceeds the limit of {MaxMakeResultLength} (BuiltInFunctionHelper.MaxMakeResultLength) in a call to MakeStr()", location);

        StringBuilder sb = new((int)length);
        for (long i = 0; i < size; i++)
            sb.Append(valueStr);
        return sb.ToString();
    }
}

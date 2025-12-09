using NCalc.Domain;

using ValueType = NCalc.Domain.ValueType;

namespace NCalc.Visitors;

/// <summary>
/// Class responsible to converting a <see cref="LogicalExpression"/> into a <see cref="string"/> representation.
/// </summary>
public class SerializationVisitor(SerializationContext context) : ILogicalExpressionVisitor<string>
{
    private readonly NumberFormatInfo _numberFormatInfo = new()
    {
        NumberDecimalSeparator = (context.AdvancedOptions is null) ? "." : context.AdvancedOptions.GetDecimalSeparatorChar().ToString()
    };

    public string Visit(TernaryExpression expression, CancellationToken cancellationToken = default)
    {
        expression.SetOptions(context.Options, context.CultureInfo, context.AdvancedOptions);

        if (expression is IfStatementExpression)
        {
            var resultBuilder = new StringBuilder();
            resultBuilder.Append("if (");
            resultBuilder.Append(EncapsulateNoValue(expression.LeftExpression, false, false));
            resultBuilder.Append(") ");
            if (expression.MiddleExpression is ExpressionGroup)
            {
                resultBuilder.Append(EncapsulateNoValue(expression.MiddleExpression, false));
            }
            else
            {
                resultBuilder.Append("{ ");
                resultBuilder.Append(EncapsulateNoValue(expression.MiddleExpression));
                resultBuilder.Append(" }");
            }

            if (!(expression.RightExpression is ValueExpression valueExp && valueExp.Type == ValueType.NoValue))
            {
                resultBuilder.Append(" else ");
                if (expression.RightExpression is ExpressionGroup)
                {
                    resultBuilder.Append(EncapsulateNoValue(expression.RightExpression, false));
                }
                else
                {
                    resultBuilder.Append("{ ");
                    resultBuilder.Append(EncapsulateNoValue(expression.RightExpression));
                    resultBuilder.Append(" }");
                }
            }

            return resultBuilder.ToString();
        }
        else
        {
            return EncapsulateNoValue(expression.LeftExpression) + "? " + EncapsulateNoValue(expression.MiddleExpression) + ": " + EncapsulateNoValue(expression.RightExpression);
        }
    }

    public string Visit(BinaryExpression expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        expression.SetOptions(context.Options, context.CultureInfo, context.AdvancedOptions);

        bool parensNeeded = false;
        bool appendSpace = false;

        var resultBuilder = new StringBuilder();

        if (expression.Type == BinaryExpressionType.WhileLoop)
        {
            resultBuilder.Append("while (");
            resultBuilder.Append(EncapsulateNoValue(expression.LeftExpression, false));
            if (expression.RightExpression is ExpressionGroup)
            {
                resultBuilder.Append(") ");
                resultBuilder.Append(EncapsulateNoValue(expression.RightExpression, false));
            }
            else
            {
                resultBuilder.Append(") { ");
                resultBuilder.Append(EncapsulateNoValue(expression.RightExpression, false));
                resultBuilder.Append(" }");
            }
            return resultBuilder.ToString();
        }
        else
        if (expression.Type == BinaryExpressionType.Factorial)
        {
            if ((expression.RightExpression is ValueExpression valueExpression) && (valueExpression.Type == ValueType.Integer) && (valueExpression.Value != null))
            {
                parensNeeded = !(expression.LeftExpression is Identifier || expression.LeftExpression is ValueExpression);
                resultBuilder.Append(EncapsulateNoValue(expression.LeftExpression, false, parensNeeded));

                var step = (int)valueExpression.Value;
                for (int i = 0; i < step; i++)
                    resultBuilder.Append('!');
                return resultBuilder.ToString();
            }
        }
        else
        {
            parensNeeded = false;
            if (expression.Type is BinaryExpressionType.RangeIndex)
                parensNeeded = !(expression.LeftExpression is Identifier || expression.LeftExpression is ValueExpression) && !(expression.LeftExpression is UnaryExpression unExp && unExp.Type == UnaryExpressionType.FromEnd);

            appendSpace = expression.Type != BinaryExpressionType.IndexAccess && expression.Type != BinaryExpressionType.RangeIndex;
            resultBuilder.Append(EncapsulateNoValue(expression.LeftExpression, appendSpace, parensNeeded));

            resultBuilder.Append(expression.Type switch
            {
                BinaryExpressionType.Assignment => context.Options.HasFlag(ExpressionOptions.UseCStyleAssignments) ? "= " : ":= ",
                BinaryExpressionType.PlusAssignment => "+= ",
                BinaryExpressionType.MinusAssignment => "-= ",
                BinaryExpressionType.MultiplyAssignment => "*= ",
                BinaryExpressionType.DivAssignment => "/= ",
                BinaryExpressionType.AndAssignment => "&= ",
                BinaryExpressionType.OrAssignment => "|= ",
                BinaryExpressionType.XOrAssignment => "^= ",
                BinaryExpressionType.And => "and ",
                BinaryExpressionType.Or => "or ",
                BinaryExpressionType.XOr => "xor ",
                BinaryExpressionType.Div => context.Options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations) ? "\u00F7 " : "/ ",
                BinaryExpressionType.IntDivB => "div ",
                BinaryExpressionType.IntDivP => "// ",
                BinaryExpressionType.Equal => context.Options.HasFlag(ExpressionOptions.UseCStyleAssignments) ? "== " : "= ",
                BinaryExpressionType.Greater => "> ",
                BinaryExpressionType.GreaterOrEqual => context.Options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations) ? "\u2265 " : ">= ",
                BinaryExpressionType.Less => "< ",
                BinaryExpressionType.LessOrEqual => context.Options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations) ? "\u2264 " : "<= ",
                BinaryExpressionType.Minus => "- ",
                BinaryExpressionType.Modulo => (context.AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.CalculatePercent) == true) ? "mod " : "% ",
                BinaryExpressionType.NotEqual => context.Options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations) ? "\u2260 " : "!= ",
                BinaryExpressionType.Plus => "+ ",
                BinaryExpressionType.Times => context.Options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations) ? "\u00D7 " : "* ",
                BinaryExpressionType.BitwiseAnd => context.Options.HasFlag(ExpressionOptions.SkipLogicalAndBitwiseOpChars) ? "bit_and " : "& ",
                BinaryExpressionType.BitwiseOr => context.Options.HasFlag(ExpressionOptions.SkipLogicalAndBitwiseOpChars) ? "bit_or " : "| ",
                BinaryExpressionType.BitwiseXOr => context.Options.HasFlag(ExpressionOptions.SkipLogicalAndBitwiseOpChars) ? "bit_xor " : "^ ",
                BinaryExpressionType.LeftShift => "<< ",
                BinaryExpressionType.RightShift => ">> ",
                BinaryExpressionType.Exponentiation => context.Options.HasFlag(ExpressionOptions.SkipLogicalAndBitwiseOpChars) ? (context.Options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations) ? "\u2291 " : "^ ") : "** ",
                BinaryExpressionType.In => context.Options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations) ? "\u2208 " : "in ",
                BinaryExpressionType.NotIn => context.Options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations) ? "\u2209 " : "not in ",
                BinaryExpressionType.Like => "like ",
                BinaryExpressionType.NotLike => "not like ",
                BinaryExpressionType.RangeIndex => "..",
                BinaryExpressionType.IndexAccess => "[",
                BinaryExpressionType.Unknown => "unknown ",
                _ => throw new ArgumentOutOfRangeException()
            });
        }

        if (expression.Type is BinaryExpressionType.RangeIndex)
            parensNeeded = !(expression.RightExpression is Identifier || expression.RightExpression is ValueExpression) && !(expression.RightExpression is UnaryExpression unExp2 && unExp2.Type == UnaryExpressionType.FromEnd);

        appendSpace = true;
        if (expression.Type == BinaryExpressionType.IndexAccess)
            appendSpace = false;

        resultBuilder.Append(
            EncapsulateNoValue(
                expression.RightExpression,
                appendSpace,//expression.Type != BinaryExpressionType.IndexAccess && expression.Type != BinaryExpressionType.RangeIndex && expression.Type != BinaryExpressionType.StatementSequence,
                parensNeeded));
        if (expression.Type == BinaryExpressionType.IndexAccess)
            resultBuilder.Append(']');

        return resultBuilder.ToString();
    }

    public string Visit(UnaryExpression expression, CancellationToken cancellationToken = default)
    {
        expression.SetOptions(context.Options, context.CultureInfo, context.AdvancedOptions);

        string result = expression.Type switch
        {
            UnaryExpressionType.Not => "!",
            UnaryExpressionType.Negate => "-",
            UnaryExpressionType.FromEnd => "^",
            UnaryExpressionType.BitwiseNot => context.Options.HasFlag(ExpressionOptions.SkipLogicalAndBitwiseOpChars) ? "bit_xor " : "~",
            UnaryExpressionType.SqRoot => "\u221a",
#if NET8_0_OR_GREATER
            UnaryExpressionType.CbRoot => "\u221b",
#endif
            UnaryExpressionType.FourthRoot => "\u221c",
            UnaryExpressionType.Return => "return ",
            _ => string.Empty
        };

        bool parensNeeded = !(expression.Expression is Identifier || expression.Expression is ValueExpression);
        result += EncapsulateNoValue(expression.Expression, parensNeeded: parensNeeded);
        return result;
    }

    public string Visit(PercentExpression expression, CancellationToken cancellationToken = default)
    {
        expression.SetOptions(context.Options, context.CultureInfo, context.AdvancedOptions);

        bool parensNeeded = !(expression.Expression is Identifier || expression.Expression is ValueExpression);
        return EncapsulateNoValue(expression.Expression, false, parensNeeded) + "%";
    }

    public string Visit(ValueExpression expression, CancellationToken cancellationToken = default)
    {
        expression.SetOptions(context.Options, context.CultureInfo, context.AdvancedOptions);

        var value = expression.Value;

        return expression.Type switch
        {
            ValueType.Boolean or ValueType.Integer => $"{value} ",
            ValueType.DateTime or ValueType.TimeSpan => $"#{value}# ",
            ValueType.Float => $"{decimal.Parse(value?.ToString() ?? string.Empty).ToString(_numberFormatInfo)} ",
            ValueType.Char => $"'{value}' ",
            ValueType.String =>
                (value is Parlot.TextSpan)
                ? $"{value} "
                : expression.StringKind switch
                {
                    StringKind.SingleQuote => $"'{expression.OriginalString}' ",
                    StringKind.DoubleQuote => $"\"{expression.OriginalString}\" ",
                    StringKind.RawDoubleQuote => $"@\"{value}\" ",
                    StringKind.BackQuote => $"`{value}` ",
                    _ => string.IsNullOrEmpty(expression.OriginalString) ? $"'{value}' " : $"'{expression.OriginalString}' ",
                },
            _ => "",
        };
    }

    public string Visit(FunctionCall function, CancellationToken cancellationToken = default)
    {
        function.SetOptions(context.Options, context.CultureInfo, context.AdvancedOptions);

        if (context.AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.UseResultReference) == true && function.Identifier.Name == "@")
        {
            return "@";
        }

        var resultBuilder = new StringBuilder(function.Identifier.Name +'(');

        for (int i = 0; i < function.Parameters.Count; i++)
        {
            resultBuilder.Append(function.Parameters[i].Accept(this, cancellationToken));
            if (i < function.Parameters.Count - 1)
            {
                resultBuilder.Remove(resultBuilder.Length - 1, 1);
                resultBuilder.Append(", ");
            }
        }

        while (resultBuilder[^1] == ' ')
            resultBuilder.Remove(resultBuilder.Length - 1, 1);

        resultBuilder.Append(") ");
        return resultBuilder.ToString();
    }

    public string Visit(Identifier identifier, CancellationToken cancellationToken = default)
    {
        if (identifier.IsBracketed)
            return $"[{identifier.Name}]";
        else
            return identifier.Name;
    }

    public string Visit(LogicalExpressionList list, CancellationToken cancellationToken = default)
    {
        list.SetOptions(context.Options, context.CultureInfo, context.AdvancedOptions);

        var resultBuilder = new StringBuilder("(");
        for (var i = 0; i < list.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resultBuilder.Append(list[i].Accept(this, cancellationToken).TrimEnd());
            if (i < list.Count - 1)
            {
                resultBuilder.Append("; ");
            }
        }
        resultBuilder.Append(')');
        return resultBuilder.ToString();
    }

    public string Visit(ExpressionGroup group, CancellationToken cancellationToken = default)
    {
        return string.Join(group.Expression.Accept(this, cancellationToken).Trim(), "{ ", " }");
    }

    public string Visit(FunctionExpression expression, CancellationToken cancellationToken = default)
    {
        StringBuilder resultBuilder = new();
        Function function = expression.Function;
        if (!string.IsNullOrEmpty(function.Description))
        {
            string desc = function.Description!;
            if (context.Options.HasFlag(ExpressionOptions.SupportCStyleComments))
            {
                resultBuilder.Append("/* ");
                resultBuilder.Append(desc);
                resultBuilder.AppendLine(" */");
            }
            else
            if (context.Options.HasFlag(ExpressionOptions.SupportPythonComments))
            {
                if (desc.Contains('\n'))
                {
                    foreach (var line in desc.Split('\n'))
                    {
                        resultBuilder.Append("# ");
                        resultBuilder.AppendLine(line);
                    }
                }
                else
                {
                    resultBuilder.Append("# ");
                    resultBuilder.AppendLine(desc);
                }
            }
        }
        resultBuilder.Append("fn (");
        bool paramAdded = false;
        foreach (var param in function.Parameters)
        {
            if (paramAdded)
                resultBuilder.Append(", ");
            else
                paramAdded = true;

            resultBuilder.Append(param.Name);
            if (param.IsOptional)
            {
                resultBuilder.Append(" = ");
                resultBuilder.Append(param.DefaultValue?.ToString() ?? "null");
            }
        }
        resultBuilder.Append(')');
        if (function.Body is ExpressionGroup)
        {
            resultBuilder.AppendLine();
            resultBuilder.Append(function.Body.Accept(this, cancellationToken));
        }
        else
        {
            resultBuilder.Append(" => ");
            resultBuilder.Append(function.Body.Accept(this, cancellationToken));
        }
        return resultBuilder.ToString();
    }

    public string Visit(StatementSequence seq, CancellationToken cancellationToken = default)
    {
        seq.SetOptions(context.Options, context.CultureInfo, context.AdvancedOptions);

        var resultBuilder = new StringBuilder();
        for (var i = 0; i < seq.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resultBuilder.Append(seq[i].Accept(this, cancellationToken).TrimEnd());
            if (i < seq.Count - 1)
            {
                resultBuilder.Append("; ");
            }
            else
                if (seq.EndsWithSeparator)
            {
                resultBuilder.Append(';');
            }
        }
        return resultBuilder.ToString();
    }
    protected virtual string EncapsulateNoValue(LogicalExpression expression, bool appendSpace = true, bool parensNeeded = false)
    {
        if (expression is ValueExpression valueExpression)
        {
            string result = valueExpression.Accept(this);
            if (!appendSpace)
                result = result.TrimEnd();
            return result;
        }

        var resultBuilder = new StringBuilder();

        /*if (((expression is BinaryExpression binaryExpression) && (binaryExpression.Type == BinaryExpressionType.Factorial)) || (expression is PercentExpression))
            parensNeeded = false;*/

        if (parensNeeded)
            resultBuilder.Append('(');
        resultBuilder.Append(expression.Accept(this));

        while (resultBuilder[^1] == ' ')
            resultBuilder.Length--;

        if (parensNeeded)
        {
            if (appendSpace)
                resultBuilder.Append(") ");
            else
                resultBuilder.Append(')');
        }
        else
        if (appendSpace)
        {
            resultBuilder.Append(' ');
        }

        return resultBuilder.ToString();
    }
}

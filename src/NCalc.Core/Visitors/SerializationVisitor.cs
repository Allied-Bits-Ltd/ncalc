﻿using NCalc.Domain;

using ValueType = NCalc.Domain.ValueType;

namespace NCalc.Visitors;

/// <summary>
/// Class responsible to converting a <see cref="LogicalExpression"/> into a <see cref="string"/> representation.
/// </summary>
public class SerializationVisitor : ILogicalExpressionVisitor<string>
{
    private readonly NumberFormatInfo _numberFormatInfo = new()
    {
        NumberDecimalSeparator = "."
    };

    public string Visit(TernaryExpression expression)
    {
        var resultBuilder = new StringBuilder();
        resultBuilder.Append(EncapsulateNoValue(expression.LeftExpression));
        resultBuilder.Append("? ");
        resultBuilder.Append(EncapsulateNoValue(expression.MiddleExpression));
        resultBuilder.Append(": ");
        resultBuilder.Append(EncapsulateNoValue(expression.RightExpression));
        return resultBuilder.ToString();
    }

    public string Visit(BinaryExpression expression)
    {
        var resultBuilder = new StringBuilder();

        if (expression.Type == BinaryExpressionType.Factorial)
        {
            if ((expression.RightExpression is ValueExpression valueExpression) && (valueExpression.Type == ValueType.Integer) && (valueExpression.Value != null))
            {
                resultBuilder.Append(EncapsulateNoValue(expression.LeftExpression, false));

                var step = (int)valueExpression.Value;
                StringBuilder builder = new StringBuilder(step + 1);
                for (int i = 0; i < step; i++)
                    builder.Append('!');
                resultBuilder.Append(builder);
                return resultBuilder.ToString();
            }
        }
        else
        {
            resultBuilder.Append(EncapsulateNoValue(expression.LeftExpression));

            resultBuilder.Append(expression.Type switch
            {
                BinaryExpressionType.And => "and ",
                BinaryExpressionType.Or => "or ",
                BinaryExpressionType.Div => "/ ",
                BinaryExpressionType.Equal => "= ",
                BinaryExpressionType.Greater => "> ",
                BinaryExpressionType.GreaterOrEqual => ">= ",
                BinaryExpressionType.Less => "< ",
                BinaryExpressionType.LessOrEqual => "<= ",
                BinaryExpressionType.Minus => "- ",
                BinaryExpressionType.Modulo => "% ",
                BinaryExpressionType.NotEqual => "!= ",
                BinaryExpressionType.Plus => "+ ",
                BinaryExpressionType.Times => "* ",
                BinaryExpressionType.BitwiseAnd => "& ",
                BinaryExpressionType.BitwiseOr => "| ",
                BinaryExpressionType.BitwiseXOr => "^ ",
                BinaryExpressionType.LeftShift => "<< ",
                BinaryExpressionType.RightShift => ">> ",
                BinaryExpressionType.Exponentiation => "** ",
                BinaryExpressionType.In => "in ",
                BinaryExpressionType.NotIn => "not in ",
                BinaryExpressionType.Like => "like ",
                BinaryExpressionType.NotLike => "not like ",
                BinaryExpressionType.Unknown => "unknown ",
                _ => throw new ArgumentOutOfRangeException()
            });
        }
        resultBuilder.Append(EncapsulateNoValue(expression.RightExpression));
        return resultBuilder.ToString();
    }

    public string Visit(UnaryExpression expression)
    {
        var resultBuilder = new StringBuilder();

        resultBuilder.Append(expression.Type switch
        {
            UnaryExpressionType.Not => "!",
            UnaryExpressionType.Negate => "-",
            UnaryExpressionType.BitwiseNot => "~",
            UnaryExpressionType.SqRoot => "\u221a",
#if NET8_0_OR_GREATER
            UnaryExpressionType.CbRoot => "\u221b",
#endif
            UnaryExpressionType.FourthRoot => "\u221c",
            _ => string.Empty
        });

        resultBuilder.Append(EncapsulateNoValue(expression.Expression));

        return resultBuilder.ToString();
    }

    public string Visit(PercentExpression expression)
    {
        return EncapsulateNoValue(expression.Expression).TrimEnd() + "%";
    }

    public string Visit(ValueExpression expression)
    {
        var resultBuilder = new StringBuilder();

        switch (expression.Type)
        {
            case ValueType.Boolean:
                resultBuilder.Append(expression.Value).Append(' ');
                break;
            case ValueType.DateTime or ValueType.TimeSpan:
                resultBuilder.Append('#').Append(expression.Value).Append('#').Append(' ');
                break;
            case ValueType.Float:
                resultBuilder.Append(decimal.Parse(expression.Value?.ToString() ?? string.Empty).ToString(_numberFormatInfo))
                    .Append(' ');
                break;
            case ValueType.Integer:
                resultBuilder.Append(expression.Value).Append(' ');
                break;
            case ValueType.String or ValueType.Char:
                resultBuilder.Append('\'').Append(expression.Value).Append('\'').Append(' ');
                break;
        }

        return resultBuilder.ToString();
    }

    public string Visit(Function function)
    {
        var resultBuilder = new StringBuilder();
        resultBuilder.Append(function.Identifier.Name).Append('(');

        for (int i = 0; i < function.Parameters.Count; i++)
        {
            resultBuilder.Append(function.Parameters[i].Accept(this));
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

    public string Visit(Identifier identifier)
    {
        return $"[{identifier.Name}]";
    }

    public string Visit(LogicalExpressionList list)
    {
        var resultBuilder = new StringBuilder().Append('(');
        for (var i = 0; i < list.Count; i++)
        {
            resultBuilder.Append(list[i].Accept(this).TrimEnd());
            if (i < list.Count - 1)
            {
                resultBuilder.Append(',');
            }
        }
        resultBuilder.Append(')');
        return resultBuilder.ToString();
    }

    protected virtual string EncapsulateNoValue(LogicalExpression expression, bool appendSpace = true)
    {
        if (expression is ValueExpression valueExpression)
        {
            string result = valueExpression.Accept(this);
            if (!appendSpace)
                result = result.TrimEnd();
            return result;
        }

        var resultBuilder = new StringBuilder();

        // Factorials don't need parenthesis around them
        bool parensNeeded = true;

        if (((expression is BinaryExpression binaryExpression) && (binaryExpression.Type == BinaryExpressionType.Factorial)) || (expression is PercentExpression))
            parensNeeded = false;

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
            resultBuilder.Append(' ');

        return resultBuilder.ToString();
    }
}

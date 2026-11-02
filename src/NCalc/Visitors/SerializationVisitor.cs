using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Helpers;

using ValueType = NCalc.Domain.ValueType;

namespace NCalc.Visitors;

/// <summary>
/// Class responsible to converting a <see cref="LogicalExpression"/> into a <see cref="string"/> representation.
/// </summary>
public class SerializationVisitor(SerializationContext context) : ILogicalExpressionVisitor<string>
{
    // Numbers are written with the decimal separator that the parser accepts ("." unless the advanced options define another one),
    // not with the separator or the minus sign of the culture of the current thread
    private readonly NumberFormatInfo _numberFormatInfo = CreateNumberFormat(context.AdvancedOptions);

    private static NumberFormatInfo CreateNumberFormat(AdvancedExpressionOptions? advancedOptions)
    {
        var numberFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        if (advancedOptions is not null)
            numberFormat.NumberDecimalSeparator = advancedOptions.GetDecimalSeparator();
        numberFormat.NumberGroupSeparator = string.Empty;
        return numberFormat;
    }

    // A double is written so that it is read back exactly: in plain notation when that is exact (0.00001), otherwise in the round-trip format (1E-30).
    // The infinities are written as a number that is too large for a double, NaN as the difference of two infinities, as they have no literal.
    private string FormatDouble(double value)
    {
        if (double.IsNaN(value))
            return "(1e400 - 1e400)";
        if (double.IsInfinity(value))
            return value > 0 ? "1e400" : "-1e400";

        string roundTrip = value.ToString("R", _numberFormatInfo);
        if (roundTrip.IndexOf('E') < 0)
            return roundTrip;

        string plain = value.ToString("0.############################", _numberFormatInfo);
        return double.TryParse(plain, NumberStyles.Float, _numberFormatInfo, out double parsed) && parsed.Equals(value) ? plain : roundTrip;
    }

    // Set while an index or a range boundary is written: those are integers, even when they are stored as floating-point values
    private bool _inIndex;

    // A fractional value is written with a decimal separator or an exponent, so that it is not read back as an integer
    private string AsFraction(string text)
        => _inIndex || text.Contains(_numberFormatInfo.NumberDecimalSeparator) || text.IndexOfAny(['E', 'e']) >= 0 || text is "NaN" or "Infinity" or "-Infinity"
            ? text
            : text + _numberFormatInfo.NumberDecimalSeparator + "0";

    /// <summary>
    /// Writes a decimal as a currency literal ("$0.1"), when the expression accepts currency symbols and a plain number would not be read
    /// back as a decimal. Returns null when it is not needed or not possible.
    /// </summary>
    private string? FormatCurrency(decimal value)
    {
        var advancedOptions = context.AdvancedOptions;
        if (advancedOptions?.Flags.HasFlag(AdvExpressionOptions.AcceptCurrencySymbol) != true || context.Options.HasFlag(ExpressionOptions.DecimalAsDefault))
            return null;

        string symbol = GetCurrencySymbol(advancedOptions);
        string separator = advancedOptions.GetCurrencyDecimalSeparator();
        if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(separator))
            return null;

        var currencyFormat = (NumberFormatInfo)_numberFormatInfo.Clone();
        currencyFormat.NumberDecimalSeparator = separator;
        string number = Math.Abs(value).ToString(currencyFormat);
        return (value < 0 ? "-" : string.Empty) + symbol + number;
    }

    /// <summary>
    /// The currency symbol that a currency literal is written with (also kept in JSON with the advanced options of the expression).
    /// </summary>
    internal static string GetCurrencySymbol(AdvancedExpressionOptions advancedOptions)
    {
        advancedOptions.GetCurrencySymbols(out string symbol, out string symbol2, out string symbol3);
        if (string.IsNullOrEmpty(symbol))
            symbol = !string.IsNullOrEmpty(symbol2) ? symbol2 : symbol3;
        return symbol;
    }

    // Dates and times are written in the ISO form, which does not depend on the culture: #2024-01-02# or #2024-01-02T10:20:30.5#
    /// <summary>
    /// Escapes a string that was not read from the text of an expression (so it has no original form), so that it can be read back
    /// as a single-quoted string.
    /// </summary>
    private static string EscapeString(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value!.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '\'': builder.Append("\\'"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default: builder.Append(c); break;
            }
        }
        return builder.ToString();
    }

    private static string FormatDateTime(DateTime value)
        => (value.TimeOfDay == TimeSpan.Zero
            ? value.ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture)
            : value.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFF", CultureInfo.InvariantCulture).TrimEnd('.'))
            + (value.Kind == DateTimeKind.Utc ? "Z" : string.Empty);

    // A time span is written as [-][d.]hh:mm:ss[.fffffff], without trailing zeros in the fraction of a second: #1.02:03:04.5#
    private static string FormatTimeSpan(TimeSpan value)
    {
        string text = value.ToString("c", CultureInfo.InvariantCulture);
        int fraction = text.IndexOf('.', text.LastIndexOf(':'));
        return fraction < 0 ? text : text.TrimEnd('0').TrimEnd('.');
    }

    public string Visit(TernaryExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();

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
                resultBuilder.Append(EncapsulateNoValue(expression.MiddleExpression, false));
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
                    resultBuilder.Append(EncapsulateNoValue(expression.RightExpression, false));
                    resultBuilder.Append(" }");
                }
            }

            return resultBuilder.ToString();
        }
        else
        {
            // The parts of a ternary operator are parsed as binary expressions, so a nested ternary operator needs parentheses
            return EncapsulateNoValue(expression.LeftExpression, parensNeeded: GetPrecedence(expression.LeftExpression) <= TernaryPrecedence) + "? "
                + EncapsulateNoValue(expression.MiddleExpression, parensNeeded: GetPrecedence(expression.MiddleExpression) <= TernaryPrecedence) + ": "
                + EncapsulateNoValue(expression.RightExpression, parensNeeded: GetPrecedence(expression.RightExpression) <= TernaryPrecedence);
        }
    }

    // How tightly an expression binds, following the grammar of LogicalExpressionParser: an operand is parenthesized when it binds
    // more loosely than its position allows, so that the text is parsed back into the same tree.
    private const int LowestPrecedence = 0;
    private const int TernaryPrecedence = 5;
    private const int UnaryPrecedence = 70;
    private const int ExponentiationPrecedence = 80;
    private const int PostfixPrecedence = 90;
    private const int PrimaryPrecedence = 100;

    private static int? GetBinaryOperatorPrecedence(BinaryExpressionType type) => type switch
    {
        BinaryExpressionType.Exponentiation => ExponentiationPrecedence,
        BinaryExpressionType.Times or BinaryExpressionType.Div or BinaryExpressionType.Modulo
            or BinaryExpressionType.IntDivB or BinaryExpressionType.IntDivP => 60,
        BinaryExpressionType.Plus or BinaryExpressionType.Minus => 55,
        BinaryExpressionType.LeftShift or BinaryExpressionType.RightShift => 50,
        BinaryExpressionType.Greater or BinaryExpressionType.GreaterOrEqual or BinaryExpressionType.Less or BinaryExpressionType.LessOrEqual
            or BinaryExpressionType.In or BinaryExpressionType.NotIn or BinaryExpressionType.Like or BinaryExpressionType.NotLike => 45,
        BinaryExpressionType.Equal or BinaryExpressionType.NotEqual => 40,
        BinaryExpressionType.BitwiseAnd => 35,
        BinaryExpressionType.BitwiseXOr => 30,
        BinaryExpressionType.BitwiseOr => 25,
        BinaryExpressionType.And => 20,
        BinaryExpressionType.XOr => 15,
        BinaryExpressionType.Or => 10,
        _ => null
    };

    private int GetPrecedence(LogicalExpression expression) => expression switch
    {
        // A negative number is written with a leading minus, which is parsed as a unary operator where one is allowed
        ValueExpression value => IsNegativeNumber(value) ? UnaryPrecedence : PrimaryPrecedence,
        BinaryExpression binary => GetBinaryOperatorPrecedence(binary.Type)
            ?? (binary.Type is BinaryExpressionType.Factorial or BinaryExpressionType.IndexAccess or BinaryExpressionType.RangeIndex ? PostfixPrecedence : LowestPrecedence),
        UnaryExpression unary => unary.Type switch
        {
            UnaryExpressionType.FromEnd => PrimaryPrecedence,
            UnaryExpressionType.Return => LowestPrecedence,
            _ => UnaryPrecedence
        },
        PercentExpression or ImaginaryNumberExpression => PostfixPrecedence,
        IfStatementExpression => LowestPrecedence,
        TernaryExpression => TernaryPrecedence,
        StatementSequence or FunctionExpression => LowestPrecedence,
        _ => PrimaryPrecedence
    };

    private bool IsNegativeNumber(ValueExpression value)
        => value.Type is ValueType.Integer or ValueType.Float && value.Accept(this).StartsWith("-", StringComparison.Ordinal);

    public string Visit(BinaryExpression expression, CancellationToken cancellationToken = default)
        => VisitBinary(expression, cancellationToken);

    // The boundaries of a range and the position of a from-end index are positions: a literal that is itself the boundary is written
    // as an integer ("s[1..3]"). A literal inside a computed boundary keeps its fraction ("s[^((2147483647.0 + 1) / 268435456)]"),
    // as it would be read back as an integer and change the result
    private string EncapsulateBoundary(LogicalExpression boundary, bool appendSpace = true, bool parensNeeded = false)
    {
        bool wasInIndex = _inIndex;
        _inIndex = boundary is ValueExpression;
        try
        {
            return EncapsulateNoValue(boundary, appendSpace, parensNeeded);
        }
        finally
        {
            _inIndex = wasInIndex;
        }
    }

    private string VisitBinary(BinaryExpression expression, CancellationToken cancellationToken)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        cancellationToken.ThrowIfCancellationRequested();

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
                parensNeeded = GetPrecedence(expression.LeftExpression) < PrimaryPrecedence;
                resultBuilder.Append(EncapsulateNoValue(expression.LeftExpression, false, parensNeeded));

                var step = Convert.ToInt32(valueExpression.Value, CultureInfo.InvariantCulture);
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
            else
            if (expression.Type is BinaryExpressionType.IndexAccess)
                // Only a name, a function call, a string or a list can be indexed directly: "s[1..4][0]" and "3![0]" are not parsed,
                // "(s[1..4])[0]" is
                parensNeeded = expression.LeftExpression is not (Identifier or FunctionCall or LogicalExpressionList or ValueExpression { Type: ValueType.String });
            else
            if (GetBinaryOperatorPrecedence(expression.Type) is int precedence)
            {
                // Binary operators are left-associative, except exponentiation, whose left operand must bind more tightly than it
                parensNeeded = expression.Type == BinaryExpressionType.Exponentiation
                    ? GetPrecedence(expression.LeftExpression) <= precedence
                    : GetPrecedence(expression.LeftExpression) < precedence;
            }

            appendSpace = expression.Type != BinaryExpressionType.IndexAccess && expression.Type != BinaryExpressionType.RangeIndex;
            resultBuilder.Append(expression.Type == BinaryExpressionType.RangeIndex
                ? EncapsulateBoundary(expression.LeftExpression, appendSpace, parensNeeded)
                : EncapsulateNoValue(expression.LeftExpression, appendSpace, parensNeeded));

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
        else
        if (GetBinaryOperatorPrecedence(expression.Type) is int operatorPrecedence)
        {
            parensNeeded = expression.Type == BinaryExpressionType.Exponentiation
                ? GetPrecedence(expression.RightExpression) < operatorPrecedence
                : GetPrecedence(expression.RightExpression) <= operatorPrecedence;
        }
        else
            parensNeeded = false;

        appendSpace = true;
        if (expression.Type == BinaryExpressionType.IndexAccess)
            appendSpace = false;

        resultBuilder.Append(expression.Type == BinaryExpressionType.RangeIndex
            ? EncapsulateBoundary(expression.RightExpression, appendSpace, parensNeeded)
            : EncapsulateNoValue(expression.RightExpression, appendSpace, parensNeeded));
        if (expression.Type == BinaryExpressionType.IndexAccess)
            resultBuilder.Append(']');

        return resultBuilder.ToString();
    }

    public string Visit(UnaryExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();

        string result = expression.Type switch
        {
            // "!" is not an operator when the operator characters are skipped
            UnaryExpressionType.Not => context.Options.HasFlag(ExpressionOptions.SkipLogicalAndBitwiseOpChars) ? "not " : "!",
            UnaryExpressionType.Negate => "-",
            UnaryExpressionType.FromEnd => "^",
            UnaryExpressionType.BitwiseNot => context.Options.HasFlag(ExpressionOptions.SkipLogicalAndBitwiseOpChars) ? "bit_not " : "~",
            UnaryExpressionType.SqRoot => "\u221a",
#if NET8_0_OR_GREATER
            UnaryExpressionType.CbRoot => "\u221b",
#endif
            UnaryExpressionType.FourthRoot => "\u221c",
            UnaryExpressionType.Return => "return ",
            _ => string.Empty
        };

        bool parensNeeded = !(expression.Expression is Identifier || (expression.Expression is ValueExpression value && !IsNegativeNumber(value)));
        result += expression.Type == UnaryExpressionType.FromEnd
            ? EncapsulateBoundary(expression.Expression, parensNeeded: parensNeeded)
            : EncapsulateNoValue(expression.Expression, parensNeeded: parensNeeded);
        return result;
    }

    public string Visit(PercentExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();

        bool parensNeeded = GetPrecedence(expression.Expression) < PrimaryPrecedence;
        return EncapsulateNoValue(expression.Expression, false, parensNeeded) + "%";
    }

    public string Visit(ImaginaryNumberExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();

        // Only a number is written directly before the "i": "x i" written as "xi" would be read as a name, so it is written as "(x)i"
        bool parensNeeded = expression.Expression is not ValueExpression || GetPrecedence(expression.Expression) < PrimaryPrecedence;
        return EncapsulateNoValue(expression.Expression, false, parensNeeded) + "i";
    }

    public string Visit(VectorExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();

        return "[" + JoinListItems(expression.Expressions, cancellationToken) + "]";
    }

    public string Visit(ValueExpression expression, CancellationToken cancellationToken = default)
    {
        // A relative date or time literal (#today#, #2 days ago#) is written as it was written, so that it stays relative
        if (expression.Relative is { } relative)
            return $"#{relative.Text}# ";

        var value = expression.Value;

        return expression.Type switch
        {
            // A ulong above long.MaxValue is written in hexadecimal, which is read back as a ulong (decimal digits would give a double).
            // When hexadecimal literals are unsigned, every uint and ulong is written in hexadecimal, so that "0xFF - 0x100" is not read
            // back as the signed "255 - 256"
            ValueType.Integer when value is ulong ulongValue && (ulongValue > long.MaxValue || context.Options.HasFlag(ExpressionOptions.HexBinOctAreUnsigned))
                => $"0x{ulongValue.ToString("X", CultureInfo.InvariantCulture)} ",
            ValueType.Integer when value is uint uintValue && context.Options.HasFlag(ExpressionOptions.HexBinOctAreUnsigned)
                => $"0x{uintValue.ToString("X", CultureInfo.InvariantCulture)} ",
            // A decimal is read back as a decimal only with a currency symbol, unless decimals are the default ("$0.1 + $0.2 = $0.3")
            ValueType.Float when value is decimal currencyValue && FormatCurrency(currencyValue) is string currencyText => currencyText + " ",
            ValueType.Boolean or ValueType.Integer => $"{Convert.ToString(value, CultureInfo.InvariantCulture)} ",
            ValueType.DateTime => value is DateTime dateTime ? $"#{FormatDateTime(dateTime)}# " : $"#{value}# ",
            ValueType.TimeSpan => value is TimeSpan timeSpan ? $"#{FormatTimeSpan(timeSpan)}# " : $"#{Convert.ToString(value, CultureInfo.InvariantCulture)}# ",
            ValueType.Float =>
                value is null
                    ? string.Empty
                    : AsFraction((value is decimal dcValue)
                        ? dcValue.ToString(_numberFormatInfo)
                        : value is BigDecimal bdValue
                            ? MathHelper.FormatBigDecimal(bdValue, _numberFormatInfo)
                            : FormatDouble(MathHelper.ConvertToDouble(value, context))) + " ",
            ValueType.Char => $"'{EscapeString(Convert.ToString(value, CultureInfo.InvariantCulture))}' ",
            // A GUID is a literal unless GUIDs are not parsed, in which case it can only be a string
            ValueType.Guid => context.Options.HasFlag(ExpressionOptions.DontParseGuids) ? $"'{value}' " : $"{value} ",
            ValueType.String =>
                (value is Parlot.TextSpan)
                ? $"{value} "
                : expression.StringKind switch
                {
                    StringKind.SingleQuote => $"'{expression.OriginalString}' ",
                    StringKind.DoubleQuote => $"\"{expression.OriginalString}\" ",
                    StringKind.RawDoubleQuote => $"@\"{value}\" ",
                    StringKind.BackQuote => $"`{value}` ",
                    _ => string.IsNullOrEmpty(expression.OriginalString) ? $"'{EscapeString(Convert.ToString(value, CultureInfo.InvariantCulture))}' " : $"'{expression.OriginalString}' ",
                },
            _ => string.Empty,
        };
    }

    public string Visit(FunctionCall function, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();

        if (context.AdvancedOptions?.Flags.HasFlag(AdvExpressionOptions.UseResultReference) == true && function.Identifier.Name == "@")
        {
            return "@";
        }

        // Not every argument ends with a space (identifiers do not), so each one is trimmed instead of removing its last character
        return function.Identifier.Name + "(" + JoinListItems(function.Parameters, cancellationToken) + ") ";
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
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();

        return "(" + JoinListItems(list, cancellationToken) + ")";
    }

    public string Visit(ExpressionGroup group, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        return string.Join(group.Expression.Accept(this, cancellationToken).Trim(), "{ ", " }");
    }

    public string Visit(FunctionExpression expression, CancellationToken cancellationToken = default)
    {
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();
        StringBuilder resultBuilder = new();
        Function function = expression.Function;
        // The description is written as "///" lines, the only form from which the parser takes it. Each line is written as it is,
        // after "///", so that the description is read back unchanged (the parser trims only the whole description)
        if (!string.IsNullOrEmpty(function.Description))
        {
            bool firstLine = true;
            foreach (var line in function.Description!.Split('\n'))
            {
                resultBuilder.Append(firstLine ? "/// " : "///");
                resultBuilder.Append(line);
                resultBuilder.Append('\n');
                firstLine = false;
            }
        }
        resultBuilder.Append("fn ");
        resultBuilder.Append(function.Name);
        resultBuilder.Append('(');
        // The parameters are separated with a comma or a semicolon, whatever the list separator is, as the parser accepts only these
        string fnSep = GetListItemSeparator(context.AdvancedOptions) == ", " ? ", " : "; ";
        bool paramAdded = false;
        foreach (var param in function.Parameters)
        {
            if (paramAdded)
                resultBuilder.Append(fnSep);
            else
                paramAdded = true;

            resultBuilder.Append(param.Name);
            if (param.IsOptional)
            {
                resultBuilder.Append(" = ");
                resultBuilder.Append(FormatDefaultValue(param.DefaultValue));
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
        EvaluationHelper.EnsureSufficientExecutionStackToVisit();

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
            if (parensNeeded)
                return "(" + result.TrimEnd() + (appendSpace ? ") " : ")");
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

    // A default value of a parameter is written as a literal (a string in quotes, a number with the separator of the parser)
    private string FormatDefaultValue(object? value)
    {
        if (value is null or ValueExpression { Type: ValueType.NoValue })
            return "null";
        // The parser stores the literal itself
        if (value is LogicalExpression literal)
            return literal.Accept(this).TrimEnd();
        try
        {
            return new ValueExpression(value).Accept(this).TrimEnd();
        }
        catch (NCalc.Exceptions.NCalcException)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        }
    }

    // Items are separated with a semicolon wherever the parser accepts one, as a comma may also be the decimal separator ("Max(1,5; 2)")
    private string GetListItemSeparator(AdvancedExpressionOptions? options)
    {
        if (options is null)
            return "; ";

        string separator = options.ListItemSeparator switch
        {
            AdvancedExpressionOptions.ListItemSeparatorKind.Comma or AdvancedExpressionOptions.ListItemSeparatorKind.CommaOrSpace => ",",
            AdvancedExpressionOptions.ListItemSeparatorKind.Colon => ":",
            AdvancedExpressionOptions.ListItemSeparatorKind.Space => string.Empty,
            _ => ";",
        };

        // A separator that is also a decimal or group separator gets a space before it, so that it is not read as a part of a number
        // ("Max(1 , 2)" instead of "Max(1, 2)", where "1," would be read as 1.0)
        if (separator.Length > 0 && (separator == options.GetDecimalSeparator() || separator == options.GetSecondaryDecimalSeparator()
            || separator == options.GetNumberGroupSeparator()))
            separator = " " + separator;

        return separator + " ";
    }

    // Tells whether the text of a list item starts with an operator rather than with an operand: a sign, "!" (which would be read
    // as a factorial of the item before it), "~", "^", a root sign, or the words "not", "bit_not" and "return"
    private static bool StartsWithOperator(string text)
    {
        if (text.Length == 0)
            return false;
        if ("-+!~^¬√∛∜".IndexOf(text[0]) >= 0)
            return true;
        return StartsWithWord(text, "not") || StartsWithWord(text, "bit_not") || StartsWithWord(text, "return");

        static bool StartsWithWord(string text, string word)
            => text.StartsWith(word, StringComparison.OrdinalIgnoreCase)
                && (text.Length == word.Length || !(char.IsLetterOrDigit(text[word.Length]) || text[word.Length] == '_'));
    }

    // Writes the items of a list or of the arguments of a function. When the items are separated by spaces only, an item that starts
    // with an operator is parenthesized ("1 (-2)", not "1 -2", which is a subtraction, and "3 (!False)", not "3 !False", which is
    // a factorial), and so is an item followed by a parenthesis or a bracket ("(a) (-2)", not "a (-2)", which is a function call,
    // or "a [b]", which is an index).
    private string JoinListItems(IList<LogicalExpression> items, CancellationToken cancellationToken)
    {
        string separator = GetListItemSeparator(context.AdvancedOptions);
        var texts = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            texts[i] = items[i].Accept(this, cancellationToken).Trim();
        }

        if (context.AdvancedOptions?.ListItemSeparator == AdvancedExpressionOptions.ListItemSeparatorKind.Space)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                if (StartsWithOperator(texts[i]))
                    texts[i] = "(" + texts[i] + ")";
            }
            // From the end, as a wrapped item makes the one before it need parentheses too
            for (int i = texts.Length - 2; i >= 0; i--)
            {
                if (texts[i + 1].StartsWith("(", StringComparison.Ordinal) || texts[i + 1].StartsWith("[", StringComparison.Ordinal))
                    texts[i] = "(" + texts[i] + ")";
            }
        }

        return string.Join(separator, texts);
    }
}

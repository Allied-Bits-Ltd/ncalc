using System.Buffers;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

using ExtendedNumerics;

using NCalc.Domain;
using NCalc.Exceptions;

using Parlot;
using Parlot.Fluent;
using Parlot.Rewriting;

using static Parlot.Fluent.Parsers;

using Identifier = NCalc.Domain.Identifier;

namespace NCalc.Parser;

/// <summary>
/// Class responsible for parsing strings into <see cref="LogicalExpression"/> objects.
/// </summary>
public static class LogicalExpressionParser
{
    private const double MinDecDouble = (double)decimal.MinValue;
    private const double MaxDecDouble = (double)decimal.MaxValue;

    private const string InvalidTokenMessage = "Invalid token in expression";

    // Support of underscores in decimal literals requires a patch in Parlot,
    // currently available in https://github.com/Allied-Bits-Ltd/parlot
    // and offered to the main project as a pull request https://github.com/sebastienros/parlot/pull/221

    const string errFailedToParsePeriodIndicator = "Failed to parse the element '{0}' of a period definition.";
    const string errDuplicatePeriodIndicator = "Period indicator '{0}' has been already used in the period definition";
    const string errUnrecognizedPeriodIndicator = "Unrecognized period indicator '{0}' in the period definition.";
    const string errUnrecognizedTimeRelationIndicator = "Unrecognized time relation indicator '{0}' in the date/time definition.";
    const string errDuplicateTimeRelationIndicators = "A date/time may contain only one time relation indicator, but two ('{0}' and '{1}') were specified.";

    /// <summary>
    /// Creates the format with which the parts of a date literal, already arranged by the masks of the expression culture, are converted
    /// to a date. It uses the Gregorian calendar and the separators "/" and ":" (which the masks contain as placeholders), and never the
    /// culture of the current thread: under a Thai or Saudi thread culture, #1/2/2024# would otherwise be read in the Buddhist or the
    /// Um Al-Qura calendar. Only the AM and PM designators are taken from the format of the expression.
    /// </summary>
    private static DateTimeFormatInfo CreateLiteralDateTimeFormat(DateTimeFormatInfo expressionFormat)
    {
        var format = (DateTimeFormatInfo)CultureInfo.InvariantCulture.DateTimeFormat.Clone();
        format.AMDesignator = expressionFormat.AMDesignator;
        format.PMDesignator = expressionFormat.PMDesignator;
        return format;
    }

    /// <summary>
    /// Creates the format with which the date and time literals are read under advanced options (with their separators): the patterns,
    /// separators and designators of the advanced options' format (the date patterns and separator from <paramref name="dateSource"/>),
    /// in the Gregorian calendar. The format of a Thai, Persian or Saudi culture would otherwise read #1/2/2024# in its own calendar.
    /// </summary>
    private static DateTimeFormatInfo CreateAdvancedLiteralDateTimeFormat(DateTimeFormatInfo format, DateTimeFormatInfo dateSource)
    {
        var result = (DateTimeFormatInfo)CultureInfo.InvariantCulture.DateTimeFormat.Clone();
        result.DateSeparator = dateSource.DateSeparator;
        result.ShortDatePattern = dateSource.ShortDatePattern;
        result.LongDatePattern = dateSource.LongDatePattern;
        result.MonthDayPattern = dateSource.MonthDayPattern;
        result.YearMonthPattern = dateSource.YearMonthPattern;
        result.TimeSeparator = format.TimeSeparator;
        result.ShortTimePattern = format.ShortTimePattern;
        result.LongTimePattern = format.LongTimePattern;
        result.FullDateTimePattern = dateSource.LongDatePattern + " " + format.LongTimePattern;
        result.AMDesignator = format.AMDesignator;
        result.PMDesignator = format.PMDesignator;
        return result;
    }

    /// <summary>
    /// Returns the masks of a date of three parts separated by "/" (with a four- or a two-digit year), in the order of the date pattern.
    /// </summary>
    private static string[] CreateDateMasks(string datePattern)
    {
        return (datePattern.Length > 0 ? datePattern[0] : 'd') switch
        {
            'M' => ["M/d/yyyy", "M/d/yy"],
            'y' => ["yyyy/M/d", "yy/M/d"],
            _ => ["d/M/yyyy", "d/M/yy"],
        };
    }

    /// <summary>
    /// Creates the error for a date or time literal that has the shape of one but is not valid (#2024-13-01#). It stays a
    /// FormatException, as before, and tells where the literal is.
    /// </summary>
    private static FormatException InvalidLiteralFormat(string kind, ParseContext ctx)
        => new($"Invalid {kind} format at position {ctx.Scanner.Cursor.Position}.");

    /// <summary>
    /// Returns the part of a cache key that stands for the culture of the current thread. Only advanced options refer to it
    /// (separators, currency symbols or the 12-hour format "from the culture" without a culture of their own); without them,
    /// parsing does not depend on the thread culture.
    /// </summary>
    internal static string GetCurrentCultureCacheKey(AdvancedExpressionOptions? extOptions)
        => extOptions is null ? string.Empty : GetCultureCacheKey(CultureInfo.CurrentCulture);

    private const string ExpressionTooDeepMessage = "The expression is nested too deeply to be parsed";

    /// <summary>
    /// Wraps a parser that the grammar enters recursively (nested sub-expressions, unary operators)
    /// and throws an <see cref="NCalcParserException"/> when the remaining stack space is too small,
    /// instead of letting the process terminate with an uncatchable StackOverflowException.
    /// </summary>
    private sealed class StackGuardParser<T>(Parser<T> parser) : Parser<T>, ISeekable
    {
        public bool CanSeek => parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => parser is ISeekable seekable ? seekable.ExpectedChars : [];

        public bool SkipWhitespace => parser is ISeekable seekable && seekable.SkipWhitespace;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
#if NET
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
                throw new NCalcParserException($"{ExpressionTooDeepMessage} at position {context.Scanner.Cursor.Position}", context.Scanner.Cursor.Position) { IsNestedTooDeeply = true };
#else
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }
            catch (InsufficientExecutionStackException ex)
            {
                throw new NCalcParserException($"{ExpressionTooDeepMessage} at position {context.Scanner.Cursor.Position}", ex, context.Scanner.Cursor.Position) { IsNestedTooDeeply = true };
            }
#endif
            return parser.Parse(context, ref result);
        }

        public override string ToString() => parser.ToString() ?? nameof(StackGuardParser<T>);
    }

    private static Parser<T> StackGuarded<T>(Parser<T> parser) => new StackGuardParser<T>(parser);

    /// <summary>
    /// Tells whether the text of a number literal (without the leading whitespace) and its value are accepted.
    /// </summary>
    private delegate bool NumberTextCheck<in T>(ReadOnlySpan<char> text, T value);

    /// <summary>
    /// Wraps a number parser and accepts its result only when the text that it read passes a check; otherwise the parser fails
    /// without consuming input, so that the next alternative (another number type, or an identifier) is tried.
    /// </summary>
    private sealed class CheckedNumberParser<T>(Parser<T> parser, NumberTextCheck<T> check) : Parser<T>
    {
        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            var startPosition = context.Scanner.Cursor.Position;
            context.SkipWhiteSpace();
            int start = context.Scanner.Cursor.Offset;

            if (parser.Parse(context, ref result)
                && check(context.Scanner.Buffer.AsSpan(start, context.Scanner.Cursor.Offset - start), result.Value))
            {
                return true;
            }

            context.Scanner.Cursor.ResetPosition(startPosition);
            return false;
        }

        public override string ToString() => parser.ToString() ?? nameof(CheckedNumberParser<T>);
    }

    private static Parser<T> CheckedNumber<T>(Parser<T> parser, NumberTextCheck<T>? check)
        => check is null ? parser : new CheckedNumberParser<T>(parser, check);

    /// <summary>
    /// With <see cref="AdvExpressionOptions.AcceptUnderscoresInNumbers"/>, an underscore separates digits, as in C#: it may only stand
    /// between two digits ("1_000", "1__000"), so "_1" is an identifier and "1_", "1_.5" or "1e_5" are not numbers.
    /// </summary>
    private static bool HasUnderscoresOnlyBetweenDigits(ReadOnlySpan<char> text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '_')
                continue;

            int end = i;
            while (end < text.Length && text[end] == '_')
                end++;

            if (i == 0 || !IsAsciiDigit(text[i - 1]) || end == text.Length || !IsAsciiDigit(text[end]))
                return false;

            i = end;
        }

        return true;
    }

    private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

    /// <summary>
    /// The number of significant digits that a double keeps; a decimal literal that decimal can keep only with fewer digits is read as a double.
    /// </summary>
    private const int DoubleSignificantDigits = 16;

    /// <summary>
    /// With DecimalAsDefault, tells whether a decimal keeps the digits of a literal: false when the literal has significant digits
    /// below the resolution of decimal (1E-28) and decimal would keep fewer of them than a double does. Such a literal (1e-30,
    /// 1.234e-27) is read as a double, as a literal beyond the range of decimal is, instead of becoming 0 or losing its digits.
    /// A literal with more digits than decimal holds, but not below its resolution, stays a decimal (decimal keeps more of them).
    /// </summary>
    private static bool DecimalKeepsDigits(ReadOnlySpan<char> text, char decimalSeparator, char decimalSeparator2)
    {
        int digitCount = 0;
        int integerDigits = -1;
        int firstNonZero = -1;
        int lastNonZero = -1;
        int leadingDigits = 0; // the first two significant digits
        int i = 0;
        for (; i < text.Length; i++)
        {
            char c = text[i];
            if (IsAsciiDigit(c))
            {
                if (c != '0')
                {
                    if (firstNonZero < 0)
                        firstNonZero = digitCount;
                    lastNonZero = digitCount;
                }
                if (firstNonZero >= 0 && digitCount - firstNonZero < 2)
                    leadingDigits = leadingDigits * 10 + (c - '0');
                digitCount++;
            }
            else if (c == decimalSeparator || (decimalSeparator2 != '\0' && c == decimalSeparator2))
            {
                integerDigits = digitCount;
            }
            else if (c == 'e' || c == 'E')
            {
                break;
            }
            // group separators and underscores are skipped
        }

        if (firstNonZero < 0)
            return true; // zero

        if (integerDigits < 0)
            integerDigits = digitCount;
        if (digitCount - firstNonZero < 2)
            leadingDigits *= 10; // "3" is 30 of "30"

        // The exponent; it is saturated, a literal with a huge exponent is far out of the range of decimal anyway
        long exponent = 0;
        bool negativeExponent = false;
        for (i++; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '-')
                negativeExponent = true;
            else if (IsAsciiDigit(c) && exponent < 1_000_000)
                exponent = exponent * 10 + (c - '0');
        }
        if (negativeExponent)
            exponent = -exponent;

        // The power of ten of the first and the last significant digit
        long mostSignificant = integerDigits - 1 - firstNonZero + exponent;
        long leastSignificant = integerDigits - 1 - lastNonZero + exponent;
        if (leastSignificant >= -28)
            return true;

        // Below the range of double too (it reads 2E-324 as 0), the literal stays a decimal 0, as before: double keeps none of its
        // digits either. The double closest to a value of 2.5E-324 or more is its smallest value 4.9E-324.
        if (mostSignificant < -324 || (mostSignificant == -324 && leadingDigits < 25))
            return true;

        // Decimal keeps the digits from the first significant one down to 1E-28
        return mostSignificant + 29 >= DoubleSignificantDigits;
    }

    /// <summary>
    /// Parses a chain of left-associative binary operators of several precedence levels in one loop with explicit stacks,
    /// instead of a nested parser per level, so that each nesting level of an expression (a group, a function argument)
    /// takes far fewer stack frames.
    /// The levels are listed from the tightest to the loosest and are tried in this order at every position.
    /// A level whose operator matches but is not followed by an operand gives way to the next level, as the nested parsers did.
    /// </summary>
    /// <summary>
    /// Tells whether an expression can be the target of an assignment: a name, or an index access on a name ("x", "l[1]").
    /// </summary>
    private static bool IsAssignmentTarget(LogicalExpression target)
        => target is Identifier
            || (target is BinaryExpression { Type: BinaryExpressionType.IndexAccess, LeftExpression: Identifier });

    /// <summary>
    /// Tells whether a run of 32 hex characters is a number with an exponent rather than a GUID without hyphens.
    /// </summary>
    private static bool IsNumberWithExponent(ReadOnlySpan<char> text)
    {
        // Decimal digits, then one 'e' or 'E', then optional digits (the sign of the exponent, if any, follows the run)
        int exponent = text.IndexOfAny('e', 'E');
        if (exponent <= 0)
            return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (i != exponent && !char.IsDigit(text[i]))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Creates a time span from the parts of a time literal. TimeSpan.TryParse would read "25:00:00" as 25 days, so the parts are
    /// checked explicitly: minutes and seconds must be below 60, and hours below 24 when the literal also has days ("1.23:00:00");
    /// without days, any number of hours is allowed ("25:00:00" is 25 hours).
    /// </summary>
    private static bool TryCreateTimeSpan(string hours, string minutes, string? seconds, bool hasDays, out TimeSpan result)
    {
        result = default;
        int secondValue = 0;
        if (!int.TryParse(hours, NumberStyles.None, CultureInfo.InvariantCulture, out int hourValue)
            || !int.TryParse(minutes, NumberStyles.None, CultureInfo.InvariantCulture, out int minuteValue)
            || (seconds is not null && !int.TryParse(seconds, NumberStyles.None, CultureInfo.InvariantCulture, out secondValue)))
            return false;

        if (minuteValue > 59 || secondValue > 59 || (hasDays && hourValue > 23))
            return false;

        result = new TimeSpan(hourValue, minuteValue, secondValue);
        return true;
    }

    /// <summary>
    /// Converts the digits after the seconds of a time span literal (#00:00:01.5#) to a time span; digits beyond the 7th (a tick) are ignored.
    /// </summary>
    private static TimeSpan GetSecondFraction(TextSpan digits)
    {
        if (digits.Length == 0)
            return TimeSpan.Zero;

        string text = digits.ToString();
        text = text.Length > 7 ? text.Substring(0, 7) : text.PadRight(7, '0');
        if (!long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks))
            throw new FormatException("Invalid TimeSpan format.");
        return TimeSpan.FromTicks(ticks);
    }

    private sealed class BinaryOperatorParser(Parser<LogicalExpression> operand, Parser<BinaryExpressionType>[] levels) : Parser<LogicalExpression>
    {
        public override bool Parse(ParseContext context, ref ParseResult<LogicalExpression> result)
        {
            context.EnterParser(this);

            if (!operand.Parse(context, ref result))
            {
                context.ExitParser(this);
                return false;
            }

            var start = result.Start;
            var end = result.End;

            // Each operand keeps the location of its end, which a node built from it as the right operand takes,
            // as the factory of a left-associative parser is called right after the right operand is parsed
            var operands = new List<(LogicalExpression Value, ExpressionLocation End)> { (result.Value, new ParlotExpressionLocation(context)) };
            var operators = new List<(int Level, BinaryExpressionType Type)>();

            while (true)
            {
                var operatorPosition = context.Scanner.Cursor.Position;
                var matched = false;

                for (var level = 0; level < levels.Length; level++)
                {
                    var operatorResult = new ParseResult<BinaryExpressionType>();
                    if (!levels[level].Parse(context, ref operatorResult))
                        continue;

                    var rightResult = new ParseResult<LogicalExpression>();
                    if (!operand.Parse(context, ref rightResult))
                    {
                        context.Scanner.Cursor.ResetPosition(operatorPosition);
                        continue;
                    }

                    // Neither the operator nor the operand read anything, so they would match forever
                    if (context.Scanner.Cursor.Offset == operatorPosition.Offset)
                        break;

                    // All operators are left-associative, so the pending ones that bind at least as tightly are applied first
                    while (operators.Count > 0 && operators[operators.Count - 1].Level <= level)
                        Reduce(operands, operators);

                    operators.Add((level, operatorResult.Value));
                    operands.Add((rightResult.Value, new ParlotExpressionLocation(context)));
                    end = rightResult.End;
                    matched = true;
                    break;
                }

                if (!matched)
                    break;
            }

            while (operators.Count > 0)
                Reduce(operands, operators);

            result = new ParseResult<LogicalExpression>(start, end, operands[0].Value);

            context.ExitParser(this);
            return true;
        }

        private static void Reduce(List<(LogicalExpression Value, ExpressionLocation End)> operands, List<(int Level, BinaryExpressionType Type)> operators)
        {
            var right = operands[operands.Count - 1];
            var left = operands[operands.Count - 2];
            operands.RemoveAt(operands.Count - 1);
            operands[operands.Count - 1] = (new BinaryExpression(operators[operators.Count - 1].Type, left.Value, right.Value).SetLocation(right.End), right.End);
            operators.RemoveAt(operators.Count - 1);
        }
    }

    private static (Parser<string>, Func<ParseContext, LogicalExpression, LogicalExpression>)[] StackGuarded(List<(Parser<string>, Func<ParseContext, LogicalExpression, LogicalExpression>)> ops)
        => ops.Select(op => (StackGuarded(op.Item1), op.Item2)).ToArray();

    private sealed record MemoEntry<T>(bool Success, int Start, int End, T Value, TextPosition EndPosition);

    /// <summary>
    /// Remembers, per parse and per input position, the outcome of the wrapped parser.
    /// An ordered choice whose alternatives start with the same parser (e.g. an assignment target and an ordinary operand)
    /// would otherwise parse the same text once per alternative at every nesting level, which takes exponential time.
    /// </summary>
    private sealed class MemoizedParser<T>(Parser<T> parser) : Parser<T>, ISeekable
    {
        public bool CanSeek => parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => parser is ISeekable seekable ? seekable.ExpectedChars : [];

        public bool SkipWhitespace => parser is ISeekable seekable && seekable.SkipWhitespace;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            if (context is not LogicalExpressionParserContext parserContext)
                return parser.Parse(context, ref result);

            var key = (this, context.Scanner.Cursor.Position.Offset);
            if (parserContext.ParseMemo.TryGetValue(key, out var cached))
            {
                var entry = (MemoEntry<T>)cached;
                if (entry.Success)
                {
                    result.Set(entry.Start, entry.End, entry.Value);
                    context.Scanner.Cursor.ResetPosition(entry.EndPosition);
                }
                return entry.Success;
            }

            var success = parser.Parse(context, ref result);
            parserContext.ParseMemo[key] = new MemoEntry<T>(success, result.Start, result.End, result.Value, context.Scanner.Cursor.Position);
            return success;
        }

        public override string ToString() => parser.ToString() ?? nameof(MemoizedParser<T>);
    }

    private static Parser<T> Memoized<T>(Parser<T> parser) => new MemoizedParser<T>(parser);

    /// <summary>
    /// Gives the context of each parse the date/time literal parser of the grammar, with which the whitespace parser tells
    /// a date/time literal (#2024-01-02#) from a Python-style comment that contains a second '#'.
    /// </summary>
    private sealed class DateTimeLiteralAwareParser(Parser<LogicalExpression> parser, Parser<LogicalExpression> dateTimeLiteral) : Parser<LogicalExpression>
    {
        public override bool Parse(ParseContext context, ref ParseResult<LogicalExpression> result)
        {
            if (context is LogicalExpressionParserContext parserContext)
                parserContext.DateTimeLiteralParser = dateTimeLiteral;
            return parser.Parse(context, ref result);
        }

        public override string ToString() => parser.ToString() ?? nameof(DateTimeLiteralAwareParser);
    }

    /// <summary>
    /// Creates the parser with the options that exist at the moment of call
    /// </summary>
    /// <returns>An instance of the newly created parser</returns>
    private static Parser<LogicalExpression> CreateExpressionParser()
    {
        return CreateExpressionParser(CultureInfo.CurrentCulture, ExpressionOptions.None, null /*AdvancedExpressionOptions.DefaultOptions*/);
    }

    public static Parser<LogicalExpression> CreateExpressionParser(CultureInfo cultureInfo, ExpressionOptions options, AdvancedExpressionOptions? extOptions)
        => CreateParser(cultureInfo, options, extOptions, literalsOnly: false);

    private static readonly ConcurrentDictionary<string, Parser<LogicalExpression>> LiteralParsers = new();
    private static readonly ConcurrentDictionary<string, Parser<LogicalExpression>> ExpressionParsers = new();

    // The grammar depends on the options, the culture of the expression, the advanced options and, where those options refer to it,
    // the culture of the current thread. A parser is built from a copy of the advanced options, so that later changes to the caller's
    // instance do not affect a parser shared under the old settings.
    private static string GetParserCacheKey(CultureInfo cultureInfo, ExpressionOptions options, AdvancedExpressionOptions? extOptions)
    {
        const string separator = "\u001F";
        return string.Concat(((long)options).ToString(CultureInfo.InvariantCulture), separator, GetCultureCacheKey(cultureInfo), separator,
            GetCurrentCultureCacheKey(extOptions), separator, extOptions?.GetCacheKey() ?? string.Empty);
    }

    private static readonly ConditionalWeakTable<CultureInfo, string> ReadOnlyCultureCacheKeys = new();
    private static readonly ConditionalWeakTable<CultureInfo, string>.CreateValueCallback CreateCultureCacheKeyCallback = CreateCultureCacheKey;

    /// <summary>
    /// Returns a key for the formats of a culture that parsing depends on (date and time patterns and separators, number and
    /// currency separators, signs). A culture that is not read-only may have been customized under a standard name, or changed
    /// after use, so the name alone does not identify it; the formats are compared instead, which also keeps the number of cached
    /// parsers bounded when a new instance of the same culture is created for each expression.
    /// The key of a read-only culture cannot change, so it is computed once per instance.
    /// </summary>
    internal static string GetCultureCacheKey(CultureInfo culture)
        => culture.IsReadOnly ? ReadOnlyCultureCacheKeys.GetValue(culture, CreateCultureCacheKeyCallback) : CreateCultureCacheKey(culture);

    private static string CreateCultureCacheKey(CultureInfo culture)
    {
        var dateTimeFormat = culture.DateTimeFormat;
        var numberFormat = culture.NumberFormat;
        return JoinCacheKeyParts(
        [
            culture.Name,
            dateTimeFormat.Calendar.GetType().FullName,
            dateTimeFormat.ShortDatePattern,
            dateTimeFormat.LongDatePattern,
            dateTimeFormat.ShortTimePattern,
            dateTimeFormat.LongTimePattern,
            dateTimeFormat.DateSeparator,
            dateTimeFormat.TimeSeparator,
            dateTimeFormat.AMDesignator,
            dateTimeFormat.PMDesignator,
            numberFormat.NumberDecimalSeparator,
            numberFormat.NumberGroupSeparator,
            numberFormat.CurrencyDecimalSeparator,
            numberFormat.CurrencyGroupSeparator,
            numberFormat.CurrencySymbol,
            numberFormat.NegativeSign,
            numberFormat.PositiveSign,
            numberFormat.PercentSymbol,
        ]);
    }

    /// <summary>
    /// Joins the parts of a cache key with a separator that is escaped inside the parts, so that two different lists of parts
    /// (e.g. ["days,x"] and ["days", "x"]) never give the same key.
    /// </summary>
    internal static string JoinCacheKeyParts(IEnumerable<string?> parts)
    {
        const char separator = '\u001E';
        var builder = new StringBuilder();
        bool first = true;
        foreach (var part in parts)
        {
            if (!first)
                builder.Append(separator);
            first = false;
            AppendEscapedCacheKeyPart(builder, part);
        }
        return builder.ToString();
    }

    /// <summary>
    /// Appends a part of a cache key, escaping the characters that separate the parts and the escape character itself.
    /// </summary>
    internal static void AppendEscapedCacheKeyPart(StringBuilder builder, string? part)
    {
        if (part is null)
            return;
        foreach (var c in part)
        {
            if (c is '\\' or '\u001E' or '\u001F')
                builder.Append('\\');
            builder.Append(c);
        }
    }

    /// <summary>
    /// Returns the expression parser for the given settings. Building the grammar takes about a millisecond, so the parser is
    /// built once per combination of settings and reused (parsers keep no state between parses).
    /// </summary>
    internal static Parser<LogicalExpression> GetExpressionParser(CultureInfo cultureInfo, ExpressionOptions options, AdvancedExpressionOptions? extOptions)
        => ExpressionParsers.GetOrAdd(GetParserCacheKey(cultureInfo, options, extOptions), _ => CreateParser(cultureInfo, options, extOptions?.Clone(), literalsOnly: false));

    /// <summary>
    /// Returns the parser that accepts only a single literal (a number, a date or time, a boolean or a GUID), optionally with a sign,
    /// as written in an expression with the given settings. The parser is created once per combination of settings.
    /// </summary>
    internal static Parser<LogicalExpression> GetLiteralParser(CultureInfo cultureInfo, ExpressionOptions options, AdvancedExpressionOptions? extOptions)
    {
        // A literal is data, so it never contains comments ('5 # five' is not 5); the context of the parse must not accept them either
        options = WithoutComments(options);
        return LiteralParsers.GetOrAdd(GetParserCacheKey(cultureInfo, options, extOptions), _ => CreateParser(cultureInfo, options, extOptions?.Clone(), literalsOnly: true));
    }

    /// <summary>
    /// Returns the options without the comment syntaxes, for parsing a single literal.
    /// </summary>
    internal static ExpressionOptions WithoutComments(ExpressionOptions options)
        => options & ~(ExpressionOptions.SupportPythonComments | ExpressionOptions.SupportCStyleComments);

    private static Parser<LogicalExpression> CreateParser(CultureInfo cultureInfo, ExpressionOptions options, AdvancedExpressionOptions? extOptions, bool literalsOnly)
    {
        /*
         * Grammar:
         * expression     => ternary ( ( "-" | "+" ) ternary )* ;
         * ternary        => logical ( "?" logical ":" logical)?
         * logical        => equality ( ( "and" | "or" ) equality )* ;
         * equality       => relational ( ( "=" | "!=" | ... ) relational )* ;
         * relational     => shift ( ( ">=" | ">" | ... ) shift )* ;
         * shift          => additive ( ( "<<" | ">>" ) additive )* ;
         * additive       => multiplicative ( ( "-" | "+" ) multiplicative )* ;
         * multiplicative => unary ( "/" | "*" | "%") unary )* ;
         * unary          => ( "-" | "not" | "!" ) exponential ;
         * exponential    => factorial ( "**" ) factorial )* ;
         * factorial      => primary ( "!" )* ;
         *
         * primary        => NUMBER
         *                  | STRING
         *                  | "true"
         *                  | "false"
         *                  | ("[" | "{") anything ("]" | "}")
         *                  | function
         *                  | list
         *                  | "(" expression ")" ;
         *
         * function       => Identifier "(" arguments ")"
         * arguments      => expression ( ("," | ";") expression )*
         */
        // The Deferred helper creates a parser that can be referenced by others before it is defined
        var expression = Deferred<LogicalExpression>();

        var expressionOrBracedStatementSequence = Deferred<LogicalExpression>();
        var bracedExpressionOrStatementSequence = Deferred<LogicalExpression>();

        bool useBigNumbers = options.HasFlag(ExpressionOptions.UseBigNumbers);

        bool unsignedHexBinOct = options.HasFlag(ExpressionOptions.HexBinOctAreUnsigned);

        bool acceptUnderscores = extOptions?.Flags.HasFlag(AdvExpressionOptions.AcceptUnderscoresInNumbers) ?? false;

        string acceptableHexChars = acceptUnderscores ? "0123456789abcdefABCDEF_" : "0123456789abcdefABCDEF";

        // Comments
        var docComment = Terms.Text("///").SkipAnd(AnyCharBefore(new PatternLiteral((x => x == '\n'), 1, 0), canBeEmpty: true, consumeDelimiter: true));

        var pythonLineComment = Terms.Text("#").SkipAnd(AnyCharBefore(new PatternLiteral((x => x == '\n'), 1, 0), canBeEmpty: true, consumeDelimiter: true));
        var cLineComment = Terms.Text("//").SkipAnd(AnyCharBefore(new PatternLiteral((x => x == '\n'), 1, 0), canBeEmpty: true, consumeDelimiter: true));
        var blockComment = Terms.Text("/*").SkipAnd(AnyCharBefore(Terms.Text("*/"), canBeEmpty: true, failOnEof: true, consumeDelimiter: true)
                .ElseError("Comment not closed."));

        List<SequenceSkipAnd<string, TextSpan>> comments = [];
        if (options.HasFlag(ExpressionOptions.SupportPythonComments))
            comments.Add(pythonLineComment);
        if (options.HasFlag(ExpressionOptions.SupportCStyleComments))
        {
            comments.Add(cLineComment);
            comments.Add(blockComment);
        }

        var comment = OneOf(comments.ToArray());

        // An alphabetic keyword must not be followed by a character that may continue an identifier,
        // so that e.g. "returnValue" or "trueValue" is an identifier and "2 mod3" is not a modulo operation
#if NET8_0_OR_GREATER
        string identifierPartChars = "_" + extOptions?.ExtraIdentifierChars;
        bool IsIdentifierPartChar(char c) => Character.IsIdentifierPart(c) || identifierPartChars.Contains(c);
#else
        static bool IsIdentifierPartChar(char c) => Character.IsIdentifierPart(c);
#endif
        var identifierPartChar = Literals.Pattern(IsIdentifierPartChar, 1, 1);
        Parser<string> Keyword(string text, bool caseInsensitive = true) => Terms.Text(text, caseInsensitive).AndSkip(Not(identifierPartChar));

        bool useComplexNumbers = (extOptions?.Flags.HasFlag(AdvExpressionOptions.ParseComplexNumbers) == true);

        // The imaginary unit is a whole word too, so that it does not take the first letter of "in" or "index"
        var imaginaryChar = Terms.Char('i').AndSkip(Not(identifierPartChar));
        Parser<string>? imaginaryCharParser = useComplexNumbers ? Keyword("i", false) : null;

        Parser<LogicalExpression>? imaginaryCharExpression = imaginaryCharParser?
                .Then<LogicalExpression>((ctx, x) => new ImaginaryNumberExpression(new ValueExpression(1), new Helpers.MathHelperOptions(cultureInfo, options)).SetLocation(new ParlotExpressionLocation(ctx))) ?? null;

        var hexNumber = Terms.Text("0x")
            .SkipAnd(Terms.Pattern(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || (acceptUnderscores && c == '_') /*acceptableHexChars.Contains(c)*/))
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                string? strValue = x.ToString();

                if (string.IsNullOrEmpty(strValue))
                    throw new NCalcParserException($"{x.ToString()} is not a valid hex number", ctx.Scanner.Cursor.Position);

                if (((LogicalExpressionParserContext)ctx).AcceptUnderscores)
                    strValue = strValue!.Replace("_", string.Empty);

                if (strValue.Length > 0)
                {
                    try
                    {
                        if (((LogicalExpressionParserContext)ctx).UnsignedHexBinOct)
                        {
                            ulong converted = Convert.ToUInt64(strValue, 16);
                            if (converted <= uint.MaxValue)
                                return new ValueExpression((object)(uint)converted).SetLocation(new ParlotExpressionLocation(ctx));
                            else
                                return new ValueExpression((object)converted).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                        else
                        {
                            // The digits are a magnitude, not a two's-complement bit pattern: 0xFFFFFFFFFFFFFFFF is ulong.MaxValue, as 0xFFFFFFFF is uint.MaxValue
                            ulong converted = Convert.ToUInt64(strValue, 16);
                            if (converted <= int.MaxValue)
                                return new ValueExpression((object)(int)converted).SetLocation(new ParlotExpressionLocation(ctx));
                            else
                            if (converted <= long.MaxValue)
                                return new ValueExpression((object)(long)converted).SetLocation(new ParlotExpressionLocation(ctx));
                            else
                                return new ValueExpression((object)converted).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                    }
                    catch (Exception ex) when (((LogicalExpressionParserContext)ctx).UseBigNumbers && (ex is OverflowException))
                    {
                        // do nothing and try to convert to BigInteger below
                    }

                    // we get here only when an OverflowException happens, so there is no need to check for useBigInteger

                    if (!BigIntegerParser.TryParseBigInteger(strValue!, 16, out BigInteger result))
                        throw new NCalcParserException($"{x.ToString()} is not a valid hex number", ctx.Scanner.Cursor.Position);

                    return new ValueExpression((object)result).SetLocation(new ParlotExpressionLocation(ctx));
                }
                else
                {
                    throw new NCalcParserException($"{x.ToString()} is not a valid hex number", ctx.Scanner.Cursor.Position);
                }
            });

        string acceptableOctalChars = acceptUnderscores ? "01234567_" : "01234567";

        Parser<string> octalPrefixParser = (extOptions?.Flags.HasFlag(AdvExpressionOptions.AcceptCStyleOctals) == true) ? OneOf(Terms.Text("0o"), Terms.Text("0")) : Terms.Text("0o");

        var octalNumber = octalPrefixParser
            .SkipAnd(Terms.Pattern(c => (c >= '0' && c <= '7') || (acceptUnderscores && c == '_')/* acceptableOctalChars.Contains(c)*/))
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                string? strValue = x.ToString();

                if (string.IsNullOrEmpty(strValue))
                    throw new NCalcParserException($"{x.ToString()} is not a valid octal number", ctx.Scanner.Cursor.Position);

                if (((LogicalExpressionParserContext)ctx).AcceptUnderscores)
                    strValue = strValue!.Replace("_", string.Empty);

                if (strValue.Length > 0)
                {
                    try
                    {
                        if (((LogicalExpressionParserContext)ctx).UnsignedHexBinOct)
                        {
                            ulong converted = Convert.ToUInt64(strValue, 8);
                            if (converted <= uint.MaxValue)
                                return new ValueExpression((object)(uint)converted).SetLocation(new ParlotExpressionLocation(ctx));
                            else
                                return new ValueExpression((object)converted).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                        else
                        {
                            // The digits are a magnitude, not a two's-complement bit pattern: 0xFFFFFFFFFFFFFFFF is ulong.MaxValue, as 0xFFFFFFFF is uint.MaxValue
                            ulong converted = Convert.ToUInt64(strValue, 8);
                            if (converted <= int.MaxValue)
                                return new ValueExpression((object)(int)converted).SetLocation(new ParlotExpressionLocation(ctx));
                            else
                            if (converted <= long.MaxValue)
                                return new ValueExpression((object)(long)converted).SetLocation(new ParlotExpressionLocation(ctx));
                            else
                                return new ValueExpression((object)converted).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                    }
                    catch (Exception ex) when (((LogicalExpressionParserContext)ctx).UseBigNumbers && (ex is OverflowException))
                    {
                        // do nothing and try to convert to BigInteger below
                    }

                    // we get here only when an OverflowException happens, so there is no need to check for useBigInteger

                    if (!BigIntegerParser.TryParseBigInteger(strValue!, 8, out BigInteger result))
                        throw new NCalcParserException($"{x.ToString()} is not a valid octal number", ctx.Scanner.Cursor.Position);

                    return new ValueExpression((object)result).SetLocation(new ParlotExpressionLocation(ctx));
                }
                else
                    throw new NCalcParserException($"{x.ToString()} is not a valid octal number", ctx.Scanner.Cursor.Position);
            });

        var binaryNumber = Terms.Text("0b")
            .SkipAnd(Terms.Pattern(c => c == '0' || c == '1' || (acceptUnderscores && c == '_')))
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                string? strValue = x.ToString();

                if (string.IsNullOrEmpty(strValue))
                    throw new NCalcParserException($"{x.ToString()} is not a valid binary number", ctx.Scanner.Cursor.Position);

                if (((LogicalExpressionParserContext)ctx).AcceptUnderscores)
                    strValue = strValue!.Replace("_", string.Empty);

                if (strValue.Length > 0)
                {
                    try
                    {
                        if (((LogicalExpressionParserContext)ctx).UnsignedHexBinOct)
                        {
                            ulong converted = Convert.ToUInt64(strValue, 2);
                            if (converted <= uint.MaxValue)
                                return new ValueExpression((object)(uint)converted).SetLocation(new ParlotExpressionLocation(ctx));
                            else
                                return new ValueExpression((object)converted).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                        else
                        {
                            // The digits are a magnitude, not a two's-complement bit pattern: 0xFFFFFFFFFFFFFFFF is ulong.MaxValue, as 0xFFFFFFFF is uint.MaxValue
                            ulong converted = Convert.ToUInt64(strValue, 2);
                            if (converted <= int.MaxValue)
                                return new ValueExpression((object)(int)converted).SetLocation(new ParlotExpressionLocation(ctx));
                            else
                            if (converted <= long.MaxValue)
                                return new ValueExpression((object)(long)converted).SetLocation(new ParlotExpressionLocation(ctx));
                            else
                                return new ValueExpression((object)converted).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                    }
                    catch (Exception ex) when (((LogicalExpressionParserContext)ctx).UseBigNumbers && (ex is OverflowException))
                    {
                        // do nothing and try to convert to BigInteger below
                    }

                    // we get here only when an OverflowException happens, so there is no need to check for useBigInteger

                    if (!BigIntegerParser.TryParseBigInteger(strValue!, 2, out BigInteger result))
                        throw new NCalcParserException($"{x.ToString()} is not a valid binary number", ctx.Scanner.Cursor.Position);

                    return new ValueExpression((object)result);
                }
                else
                    throw new NCalcParserException($"{x.ToString()} is not a valid binary number", ctx.Scanner.Cursor.Position);
            });

        Parser<LogicalExpression> hexOctBinNumber;

        hexOctBinNumber = OneOf(hexNumber!, octalNumber!, binaryNumber!);

        char decimalSeparator = (extOptions is not null) ? extOptions.GetDecimalSeparatorChar() : Parlot.Fluent.NumberLiterals.DefaultDecimalSeparator; // this method will return the default separator, if needed
        char decimalSeparator2 = (extOptions?.GetSecondaryDecimalSeparatorChar()) ?? '\0';
        char numGroupSeparator = (extOptions is not null) ? extOptions.GetNumberGroupSeparatorChar() : Parlot.Fluent.NumberLiterals.DefaultGroupSeparator; // this method will return the default separator, if needed

        NumberOptions useNumberGroupSeparatorFlag = ((extOptions is not null) && (numGroupSeparator != '\0')) ? NumberOptions.AllowGroupSeparators : NumberOptions.None;
        NumberOptions useUnderscoreFlag = acceptUnderscores ? NumberOptions.AllowUnderscore : NumberOptions.None;

        // A group separator equal to the decimal separator would make "0.5" the number 5, and numbers could not be written back
        if (useNumberGroupSeparatorFlag != NumberOptions.None && numGroupSeparator == decimalSeparator)
            throw new NCalcParserException($"The number group separator '{numGroupSeparator}' is the same as the decimal separator. Use different separators in the advanced options.");

        // decimalSeparator2 takes precedence over an equal group separator when it is specified, in all number parsers (the integer
        // parsers included: otherwise "1,5" followed by a list separator would be the integer 15)
        if (decimalSeparator2 != '\0' && decimalSeparator2 == numGroupSeparator)
            useNumberGroupSeparatorFlag = 0;

        // With AcceptUnderscoresInNumbers, an underscore may only stand between digits ("_1" is an identifier, "1_" is not a number)
        NumberTextCheck<int>? intUnderscoreCheck = acceptUnderscores ? static (text, _) => HasUnderscoresOnlyBetweenDigits(text) : null;
        NumberTextCheck<long>? longUnderscoreCheck = acceptUnderscores ? static (text, _) => HasUnderscoresOnlyBetweenDigits(text) : null;
        NumberTextCheck<BigInteger>? bigIntegerUnderscoreCheck = acceptUnderscores ? static (text, _) => HasUnderscoresOnlyBetweenDigits(text) : null;
        NumberTextCheck<decimal>? decimalUnderscoreCheck = acceptUnderscores ? static (text, _) => HasUnderscoresOnlyBetweenDigits(text) : null;
        NumberTextCheck<double>? doubleUnderscoreCheck = acceptUnderscores ? static (text, _) => HasUnderscoresOnlyBetweenDigits(text) : null;

        // The exclusions must match right after the digits (Literals, not Terms): a separator or a word such as "else" after whitespace does not make the literal fractional.
        // A decimal separator makes the literal fractional only when a digit follows it: with a comma as both the decimal and the list
        // separator, "1, 2" are two integers. An exponent is "E" followed by a digit or a sign.
        Parser<string> exponentStart = Literals.Text("E", true).AndSkip(Literals.Pattern(static c => (c >= '0' && c <= '9') || c == '+' || c == '-', 1, 1));
        Parser<char> digitChar = Literals.Pattern(static c => c >= '0' && c <= '9', 1, 1).Then(static s => s.Span[0]);
        Parser<string>[] floatNumExclusions =
            (decimalSeparator2 != '\0')
            ? [Literals.Text(decimalSeparator.ToString()).AndSkip(digitChar), Literals.Text(decimalSeparator2.ToString()).AndSkip(digitChar), exponentStart]
            : [Literals.Text(decimalSeparator.ToString()).AndSkip(digitChar), exponentStart];

        //var intNumberParser
        var intNumber = CheckedNumber(Terms.Number<int>(NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag, decimalSeparator, numGroupSeparator), intUnderscoreCheck)
            .AndSkip(Not(OneOf(floatNumExclusions)))
            .Then<LogicalExpression>(static (ctx, d) => new ValueExpression(d).SetLocation(new ParlotExpressionLocation(ctx)));

        var longNumber = CheckedNumber(Terms.Number<long>(NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag, decimalSeparator, numGroupSeparator), longUnderscoreCheck)
            .AndSkip(Not(OneOf(floatNumExclusions)))
            .Then<LogicalExpression>(static (ctx, d) => new ValueExpression(d).SetLocation(new ParlotExpressionLocation(ctx)));

        Parser<LogicalExpression>? bigIntNumber = null;

        if (useBigNumbers)
        {
            bigIntNumber = CheckedNumber(Terms.Number<BigInteger>(NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag, decimalSeparator, numGroupSeparator), bigIntegerUnderscoreCheck)
                .AndSkip(Not(OneOf(floatNumExclusions)))
                .Then<LogicalExpression>(static (ctx, d) =>
                    {
                        if (d >= ulong.MinValue && d <= ulong.MaxValue)
                            return new ValueExpression((object)(ulong)d).SetLocation(new ParlotExpressionLocation(ctx));
                        else
                            return new ValueExpression(d).SetLocation(new ParlotExpressionLocation(ctx));
                    });
        }

        // With DecimalAsDefault, a literal that decimal could keep only with fewer digits than double (1e-30) is read as a double
        NumberTextCheck<decimal>? decimalCheck = options.HasFlag(ExpressionOptions.DecimalAsDefault)
            ? (text, _) => (!acceptUnderscores || HasUnderscoresOnlyBetweenDigits(text)) && DecimalKeepsDigits(text, decimalSeparator, decimalSeparator2)
            : decimalUnderscoreCheck;
        var decimalNumber = CheckedNumber(Terms.Number<decimal>(NumberOptions.Float | useNumberGroupSeparatorFlag | useUnderscoreFlag | NumberOptions.RequireFractionalPartForDecimals, decimalSeparator, numGroupSeparator, decimalSeparator2), decimalCheck)
            .Then<LogicalExpression>(static (ctx, val) =>
            {
                return new ValueExpression(val).SetLocation(new ParlotExpressionLocation(ctx));
            });

        var doubleNumber = CheckedNumber(Terms.Number<double>(NumberOptions.Float | useNumberGroupSeparatorFlag | useUnderscoreFlag | NumberOptions.RequireFractionalPartForDecimals, decimalSeparator, numGroupSeparator, decimalSeparator2), doubleUnderscoreCheck)
            .Then<LogicalExpression>(static (ctx, val) =>
            {
                return new ValueExpression(val).SetLocation(new ParlotExpressionLocation(ctx));
            });

        Parser<LogicalExpression>? bigDecimalNumber = null;
        if (useBigNumbers)
        {
            string acceptableDecChars = acceptUnderscores ? "0123456789_" : "0123456789";

            string decimalSeparators;
            if (decimalSeparator2 != '\0')
                decimalSeparators = string.Concat(decimalSeparator, decimalSeparator2);
            else
                decimalSeparators = decimalSeparator.ToString();

            // A number has no whitespace inside (as with Terms.Number): the parts are Literals, and the whitespace before the literal is skipped once
            Parser<LogicalExpression> bigIntNumberD = Literals.Number<BigInteger>((NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag), decimalSeparator, numGroupSeparator)
                .Then<LogicalExpression>(static (d) =>
                {
                    if (d >= ulong.MinValue && d <= ulong.MaxValue)
                        return new ValueExpression((object)(ulong)d);
                    else
                        return new ValueExpression(d);
                });
            Parser<string> fractionD = Literals.AnyOf(decimalSeparators.AsSpan(), 1, 1)
                .SkipAnd(Literals.Pattern(c => acceptableDecChars.Contains(c)))
                .Then(static s => s.ToString());
            Parser<string> exponentD = Literals.AnyOf("Ee".AsSpan(), 1, 1)
                .SkipAnd(Literals.Number<BigInteger>(NumberOptions.Integer | useUnderscoreFlag, decimalSeparator, numGroupSeparator))
                .Then(static d => d.ToString(CultureInfo.InvariantCulture));

            // Digits with a fraction and an optional exponent ("1.5", "1.5e3"), digits with an exponent ("1e400", read as "1.0e400"),
            // or a fraction alone (".5")
            Parser<(LogicalExpression? IntegerPart, string Fraction, string? Exponent)> bigDecimalParts = OneOf(
                bigIntNumberD.And(OneOf(fractionD.And(ZeroOrOne(exponentD)), exponentD.Then(static e => (string.Empty, e))))
                    .Then<(LogicalExpression? IntegerPart, string Fraction, string? Exponent)>(static x => (x.Item1, x.Item2.Item1, x.Item2.Item2)),
                fractionD.And(ZeroOrOne(exponentD))
                    .Then<(LogicalExpression? IntegerPart, string Fraction, string? Exponent)>(static x => (null, x.Item1, x.Item2)));

            bigDecimalNumber =
                (acceptUnderscores
                    ? new CheckedNumberParser<(LogicalExpression? IntegerPart, string Fraction, string? Exponent)>(bigDecimalParts, static (text, _) => HasUnderscoresOnlyBetweenDigits(text))
                    : SkipWhiteSpace(bigDecimalParts))
                .When((_, val) => TryParseDecimal(val, acceptUnderscores) is not null)
                .Then<LogicalExpression>(static (ctx, val) =>
                {
                    bool decimalDefault = ((LogicalExpressionParserContext)ctx).Options.HasFlag(ExpressionOptions.DecimalAsDefault);

                    BigDecimal? value = TryParseDecimal(val, ((LogicalExpressionParserContext)ctx).AcceptUnderscores);

                    if (value is null)
                        return new ValueExpression();  // never happens - the When condition ensures that val can be parsed

                    // A value far beyond the range of double (or below its smallest value) stays a BigDecimal; it is not compared with
                    // the limits of decimal and double, which would expand its exponent into digits (1e9999999)
                    long magnitude = (long)value.Value.Exponent + BigInteger.Abs(value.Value.Mantissa).ToString(CultureInfo.InvariantCulture).Length;
                    if (!value.Value.Mantissa.IsZero && (magnitude > 400 || magnitude < -400))
                    {
                        // A literal with more integer digits (or leading zeros) than MathHelper.MaxPowResultDigits is a double, infinity
                        // or 0, as it was before such literals were big numbers: as a BigDecimal it would be written with all its digits
                        // by ToString() and could not be used in arithmetic
                        if (Math.Abs(magnitude) > NCalc.Helpers.MathHelper.MaxPowResultDigits)
                        {
                            double outOfRange = magnitude > 0 ? double.PositiveInfinity : 0.0;
                            return new ValueExpression(value.Value.Mantissa.Sign < 0 ? -outOfRange : outOfRange).SetLocation(new ParlotExpressionLocation(ctx));
                        }

                        return new ValueExpression(value).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    if (decimalDefault && (value >= decimal.MinValue && value <= decimal.MaxValue))
                    {
                        decimal decValue = NCalc.Helpers.MathHelper.BigDecimalToDecimal(value.Value);
                        if (value == decValue)
                            return new ValueExpression(decValue).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    if (value >= double.MinValue && value <= double.MaxValue)
                    {
                        double dValue = NCalc.Helpers.MathHelper.BigDecimalToDouble(value.Value);
                        if (value == dValue)
                            return new ValueExpression(dValue).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    if (!decimalDefault && (value >= decimal.MinValue && value <= decimal.MaxValue))
                    {
                        decimal decValue = NCalc.Helpers.MathHelper.BigDecimalToDecimal(value.Value);
                        if (value == decValue)
                            return new ValueExpression(decValue).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    return new ValueExpression(value).SetLocation(new ParlotExpressionLocation(ctx));
                });
        }

        var decimalOrDoubleNumber = (bigDecimalNumber is not null)
            ? (options.HasFlag(ExpressionOptions.DecimalAsDefault)
              ? OneOf(bigDecimalNumber, decimalNumber, doubleNumber)
              : OneOf(bigDecimalNumber, doubleNumber, decimalNumber))
            : (options.HasFlag(ExpressionOptions.DecimalAsDefault)
              ? OneOf(decimalNumber, doubleNumber)
              : OneOf(doubleNumber, decimalNumber));

        // Add currency support

        bool supportCurrency = (extOptions?.Flags.HasFlag(AdvExpressionOptions.AcceptCurrencySymbol) == true);

        Parser<LogicalExpression>? currency = null;

        if (supportCurrency)
        {
            string currencySymbol = string.Empty;
            string currencySymbol2 = string.Empty;
            string currencySymbol3 = string.Empty;

            extOptions!.GetCurrencySymbols(out currencySymbol, out currencySymbol2, out currencySymbol3);

            if (!string.IsNullOrEmpty(currencySymbol) || !string.IsNullOrEmpty(currencySymbol2) || !string.IsNullOrEmpty(currencySymbol3))
            {
                char currencyDecimalSeparator = extOptions.GetCurrencyDecimalSeparatorChar(); // this method will return the default separator, if needed
                char currencyNumGroupSeparator = extOptions.GetCurrencyNumberGroupSeparatorChar(); // this method will return the default separator, if needed
                char currencyDecimalSeparator2 = decimalSeparator2;
                if (currencyDecimalSeparator2 == currencyDecimalSeparator)
                {
                    if (decimalSeparator != currencyDecimalSeparator) // decimal separators for currency and numbers are inverted
                    {
                        currencyDecimalSeparator2 = decimalSeparator;
                    }
                }

                List <Parser<string>> currencyChars = [];

                if (!string.IsNullOrEmpty(currencySymbol)) currencyChars.Add(Terms.Text(currencySymbol, true));
                if (!string.IsNullOrEmpty(currencySymbol2)) currencyChars.Add(Terms.Text(currencySymbol2, true));
                if (!string.IsNullOrEmpty(currencySymbol3)) currencyChars.Add(Terms.Text(currencySymbol3, true));

                Parser<string>[] currencyCharsArray = [.. currencyChars];

                Parser<LogicalExpression>? currency1 = null;
                Parser<LogicalExpression>? currency2 = null;

                var decimalCurrencyNumber = CheckedNumber(Terms.Number<decimal>((NumberOptions.Float & ~NumberOptions.AllowExponent) | useNumberGroupSeparatorFlag | useUnderscoreFlag, currencyDecimalSeparator, currencyNumGroupSeparator, currencyDecimalSeparator2), decimalUnderscoreCheck)
                    .Then<LogicalExpression>(static (ctx, val) => new ValueExpression(val).SetLocation(new ParlotExpressionLocation(ctx)));

                currency1 = OneOf(currencyCharsArray).SkipAnd(SkipWhiteSpace(OneOf(decimalCurrencyNumber, intNumber, longNumber)))
                    .Then<LogicalExpression>(static (_, val) => val);

                currency2 = OneOf(decimalCurrencyNumber, intNumber, longNumber).AndSkip(SkipWhiteSpace(OneOf(currencyCharsArray)))
                    .Then<LogicalExpression>(static (_, val) => val);

                currency = OneOf(currency1!, currency2!);
            }
        }

        // Add percent support
        bool calculatePercent = (extOptions?.Flags.HasFlag(AdvExpressionOptions.CalculatePercent) == true);
        var percentChar = Terms.Char('%'); // CultureInfo defines a percent character, but we are yet to see a character different from '%'

        bool useVectors = (extOptions?.Flags.HasFlag(AdvExpressionOptions.ParseVectors) == true);
        //var vectorStr = Terms.Text("vec!");

        bool useCharsForOps = !options.HasFlag(ExpressionOptions.SkipLogicalAndBitwiseOpChars);
        bool useUnicodeForOps = options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations);

        var comma = Terms.Char(',');
        var divided = useUnicodeForOps ? OneOf(Terms.Text("/"), Terms.Text("\u2236"), Terms.Text("\u00F7")) : Terms.Text("/");
        var times = useUnicodeForOps ? OneOf(Terms.Text("*"), Terms.Text("\u00D7"), Terms.Text("\u2219")) : Terms.Text("*");
        var modulo = calculatePercent ? Keyword("mod") : OneOf(Terms.Text("%"), Keyword("mod"));
        var intDivB = OneOf(Terms.Text("\\"), Keyword("div"));
        var intDivP = Terms.Text("//");
        var minus = Terms.Text("-");
        var plus = Terms.Text("+");

        var equal = options.HasFlag(ExpressionOptions.UseCStyleAssignments) ? Terms.Text("==") : OneOf(Terms.Text("=="), Terms.Text("="));
        var notEqual = useUnicodeForOps ? OneOf(Terms.Text("<>"), Terms.Text("!="), Terms.Text("\u2260")) : OneOf(Terms.Text("<>"), Terms.Text("!="));
        var @in = useUnicodeForOps ? OneOf(Keyword("in"), Terms.Text("\u2208")) : Keyword("in");
        var notIn = useUnicodeForOps ? OneOf(Keyword("not in"), Terms.Text("\u2209")) : Keyword("not in");

        var like = Keyword("like");
        var notLike = Keyword("not like");

        var greater = Terms.Text(">");
        var greaterOrEqual = useUnicodeForOps ? OneOf(Terms.Text(">="), Terms.Text("\u2265")) : Terms.Text(">=");
        var less = Terms.Text("<");
        var lessOrEqual = useUnicodeForOps ? OneOf(Terms.Text("<="), Terms.Text("\u2264")) : Terms.Text("<=");

        var leftShift = Terms.Text("<<");
        var rightShift = Terms.Text(">>");

        var exponent = useUnicodeForOps
            ? (useCharsForOps
                    ? OneOf(Terms.Text("**"), Terms.Text("\u2191"), Terms.Text("\u2291"))
                    : OneOf(Terms.Text("**"), Terms.Text("^"), Terms.Text("\u2191"), Terms.Text("\u2291")))
            : (useCharsForOps
                    ? Terms.Text("**")
                    : OneOf(Terms.Text("**"), Terms.Text("^"))); // when useCharsForOps is true, caret is used for bitwise XOR
        var openParen = Terms.Char('(');
        var closeParen = Terms.Char(')');
        var openBrace = Terms.Char('[');
        var closeBrace = Terms.Char(']');
        var openCurlyBrace = Terms.Char('{');
        var closeCurlyBrace = Terms.Char('}');
        var questionMark = Terms.Char('?');
        var exclamationMark = Terms.Char('!');
        var colon = Terms.Char(':');
        var semicolon = Terms.Char(';');

        var space = Literals.Char(' ');

        var dotChar = Terms.Char('.');
        var hyphen = Terms.Text("-");
        var hyphenChar = Terms.Char('-');

        var statementEnd = semicolon;

        Parser<string>? root2 = useCharsForOps ? Terms.Text("\u221A") : null;
#if NET8_0_OR_GREATER
        Parser<string>? root3 = useCharsForOps ? Terms.Text("\u221B") : null;
#endif
        Parser<string>? root4 = useCharsForOps ? Terms.Text("\u221C") : null;

        var resultRefChar = Terms.Char('@');
        var atChar = Terms.Char('@');

#if NET8_0_OR_GREATER
        string extraIdentChars = "_";
        if (extOptions is not null)
            extraIdentChars += extOptions.ExtraIdentifierChars;

        string extraStartIdentChar = supportCurrency ? "" : "$";
        string startChars = extraStartIdentChar + extraIdentChars + Character.AZ;
        string partChars = extraIdentChars + Character.AlphaNumeric;
        var letterIdentifier = Terms.Identifier((c) => startChars.Contains(c), (c) => partChars.Contains(c));
#else
        var letterIdentifier = Terms.Identifier();
#endif
        var identifier = letterIdentifier;
        // We don't let $ at the beginning of identifiers as it may be confused with currency

        Parser<string>? not;
        Parser<string>? and;
        Parser<string>? or;
        Parser<string>? xor;

        if (useCharsForOps)
        {
            and = useUnicodeForOps ? OneOf(Keyword("AND"), Terms.Text("&&"), Terms.Text("\u2227")) : OneOf(Keyword("AND"), Terms.Text("&&"));
            or = useUnicodeForOps ? OneOf(Keyword("OR"), Terms.Text("||"), Terms.Text("\u2228")) : OneOf(Keyword("OR"), Terms.Text("||"));
            not = useUnicodeForOps
                ? OneOf(Keyword("NOT").AndSkip(OneOf(Literals.WhiteSpace().Or(Not(AnyCharBefore(openParen))))), Terms.Text("!"), Terms.Text("\u00ac"))
                : OneOf(Keyword("NOT").AndSkip(OneOf(Literals.WhiteSpace().Or(Not(AnyCharBefore(openParen))))), Terms.Text("!"));
        }
        else
        {
            and = useUnicodeForOps ? OneOf(Keyword("AND"), Terms.Text("\u2227")) : Keyword("AND");
            or = useUnicodeForOps ? OneOf(Keyword("OR"), Terms.Text("\u2228")) : Keyword("OR");
            not = useUnicodeForOps
                ? OneOf(Keyword("NOT").AndSkip(OneOf(Literals.WhiteSpace().Or(Not(AnyCharBefore(openParen))))), Terms.Text("\u00ac"))
                : Keyword("NOT").AndSkip(OneOf(Literals.WhiteSpace().Or(Not(AnyCharBefore(openParen)))));
        }
        xor = useUnicodeForOps ? OneOf(Keyword("XOR"), Terms.Text("\u2295"), Terms.Text("\u22BB")) : Keyword("XOR");

        var bitwiseAnd = useCharsForOps ? OneOf(Keyword("BIT_AND"), Terms.Text("&")) : Keyword("BIT_AND");
        var bitwiseOr = useCharsForOps ? OneOf(Keyword("BIT_OR"), Terms.Text("|"))  : Keyword("BIT_OR");
        var bitwiseXOr = useCharsForOps ? OneOf(Keyword("BIT_XOR"), Terms.Text("^")) : Keyword("BIT_XOR");
        var bitwiseNot = useCharsForOps ? OneOf(Keyword("BIT_NOT"), Terms.Text("~")) : Keyword("BIT_NOT");
        var returnParser = Keyword("return");

        var assignmentOperator = useUnicodeForOps
                                    ? OneOf(Terms.Text("\u2254"),
                                            (options.HasFlag(ExpressionOptions.UseCStyleAssignments)
                                                ? Terms.Text("=")
                                                : Terms.Text(":=")))
                                    : options.HasFlag(ExpressionOptions.UseCStyleAssignments)
                                                ? Terms.Text("=")
                                                : Terms.Text(":=");

        var plusAssign = Terms.Text("+=");
        var minusAssign = Terms.Text("-=");
        var multiplyAssign = useUnicodeForOps ? OneOf(Terms.Text("*="), Terms.Text("\u00D7="), Terms.Text("\u2219=")) : Terms.Text("*=");
        var divAssign = useUnicodeForOps ? OneOf(Terms.Text("/="), Terms.Text("\u2236="), Terms.Text("\u00F7=")) : Terms.Text("/=");
        var orAssign = Terms.Text("|=");
        var andAssign = Terms.Text("&=");
        var xorAssign = Terms.Text("^=");

        var rangeText = Terms.Text("..");
        var fromEndText = Terms.Text("^");

        // "(" expression ")"
        var groupExpression = Between(openParen, expression, closeParen);

        var braceIdentifier = openBrace
            .SkipAnd(AnyCharBefore(closeBrace, failOnEof: true, consumeDelimiter: true).ElseError("Bracket not closed."));

        var curlyBraceIdentifier =
            openCurlyBrace.SkipAnd(AnyCharBefore(closeCurlyBrace, failOnEof: true, consumeDelimiter: true)
                .ElseError("Brace not closed."));

        var resultReference = resultRefChar
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                ExpressionLocation loc = new ParlotExpressionLocation(ctx);
                return new FunctionCall((Identifier)new Identifier(x.ToString()!).SetLocation(loc), []).SetLocation(loc);
            });

        // ("[" | "{") identifier ("]" | "}")
        Parser<LogicalExpression> identifierExpression = identifier
                .Then<LogicalExpression>(static (ctx, x) => new Identifier(x.ToString()!).SetLocation(new ParlotExpressionLocation(ctx)));
        Parser<LogicalExpression> bracketedIdentifierExpression = braceIdentifier
                .Then<LogicalExpression>(static (ctx, x) => new Identifier(x.ToString()!).SetBracketed(true).SetLocation(new ParlotExpressionLocation(ctx)));

        // [^][lowerBound][..][^][upperBound]: '^' must be followed by a bound, and an index without a range needs a value ([] and [^] are invalid)
        var rangedIndex = openBrace.SkipAnd(ZeroOrOne(fromEndText)).And(ZeroOrOne(expressionOrBracedStatementSequence)).And(ZeroOrOne(rangeText)).And(ZeroOrOne(fromEndText)).And(ZeroOrOne(expressionOrBracedStatementSequence)).AndSkip(closeBrace)
            .When(static (_, x) => (x.Item1 is null || x.Item2 is not null) && (x.Item4 is null || x.Item5 is not null) && (x.Item2 is not null || x.Item3 is not null)
                && (x.Item3 is not null || x.Item5 is null));

        AdvancedExpressionOptions.ListItemSeparatorKind argumentSeparator = extOptions?.ListItemSeparator ?? AdvancedExpressionOptions.ListItemSeparatorKind.CommaOrSemicolon;

        Parser<char> argumentSeparatorChar = argumentSeparator switch
        {
            AdvancedExpressionOptions.ListItemSeparatorKind.CommaOrSemicolon => comma.Or(semicolon),
            AdvancedExpressionOptions.ListItemSeparatorKind.Comma => comma,
            AdvancedExpressionOptions.ListItemSeparatorKind.Semicolon => semicolon,
            AdvancedExpressionOptions.ListItemSeparatorKind.Colon => colon,
            AdvancedExpressionOptions.ListItemSeparatorKind.Space => space,
            AdvancedExpressionOptions.ListItemSeparatorKind.CommaOrSpace => comma.Or(space),
            AdvancedExpressionOptions.ListItemSeparatorKind.CommaOrSemicolonOrSpace => OneOf(comma, semicolon, space),
            AdvancedExpressionOptions.ListItemSeparatorKind.SemicolonOrColon => semicolon.Or(colon),
            AdvancedExpressionOptions.ListItemSeparatorKind.SemicolonOrSpace => semicolon.Or(space),
            AdvancedExpressionOptions.ListItemSeparatorKind.SemicolonOrColonOrSpace => OneOf(colon, semicolon, space),
            _ => comma.Or(semicolon),
        };

        // list => "(" (expression ("," expression)*)? ")"
        var populatedList =
            Between(openParen, Separated(argumentSeparatorChar/*(decimalSeparator == ',' || decimalSeparator2 == ',' || numGroupSeparator == ',' ? semicolon : comma.Or(semicolon))*/, expressionOrBracedStatementSequence),
                    closeParen.ElseError("Parenthesis not closed or an argument is missing."))
                .Then<LogicalExpression>(static (ctx, values) => new LogicalExpressionList(values).SetLocation(new ParlotExpressionLocation(ctx)));

        var emptyList = openParen.AndSkip(closeParen).Then<LogicalExpression>(static _ => new LogicalExpressionList());

        var list = OneOf(emptyList, populatedList);

        var vectorData =
            Between(openBrace, Separated(argumentSeparatorChar/*(decimalSeparator == ',' || decimalSeparator2 == ',' || numGroupSeparator == ',' ? semicolon : comma.Or(semicolon))*/, expressionOrBracedStatementSequence),
                    closeBrace.ElseError("Brace not closed."))
                .Then<LogicalExpression>(static (ctx, values) => new LogicalExpressionList(values).SetLocation(new ParlotExpressionLocation(ctx)));

        var vector = vectorData
            .Then<LogicalExpression>((ctx, x) =>
            {
                ExpressionLocation loc = new ParlotExpressionLocation(ctx);
                return new VectorExpression((LogicalExpressionList)x, new Helpers.MathHelperOptions(cultureInfo, options)).SetLocation(loc);
            });

        var function = identifier
            .And(list)
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                ExpressionLocation loc = new ParlotExpressionLocation(ctx);
                return new FunctionCall((Identifier)new Identifier(x.Item1.ToString()!).SetLocation(loc), (LogicalExpressionList)x.Item2).SetLocation(loc);
            });

        var percentFunction = percentChar
            .And(list)
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                ExpressionLocation loc = new ParlotExpressionLocation(ctx);
                return new FunctionCall((Identifier)new Identifier("%").SetLocation(loc), (LogicalExpressionList)x.Item2).SetLocation(loc);
            });

        Parser<LogicalExpression> functionOrResultRef;

        List<Parser<LogicalExpression>> funcList = [function];
        if (extOptions?.Flags.HasFlag(AdvExpressionOptions.UseResultReference) == true)
            funcList.Add(resultReference);
        if (calculatePercent)
            funcList.Add(percentFunction);
        functionOrResultRef = OneOf(funcList.ToArray());

        var booleanTrue = Keyword("true")
                .Then<LogicalExpression>(static (ctx, _) => new ValueExpression(true).SetLocation(new ParlotExpressionLocation(ctx)));
        var booleanFalse = Keyword("false")
            .Then<LogicalExpression>(static (ctx, _) => new ValueExpression(false).SetLocation(new ParlotExpressionLocation(ctx)));

        var theNull = Keyword("null")
            .Then<LogicalExpression>(static (ctx, _) => new ValueExpression().SetLocation(new ParlotExpressionLocation(ctx)));

        var singleQuotesStringValue = Terms.String(quotes: StringLiteralQuotes.Single, returnDecoded: false)
                .Then<LogicalExpression>(static (ctx, value) =>
                {
                    string? originalValue = value.ToString();
                    if (originalValue is null)
                        return new ValueExpression(null);

                    TextSpan decodedValue = Character.DecodeString(originalValue);

                    // One character, also when it is written with an escape sequence ('\n', '\t', '\''), is a char
                    if (decodedValue.Length == 1 &&
                        ((LogicalExpressionParserContext)ctx).Options.HasFlag(ExpressionOptions.AllowCharValues))
                    {
                        return new ValueExpression(decodedValue.Span[0]).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    return new ValueExpression(decodedValue.ToString(), originalValue, StringKind.SingleQuote).SetLocation(new ParlotExpressionLocation(ctx));
                });

        var doubleQuotesStringValue = Terms.String(quotes: StringLiteralQuotes.Double, returnDecoded: false)
                .Then<LogicalExpression>(static (ctx, value) =>
                {
                    string? originalValue = value.ToString();
                    if (originalValue is null)
                        return new ValueExpression(null);

                    TextSpan decodedValue = Character.DecodeString(originalValue);

                    return new ValueExpression(decodedValue.ToString(), originalValue, StringKind.DoubleQuote).SetLocation(new ParlotExpressionLocation(ctx));
                });

        var rawStringValue = atChar.SkipAnd(Terms.Char('"')).SkipAnd(Literals.NoneOf("\"".AsSpan())).AndSkip(Terms.Char('"'))
            .Then<LogicalExpression>(static (ctx, value) =>
                new ValueExpression(value.ToString(), StringKind.RawDoubleQuote).SetLocation(new ParlotExpressionLocation(ctx)));

        var backQuoteStringValue = Terms.Char('`').SkipAnd(Literals.NoneOf("`".AsSpan())).AndSkip(Terms.Char('`'))
            .Then<LogicalExpression>(static (ctx, value) =>
                new ValueExpression(value.ToString(), StringKind.BackQuote).SetLocation(new ParlotExpressionLocation(ctx)));

        var stringValue = OneOf(singleQuotesStringValue, doubleQuotesStringValue, rawStringValue, backQuoteStringValue);

        var charIsNumber = Literals.Pattern(char.IsNumber);
        var charIsNumberWithWhitespace = Terms.Pattern(char.IsNumber);

        // Add proper date and time support

        SequenceAndSkip<LogicalExpression, char>? dateTime = null;

        // The same literal starting exactly at the '#', which tells a date/time literal from a Python-style comment
        Parser<LogicalExpression>? dateTimeAtHash = null;

        if (!options.HasFlag(ExpressionOptions.DontParseDates))
        {
            DateTimeFormatInfo dateTimeFormat = cultureInfo.DateTimeFormat;
            if (extOptions?.GetFormat(typeof(DateTimeFormatInfo)) is DateTimeFormatInfo advancedFormat)
            {
                // With built-in separators, the advanced options give the format of the thread culture, but dates are read as without
                // advanced options: in the order of the parts of the culture of the options, or else of the expression
                dateTimeFormat = CreateAdvancedLiteralDateTimeFormat(advancedFormat,
                    extOptions.DateSeparatorType == AdvancedExpressionOptions.SeparatorType.BuiltIn
                        ? (extOptions.OwnCultureInfo ?? cultureInfo).DateTimeFormat
                        : advancedFormat);
            }
            DateTimeFormatInfo literalDateTimeFormat = CreateLiteralDateTimeFormat(dateTimeFormat);

            // The parts of a time are joined with ':' for DateTime.TryParse, which reads it with this format: joined with a custom time
            // separator such as '-' or '+', the last part would be read as a UTC offset (#2024/01/02 10-30-05# was 05:30 local time)
            var timeDateTimeFormat = (DateTimeFormatInfo)dateTimeFormat.Clone();
            timeDateTimeFormat.TimeSeparator = ":";

            // A date written with the custom date separator '-' is read with this format, as the same date written with '/' would be
            var slashDateTimeFormat = (DateTimeFormatInfo)dateTimeFormat.Clone();
            slashDateTimeFormat.DateSeparator = "/";

            Sequence<TextSpan, TextSpan, TextSpan> dateDefinition;
            Sequence<TextSpan, TextSpan, TextSpan> dateDefinitionIso;

            Parser<LogicalExpression> date;
            Parser<LogicalExpression> dateIso;

            // The following block prepares the masks for the approach to parsing used by ncalc by default -
            // parsing of "x/y/z" in dates with the current culture info (which will likely not work in some locales).
            // So, these masks below fix the format to be "x/y/z" in the order used by the current culture.
            string[] ncalcDateMasks = new string[2];
            string[] ncalcDateTimeMasks = new string[2];
            string[] ncalcDateShortTimeMasks = new string[2];
            string[] ncalcDateTime12Masks = new string[4];
            string[] ncalcDateShortTime12Masks = new string[4];

            CultureInfo culture = cultureInfo;// ?? CultureInfo.CurrentCulture;

            string builtInDateSep = (cultureInfo/* ?? CultureInfo.CurrentCulture*/).DateTimeFormat.DateSeparator;
            string builtInTimeSep = (cultureInfo/* ?? CultureInfo.CurrentCulture*/).DateTimeFormat.TimeSeparator;

            string datePattern = culture.DateTimeFormat.ShortDatePattern;
            if (string.IsNullOrEmpty(datePattern))
            {
                ncalcDateMasks[0] = string.Join(builtInDateSep, "d", "M", "yyyy");
                ncalcDateMasks[1] = string.Join(builtInDateSep, "d", "M", "yy");
            }
            else
            {
                switch (datePattern[0])
                {
                    case 'd':
                        ncalcDateMasks[0] = string.Join(builtInDateSep, "d", "M", "yyyy");
                        ncalcDateMasks[1] = string.Join(builtInDateSep, "d", "M", "yy");
                        break;
                    case 'M':
                        ncalcDateMasks[0] = string.Join(builtInDateSep, "M", "d", "yyyy");
                        ncalcDateMasks[1] = string.Join(builtInDateSep, "M", "d", "yy");
                        break;
                    case 'y':
                        ncalcDateMasks[0] = string.Join(builtInDateSep, "yyyy", "M", "d");
                        ncalcDateMasks[1] = string.Join(builtInDateSep, "yy", "M", "d");
                        break;
                    default:
                        ncalcDateMasks[0] = string.Join(builtInDateSep, "d", "M", "yyyy");
                        ncalcDateMasks[1] = string.Join(builtInDateSep, "d", "M", "yy");
                        break;
                }
            }

            // Define some masks for date-time values with both long and short time
            ncalcDateTimeMasks[0] = string.Join(" ", ncalcDateMasks[0], string.Join(builtInTimeSep, "H", "m", "s"));
            ncalcDateTimeMasks[1] = string.Join(" ", ncalcDateMasks[1], string.Join(builtInTimeSep, "H", "m", "s"));
            ncalcDateShortTimeMasks[0] = string.Join(" ", ncalcDateMasks[0], string.Join(builtInTimeSep, "H", "m"));
            ncalcDateShortTimeMasks[1] = string.Join(" ", ncalcDateMasks[1], string.Join(builtInTimeSep, "H", "m"));

            bool useSecondDate = false;
            bool onlyCustomDateTranslation = false;
            string customDateSep = builtInDateSep;

            if (extOptions is not null)
            {
                customDateSep = extOptions.GetDateSeparator();
                if (customDateSep != builtInDateSep && !extOptions.Flags.HasFlag(AdvExpressionOptions.SkipBuiltInDateSeparator))
                {
                    useSecondDate = true; // we use the second date separator when both custom separator and the default slash are enabled
                }
                else
                if (customDateSep == builtInDateSep)
                {
                    onlyCustomDateTranslation = true;
                }
            }

            var secondDateSep = Terms.Text(customDateSep); // this may be a custom separator or "/"
            if (useSecondDate)
            {
                if (customDateSep.Contains(' '))
                {
                    // If the date separator contains spaces (sk-SK, we salute you), we need to let people enter both "12.05.2025" and "12. 05. 2025"
                    // And for this, we use a third separator - a trimmed version of the one we have from the culture info or custom settings.
                    var thirdDateSep = Terms.Text(customDateSep.Trim());
                    dateDefinition = charIsNumber
                        .AndSkip(OneOf(divided, secondDateSep, thirdDateSep))
                        .And(charIsNumber)
                        .AndSkip(OneOf(divided, secondDateSep, thirdDateSep))
                        .And(charIsNumber);
                }
                else
                {
                    dateDefinition = charIsNumber
                        .AndSkip(OneOf(divided, secondDateSep))
                        .And(charIsNumber)
                        .AndSkip(OneOf(divided, secondDateSep))
                        .And(charIsNumber);
                }
            }
            else
            {
                dateDefinition = charIsNumber
                    .AndSkip(secondDateSep)
                    .And(charIsNumber)
                    .AndSkip(secondDateSep)
                    .And(charIsNumber);
            }

            string[] customDateMasks = CreateDateMasks(dateTimeFormat.ShortDatePattern);

            DateTime? ParseDate((TextSpan, TextSpan, TextSpan) date)
            {
                string customDateSepForDT = dateTimeFormat.DateSeparator;

                // A date written with the custom separator '-' is read as the same date written with the custom separator '/':
                // DateTime.TryParse reads "12-31-2024" whatever the date order (as a form of ISO date), and the built-in masks would
                // read it in the order of the culture
                bool hyphenDate = customDateSep == "-" && date.Item1.Buffer is { } buffer
                    && date.Item1.Offset + date.Item1.Length < buffer.Length && buffer[date.Item1.Offset + date.Item1.Length] == '-';
                if (hyphenDate)
                {
                    customDateSepForDT = "/";
                }

                if (useSecondDate || onlyCustomDateTranslation)
                {
                    // The parts in the order of the date pattern of the advanced options; then other forms that the format accepts,
                    // but not a time of day, which DateTime.TryParse also reads where '.' separates both (fi-FI: "10.30.00")
                    if (DateTime.TryParseExact($"{date.Item1}/{date.Item2}/{date.Item3}", customDateMasks, literalDateTimeFormat, DateTimeStyles.None, out var exact))
                        return exact;
                    if (DateTime.TryParse($"{date.Item1}{customDateSepForDT}{date.Item2}{customDateSepForDT}{date.Item3}", hyphenDate ? slashDateTimeFormat : dateTimeFormat, DateTimeStyles.NoCurrentDateDefault, out var result)
                        && result.TimeOfDay == TimeSpan.Zero && result != DateTime.MinValue)
                        return result;
                }
                if ((useSecondDate || !onlyCustomDateTranslation) && !hyphenDate)
                {
                    // The masks follow the order of the parts in the culture of the expression
                    if (DateTime.TryParseExact($"{date.Item1}{builtInDateSep}{date.Item2}{builtInDateSep}{date.Item3}", ncalcDateMasks, literalDateTimeFormat, DateTimeStyles.None, out var result))
                        return result;
                }
                return null;
            }

            // Where the date separator is also the time separator (fi-FI: "."), "10.30.00" may be a time span, and "1.10.30" may be
            // the days and the hours of "1.10.30.00": three numbers are a date only when they make a valid date and the literal ends
            // after them; otherwise they are read as a time span
            // The same holds where '-' is both the custom date and the custom time separator (#10-30-05#)
            bool hyphenDateAndTime = customDateSep == "-" && extOptions?.GetTimeSeparator() == "-";
            Parser<(TextSpan, TextSpan, TextSpan)> dateParts = (builtInDateSep == builtInTimeSep || customDateSep == builtInTimeSep || hyphenDateAndTime)
                ? dateDefinition.When((_, date) => ParseDate(date) is not null).AndSkip(Not(Not(Literals.Char('#'))))
                : dateDefinition;

            // date => number/number/number or custom
            date = dateParts.Then<LogicalExpression>((ctx, date) =>
                ParseDate(date) is DateTime result
                    ? new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx))
                    : throw InvalidLiteralFormat("DateTime", ctx));

            dateDefinitionIso = charIsNumber
                    .AndSkip(hyphenChar)
                    .And(charIsNumber)
                    .AndSkip(hyphenChar)
                    .And(charIsNumber);

            // dateIso => number-number-number['Z']
            // An ISO date starts with a year of four digits; where '-' is also the date or the time separator of the options, anything
            // else (#01-02-2024# in the DMY or MDY order, the time #10-30-00#) is left to the parsers of those separators
            bool hyphenIsSeparator = customDateSep == "-" || extOptions?.GetTimeSeparator() == "-";
            dateIso = dateDefinitionIso.And(ZeroOrOne(Literals.Char('Z'))).When((_, date) => date.Item1.Length == 4 || !hyphenIsSeparator).Then<LogicalExpression>((ctx, date) =>
            {
                if (DateTime.TryParseExact($"{date.Item1}-{date.Item2}-{date.Item3}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                {
                    // Only a date with the 'Z' designator is in UTC
                    if (date.Item4 == 'Z')
                        result = DateTime.SpecifyKind(result, DateTimeKind.Utc);
                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                }

                throw InvalidLiteralFormat("DateTime", ctx);
            });

            Sequence<TextSpan, TextSpan, TextSpan, string>? time12Definition = null;
            Sequence<TextSpan, TextSpan, TextSpan> timeDefinition;
            Sequence<TextSpan, TextSpan, TextSpan, TextSpan> timeDefinitionIso;
            //Sequence<TextSpan, TextSpan, TextSpan, TextSpan> timeDefinitionIsoWithMSec;
            Sequence<string, TextSpan, TextSpan, TextSpan, TextSpan> timeSpanDefinition;
            Sequence<TextSpan, TextSpan, string>? shortTime12Definition = null;
            Sequence<TextSpan, TextSpan> shortTimeDefinition;
            Sequence<string, TextSpan, TextSpan, TextSpan> shortTimeSpanDefinition;

            bool use12HourTime = (extOptions is null) ? dateTimeFormat.ShortTimePattern.Contains("t") : extOptions.Use12HourTime();

            Parser<string>? amTimeIndicator = use12HourTime ? Terms.Text(dateTimeFormat.AMDesignator, true) : null;
            Parser<string>? pmTimeIndicator = use12HourTime ? Terms.Text(dateTimeFormat.PMDesignator, true) : null;

            Parser<string>? amTimeIndicatorFirstChar = null;
            Parser<string>? pmTimeIndicatorFirstChar = null;

            string amTimeFirstChar = string.Empty;
            string pmTimeFirstChar = string.Empty;
            string amTimeFirstCharLower = string.Empty;
            string pmTimeFirstCharLower = string.Empty;

            if (use12HourTime)
            {
                if (!string.IsNullOrEmpty(dateTimeFormat.AMDesignator))
                {
                    amTimeFirstChar = dateTimeFormat.AMDesignator[..1];
                    amTimeFirstCharLower = dateTimeFormat.AMDesignator[..1].ToLower();

                    amTimeIndicatorFirstChar = Terms.Text(amTimeFirstChar, true);
                }
                if (!string.IsNullOrEmpty(dateTimeFormat.PMDesignator))
                {
                    pmTimeFirstChar = dateTimeFormat.PMDesignator[..1];
                    pmTimeFirstCharLower = dateTimeFormat.PMDesignator[..1].ToLower();
                    pmTimeIndicatorFirstChar = Terms.Text(pmTimeFirstChar, true);
                }

                ncalcDateTime12Masks[0] = string.Join(" ", ncalcDateMasks[0], "h:m:s t");
                ncalcDateTime12Masks[1] = string.Join(" ", ncalcDateMasks[1], "h:m:s t");
                ncalcDateTime12Masks[2] = string.Join(" ", ncalcDateMasks[0], "h:m:s tt");
                ncalcDateTime12Masks[3] = string.Join(" ", ncalcDateMasks[1], "h:m:s tt");
                ncalcDateShortTime12Masks[0] = string.Join(" ", ncalcDateMasks[0], "h:m t");
                ncalcDateShortTime12Masks[1] = string.Join(" ", ncalcDateMasks[1], "h:m t");
                ncalcDateShortTime12Masks[2] = string.Join(" ", ncalcDateMasks[0], "h:m tt");
                ncalcDateShortTime12Masks[3] = string.Join(" ", ncalcDateMasks[1], "h:m tt");
            }

            bool useSecondTime = false;
            bool onlyCustomTimeTranslation = false;
            string customTimeSep = builtInTimeSep;

            if (extOptions is not null)
            {
                customTimeSep = extOptions.GetTimeSeparator();
                if (customTimeSep != builtInTimeSep && !extOptions.Flags.HasFlag(AdvExpressionOptions.SkipBuiltInTimeSeparator))
                {
                    useSecondTime = true; // we use the second time separator when both custom separator and the default one are enabled and are different
                }
                else
                if (customTimeSep == builtInTimeSep)
                {
                    onlyCustomTimeTranslation = true;
                }
            }

            var secondTimeSep = Terms.Text(customTimeSep); // this may be a custom separator or ":"
            var colonText = Terms.Text(":");
            Parser<string> timeSpanSeparator;
            if (useSecondTime)
            {
                if (customTimeSep.Contains(' '))
                {
                    // If the time separator by chance contains spaces, we need to let people enter both "10:10:00" and "10: 10: 00"
                    // And for this, we use a third separator - a trimmed version of the one we have from the culture info.
                    var thirdTimeSep = Terms.Text(customTimeSep.Trim());
                    if (use12HourTime)
                    {
                        time12Definition = charIsNumber
                            .AndSkip(OneOf(divided, secondTimeSep, thirdTimeSep))
                            .And(charIsNumber)
                            .AndSkip(OneOf(divided, secondTimeSep, thirdTimeSep))
                            .And(OneOf(charIsNumber, charIsNumberWithWhitespace))
                            .And(OneOf(amTimeIndicator!, pmTimeIndicator!, amTimeIndicatorFirstChar!, pmTimeIndicatorFirstChar!));
                        shortTime12Definition = charIsNumber
                            .AndSkip(OneOf(divided, secondTimeSep, thirdTimeSep))
                            .And(OneOf(charIsNumber, charIsNumberWithWhitespace))
                            .And(OneOf(amTimeIndicator!, pmTimeIndicator!, amTimeIndicatorFirstChar!, pmTimeIndicatorFirstChar!));
                    }

                    timeDefinition = charIsNumber
                        .AndSkip(OneOf(divided, secondTimeSep, thirdTimeSep))
                        .And(charIsNumber)
                        .AndSkip(OneOf(divided, secondTimeSep, thirdTimeSep))
                        .And(charIsNumber);
                    shortTimeDefinition = charIsNumber
                        .AndSkip(OneOf(divided, secondTimeSep, thirdTimeSep))
                        .And(charIsNumber);
                    timeSpanSeparator = OneOf(divided, secondTimeSep, thirdTimeSep, colonText);
                }
                else
                {
                    if (use12HourTime)
                    {
                        time12Definition = charIsNumber
                            .AndSkip(OneOf(divided, secondTimeSep))
                            .And(charIsNumber)
                            .AndSkip(OneOf(divided, secondTimeSep))
                            .And(OneOf(charIsNumber, charIsNumberWithWhitespace))
                            .And(OneOf(amTimeIndicator!, pmTimeIndicator!, amTimeIndicatorFirstChar!, pmTimeIndicatorFirstChar!));
                        shortTime12Definition = charIsNumber
                            .AndSkip(OneOf(divided, secondTimeSep))
                            .And(OneOf(charIsNumber, charIsNumberWithWhitespace))
                            .And(OneOf(amTimeIndicator!, pmTimeIndicator!, amTimeIndicatorFirstChar!, pmTimeIndicatorFirstChar!));
                    }

                    timeDefinition = charIsNumber
                        .AndSkip(OneOf(divided, secondTimeSep))
                        .And(charIsNumber)
                        .AndSkip(OneOf(divided, secondTimeSep))
                        .And(charIsNumber);

                    shortTimeDefinition = charIsNumber
                        .AndSkip(OneOf(divided, secondTimeSep))
                        .And(charIsNumber);

                    timeSpanSeparator = OneOf(divided, secondTimeSep, colonText);
                }
            }
            else
            {
                if (use12HourTime)
                {
                    time12Definition = charIsNumber
                        .AndSkip(secondTimeSep)
                        .And(charIsNumber)
                        .AndSkip(secondTimeSep)
                        .And(OneOf(charIsNumber, charIsNumberWithWhitespace))
                        .And(OneOf(amTimeIndicator!, pmTimeIndicator!, amTimeIndicatorFirstChar!, pmTimeIndicatorFirstChar!));
                    shortTime12Definition = charIsNumber
                        .AndSkip(secondTimeSep)
                        .And(OneOf(charIsNumber, charIsNumberWithWhitespace))
                        .And(OneOf(amTimeIndicator!, pmTimeIndicator!, amTimeIndicatorFirstChar!, pmTimeIndicatorFirstChar!));
                }

                timeDefinition = charIsNumber
                    .AndSkip(secondTimeSep)
                    .And(charIsNumber)
                    .AndSkip(secondTimeSep)
                    .And(charIsNumber);

                shortTimeDefinition = charIsNumber
                    .AndSkip(secondTimeSep)
                    .And(charIsNumber);

                timeSpanSeparator = customTimeSep == ":" ? secondTimeSep : OneOf(secondTimeSep, colonText);
            }

            // timeSpan => [[-]number.]number:number:number
            // A time span always accepts ':' (ToString() writes #hh:mm:ss#), also where the time separator of the culture is '.' (fi-FI).
            // The days ("1." in #1.10:30:00#) are taken only when the rest of the time span follows them, so that #10.30.00# is not
            // read as 10 days followed by an incomplete time
            var timeSpanRest = charIsNumber.AndSkip(timeSpanSeparator).And(charIsNumber).AndSkip(timeSpanSeparator).And(charIsNumber);
            var shortTimeSpanRest = charIsNumber.AndSkip(timeSpanSeparator).And(charIsNumber);

            timeSpanDefinition = ZeroOrOne(minus).And(ZeroOrOne(charIsNumber.AndSkip(dotChar).AndSkip(Not(Not(timeSpanRest))))).And(charIsNumber)
                .AndSkip(timeSpanSeparator)
                .And(charIsNumber)
                .AndSkip(timeSpanSeparator)
                .And(charIsNumber);

            shortTimeSpanDefinition = ZeroOrOne(minus).And(ZeroOrOne(charIsNumber.AndSkip(dotChar).AndSkip(Not(Not(shortTimeSpanRest))))).And(charIsNumber)
                .AndSkip(timeSpanSeparator)
                .And(charIsNumber);

            timeDefinitionIso = charIsNumber
                    .AndSkip(colon)
                    .And(charIsNumber)
                    .AndSkip(colon)
                    .And(charIsNumber).And(ZeroOrOne(dotChar.SkipAnd(charIsNumber)));

            Parser<LogicalExpression>? time12 = null;
            Parser<LogicalExpression>? shortTime12 = null;

            // A time span may have a fraction of a second (#00:00:01.5#), which is how ToString() writes one
            var time = timeSpanDefinition.And(ZeroOrOne(Literals.Char('.').SkipAnd(charIsNumber))).Then<LogicalExpression>((ctx, time) =>
            {
                string customTimeSepForDT = timeDateTimeFormat.TimeSeparator;
                if (useSecondTime || onlyCustomTimeTranslation)
                {
                    if (DateTime.TryParse($"{time.Item3}{customTimeSepForDT}{time.Item4}{customTimeSepForDT}{time.Item5}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                    {
                        TimeSpan tsResult = result.TimeOfDay + GetSecondFraction(time.Item6);

                        if (time.Item2.Length > 0)
                        {
                            int days = Int32.Parse(time.Item2.Span.ToString());
                            tsResult = tsResult.Add(TimeSpan.FromDays(days));
                        }
                        if (time.Item1 == "-")
                        {
                            tsResult = tsResult.Negate();
                        }
                        return new ValueExpression(tsResult).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                }
                // The parts are already split at the separator, so they are read the same way on every path: a time span literal
                // may have 24 or more hours (#25:00:00#), which DateTime.TryParse rejects
                {
                    if (TryCreateTimeSpan(time.Item3.ToString(), time.Item4.ToString(), time.Item5.ToString(), time.Item2.Length > 0, out var result))
                    {
                        TimeSpan tsResult = result + GetSecondFraction(time.Item6);

                        if (time.Item2.Length > 0)
                        {
                            int days = Int32.Parse(time.Item2.Span.ToString());
                            tsResult = tsResult.Add(TimeSpan.FromDays(days));
                        }
                        if (time.Item1 == "-")
                        {
                            tsResult = tsResult.Negate();
                        }
                        return new ValueExpression(tsResult).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                }

                // Where the date separator is also the time separator (fi-FI: "."), an invalid date (#29.02.2023#) comes here:
                // three parts with a year of four digits are reported as a date
                bool hasShapeOfDate = (builtInDateSep == builtInTimeSep || customDateSep == builtInTimeSep)
                    && time.Item1 != "-" && time.Item2.Length == 0 && time.Item6.Length == 0 && time.Item5.Length == 4;
                throw InvalidLiteralFormat(hasShapeOfDate ? "DateTime" : "TimeSpan", ctx);
            });

            var shortTime = shortTimeSpanDefinition.Then<LogicalExpression>((ctx, time) =>
            {
                string customTimeSepForDT = timeDateTimeFormat.TimeSeparator;
                if (useSecondTime || onlyCustomTimeTranslation)
                {
                    if (DateTime.TryParse($"{time.Item3}{customTimeSepForDT}{time.Item4}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                    {
                        TimeSpan tsResult = result.TimeOfDay;

                        if (time.Item2.Length > 0)
                        {
                            int days = Int32.Parse(time.Item2.Span.ToString());
                            tsResult = tsResult.Add(TimeSpan.FromDays(days));
                        }
                        if (time.Item1 == "-")
                        {
                            tsResult  = TimeSpan.FromMilliseconds(-tsResult.TotalMilliseconds);
                        }
                        return new ValueExpression(tsResult).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                }
                // The parts are already split at the separator, so they are read the same way on every path: a time span literal
                // may have 24 or more hours (#25:00:00#), which DateTime.TryParse rejects
                {
                    if (TryCreateTimeSpan(time.Item3.ToString(), time.Item4.ToString(), null, time.Item2.Length > 0, out var result))
                    {
                        TimeSpan tsResult = result;

                        if (time.Item2.Length > 0)
                        {
                            int days = Int32.Parse(time.Item2.Span.ToString());
                            tsResult = tsResult.Add(TimeSpan.FromDays(days));
                        }
                        if (time.Item1 == "-")
                        {
                            tsResult = TimeSpan.FromMilliseconds(-tsResult.TotalMilliseconds);
                        }
                        return new ValueExpression(tsResult).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                }

                throw InvalidLiteralFormat("TimeSpan", ctx);
            });

            if (use12HourTime)
            {
                string customTimeSepForDT = timeDateTimeFormat.TimeSeparator;
                string amSpacer = "";
                if (dateTimeFormat.ShortTimePattern.Contains(" t"))
                    amSpacer = " ";

                time12 = time12Definition!.Then<LogicalExpression>((ctx, time) =>
                {
                    string amPMValue = time.Item4;
                    if (amPMValue.ToLower().Equals(amTimeFirstCharLower))
                        amPMValue = dateTimeFormat.AMDesignator;
                    else
                    if (amPMValue.ToLower().Equals(pmTimeFirstCharLower))
                        amPMValue = dateTimeFormat.PMDesignator;

                    if (useSecondTime || onlyCustomTimeTranslation)
                    {
                        if (DateTime.TryParse($"{time.Item1}{customTimeSepForDT}{time.Item2}{customTimeSepForDT}{time.Item3}{amSpacer}{amPMValue}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                        {
                            return new ValueExpression(result.TimeOfDay).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                    }
                    if (useSecondTime || !onlyCustomTimeTranslation)
                    {
                        // Use the existing ncalc approach with the current culture
                        if (TimeSpan.TryParse($"{time.Item1}{builtInTimeSep}{time.Item2}{builtInTimeSep}{time.Item3}{amSpacer}{amPMValue}", out var result))
                        {
                            return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                    }

                    throw InvalidLiteralFormat("TimeSpan", ctx);
                });

                shortTime12 = shortTime12Definition!.Then<LogicalExpression>((ctx, time) =>
                {
                    string customTimeSepForDT = timeDateTimeFormat.TimeSeparator;
                    string amPMValue = time.Item3;
                    if (amPMValue.ToLower().Equals(amTimeFirstCharLower))
                        amPMValue = dateTimeFormat.AMDesignator;
                    else
                    if (amPMValue.ToLower().Equals(pmTimeFirstCharLower))
                        amPMValue = dateTimeFormat.PMDesignator;

                    if (useSecondTime || onlyCustomTimeTranslation)
                    {
                        if (DateTime.TryParse($"{time.Item1}{customTimeSepForDT}{time.Item2}{amSpacer}{amPMValue}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                        {
                            return new ValueExpression(result.TimeOfDay).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                    }
                    if (useSecondTime || !onlyCustomTimeTranslation)
                    {
                        if (TimeSpan.TryParse($"{time.Item1}{builtInTimeSep}{time.Item2}{amSpacer}{amPMValue}", out var result))
                        {
                            return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                        }
                    }

                    throw InvalidLiteralFormat("TimeSpan", ctx);
                });
            }

            // dateAndTime => number/number/number number:number:number or custom
            var dateAndTime = dateDefinition.AndSkip(Literals.WhiteSpace()).And(timeDefinition).Then<LogicalExpression>((ctx, dateTime) =>
                {
                    string customDateSepForDT = dateTimeFormat.DateSeparator;
                    string customTimeSepForDT = timeDateTimeFormat.TimeSeparator;
                    if (useSecondDate || onlyCustomDateTranslation)
                    {
                        if (useSecondTime || onlyCustomTimeTranslation)
                        {
                            if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{customTimeSepForDT}{dateTime.Item4.Item3}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }
                        if (useSecondTime || !onlyCustomTimeTranslation)
                        {
                            if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2}{builtInTimeSep}{dateTime.Item4.Item3}", dateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }
                    }
                    if (useSecondDate || !onlyCustomDateTranslation)
                    {
                        if (useSecondTime || onlyCustomTimeTranslation)
                        {
                            if (DateTime.TryParse($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{customTimeSepForDT}{dateTime.Item4.Item3}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }

                        if (useSecondTime || !onlyCustomTimeTranslation)
                        {
                            // Use the existing approach
                            if (DateTime.TryParseExact($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2}{builtInTimeSep}{dateTime.Item4.Item3}", ncalcDateTimeMasks, literalDateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }
                    }

                    throw InvalidLiteralFormat("DateTime", ctx);
                });

            // dateAndTimeIso => number-number-number'T'number:number:number[.number]['Z']
            var dateAndTimeIso = dateDefinitionIso.AndSkip(Terms.Char('T')).And(timeDefinitionIso).And(ZeroOrOne(Literals.Char('Z'))).Then<LogicalExpression>((ctx, dateTime) =>
                {
                    int year = -1;
                    int month = -1;
                    int day = -1;
                    int hour = -1;
                    int minute = -1;
                    int second = -1;
                    long fractionTicks = 0;

                    int.TryParse(dateTime.Item1.ToString(), out year);
                    int.TryParse(dateTime.Item2.ToString(), out month);
                    int.TryParse(dateTime.Item3.ToString(), out day);
                    int.TryParse(dateTime.Item4.Item1.ToString(), out hour);
                    int.TryParse(dateTime.Item4.Item2.ToString(), out minute);
                    int.TryParse(dateTime.Item4.Item3.ToString(), out second);

                    // The fraction of a second has up to 7 digits (ticks), as in the round-trip format "o"; further digits are below the tick resolution
                    string fraction = dateTime.Item4.Item4.ToString();
                    if (fraction.Length > 0)
                        fractionTicks = long.Parse(fraction.PadRight(7, '0').Substring(0, 7), CultureInfo.InvariantCulture);

                    try
                    {
                        // As in ISO 8601, a time without the 'Z' designator is not in UTC
                        DateTime result = new DateTime(year, month, day, hour, minute, second, dateTime.Item5 == 'Z' ? DateTimeKind.Utc : DateTimeKind.Unspecified).AddTicks(fractionTicks);
                        return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                    catch
                    {
                        throw InvalidLiteralFormat("DateTime", ctx);
                    }
                });

            var dateAndShortTime = dateDefinition.AndSkip(Literals.WhiteSpace()).And(shortTimeDefinition).Then<LogicalExpression>((ctx, dateTime) =>
                {
                    string customDateSepForDT = dateTimeFormat.DateSeparator;
                    string customTimeSepForDT = timeDateTimeFormat.TimeSeparator;
                    if (useSecondDate || onlyCustomDateTranslation)
                    {
                        if (useSecondTime || onlyCustomTimeTranslation)
                        {
                            if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }
                        if (useSecondTime || !onlyCustomTimeTranslation)
                        {
                            if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2}", dateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }
                    }
                    if (useSecondDate || !onlyCustomDateTranslation)
                    {
                        if (useSecondTime || onlyCustomTimeTranslation)
                        {
                            if (DateTime.TryParse($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }

                        if (useSecondTime || !onlyCustomTimeTranslation)
                        {
                            // Use the existing approach
                            if (DateTime.TryParseExact($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2}", ncalcDateShortTimeMasks, literalDateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }
                    }

                    throw InvalidLiteralFormat("DateTime", ctx);
                });

            Parser<LogicalExpression>? dateAndTime12 = null;
            Parser<LogicalExpression>? dateAndShortTime12 = null;

            if (use12HourTime)
            {
                // if there is a space expected before A/P or am/pm, we need to add it to the expression
                string amSpacer = "";
                if (dateTimeFormat.ShortTimePattern.Contains(" t"))
                    amSpacer = " ";

                dateAndTime12 = dateDefinition.AndSkip(Literals.WhiteSpace()).And(time12Definition!).Then<LogicalExpression>((ctx, dateTime) =>
                    {
                        string customDateSepForDT = dateTimeFormat.DateSeparator;
                        string customTimeSepForDT = timeDateTimeFormat.TimeSeparator;
                        string amPMValue = dateTime.Item4.Item4;
                        if (amPMValue.ToLower().Equals(amTimeFirstCharLower))
                            amPMValue = dateTimeFormat.AMDesignator;
                        else
                        if (amPMValue.ToLower().Equals(pmTimeFirstCharLower))
                            amPMValue = dateTimeFormat.PMDesignator;

                        if (useSecondDate || onlyCustomDateTranslation)
                        {
                            if (useSecondTime || onlyCustomTimeTranslation)
                            {
                                if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{customTimeSepForDT}{dateTime.Item4.Item3}{amSpacer}{amPMValue}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }
                            if (useSecondTime || !onlyCustomTimeTranslation)
                            {
                                if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}:{dateTime.Item4.Item2}:{dateTime.Item4.Item3}{amSpacer}{amPMValue}", dateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }
                        }
                        if (useSecondDate || !onlyCustomDateTranslation)
                        {
                            if (useSecondTime || onlyCustomTimeTranslation)
                            {
                                if (DateTime.TryParse($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{customTimeSepForDT}{dateTime.Item4.Item3}{amSpacer}{amPMValue}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }

                            if (useSecondTime || !onlyCustomTimeTranslation)
                            {
                                // Use the existing approach
                                if (DateTime.TryParseExact($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2}{builtInTimeSep}{dateTime.Item4.Item3} {amPMValue}", ncalcDateTime12Masks, literalDateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }
                        }

                        throw InvalidLiteralFormat("DateTime", ctx);
                    });

                dateAndShortTime12 = dateDefinition.AndSkip(Literals.WhiteSpace()).And(shortTime12Definition!).Then<LogicalExpression>((ctx, dateTime) =>
                    {
                        string customDateSepForDT = dateTimeFormat.DateSeparator;
                        string customTimeSepForDT = timeDateTimeFormat.TimeSeparator;
                        string amPMValue = dateTime.Item4.Item3;
                        if (amPMValue.ToLower().Equals(amTimeFirstCharLower))
                            amPMValue = dateTimeFormat.AMDesignator;
                        else
                        if (amPMValue.ToLower().Equals(pmTimeFirstCharLower))
                            amPMValue = dateTimeFormat.PMDesignator;

                        if (useSecondDate || onlyCustomDateTranslation)
                        {
                            if (useSecondTime || onlyCustomTimeTranslation)
                            {
                                if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{amSpacer}{amPMValue}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }
                            if (useSecondTime || !onlyCustomTimeTranslation)
                            {
                                if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2}{amSpacer}{amPMValue}", dateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }
                        }
                        if (useSecondDate || !onlyCustomDateTranslation)
                        {
                            if (useSecondTime || onlyCustomTimeTranslation)
                            {
                                if (DateTime.TryParse($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{amSpacer}{amPMValue}", timeDateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }

                            if (useSecondTime || !onlyCustomTimeTranslation)
                            {
                                // Use the existing approach
                                if (DateTime.TryParseExact($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2} {amPMValue}", ncalcDateShortTime12Masks, literalDateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }
                        }

                        throw InvalidLiteralFormat("DateTime", ctx);
                    });
            }

            Parser<LogicalExpression>? humaneTimeSpan = null;

            if (extOptions?.Flags.HasFlag(AdvExpressionOptions.ParseHumanePeriods) == true)
            {
                Parser<string>? alphaText = Terms.Pattern(c => char.IsLetter(c) || c == '\'').Then<string>(x => x.ToString() ?? string.Empty);

                var intNumberForPeriod = CheckedNumber(Terms.Number<int>(NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag, decimalSeparator, numGroupSeparator), intUnderscoreCheck)
                    .AndSkip(Not(OneOf(floatNumExclusions)))
                    .Then<int>(d => d);

                humaneTimeSpan = ZeroOrOne(alphaText).And(ZeroOrMany(intNumberForPeriod.And(alphaText.AndSkip(ZeroOrOne(Terms.Char('.')))))).And(ZeroOrOne(alphaText)).Then<LogicalExpression>((ctx, val) =>
                {
                    string indicator;
                    int elemValue;
                    int yearValue = 0;
                    int monthValue = 0;
                    int weekValue = 0;
                    int dayValue = 0;
                    int hourValue = 0;
                    int minuteValue = 0;
                    int secondValue = 0;
                    int msecValue = 0;

                    string? prefix = val.Item1;
                    string? suffix = val.Item3;

                    for (int i = 0; i < val.Item2.Count; i++)
                    {
                        var entry = val.Item2[i];
                        elemValue = entry.Item1;
                        indicator = entry.Item2;

                        if (string.IsNullOrEmpty(indicator))
                            throw new Exception(string.Format(errFailedToParsePeriodIndicator, entry.ToString()));

                        indicator = indicator.ToLowerInvariant();
                        if (extOptions.PeriodYearIndicators.Contains(indicator))
                        {
                            if (yearValue != 0)
                                throw new FormatException(string.Format(errDuplicatePeriodIndicator, entry.Item2));
                            yearValue = elemValue;
                        }
                        else
                        if (extOptions.PeriodMonthIndicators.Contains(indicator))
                        {
                            if (monthValue != 0)
                                throw new FormatException(string.Format(errDuplicatePeriodIndicator, entry.Item2));
                            monthValue = elemValue;
                        }
                        else
                        if (extOptions.PeriodWeekIndicators.Contains(indicator))
                        {
                            if (weekValue != 0)
                                throw new FormatException(string.Format(errDuplicatePeriodIndicator, entry.Item2));
                            weekValue = elemValue;
                        }
                        else
                        if (extOptions.PeriodDayIndicators.Contains(indicator))
                        {
                            if (dayValue != 0)
                                throw new FormatException(string.Format(errDuplicatePeriodIndicator, entry.Item2));
                            dayValue = elemValue;
                        }
                        else
                        if (extOptions.PeriodHourIndicators.Contains(indicator))
                        {
                            if (hourValue != 0)
                                throw new FormatException(string.Format(errDuplicatePeriodIndicator, entry.Item2));
                            hourValue = elemValue;
                        }
                        else
                        if (extOptions.PeriodMinuteIndicators.Contains(indicator))
                        {
                            if (minuteValue != 0)
                                throw new FormatException(string.Format(errDuplicatePeriodIndicator, entry.Item2));
                            minuteValue = elemValue;
                        }
                        else
                        if (extOptions.PeriodSecondIndicators.Contains(indicator))
                        {
                            if (secondValue != 0)
                                throw new FormatException(string.Format(errDuplicatePeriodIndicator, entry.Item2));
                            secondValue = elemValue;
                        }
                        else
                        if (extOptions.PeriodMSecIndicators.Contains(indicator))
                        {
                            if (msecValue != 0)
                                throw new FormatException(string.Format(errDuplicatePeriodIndicator, entry.Item2));
                            msecValue = elemValue;
                        }
                        else
                        {
                            throw new FormatException(string.Format(errUnrecognizedPeriodIndicator, entry.Item2));
                        }
                    }

                    // The text of the literal is kept, so that ToString() writes it as it was written (with the same indicator words)
                    var text = new StringBuilder();
                    if (!string.IsNullOrEmpty(prefix))
                        text.Append(prefix);
                    foreach (var entry in val.Item2)
                    {
                        if (text.Length > 0)
                            text.Append(' ');
                        text.Append(entry.Item1.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(entry.Item2);
                    }
                    if (!string.IsNullOrEmpty(suffix))
                    {
                        if (text.Length > 0)
                            text.Append(' ');
                        text.Append(suffix);
                    }

                    var relative = new RelativeDateTime
                    {
                        Text = text.ToString(),
                        Years = yearValue,
                        Months = monthValue,
                        Weeks = weekValue,
                        Days = dayValue,
                        Hours = hourValue,
                        Minutes = minuteValue,
                        Seconds = secondValue,
                        Milliseconds = msecValue,
                    };

                    // The values depend on the current time, so they are computed whenever the expression is evaluated, not when it is parsed
                    if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix))
                    {
                        relative.Kind = RelativeDateTimeKind.Period;
                    }
                    else
                    {
                        if (!(string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix)))
                        {
                            throw new FormatException(string.Format(errDuplicateTimeRelationIndicators, prefix, suffix));
                        }

                        prefix = prefix?.ToLowerInvariant();
                        suffix = suffix?.ToLowerInvariant();

                        // people are interested in local time now, today, before, or after current moment
                        if (prefix is not null && extOptions.PeriodNowIndicators.Contains(prefix))
                            relative.Kind = RelativeDateTimeKind.Now;
                        else
                        if (prefix is not null && extOptions.PeriodTodayIndicators.Contains(prefix))
                            relative.Kind = RelativeDateTimeKind.Today;
                        else
                        if (prefix is not null && extOptions.PeriodYesterdayIndicators.Contains(prefix))
                            relative.Kind = RelativeDateTimeKind.Yesterday;
                        else
                        if (prefix is not null && extOptions.PeriodTomorrowIndicators.Contains(prefix))
                            relative.Kind = RelativeDateTimeKind.Tomorrow;
                        else
                        if ((prefix is not null && extOptions.PeriodPastIndicators.Contains(prefix)) || (suffix is not null && extOptions.PeriodPastIndicators.Contains(suffix)))
                            relative.Kind = RelativeDateTimeKind.Past;
                        else
                        if ((prefix is not null && extOptions.PeriodFutureIndicators.Contains(prefix)) || (suffix is not null && extOptions.PeriodFutureIndicators.Contains(suffix)))
                            relative.Kind = RelativeDateTimeKind.Future;
                        else
                        {
                            throw new FormatException(string.Format(errUnrecognizedTimeRelationIndicator, prefix));
                        }
                    }

                    return ValueExpression.CreateRelative(relative).SetLocation(new ParlotExpressionLocation(ctx));
                });
            }
            List<Parser<LogicalExpression>> timeParts = use12HourTime
                ? [dateAndTimeIso, dateAndTime12!, dateAndShortTime12!, dateAndTime, dateAndShortTime, dateIso, date, time12!, shortTime12!, time, shortTime]
                : [dateAndTimeIso, dateAndTime, dateAndShortTime, dateIso, date, time, shortTime];

            if (humaneTimeSpan is not null)
                timeParts.Add(humaneTimeSpan);

            // datetime => '#' dateAndTime | date | shortTime | time  '#';
            dateTime = Terms
                .Char('#')
                .SkipAnd(OneOf(timeParts.ToArray()))
                .AndSkip(Literals.Char('#'));

            if (options.HasFlag(ExpressionOptions.SupportPythonComments))
                dateTimeAtHash = Literals.Char('#').SkipAnd(OneOf(timeParts.ToArray())).AndSkip(Literals.Char('#'));
        }

        var isHexDigit = Character.IsHexDigit;

        Parser<LogicalExpression>? guid = null;

        if (!options.HasFlag(ExpressionOptions.DontParseGuids))
        {
            // Only the first group may follow whitespace; the hyphens and the other groups follow directly, so "a - b - c - d - e"
            // stays a subtraction, and the GUID must be a whole token
            var eightHexSequence = Terms
                .Pattern(isHexDigit, 8, 8);

            var guidHyphen = Literals.Char('-');

            var fourHexSequence = Literals
                .Pattern(isHexDigit, 4, 4);

            var twelveHexSequence = Literals
                .Pattern(isHexDigit, 12, 12);

            var thirtyTwoHexSequence = Terms
                .Pattern(isHexDigit, 32, 32);

            var guidWithHyphens = eightHexSequence
                    .AndSkip(guidHyphen)
                    .And(fourHexSequence)
                    .AndSkip(guidHyphen)
                    .And(fourHexSequence)
                    .AndSkip(guidHyphen)
                    .And(fourHexSequence)
                    .AndSkip(guidHyphen)
                    .And(twelveHexSequence)
                    .AndSkip(Not(identifierPartChar))
                .Then<LogicalExpression>(static (ctx, g) =>
                        new ValueExpression(Guid.Parse(g.Item1.ToString() + g.Item2 + g.Item3 + g.Item4 + g.Item5)).SetLocation(new ParlotExpressionLocation(ctx)));

            Parser<LogicalExpression> guidWithoutHyphens;

            // 32 hex characters without hyphens are a GUID only when they mix decimal digits and letters:
            // all digits are a number, and all letters are an identifier
            // Digits with a single 'e' (100…0e1, or 100…0e followed by "+1") are a number with an exponent.
            // A run that starts with a letter stays a GUID (e.g. deadbeefcafebabeface0123456789ab); an identifier of this shape
            // can be written in brackets
            guidWithoutHyphens = thirtyTwoHexSequence
                .AndSkip(Not(identifierPartChar))
                .When(static (_, g) => g.Span.IndexOfAny("0123456789".AsSpan()) >= 0 && g.Span.IndexOfAny("abcdefABCDEF".AsSpan()) >= 0
                    && !IsNumberWithExponent(g.Span))
                .Then<LogicalExpression>(static (ctx, g) => new ValueExpression(Guid.Parse(g.ToString()!)).SetLocation(new ParlotExpressionLocation(ctx)));

            guid = OneOf(guidWithHyphens, guidWithoutHyphens);
        }

        if (literalsOnly)
        {
            List<Parser<LogicalExpression>> literalParsers = [];
            if (guid is not null)
                literalParsers.Add(guid);
            literalParsers.Add(hexOctBinNumber);
            if (currency is not null)
                literalParsers.Add(currency);
            literalParsers.Add(intNumber);
            literalParsers.Add(longNumber);
            if (bigIntNumber is not null)
                literalParsers.Add(bigIntNumber);
            literalParsers.Add(decimalOrDoubleNumber);
            literalParsers.Add(booleanTrue);
            literalParsers.Add(booleanFalse);
            if (dateTime is not null)
                literalParsers.Add(dateTime);

            // Number parsers accept a sign themselves; the explicit one serves the other literals (e.g. hex numbers), and only one sign is allowed
            var literal = OneOf(literalParsers.ToArray());
            var noSecondSign = Not(OneOf(minus, plus));
            var signedLiteral = OneOf(
                minus.AndSkip(noSecondSign).SkipAnd(literal).Then<LogicalExpression>(static (ctx, x) => CreateNegation(ctx, x)),
                plus.AndSkip(noSecondSign).SkipAnd(literal),
                literal);

            return signedLiteral.AndSkip(ZeroOrMany(new WhiteSpaceParser(options))).Eof();
        }

        // primary => GUID | Percent | NUMBER | identifier | DateTime | string | resultReference | function | boolean | groupExpression | identifier | list ;

        List<Parser<LogicalExpression>> enabledParsers = [];

        if (guid is not null)
            enabledParsers.Add(guid);
        enabledParsers.Add(hexOctBinNumber);
        if (currency is not null)
            enabledParsers.Add(currency);
        enabledParsers.Add(intNumber);
        enabledParsers.Add(longNumber);
        if (bigIntNumber is not null)
            enabledParsers.Add(bigIntNumber);
        enabledParsers.Add(decimalOrDoubleNumber);
        enabledParsers.Add(booleanTrue);
        enabledParsers.Add(booleanFalse);
        if (dateTime is not null) // dateTime will be initialized unless options.HasFlag(ExpressionOptions.DontParseDates)
            enabledParsers.Add(dateTime);
        enabledParsers.Add(stringValue);
        if (useVectors)
            enabledParsers.Add(vector);
        enabledParsers.Add(functionOrResultRef);
        enabledParsers.Add(groupExpression);
        enabledParsers.Add(bracketedIdentifierExpression);
        if (imaginaryCharExpression is not null)
            enabledParsers.Add(imaginaryCharExpression);
        enabledParsers.Add(identifierExpression);
        enabledParsers.Add(list);
        enabledParsers.Add(bracedExpressionOrStatementSequence);

        var primary = OneOf(enabledParsers.ToArray());

        Parser<LogicalExpression> indexedAccess = primary.And(ZeroOrOne(rangedIndex))
            .Then((ctx, x) =>
            {
                // x.Item2 contains [^][lowerBound][..][^][upperBound]
                if (x.Item2.Item2 is null && x.Item2.Item3 is null && x.Item2.Item5 is null)
                {
                    // there is just a primary discovered
                    return x.Item1;
                }

                if (x.Item2.Item2 is null && x.Item2.Item3 is not null && x.Item2.Item5 is null) // [..] is the whole value
                {
                    return new BinaryExpression(
                        BinaryExpressionType.IndexAccess,
                        x.Item1,
                        new BinaryExpression(BinaryExpressionType.RangeIndex, new ValueExpression(), new ValueExpression())
                    ).SetLocation(new ParlotExpressionLocation(ctx));
                }

                if (x.Item2.Item2 is not null && x.Item2.Item3 is null && x.Item2.Item5 is null) // only the first index is available
                {
                    // The first and only index is from end
                    if (x.Item2.Item1 is not null)
                        return new BinaryExpression(BinaryExpressionType.IndexAccess, x.Item1, new UnaryExpression(UnaryExpressionType.FromEnd, x.Item2.Item2)).SetLocation(new ParlotExpressionLocation(ctx));

                    return new BinaryExpression(BinaryExpressionType.IndexAccess, x.Item1, x.Item2.Item2).SetLocation(new ParlotExpressionLocation(ctx));
                }
                else
                if (x.Item2.Item2 is not null && x.Item2.Item3 is not null && x.Item2.Item5 is null) // the last index is missing
                {
                    return new BinaryExpression(
                        BinaryExpressionType.IndexAccess,
                        x.Item1,
                        new BinaryExpression(
                            BinaryExpressionType.RangeIndex,
                            (x.Item2.Item1 is not null)
                                ? new UnaryExpression(UnaryExpressionType.FromEnd, x.Item2.Item2)
                                : x.Item2.Item2,
                            new ValueExpression())
                    ).SetLocation(new ParlotExpressionLocation(ctx));
                }
                else
                if (x.Item2.Item2 is null && x.Item2.Item3 is not null && x.Item2.Item5 is not null) // the first index is missing
                {
                    return new BinaryExpression(
                        BinaryExpressionType.IndexAccess,
                        x.Item1,
                        new BinaryExpression(
                            BinaryExpressionType.RangeIndex,
                            new ValueExpression(),
                            (x.Item2.Item4 is not null)
                                ? new UnaryExpression(UnaryExpressionType.FromEnd, x.Item2.Item5)
                                : x.Item2.Item5)
                    ).SetLocation(new ParlotExpressionLocation(ctx));
                }
                else
                if (x.Item2.Item2 is not null && x.Item2.Item3 is not null && x.Item2.Item5 is not null) // both indices are present
                {
                    return new BinaryExpression(
                        BinaryExpressionType.IndexAccess,
                        x.Item1,
                        new BinaryExpression(
                            BinaryExpressionType.RangeIndex,
                            (x.Item2.Item1 is not null)
                                ? new UnaryExpression(UnaryExpressionType.FromEnd, x.Item2.Item2)
                                : x.Item2.Item2,
                            (x.Item2.Item4 is not null)
                                ? new UnaryExpression(UnaryExpressionType.FromEnd, x.Item2.Item5)
                                : x.Item2.Item5)).SetLocation(new ParlotExpressionLocation(ctx));
                }
                else
                {
                    throw new NCalcParserException("Ranged index could not be parsed", ctx.Scanner.Cursor.Position);
                }
            });

        // Several alternatives start with the same operand: a parenthesized expression and a list (whose first item is parsed
        // again when the group fails, e.g. at a separator or at a literal that is not a valid date), and an assignment target
        // and an ordinary operand. Each position is therefore parsed once rather than once per alternative at every nesting
        // level, which would take time exponential in the nesting depth
        indexedAccess = Memoized(indexedAccess);

        // factorial => primary ("!")* ;
        // A factorial includes any primary
        // Only a '=' directly after '!' makes it the "!=" operator; "3! == 6" and "3! = 6" compare a factorial
        var factorial = indexedAccess.And(ZeroOrMany(exclamationMark.AndSkip(Not(Literals.Char('=')))))
            .Then((ctx, x) =>
            {
                if (x.Item2.Count == 0)
                {
                    // there is just a primary discovered
                    return x.Item1;
                }
                ExpressionLocation loc = new ParlotExpressionLocation(ctx);
                return new BinaryExpression(BinaryExpressionType.Factorial, x.Item1, new ValueExpression(x.Item2.Count).SetLocation(loc)).SetLocation(loc);
            }
        );

        Parser<LogicalExpression> nextParser = factorial;

        if (useComplexNumbers)
        {
            nextParser = nextParser.And(ZeroOrOne(imaginaryChar, '\0'))
                .Then<LogicalExpression>((ctx, x) =>
                {
                    if (x.Item2 == '\0')
                    {
                        // there is just a primary discovered
                        return x.Item1;
                    }
                    return new ImaginaryNumberExpression(x.Item1, new Helpers.MathHelperOptions(cultureInfo, options)).SetLocation(new ParlotExpressionLocation(ctx));
                });
        }

        if (calculatePercent)
        {
            Parser<LogicalExpression>? numberPercent = nextParser.And(ZeroOrOne(percentChar, '\0'))
                .Then<LogicalExpression>(static (ctx, x) =>
                {
                    if (x.Item2 == '\0')
                    {
                        // there is just a primary discovered
                        return x.Item1;
                    }
                    return new PercentExpression(x.Item1).SetLocation(new ParlotExpressionLocation(ctx));
                });
            Parser<LogicalExpression>? numberPercent2 = percentChar.And(nextParser)
                .Then<LogicalExpression>(static (ctx, x) => new PercentExpression(x.Item2).SetLocation(new ParlotExpressionLocation(ctx)));

            nextParser = OneOf(numberPercent!, numberPercent2!);
        }

            /*if (calculatePercent || useComplexNumbers)
            {
                Parser<LogicalExpression>? numberPercent = calculatePercent ? factorial.And(ZeroOrOne(percentChar, '\0'))
                    .Then<LogicalExpression>(static (ctx, x) =>
                    {
                        if (x.Item2 == '\0')
                        {
                            // there is just a primary discovered
                            return x.Item1;
                        }
                        return new PercentExpression(x.Item1).SetLocation(new ParlotExpressionLocation(ctx));
                    }) : null;
                Parser<LogicalExpression>? numberPercent2 = calculatePercent ? percentChar.And(factorial)
                    .Then<LogicalExpression>(static (ctx, x) => new PercentExpression(x.Item2).SetLocation(new ParlotExpressionLocation(ctx))) : null;

                Parser<LogicalExpression>? numberComplex = useComplexNumbers ? factorial.And(ZeroOrOne(imaginaryChar, '\0'))
                    .Then<LogicalExpression>((ctx, x) =>
                    {
                        if (x.Item2 == '\0')
                        {
                            // there is just a primary discovered
                            return x.Item1;
                        }
                        return new ImaginaryNumberExpression(x.Item1, new Helpers.MathHelperOptions(cultureInfo, options)).SetLocation(new ParlotExpressionLocation(ctx));
                    }) : null;

                if (calculatePercent && useComplexNumbers)
                    factorialOrPercent = OneOf(numberPercent!, numberPercent2!, numberComplex!);
                else
                if (calculatePercent)
                    factorialOrPercent = OneOf(numberPercent!, numberPercent2!);
                else
                    factorialOrPercent = numberComplex!;
            }
            else
            {
                factorialOrPercent = factorial;
            }*/

        // ( "-" | "!" | "not" | "~" | root2 | root3 | root4 ) factorial | exponential | primary;
        List<(Parser<string>, Func<ParseContext, LogicalExpression, LogicalExpression>)> unaryOps =
        [
            (not, static (ctx, value) => new UnaryExpression(UnaryExpressionType.Not, value).SetLocation(new ParlotExpressionLocation(ctx))),
            (minus, static (ctx, value)  => CreateNegation(ctx, value)),
            (bitwiseNot, static (ctx, value) => new UnaryExpression(UnaryExpressionType.BitwiseNot, value).SetLocation(new ParlotExpressionLocation(ctx))),
            //(returnParser, static (ctx, value) => new UnaryExpression(UnaryExpressionType.Return, value).SetLocation(new ParlotExpressionLocation(ctx))),
        ];
        if (root2 is not null)
            unaryOps.Add((root2, static (ctx, value) => new UnaryExpression(UnaryExpressionType.SqRoot, value).SetLocation(new ParlotExpressionLocation(ctx))));
#if NET8_0_OR_GREATER
        if (root3 is not null)
            unaryOps.Add((root3, static (ctx, value) => new UnaryExpression(UnaryExpressionType.CbRoot, value).SetLocation(new ParlotExpressionLocation(ctx))));
#endif
        if (root4 is not null)
            unaryOps.Add((root4, static (ctx, value) => new UnaryExpression(UnaryExpressionType.FourthRoot, value).SetLocation(new ParlotExpressionLocation(ctx))));
        // A prefix operator may also precede an exponent ("2 ** -x"); like the operands of a binary operator, the exponent may also be a percent or an imaginary number
        var exponentPrefix = OneOf(unaryOps.Select(op => StackGuarded(op.Item1).Then(_ => op.Item2)).ToArray());

        // Either a factorial, primary, or exponential
        // exponential => factorial ( "**" prefix* factorial )* ;
        var exponential = nextParser.And(ZeroOrMany(exponent.SkipAnd(ZeroOrMany(exponentPrefix).And(nextParser))))
        .Then(static (ctx, x) =>
        {
            if (x.Item2.Count == 0)
                return x.Item1;

            // Exponentiation is right-associative, and a prefix operator of an exponent applies to the rest of the chain, as in Python:
            // a ** b ** c = a ** (b ** c), and a ** -b ** c = a ** -(b ** c)
            LogicalExpression? result = null;
            for (int i = x.Item2.Count - 1; i >= 0; i--)
            {
                var (prefixes, operand) = x.Item2[i];
                result = result is null
                    ? operand
                    : new BinaryExpression(BinaryExpressionType.Exponentiation, operand, result).SetLocation(new ParlotExpressionLocation(ctx));
                for (int p = prefixes.Count - 1; p >= 0; p--)
                    result = prefixes[p](ctx, result);
            }

            return new BinaryExpression(BinaryExpressionType.Exponentiation, x.Item1, result!).SetLocation(new ParlotExpressionLocation(ctx));
        });

        // The operators are guarded because Parlot's Unary parser recurses once per prefix operator
        var unary = exponential.Unary(StackGuarded(unaryOps));

        // multiplicative => unary ( ( "/" | "*" | "%" ) unary )* ;
        List<Parser<BinaryExpressionType>> multiplicativeOps = [
            intDivB.Then(BinaryExpressionType.IntDivB),
            divided.Then(BinaryExpressionType.Div),
            times.Then(BinaryExpressionType.Times),
            modulo.Then(BinaryExpressionType.Modulo)];
        if (!options.HasFlag(ExpressionOptions.SupportCStyleComments))
        {
            multiplicativeOps.Insert(1, intDivP.Then(BinaryExpressionType.IntDivP));
        }

        // The binary operator levels, all left-associative, from the loosest to the tightest
        // (the array below lists them the other way round, in the order in which they are tried):
        // logical        => bitwise ( "or" | "xor" | "and", each its own level ) bitwise )* ;
        // bitwise        => equality ( ( "|" | "^" | "&", each its own level ) equality )* ;
        // equality       => relational ( ( "=" | "!=" ) relational )* ;
        // relational     => shift ( ( ">=" | "<=" | "<" | ">" | "in" | "not in" | "like" | "not like" ) shift )* ;
        // shift          => additive ( ( "<<" | ">>" ) additive )* ;
        // additive       => multiplicative ( ( "-" | "+" ) multiplicative )* ;
        Parser<BinaryExpressionType>[] binaryLevels =
        [
            OneOf(multiplicativeOps.ToArray()),
            OneOf(
                plus.Then(BinaryExpressionType.Plus),
                minus.Then(BinaryExpressionType.Minus)),
            OneOf(
                leftShift.Then(BinaryExpressionType.LeftShift),
                rightShift.Then(BinaryExpressionType.RightShift)),
            OneOf(
                greaterOrEqual.Then(BinaryExpressionType.GreaterOrEqual),
                lessOrEqual.Then(BinaryExpressionType.LessOrEqual),
                less.Then(BinaryExpressionType.Less),
                greater.Then(BinaryExpressionType.Greater),
                @in.Then(BinaryExpressionType.In),
                notIn.Then(BinaryExpressionType.NotIn),
                like.Then(BinaryExpressionType.Like),
                notLike.Then(BinaryExpressionType.NotLike)),
            OneOf(
                equal.Then(BinaryExpressionType.Equal),
                notEqual.Then(BinaryExpressionType.NotEqual)),
            bitwiseAnd.Then(BinaryExpressionType.BitwiseAnd),
            bitwiseXOr.Then(BinaryExpressionType.BitwiseXOr),
            bitwiseOr.Then(BinaryExpressionType.BitwiseOr),
            and.Then(BinaryExpressionType.And),
            xor.Then(BinaryExpressionType.XOr),
            or.Then(BinaryExpressionType.Or),
        ];

        // One parser for all binary levels: a nested parser per level would cost a dozen stack frames per nesting level
        var logical = new BinaryOperatorParser(unary, binaryLevels);

        /*
         * old
        var andTypeParser = and.Then(BinaryExpressionType.And)
            .Or(bitwiseAnd.Then(BinaryExpressionType.BitwiseAnd));

        var orTypeParser = or.Then(BinaryExpressionType.Or)
            .Or(bitwiseOr.Then(BinaryExpressionType.BitwiseOr));

        var xorTypeParser = xor.Then(BinaryExpressionType.XOr)
            .Or(bitwiseXOr.Then(BinaryExpressionType.BitwiseXOr));

        // "and" has higher precedence than "or"
        var andParser = equality.And(ZeroOrMany(andTypeParser.And(equality)))
            .Then(ParseBinaryExpression);

        var orParser = andParser.And(ZeroOrMany(orTypeParser.And(andParser)))
            .Then(ParseBinaryExpression);

        var xorParser = andParser.And(ZeroOrMany(xorTypeParser.And(andParser)))
            .Then(ParseBinaryExpression);

        // logical => equality ( ( "and" | "or" | "xor" ) equality )* ;
        var logical = OneOf(orParser, xorParser).And(ZeroOrMany(xorTypeParser.And(orParser)))
            .Then(ParseBinaryExpression);
        */

        // ternary => logical("?" logical ":" logical) ?
        // ternary => logical ( "?" ternary ":" ternary )? ; right-associative, as in C#: a ? b : c ? d : e is a ? b : (c ? d : e)
        var ternary = Deferred<LogicalExpression>();
        ternary.Parser = StackGuarded(logical.And(ZeroOrOne(questionMark.SkipAnd(ternary).AndSkip(colon).And(ternary)))
            .Then(static (ctx, x) =>
                x.Item2.Item1 is null
                    ? x.Item1
                    : new TernaryExpression(x.Item1, x.Item2.Item1, x.Item2.Item2).SetLocation(new ParlotExpressionLocation(ctx))));

        List<(Parser<string>, Func<ParseContext, LogicalExpression, LogicalExpression>)> returnOps =
        [
            (returnParser, static (ctx, value) => new UnaryExpression(UnaryExpressionType.Return, value).SetLocation(new ParlotExpressionLocation(ctx))),
        ];
        var @return = ternary.Unary(StackGuarded(returnOps));

        List<Parser<string>> operatorSequenceElements = [
            intDivB, divided, times, modulo, plus,
            minus, leftShift, rightShift, greaterOrEqual,
            lessOrEqual, greater, less, equal,
            notEqual];
        if (!options.HasFlag(ExpressionOptions.SupportCStyleComments))
            operatorSequenceElements.Insert(0, intDivP);

        // The position after the operators is remembered, as the error is raised only after the operand that follows them
        var operatorSequence = @return.LeftAssociative(
            (OneOrMany(OneOf(operatorSequenceElements.ToArray()))
                .Then(static (ctx, ops) =>
                {
                    ((LogicalExpressionParserContext)ctx).OperatorSequencePosition = ctx.Scanner.Cursor.Position;
                    return ops;
                }),
                static (ctx, _, _) =>
                {
                    var position = ((LogicalExpressionParserContext)ctx).OperatorSequencePosition;
                    throw new NCalcParserException($"Unknown operator sequence at position {position}", position);
                }));

        List<Parser<LogicalExpression>> statements = [];

        if (options.HasFlag(ExpressionOptions.UseLoops))
        {
            var whileLoop = Keyword("while").SkipAnd(Terms.Text("(")).SkipAnd(expressionOrBracedStatementSequence).AndSkip(Terms.Text(")")).And(expressionOrBracedStatementSequence)
                .Then<LogicalExpression>(static (_, x) =>
                    new BinaryExpression(BinaryExpressionType.WhileLoop, x.Item1, x.Item2)
                );
            statements.Add(whileLoop);
        }

        if (options.HasFlag(ExpressionOptions.UseIfStatement))
        {
            var ifStatement = Keyword("if").SkipAnd(Terms.Text("(")).SkipAnd(expressionOrBracedStatementSequence).AndSkip(Terms.Text(")")).And(expressionOrBracedStatementSequence).And(ZeroOrOne(Keyword("else").SkipAnd(expressionOrBracedStatementSequence)))
                    .Then<LogicalExpression>(static (_, x) =>
                        new IfStatementExpression(x.Item1, x.Item2, (LogicalExpression?)x.Item3 ?? new ValueExpression())
                    );

            statements.Add(ifStatement);
        }

        enabledParsers.Clear();
        if (guid is not null)
            enabledParsers.Add(guid);
        enabledParsers.Add(hexOctBinNumber);
        if (currency is not null)
            enabledParsers.Add(currency);
        enabledParsers.Add(intNumber);
        enabledParsers.Add(longNumber);
        if (bigIntNumber is not null)
            enabledParsers.Add(bigIntNumber);
        enabledParsers.Add(decimalOrDoubleNumber);
        enabledParsers.Add(booleanTrue);
        enabledParsers.Add(booleanFalse);
        enabledParsers.Add(theNull);
        if (dateTime is not null) // dateTime will be initialized unless options.HasFlag(ExpressionOptions.DontParseDates)
            enabledParsers.Add(dateTime);
        enabledParsers.Add(stringValue);

        var paramName = letterIdentifier.And(ZeroOrOne(Terms.Char('=').SkipAnd(OneOf(enabledParsers.ToArray())))).Then<FunctionParameter>(static x => new FunctionParameter(x.Item1.ToString()!, x.Item2 is not null, x.Item2));

        var populatedParamList =
            Between(openParen, Separated(comma.Or(semicolon), paramName),
                    closeParen.ElseError("Parenthesis not closed or a parameter is missing."));

        Parser<IReadOnlyList<FunctionParameter>>? emptyParamList = openParen.AndSkip(closeParen).Then<IReadOnlyList<FunctionParameter>>(static _ => []);

        var paramList = OneOf(emptyParamList, populatedParamList);

        Parser<LogicalExpression> funcBodyExpression = Terms.Text("=>").SkipAnd(expression)
            .Then<LogicalExpression>(static (ctx, x) => x.SetLocation(new ParlotExpressionLocation(ctx)));

        Parser<LogicalExpression> functionDecl = ZeroOrMany(docComment).AndSkip(Keyword("fn", false)).And(letterIdentifier).And(paramList).And(OneOf(bracedExpressionOrStatementSequence, funcBodyExpression))
            .Then<LogicalExpression>((ctx, x) =>
            {
                Function function = new(x.Item2.ToString()!, x.Item4) { Description = string.Join("\n", x.Item1).Trim(), };

                bool optionalFound = false;
                int mandatoryParams = 0;
                StringComparison paramNameCompareRule = ((LogicalExpressionParserContext)ctx).Options.HasFlag(ExpressionOptions.LowerCaseIdentifierLookup) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                foreach (var param in x.Item3)
                {
                    if (param.IsOptional)
                        optionalFound = true;
                    else
                    if (optionalFound)
                        throw new NCalcParserException($"In the declaration of the function '{function.Name}', optional parameters may not be declared ahead of required ones", ctx.Scanner.Cursor.Position);
                    else
                        mandatoryParams++;

                    if (function.Parameters.Any((x) => x.Name.Equals(param.Name, paramNameCompareRule)))
                        throw new NCalcParserException($"Duplicate parameter name '{param.Name}' in the declaration of the function '{function.Name}'", ctx.Scanner.Cursor.Position);

                    function.Parameters.Add(param);
                    function.MandatoryParamCount = mandatoryParams;
                }
                if (((LogicalExpressionParserContext)ctx).UserFunctions.ContainsKey(function.Name))
                    throw new NCalcParserException($"The function '{function.Name}' is declared more than once", ctx.Scanner.Cursor.Position);
                ((LogicalExpressionParserContext)ctx).UserFunctions.Add(function.Name, function);
                return new FunctionExpression(function);
            }).Named("FunctionDeclaration");

        statements.Add(operatorSequence);

        Parser<LogicalExpression>? topLevel = null;

        if (options.HasFlag(ExpressionOptions.UseAssignments))
        {
            var assignmentTypeParser = assignmentOperator.Then(BinaryExpressionType.Assignment);

            var assignment = OneOf(indexedAccess, identifierExpression).And(OneOf(assignmentOperator, plusAssign, minusAssign, multiplyAssign, divAssign, orAssign, xorAssign, andAssign)).And(expressionOrBracedStatementSequence)
            .Then<LogicalExpression>(static (ctx, x) =>
                {
                    var expressionType = x.Item2 switch
                    {
                        "+=" => BinaryExpressionType.PlusAssignment,
                        "-=" => BinaryExpressionType.MinusAssignment,
                        "\u00D7=" or "\u2219=" or "*=" => BinaryExpressionType.MultiplyAssignment,
                        "/=" or "\u2236=" or "\u00F7=" => BinaryExpressionType.DivAssignment,
                        "&=" => BinaryExpressionType.AndAssignment,
                        "|=" => BinaryExpressionType.OrAssignment,
                        "^=" => BinaryExpressionType.XOrAssignment,
                        _ => BinaryExpressionType.Assignment,
                    };
                    ExpressionLocation loc = new ParlotExpressionLocation(ctx);

                    // Only a name or an element of a named list or string can be assigned to: "5 = x" or "Abs(x) := 7" must not
                    // silently return the right-hand side. A comparison such as "5 == x" never gets here, as "= x" is not an expression
                    if (!IsAssignmentTarget(x.Item1))
                        throw new NCalcParserException($"The left side of an assignment must be a variable or an indexed variable at position {ctx.Scanner.Cursor.Position}", ctx.Scanner.Cursor.Position);

                    return (BinaryExpression)new BinaryExpression(expressionType, x.Item1, x.Item3/*[0]*/).SetLocation(loc).SetOptions(((LogicalExpressionParserContext)ctx).Options, ((LogicalExpressionParserContext)ctx).CultureInfo, ((LogicalExpressionParserContext)ctx).AdvancedOptions);
                }
            ).Named("Assignment");

            statements.Insert(0, assignment);
        }

        var statementsArray = statements.ToArray();

        var expressionOrAssignment = OneOf(statementsArray);

        topLevel = expressionOrAssignment;

        if (options.HasFlag(ExpressionOptions.UseStatementSequences))
        {
            statements.Insert(0, functionDecl);
            var expressionOrAssignmentOrFunctionDecl = OneOf(statements.ToArray());
            var separator = Terms.Pattern((c) => c == ';');
            var statementSequence = expressionOrAssignmentOrFunctionDecl.And(ZeroOrMany(separator.SkipAnd(expressionOrAssignmentOrFunctionDecl))).And(ZeroOrMany(separator));
            var statementSequenceParser = statementSequence
                .Then(static (ctx, x) =>
                {
                    StatementSequence seq;
                    LogicalExpression result = null!;
                    ExpressionLocation loc = new ParlotExpressionLocation(ctx);
                    if (x.Item2.Count == 0)
                    {
                        result = x.Item1;
                    }
                    else
                    {
                        seq = new(x.Item3.Count > 0);
                        seq.Add(x.Item1);
                        for (int i = 0; i < x.Item2.Count; i++)
                            seq.Add(x.Item2[i]);

                        seq.SetLocation(loc).SetOptions(((LogicalExpressionParserContext)ctx).Options, ((LogicalExpressionParserContext)ctx).CultureInfo, ((LogicalExpressionParserContext)ctx).AdvancedOptions);
                        result = seq;
                    }
                    return result;
                }).Named("StatementSequence");

            topLevel = statementSequenceParser;
        }

        var curlyBracedTopLevel = openCurlyBrace.SkipAnd(topLevel.AndSkip(closeCurlyBrace))
            .Then<LogicalExpression>(static (ctx, x) =>
              new ExpressionGroup(x).SetLocation(new ParlotExpressionLocation(ctx)));

        // Every nesting level (groups, function arguments, lists, braces) re-enters these parsers, so they are stack-guarded
        expression.Parser = StackGuarded(expressionOrAssignment);

        expressionOrBracedStatementSequence.Parser = StackGuarded(OneOf(curlyBracedTopLevel, expressionOrAssignment));
        bracedExpressionOrStatementSequence.Parser = StackGuarded(curlyBracedTopLevel);

        var whitespaceParser = new WhiteSpaceParser(options);

        Parser<LogicalExpression> expressionParser = OneOf(topLevel, curlyBracedTopLevel).AndSkip(ZeroOrMany(whitespaceParser)).Eof()
                .ElseError(InvalidTokenMessage);

        if (dateTimeAtHash is not null)
            expressionParser = new DateTimeLiteralAwareParser(expressionParser, dateTimeAtHash);

        //AppContext.TryGetSwitch("NCalc.EnableParlotParserCompilation", out var enableParserCompilation);

        return /*enableParserCompilation ? expressionParser.Compile() : */expressionParser;
    }

    private static BigDecimal? TryParseDecimal((LogicalExpression? IntegerPart, string Fraction, string? Exponent) val, bool useUnderscores)
    {
        // The value is built from its digits and its exponent, which does not depend on the culture and does not expand a huge
        // exponent into digits (1e9999999 is kept as a mantissa and an exponent)
        BigInteger integerPart = val.IntegerPart is ValueExpression { Value: { } integerValue }
            ? integerValue switch
            {
                ulong ul => ul,
                BigInteger bi => bi,
                _ => BigInteger.Parse(Convert.ToString(integerValue, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
            }
            : BigInteger.Zero;

        // With the fractional part, we have text that we may need to sanitize
        string fracPart = val.Fraction;
        if (useUnderscores)
            fracPart = fracPart.Replace("_", "");

        BigInteger mantissa = integerPart;
        if (fracPart.Length > 0)
        {
            BigInteger fraction = BigInteger.Parse(fracPart, NumberStyles.None, CultureInfo.InvariantCulture);
            mantissa = integerPart.Sign < 0
                ? integerPart * BigInteger.Pow(10, fracPart.Length) - fraction
                : integerPart * BigInteger.Pow(10, fracPart.Length) + fraction;
        }

        BigInteger exponent = -fracPart.Length;
        if (!string.IsNullOrEmpty(val.Exponent))
            exponent += BigInteger.Parse(val.Exponent, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

        // An exponent beyond the range of int cannot be held by BigDecimal; such a literal is left to the double parser (infinity or 0)
        if (exponent < int.MinValue || exponent > int.MaxValue)
            return null;

        return new BigDecimal(mantissa, (int)exponent);
    }

    /// <summary>
    /// Creates the negation of an operand. The magnitude of the smallest long does not fit in a long, so it is read as a larger type,
    /// and "-9223372036854775808" is folded into a long literal (otherwise it would lose precision as a double).
    /// "-2147483648" stays a long, so that e.g. "-2147483648 - 1" does not wrap around in 32 bits.
    /// </summary>
    private static LogicalExpression CreateNegation(ParseContext ctx, LogicalExpression value)
    {
        const string LongMinValueMagnitude = "9223372036854775808";

        if (value is ValueExpression literal)
        {
            object? folded = literal.Value switch
            {
                ulong ul when ul == 9223372036854775808UL => long.MinValue,
                BigInteger bi when bi == 9223372036854775808UL => long.MinValue,
                // Without big numbers the magnitude is read as a double; only the integer literal is folded, not e.g. 9.223372036854775808E18
                double d when d == 9223372036854775808.0 && EndsWithLiteral(ctx, LongMinValueMagnitude) => long.MinValue,
                _ => null
            };
            if (folded is not null)
                return new ValueExpression(folded).SetLocation(new ParlotExpressionLocation(ctx));
        }

        return new UnaryExpression(UnaryExpressionType.Negate, value).SetLocation(new ParlotExpressionLocation(ctx));
    }

    private static bool EndsWithLiteral(ParseContext ctx, string text)
    {
        var buffer = ctx.Scanner.Buffer;
        var end = ctx.Scanner.Cursor.Offset;
        var start = end - text.Length;
        return start >= 0
            && string.CompareOrdinal(buffer, start, text, 0, text.Length) == 0
            && (start == 0 || !char.IsLetterOrDigit(buffer[start - 1]) && buffer[start - 1] != '.' && buffer[start - 1] != '_');
    }

    private static LogicalExpression ParseBinaryExpression(ParseContext ctx, (LogicalExpression, IReadOnlyList<(BinaryExpressionType, LogicalExpression)>) x)
    {
        var result = x.Item1;

        foreach (var op in x.Item2)
        {
            result = new BinaryExpression(op.Item1, result, op.Item2).SetLocation(new ParlotExpressionLocation(ctx));
        }

        return result;
    }

    public static LogicalExpression Parse(LogicalExpressionParserContext context)
    {
        Parser<LogicalExpression> parserToUse = GetExpressionParser(context.CultureInfo, context.Options, context.AdvancedOptions);

        return Parse(parserToUse, context);
    }

    /// <summary>
    /// The stack size, in bytes, of the thread on which an expression that is nested too deeply to be parsed on the calling thread
    /// is parsed once more. The default is 16 MB; 0 turns the second attempt off.
    /// The resulting expression tree is as deep as its text, so evaluating it may require <see cref="ExpressionOptions.UseNonRecursiveEvaluator"/>.
    /// The non-recursive evaluators use this size too, for the threads on which they continue the evaluation of deeply nested function calls
    /// and expressions used as parameter values (the asynchronous evaluator continues on thread pool threads); handlers called there run on
    /// those threads. 0 turns this off as well: the evaluation stays on the calling thread and fails with an
    /// <see cref="Exceptions.NCalcEvaluationException"/> when its stack is used up.
    /// </summary>
    public static int DeepExpressionStackSize
    {
        get => _deepExpressionStackSize;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _deepExpressionStackSize = value;
        }
    }

    private static int _deepExpressionStackSize = 16 * 1024 * 1024;

    public static LogicalExpression Parse(Parser<LogicalExpression> parserToUse, LogicalExpressionParserContext context)
    {
        // The parsers check the token only now and then, so a short text would be parsed with a cancelled token
        context.CancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ParseOnCurrentThread(parserToUse, context);
        }
        catch (NCalcParserException ex) when (ex.IsNestedTooDeeply && DeepExpressionStackSize > 0)
        {
            var result = ParseOnLargeStack(parserToUse, context);
            if (result is null)
                throw;
            return result;
        }
    }

    /// <summary>
    /// Parses the expression again on a new thread with a stack of <see cref="DeepExpressionStackSize"/> bytes.
    /// Ordinary expressions never get here, so they pay nothing for it.
    /// </summary>
    /// <returns>The expression, or <c>null</c> when the platform cannot start such a thread.</returns>
    private static LogicalExpression? ParseOnLargeStack(Parser<LogicalExpression> parserToUse, LogicalExpressionParserContext context)
    {
#if NET
        if (OperatingSystem.IsBrowser())
            return null;
#endif
        // The failed attempt left the parsers it was in registered in the context, so the second attempt gets a fresh one
        var retryContext = new LogicalExpressionParserContext(context.Scanner.Buffer, context.Options, context.CultureInfo, context.AdvancedOptions, context.CancellationToken)
        {
            OnEnterParser = context.OnEnterParser,
            OnExitParser = context.OnExitParser,
        };

        // Some literals are parsed with the current culture, which a new thread would not have
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        LogicalExpression? result = null;
        ExceptionDispatchInfo? error = null;

        var thread = new Thread(() =>
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            try
            {
                result = ParseOnCurrentThread(parserToUse, retryContext);
            }
            catch (Exception ex)
            {
                error = ExceptionDispatchInfo.Capture(ex);
            }
        }, DeepExpressionStackSize);

        try
        {
            thread.Start();
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or OutOfMemoryException)
        {
            return null;
        }
        thread.Join();

        error?.Throw();

        foreach (var function in retryContext.UserFunctions)
            context.UserFunctions[function.Key] = function.Value;

        return result!;
    }

    private static LogicalExpression ParseOnCurrentThread(Parser<LogicalExpression> parserToUse, LogicalExpressionParserContext context)
    {
        if (parserToUse.TryParse(context, out var result, out var error))
            return result;

        string message;
        TextPosition position;
        if (error is not null)
        {
            position = error.Position;
            message = error.Message ?? "Error parsing the expression";
        }
        else
        {
            position = context.Scanner.Cursor.Position;
            message = "Error parsing the expression";
        }

        // An error raised after the parser has gone back to an earlier alternative is reported where that alternative started
        // (usually the start of the text), so the position of the first character that no parser could consume is reported instead
        var furthest = FindFurthestParsedOffset(parserToUse, context);
        if (furthest > position.Offset)
            position = GetTextPosition(context.Scanner.Buffer, furthest);

        throw new NCalcParserException($"{message} at position {position}", position);
    }

    /// <summary>
    /// Parses a text that could not be parsed once more, recording how far the parsers got, and returns the offset of the first
    /// character after that point that is not whitespace. Only failed parses pay for this.
    /// </summary>
    private static int FindFurthestParsedOffset(Parser<LogicalExpression> parserToUse, LogicalExpressionParserContext context)
    {
        var buffer = context.Scanner.Buffer;
        int furthest = 0;
        var tracingContext = new LogicalExpressionParserContext(buffer, context.Options, context.CultureInfo, context.AdvancedOptions, context.CancellationToken)
        {
            OnExitParser = (parser, ctx) =>
            {
                if (ctx.Scanner.Cursor.Offset > furthest)
                    furthest = ctx.Scanner.Cursor.Offset;
            },
        };

        try
        {
            parserToUse.TryParse(tracingContext, out _, out _);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The offset reached before the failure is still useful
        }

        while (furthest < buffer.Length && char.IsWhiteSpace(buffer[furthest]))
            furthest++;

        return furthest;
    }

    private static TextPosition GetTextPosition(string buffer, int offset)
    {
        int line = 1;
        int column = 1;
        for (int i = 0; i < offset && i < buffer.Length; i++)
        {
            if (buffer[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
                column++;
        }
        return new TextPosition(offset, line, column);
    }
}

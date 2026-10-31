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
    private static readonly ValueExpression True = new(true);
    private static readonly ValueExpression False = new(false);
    private static readonly ValueExpression Null = new();

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

    class CurrentCultureDateTimeFormatProvider : IFormatProvider
    {
        public object GetFormat(Type? formatType)
        {
            if (formatType?.Equals(typeof(DateTimeFormatInfo)) == true)
            {
                return CultureInfo.CurrentCulture.DateTimeFormat;
            }
            else
            {
                return null!;
            }
        }
    }

    private static IFormatProvider _currentCultureFormatProvider = new CurrentCultureDateTimeFormatProvider();

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
    /// Parses a chain of left-associative binary operators of several precedence levels in one loop with explicit stacks,
    /// instead of a nested parser per level, so that each nesting level of an expression (a group, a function argument)
    /// takes far fewer stack frames.
    /// The levels are listed from the tightest to the loosest and are tried in this order at every position.
    /// A level whose operator matches but is not followed by an operand gives way to the next level, as the nested parsers did.
    /// </summary>
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

    /// <summary>
    /// Returns the parser that accepts only a single literal (a number, a date or time, a boolean or a GUID), optionally with a sign,
    /// as written in an expression with the given settings. The parser is created once per combination of settings.
    /// </summary>
    internal static Parser<LogicalExpression> GetLiteralParser(CultureInfo cultureInfo, ExpressionOptions options, AdvancedExpressionOptions? extOptions)
    {
        const string separator = "\u001F";
        var key = string.Concat(((long)options).ToString(CultureInfo.InvariantCulture), separator, cultureInfo.Name, separator, extOptions?.GetCacheKey() ?? string.Empty);
        return LiteralParsers.GetOrAdd(key, _ => CreateParser(cultureInfo, options, extOptions, literalsOnly: true));
    }

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
                    throw new ArgumentException($"{x.ToString()} is not a valid hex number");

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
                        throw new ArgumentException($"{x.ToString()} is not a valid hex number");

                    return new ValueExpression((object)result).SetLocation(new ParlotExpressionLocation(ctx));
                }
                else
                {
                    throw new ArgumentException($"{x.ToString()} is not a valid hex number");
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
                    throw new ArgumentException($"{x.ToString()} is not a valid octal number");

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
                        throw new ArgumentException($"{x.ToString()} is not a valid octal number");

                    return new ValueExpression((object)result).SetLocation(new ParlotExpressionLocation(ctx));
                }
                else
                    throw new ArgumentException($"{x.ToString()} is not a valid octal number");
            });

        var binaryNumber = Terms.Text("0b")
            .SkipAnd(Terms.Pattern(c => c == '0' || c == '1' || (acceptUnderscores && c == '_')))
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                string? strValue = x.ToString();

                if (string.IsNullOrEmpty(strValue))
                    throw new ArgumentException($"{x.ToString()} is not a valid binary number");

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
                        throw new ArgumentException($"{x.ToString()} is not a valid binary number");

                    return new ValueExpression((object)result);
                }
                else
                    throw new ArgumentException($"{x.ToString()} is not a valid binary number");
            });

        Parser<LogicalExpression> hexOctBinNumber;

        hexOctBinNumber = OneOf(hexNumber!, octalNumber!, binaryNumber!);

        char decimalSeparator = (extOptions is not null) ? extOptions.GetDecimalSeparatorChar() : Parlot.Fluent.NumberLiterals.DefaultDecimalSeparator; // this method will return the default separator, if needed
        char decimalSeparator2 = (extOptions?.GetSecondaryDecimalSeparatorChar()) ?? '\0';
        char numGroupSeparator = (extOptions is not null) ? extOptions.GetNumberGroupSeparatorChar() : Parlot.Fluent.NumberLiterals.DefaultGroupSeparator; // this method will return the default separator, if needed

        NumberOptions useNumberGroupSeparatorFlag = ((extOptions is not null) && (numGroupSeparator != '\0')) ? NumberOptions.AllowGroupSeparators : NumberOptions.None;
        NumberOptions useUnderscoreFlag = acceptUnderscores ? NumberOptions.AllowUnderscore : NumberOptions.None;

        Parser<string>[] floatNumExclusions =
            (decimalSeparator2 != '\0')
            ? [Terms.Text(decimalSeparator.ToString()), Terms.Text(decimalSeparator2.ToString()), Terms.Text("E", true)]
            : [Terms.Text(decimalSeparator.ToString()), Terms.Text("E", true)];

        //var intNumberParser
        var intNumber = Terms.Number<int>(NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag, decimalSeparator, numGroupSeparator)
            .AndSkip(Not(OneOf(floatNumExclusions)))
            .Then<LogicalExpression>(static (ctx, d) => new ValueExpression(d).SetLocation(new ParlotExpressionLocation(ctx)));

        var longNumber = Terms.Number<long>(NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag, decimalSeparator, numGroupSeparator)
            .AndSkip(Not(OneOf(floatNumExclusions)))
            .Then<LogicalExpression>(static (ctx, d) => new ValueExpression(d).SetLocation(new ParlotExpressionLocation(ctx)));

        Parser<LogicalExpression>? bigIntNumber = null;

        if (useBigNumbers)
        {
            bigIntNumber = Terms.Number<BigInteger>(NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag, decimalSeparator, numGroupSeparator)
                .AndSkip(Not(OneOf(floatNumExclusions)))
                .Then<LogicalExpression>(static (ctx, d) =>
                    {
                        if (d >= ulong.MinValue && d <= ulong.MaxValue)
                            return new ValueExpression((object)(ulong)d).SetLocation(new ParlotExpressionLocation(ctx));
                        else
                            return new ValueExpression(d).SetLocation(new ParlotExpressionLocation(ctx));
                    });
        }

        if (decimalSeparator2 != '\0' && decimalSeparator2 == numGroupSeparator)
            useNumberGroupSeparatorFlag = 0; // decimalSeparator2 takes precedence when it is specified.
        var decimalNumber = Terms.Number<decimal>(NumberOptions.Float | useNumberGroupSeparatorFlag | useUnderscoreFlag | NumberOptions.RequireFractionalPartForDecimals, decimalSeparator, numGroupSeparator, decimalSeparator2)
            .Then<LogicalExpression>(static (ctx, val) =>
            {
                return new ValueExpression(val).SetLocation(new ParlotExpressionLocation(ctx));
            });

        var doubleNumber = Terms.Number<double>(NumberOptions.Float | useNumberGroupSeparatorFlag | useUnderscoreFlag | NumberOptions.RequireFractionalPartForDecimals, decimalSeparator, numGroupSeparator, decimalSeparator2)
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

            Parser<LogicalExpression> bigIntNumberD = Terms.Number<BigInteger>((NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag), decimalSeparator, numGroupSeparator)
                //.AndSkip(Not(OneOf(floatNumExclusions)))
                .Then<LogicalExpression>(static (d) =>
                {
                    if (d >= ulong.MinValue && d <= ulong.MaxValue)
                        return new ValueExpression((object)(ulong)d);
                    else
                        return new ValueExpression(d);
                });
            Parser<LogicalExpression> bigUIntNumberD = Terms.Pattern(c => acceptableDecChars.Contains(c))
                .Then<LogicalExpression>(static s => new ValueExpression(s));

            bigDecimalNumber =
                ZeroOrOne(bigIntNumberD)
                .And(Terms.AnyOf(decimalSeparators.AsSpan(), 1, 1))
                .And(bigUIntNumberD)
                .And(ZeroOrOne(Terms.AnyOf("Ee".AsSpan(), 1, 1).And(bigIntNumberD)))
                .When((_, val) => TryParseDecimal(val, acceptUnderscores) is not null)
                .Then<LogicalExpression>(static (ctx, val) =>
                {
                    bool decimalDefault = ((LogicalExpressionParserContext)ctx).Options.HasFlag(ExpressionOptions.DecimalAsDefault);

                    BigDecimal? value = TryParseDecimal(val, ((LogicalExpressionParserContext)ctx).AcceptUnderscores);

                    if (value is null)
                        return new ValueExpression();  // never happens - the When condition ensures that val can be parsed

                    if (decimalDefault && (value >= decimal.MinValue && value <= decimal.MaxValue))
                    {
                        decimal decValue = (decimal)value;
                        if (value == decValue)
                            return new ValueExpression(decValue).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    if (value >= double.MinValue && value <= double.MaxValue)
                    {
                        double dValue = (double)value;
                        if (value == dValue)
                            return new ValueExpression(dValue).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    if (!decimalDefault && (value >= decimal.MinValue && value <= decimal.MaxValue))
                    {
                        decimal decValue = (decimal)value;
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

                var decimalCurrencyNumber = Terms.Number<decimal>((NumberOptions.Float & ~NumberOptions.AllowExponent) | useNumberGroupSeparatorFlag | useUnderscoreFlag, currencyDecimalSeparator, currencyNumGroupSeparator, currencyDecimalSeparator2)
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
        bool useAssignments = options.HasFlag(ExpressionOptions.UseAssignments);

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
                    ? OneOf(Terms.Text("**"), Terms.Text("\u2291"))
                    : OneOf(Terms.Text("**"), Terms.Text("^"), Terms.Text("\u2291")))
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

        var rangedIndex = openBrace.SkipAnd(ZeroOrOne(fromEndText)).And(ZeroOrOne(expressionOrBracedStatementSequence)).And(ZeroOrOne(rangeText)).And(ZeroOrOne(fromEndText)).And(ZeroOrOne(expressionOrBracedStatementSequence)).AndSkip(closeBrace);

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
                    closeParen.ElseError("Parenthesis not closed."))
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
                .Then<LogicalExpression>(True);
        var booleanFalse = Keyword("false")
            .Then<LogicalExpression>(False);

        var theNull = Keyword("null")
            .Then<LogicalExpression>(Null);

        var singleQuotesStringValue = Terms.String(quotes: StringLiteralQuotes.Single, returnDecoded: false)
                .Then<LogicalExpression>(static (ctx, value) =>
                {
                    if (value.Length == 1 &&
                        ((LogicalExpressionParserContext)ctx).Options.HasFlag(ExpressionOptions.AllowCharValues))
                    {
                        return new ValueExpression(value.Span[0]).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    string? originalValue = value.ToString();
                    if (originalValue is null)
                        return new ValueExpression(null);

                    TextSpan decodedValue = Character.DecodeString(originalValue);

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

        if (!options.HasFlag(ExpressionOptions.DontParseDates))
        {
            DateTimeFormatInfo dateTimeFormat = extOptions?.GetFormat(typeof(DateTimeFormatInfo)) as DateTimeFormatInfo ?? cultureInfo.DateTimeFormat;

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

            // date => number/number/number or custom
            date = dateDefinition.Then<LogicalExpression>((ctx, date) =>
            {
                string customDateSepForDT = dateTimeFormat.DateSeparator;
                if (useSecondDate || onlyCustomDateTranslation)
                {
                    if (DateTime.TryParse($"{date.Item1}{customDateSepForDT}{date.Item2}{customDateSepForDT}{date.Item3}", dateTimeFormat, DateTimeStyles.None, out var result))
                    {
                        return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                }
                if (useSecondDate || !onlyCustomDateTranslation)
                {
                    // Use the existing ncalc approach with the current culture
                    if (DateTime.TryParseExact($"{date.Item1}{builtInDateSep}{date.Item2}{builtInDateSep}{date.Item3}", ncalcDateMasks, _currentCultureFormatProvider, DateTimeStyles.None, out var result))
                    {
                        return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                }

                throw new FormatException("Invalid DateTime format.");
            });

            dateDefinitionIso = charIsNumber
                    .AndSkip(hyphenChar)
                    .And(charIsNumber)
                    .AndSkip(hyphenChar)
                    .And(charIsNumber);

            // dateIso => number-number-number
            dateIso = dateDefinitionIso.Then<LogicalExpression>((ctx, date) =>
            {
                if (DateTime.TryParseExact($"{date.Item1}-{date.Item2}-{date.Item3}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                {
                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                }

                throw new FormatException("Invalid DateTime format.");
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
                customTimeSep = extOptions.TimeSeparator;
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
                    timeSpanDefinition = ZeroOrOne(minus).And(ZeroOrOne(charIsNumber.AndSkip(dotChar))).And(charIsNumber)
                        .AndSkip(OneOf(divided, secondTimeSep, thirdTimeSep))
                        .And(charIsNumber)
                        .AndSkip(OneOf(divided, secondTimeSep, thirdTimeSep))
                        .And(charIsNumber);
                    shortTimeSpanDefinition = ZeroOrOne(minus).And(ZeroOrOne(charIsNumber.AndSkip(dotChar))).And(charIsNumber)
                        .AndSkip(OneOf(divided, secondTimeSep, thirdTimeSep))
                        .And(charIsNumber);
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

                    timeSpanDefinition = ZeroOrOne(minus).And(ZeroOrOne((charIsNumber).AndSkip(dotChar))).And(charIsNumber)
                        .AndSkip(OneOf(divided, secondTimeSep))
                        .And(charIsNumber)
                        .AndSkip(OneOf(divided, secondTimeSep))
                        .And(charIsNumber);

                    shortTimeSpanDefinition = ZeroOrOne(minus).And(ZeroOrOne(charIsNumber.AndSkip(dotChar))).And(charIsNumber)
                        .AndSkip(OneOf(divided, secondTimeSep))
                        .And(charIsNumber);
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

                // timeSpan => [[-]number.]number:number:number
                timeSpanDefinition = ZeroOrOne(minus).And(ZeroOrOne((charIsNumber).AndSkip(dotChar))).And(charIsNumber)
                    .AndSkip(secondTimeSep)
                    .And(charIsNumber)
                    .AndSkip(secondTimeSep)
                    .And(charIsNumber);

                shortTimeSpanDefinition = ZeroOrOne(minus).And(ZeroOrOne(charIsNumber.AndSkip(dotChar))).And(charIsNumber)
                    .AndSkip(secondTimeSep)
                    .And(charIsNumber);
            }

            timeDefinitionIso = charIsNumber
                    .AndSkip(colon)
                    .And(charIsNumber)
                    .AndSkip(colon)
                    .And(charIsNumber).And(ZeroOrOne(dotChar.SkipAnd(charIsNumber)));

            Parser<LogicalExpression>? time12 = null;
            Parser<LogicalExpression>? shortTime12 = null;

            var time = timeSpanDefinition.Then<LogicalExpression>((ctx, time) =>
            {
                string customTimeSepForDT = dateTimeFormat.TimeSeparator;
                if (useSecondTime || onlyCustomTimeTranslation)
                {
                    if (DateTime.TryParse($"{time.Item3}{customTimeSepForDT}{time.Item4}{customTimeSepForDT}{time.Item5}", dateTimeFormat, DateTimeStyles.None, out var result))
                    {
                        TimeSpan tsResult = result.TimeOfDay;

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
                if (useSecondTime || !onlyCustomTimeTranslation)
                {
                    if (TryCreateTimeSpan(time.Item3.ToString(), time.Item4.ToString(), time.Item5.ToString(), time.Item2.Length > 0, out var result))
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

                throw new FormatException("Invalid TimeSpan format.");
            });

            var shortTime = shortTimeSpanDefinition.Then<LogicalExpression>((ctx, time) =>
            {
                string customTimeSepForDT = dateTimeFormat.TimeSeparator;
                if (useSecondTime || onlyCustomTimeTranslation)
                {
                    if (DateTime.TryParse($"{time.Item3}{customTimeSepForDT}{time.Item4}", dateTimeFormat, DateTimeStyles.None, out var result))
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
                if (useSecondTime || !onlyCustomTimeTranslation)
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

                throw new FormatException("Invalid TimeSpan format.");
            });

            if (use12HourTime)
            {
                string customTimeSepForDT = dateTimeFormat.TimeSeparator;
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
                        if (DateTime.TryParse($"{time.Item1}{customTimeSepForDT}{time.Item2}{customTimeSepForDT}{time.Item3}{amSpacer}{amPMValue}", dateTimeFormat, DateTimeStyles.None, out var result))
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

                    throw new FormatException("Invalid TimeSpan format.");
                });

                shortTime12 = shortTime12Definition!.Then<LogicalExpression>((ctx, time) =>
                {
                    string customTimeSepForDT = dateTimeFormat.TimeSeparator;
                    string amPMValue = time.Item3;
                    if (amPMValue.ToLower().Equals(amTimeFirstCharLower))
                        amPMValue = dateTimeFormat.AMDesignator;
                    else
                    if (amPMValue.ToLower().Equals(pmTimeFirstCharLower))
                        amPMValue = dateTimeFormat.PMDesignator;

                    if (useSecondTime || onlyCustomTimeTranslation)
                    {
                        if (DateTime.TryParse($"{time.Item1}{customTimeSepForDT}{time.Item2}{amSpacer}{amPMValue}", dateTimeFormat, DateTimeStyles.None, out var result))
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

                    throw new FormatException("Invalid TimeSpan format.");
                });
            }

            // dateAndTime => number/number/number number:number:number or custom
            var dateAndTime = dateDefinition.AndSkip(Literals.WhiteSpace()).And(timeDefinition).Then<LogicalExpression>((ctx, dateTime) =>
                {
                    string customDateSepForDT = dateTimeFormat.DateSeparator;
                    string customTimeSepForDT = dateTimeFormat.TimeSeparator;
                    if (useSecondDate || onlyCustomDateTranslation)
                    {
                        if (useSecondTime || onlyCustomTimeTranslation)
                        {
                            if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{customTimeSepForDT}{dateTime.Item4.Item3}", dateTimeFormat, DateTimeStyles.None, out var result))
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
                            if (DateTime.TryParse($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{customTimeSepForDT}{dateTime.Item4.Item3}", dateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }

                        if (useSecondTime || !onlyCustomTimeTranslation)
                        {
                            // Use the existing approach
                            if (DateTime.TryParseExact($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2}{builtInTimeSep}{dateTime.Item4.Item3}", ncalcDateTimeMasks, _currentCultureFormatProvider, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }
                    }

                    throw new FormatException("Invalid DateTime format.");
                });

            // dateAndTimeIso => number-number-number'T'number:number:number[.number]Z
            var dateAndTimeIso = dateDefinitionIso.AndSkip(Terms.Char('T')).And(timeDefinitionIso).AndSkip(ZeroOrOne(Terms.Char('Z'))).Then<LogicalExpression>((ctx, dateTime) =>
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
                        DateTime result = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc).AddTicks(fractionTicks);
                        return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                    catch
                    {
                        throw new FormatException("Invalid DateTime format.");
                    }
                });

            var dateAndShortTime = dateDefinition.AndSkip(Literals.WhiteSpace()).And(shortTimeDefinition).Then<LogicalExpression>((ctx, dateTime) =>
                {
                    string customDateSepForDT = dateTimeFormat.DateSeparator;
                    string customTimeSepForDT = dateTimeFormat.TimeSeparator;
                    if (useSecondDate || onlyCustomDateTranslation)
                    {
                        if (useSecondTime || onlyCustomTimeTranslation)
                        {
                            if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}", dateTimeFormat, DateTimeStyles.None, out var result))
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
                            if (DateTime.TryParse($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}", dateTimeFormat, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }

                        if (useSecondTime || !onlyCustomTimeTranslation)
                        {
                            // Use the existing approach
                            if (DateTime.TryParseExact($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2}", ncalcDateShortTimeMasks, _currentCultureFormatProvider, DateTimeStyles.None, out var result))
                            {
                                return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                            }
                        }
                    }

                    throw new FormatException("Invalid DateTime format.");
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
                        string customTimeSepForDT = dateTimeFormat.TimeSeparator;
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
                                if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{customTimeSepForDT}{dateTime.Item4.Item3}{amSpacer}{amPMValue}", dateTimeFormat, DateTimeStyles.None, out var result))
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
                                if (DateTime.TryParse($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{customTimeSepForDT}{dateTime.Item4.Item3}{amSpacer}{amPMValue}", dateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }

                            if (useSecondTime || !onlyCustomTimeTranslation)
                            {
                                // Use the existing approach
                                if (DateTime.TryParseExact($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2}{builtInTimeSep}{dateTime.Item4.Item3} {amPMValue}", ncalcDateTime12Masks, _currentCultureFormatProvider, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }
                        }

                        throw new FormatException("Invalid DateTime format.");
                    });

                dateAndShortTime12 = dateDefinition.AndSkip(Literals.WhiteSpace()).And(shortTime12Definition!).Then<LogicalExpression>((ctx, dateTime) =>
                    {
                        string customDateSepForDT = dateTimeFormat.DateSeparator;
                        string customTimeSepForDT = dateTimeFormat.TimeSeparator;
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
                                if (DateTime.TryParse($"{dateTime.Item1}{customDateSepForDT}{dateTime.Item2}{customDateSepForDT}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{amSpacer}{amPMValue}", dateTimeFormat, DateTimeStyles.None, out var result))
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
                                if (DateTime.TryParse($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{customTimeSepForDT}{dateTime.Item4.Item2}{amSpacer}{amPMValue}", dateTimeFormat, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }

                            if (useSecondTime || !onlyCustomTimeTranslation)
                            {
                                // Use the existing approach
                                if (DateTime.TryParseExact($"{dateTime.Item1}{builtInDateSep}{dateTime.Item2}{builtInDateSep}{dateTime.Item3} {dateTime.Item4.Item1}{builtInTimeSep}{dateTime.Item4.Item2} {amPMValue}", ncalcDateShortTime12Masks, _currentCultureFormatProvider, DateTimeStyles.None, out var result))
                                {
                                    return new ValueExpression(result).SetLocation(new ParlotExpressionLocation(ctx));
                                }
                            }
                        }

                        throw new FormatException("Invalid DateTime format.");
                    });
            }

            Parser<LogicalExpression>? humaneTimeSpan = null;

            if (extOptions?.Flags.HasFlag(AdvExpressionOptions.ParseHumanePeriods) == true)
            {
                Parser<string>? alphaText = Terms.Pattern(c => char.IsLetter(c) || c == '\'').Then<string>(x => x.ToString() ?? string.Empty);

                var intNumberForPeriod = Terms.Number<int>(NumberOptions.Integer | useNumberGroupSeparatorFlag | useUnderscoreFlag, decimalSeparator, numGroupSeparator)
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

                    // Shifts a moment by the period; the period is applied when the value is read, as "1 month" depends on the month
                    DateTime Shift(DateTime dt, int sign)
                    {
                        if (yearValue != 0)
                            dt = dt.AddYears(sign * yearValue);
                        if (monthValue != 0)
                            dt = dt.AddMonths(sign * monthValue);
                        if (weekValue != 0)
                            dt = dt.AddDays(sign * weekValue * 7);
                        if (dayValue != 0)
                            dt = dt.AddDays(sign * dayValue);
                        if (hourValue != 0)
                            dt = dt.AddHours(sign * hourValue);
                        if (minuteValue != 0)
                            dt = dt.AddMinutes(sign * minuteValue);
                        if (secondValue != 0)
                            dt = dt.AddSeconds(sign * secondValue);
                        if (msecValue != 0)
                            dt = dt.AddMilliseconds(sign * msecValue);
                        return dt;
                    }

                    // The values below depend on the current time, so they are computed whenever the expression is evaluated, not when it is parsed
                    if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix))
                    {
                        return new ValueExpression(() =>
                        {
                            DateTime current = DateTime.UtcNow;
                            return Shift(current, 1) - current;
                        }, NCalc.Domain.ValueType.TimeSpan).SetLocation(new ParlotExpressionLocation(ctx));
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
                        Func<DateTime, DateTime> fromNow;
                        if (prefix is not null && extOptions.PeriodNowIndicators.Contains(prefix))
                            fromNow = static now => now;
                        else
                        if (prefix is not null && extOptions.PeriodTodayIndicators.Contains(prefix))
                            fromNow = static now => now.Date;
                        else
                        if (prefix is not null && extOptions.PeriodYesterdayIndicators.Contains(prefix))
                            fromNow = static now => now.Date.AddDays(-1);
                        else
                        if (prefix is not null && extOptions.PeriodTomorrowIndicators.Contains(prefix))
                            fromNow = static now => now.Date.AddDays(1);
                        else
                        if ((prefix is not null && extOptions.PeriodPastIndicators.Contains(prefix)) || (suffix is not null && extOptions.PeriodPastIndicators.Contains(suffix)))
                            fromNow = now => Shift(now, -1);
                        else
                        if ((prefix is not null && extOptions.PeriodFutureIndicators.Contains(prefix)) || (suffix is not null && extOptions.PeriodFutureIndicators.Contains(suffix)))
                            fromNow = now => Shift(now, 1);
                        else
                        {
                            throw new FormatException(string.Format(errUnrecognizedTimeRelationIndicator, prefix));
                        }

                        return new ValueExpression(() => fromNow(DateTime.Now), NCalc.Domain.ValueType.DateTime).SetLocation(new ParlotExpressionLocation(ctx));
                    }
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
            guidWithoutHyphens = thirtyTwoHexSequence
                .AndSkip(Not(identifierPartChar))
                .When(static (_, g) => g.Span.IndexOfAny("0123456789".AsSpan()) >= 0 && g.Span.IndexOfAny("abcdefABCDEF".AsSpan()) >= 0)
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
                minus.AndSkip(noSecondSign).SkipAnd(literal).Then<LogicalExpression>(static x => new UnaryExpression(UnaryExpressionType.Negate, x)),
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

        // The assignment target and the leftmost operand of an ordinary expression both start with indexedAccess,
        // so each position is parsed once rather than once per alternative at every nesting level
        if (useAssignments)
            indexedAccess = Memoized(indexedAccess);

        // factorial => primary ("!")* ;
        // A factorial includes any primary
        var factorial = indexedAccess.And(ZeroOrMany(exclamationMark.AndSkip(Not(equal))))
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

        // Either a factorial, primary, or exponential
        // exponential => factorial ( "**" factorial )* ;
        var exponential = nextParser.And(ZeroOrMany(exponent.And(factorial)))
        .Then(static (ctx, x) =>
        {
            LogicalExpression result = null!;

            switch (x.Item2.Count)
            {
                case 0:
                    return x.Item1;
                case 1:
                    return new BinaryExpression(BinaryExpressionType.Exponentiation, x.Item1, x.Item2[0].Item2).SetLocation(new ParlotExpressionLocation(ctx));
                default:
                {
                    // Exponentiation is right-associative: a ** b ** c ** d = a ** (b ** (c ** d))
                    result = x.Item2[x.Item2.Count - 1].Item2;
                    for (int i = x.Item2.Count - 2; i >= 0; i--)
                    {
                        result = new BinaryExpression(BinaryExpressionType.Exponentiation, x.Item2[i].Item2,
                            result).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    return new BinaryExpression(BinaryExpressionType.Exponentiation, x.Item1, result).SetLocation(new ParlotExpressionLocation(ctx));
                }
            }
        });

        // ( "-" | "!" | "not" | "~" | root2 | root3 | root4 ) factorial | exponential | primary;
        List<(Parser<string>, Func<ParseContext, LogicalExpression, LogicalExpression>)> unaryOps =
        [
            (not, static (ctx, value) => new UnaryExpression(UnaryExpressionType.Not, value).SetLocation(new ParlotExpressionLocation(ctx))),
            (minus, static (ctx, value)  => new UnaryExpression(UnaryExpressionType.Negate, value).SetLocation(new ParlotExpressionLocation(ctx))),
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
        var ternary = logical.And(ZeroOrOne(questionMark.SkipAnd(logical).AndSkip(colon).And(logical)))
            .Then(static (ctx, x) =>
                x.Item2.Item1 is null
                    ? x.Item1
                    : new TernaryExpression(x.Item1, x.Item2.Item1, x.Item2.Item2).SetLocation(new ParlotExpressionLocation(ctx)));

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

        var operatorSequence = @return.LeftAssociative(
            (OneOrMany(OneOf(operatorSequenceElements.ToArray())),
                static (_, _) => throw new InvalidOperationException("Unknown operator sequence.")));

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
                    closeParen.ElseError("Parenthesis not closed."));

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

        var expressionParser = OneOf(topLevel, curlyBracedTopLevel).AndSkip(ZeroOrMany(whitespaceParser)).Eof()
                .ElseError(InvalidTokenMessage);

        //AppContext.TryGetSwitch("NCalc.EnableParlotParserCompilation", out var enableParserCompilation);

        return /*enableParserCompilation ? expressionParser.Compile() : */expressionParser;
    }

    private static BigDecimal? TryParseDecimal((LogicalExpression, TextSpan, LogicalExpression, (TextSpan, LogicalExpression)) val, bool useUnderscores)
    {
        StringBuilder sb = new();
        if (val.Item1 is not null)
            sb.Append(val.Item1.ToString());
        else
            sb.Append('0');

        sb.Append(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator); // BigDecimal uses the current culture's separator
        // With the fractional part, we have text that we may need to sanitize
        string fracPart = val.Item3.ToString();
        if (fracPart.Length > 0 && fracPart[0] == '\'')
            fracPart = fracPart[1..^1];
        if (useUnderscores)
            fracPart = fracPart.Replace("_", "");
        sb.Append(fracPart);
        /*if (val.Item4.Item1.Length == 0)
        {
            if (val.Item4.Item2 is not null)
                return null;
        }
*/
        if (val.Item4.Item1.Length != 0 && val.Item4.Item2 is not null)
        {
            sb.Append('E');
            sb.Append(val.Item4.Item2.ToString()); // fractional part
        }
        if (BigDecimal.TryParse(sb.ToString(), out BigDecimal result))
            return result;
        else
            return null;
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
        Parser<LogicalExpression> parserToUse = CreateExpressionParser(context.CultureInfo, context.Options, context.AdvancedOptions);

        return Parse(parserToUse, context);
    }

    /// <summary>
    /// The stack size, in bytes, of the thread on which an expression that is nested too deeply to be parsed on the calling thread
    /// is parsed once more. The default is 16 MB; 0 turns the second attempt off.
    /// The resulting expression tree is as deep as its text, so evaluating it may require <see cref="ExpressionOptions.UseNonRecursiveEvaluator"/>.
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
            message = $"{error.Message} at position {position}";
        }
        else
        {
            position = context.Scanner.Cursor.Position;
            message = $"Error parsing the expression at position {position}";
        }

        throw new NCalcParserException(message, position);
    }
}

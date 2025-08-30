using System.Buffers;
using System.Numerics;
using ExtendedNumerics;
using NCalc.Domain;
using NCalc.Exceptions;

using Parlot;
using Parlot.Fluent;

using static Parlot.Fluent.Parsers;

using Identifier = NCalc.Domain.Identifier;

namespace NCalc.Parser;

/// <summary>
/// Class responsible for parsing strings into <see cref="LogicalExpression"/> objects.
/// </summary>
public static class LogicalExpressionParser
{
    private static readonly ConcurrentDictionary<CultureInfo, Parser<LogicalExpression>> Parsers = new();

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

    static LogicalExpressionParser()
    {
        // InternalInit sets Parser (as before), and then we set it again here to satisfy the compiler's requirements
        Parsers[CultureInfo.CurrentCulture] = CreateExpressionParser(CultureInfo.CurrentCulture, ExpressionOptions.None, null /*AdvancedExpressionOptions.DefaultOptions*/);
    }

    /// <summary>
    /// Creates the parser with the options that exist at the moment of call
    /// </summary>
    public static void ReInitialize()
    {
        Parsers[CultureInfo.CurrentCulture] = CreateExpressionParser();
    }

    /// <summary>
    /// Creates the parser with the options that exist at the moment of call
    /// </summary>
    /// <returns>An instance of the newly created parser</returns>
    private static Parser<LogicalExpression> CreateExpressionParser()
    {
        return CreateExpressionParser(CultureInfo.CurrentCulture, ExpressionOptions.None, null /*AdvancedExpressionOptions.DefaultOptions*/);
    }

    private static Parser<LogicalExpression> CreateExpressionParser(CultureInfo cultureInfo, ExpressionOptions options, AdvancedExpressionOptions? extOptions)
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

        var hexNumber = Terms.Text("0x")
            .SkipAnd(Terms.Pattern(c => acceptableHexChars.Contains(c)))
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                string? strValue = x.ToString();

                if (string.IsNullOrEmpty(strValue))
                    throw new ArgumentException($"{strValue} is not a valid hex number");

                if (((LogicalExpressionParserContext)ctx).AcceptUnderscores)
                    strValue = strValue!.Replace("_", string.Empty);

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
                        long converted = Convert.ToInt64(strValue, 16);
                        if (converted >= int.MinValue && converted <= int.MaxValue)
                            return new ValueExpression((object)(int)converted).SetLocation(new ParlotExpressionLocation(ctx));
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
                    throw new ArgumentException($"{strValue} is not a valid hex number");

                return new ValueExpression((object) result).SetLocation(new ParlotExpressionLocation(ctx));
            });

        string acceptableOctalChars = acceptUnderscores ? "01234567_" : "01234567";

        Parser<string> octalPrefixParser = (extOptions?.Flags.HasFlag(AdvExpressionOptions.AcceptCStyleOctals) == true) ? OneOf(Terms.Text("0o"), Terms.Text("0")) : Terms.Text("0o");

        var octalNumber = octalPrefixParser
            .SkipAnd(Terms.Pattern(c => acceptableOctalChars.Contains(c)))
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                string? strValue = x.ToString();

                if (string.IsNullOrEmpty(strValue))
                    throw new ArgumentException($"{strValue} is not a valid octal number");

                if (((LogicalExpressionParserContext)ctx).AcceptUnderscores)
                    strValue = strValue!.Replace("_", string.Empty);

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
                        long converted = Convert.ToInt64(strValue, 8);
                        if (converted >= int.MinValue && converted <= int.MaxValue)
                            return new ValueExpression((object)(int)converted).SetLocation(new ParlotExpressionLocation(ctx));
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
                    throw new ArgumentException($"{strValue} is not a valid octal number");

                return new ValueExpression((object)result).SetLocation(new ParlotExpressionLocation(ctx));
            });

        var binaryNumber = Terms.Text("0b")
            .SkipAnd(Terms.Pattern(c => c == '0' || c == '1' || (acceptUnderscores && c == '_')))
            .Then<LogicalExpression>(static (ctx, x) =>
            {
                string? strValue = x.ToString();

                if (string.IsNullOrEmpty(strValue))
                    throw new ArgumentException($"{strValue} is not a valid binary number");

                if (((LogicalExpressionParserContext)ctx).AcceptUnderscores)
                    strValue = strValue!.Replace("_", string.Empty);

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
                        long converted = Convert.ToInt64(strValue, 2);
                        if (converted >= int.MinValue && converted <= int.MaxValue)
                            return new ValueExpression((object)(int)converted).SetLocation(new ParlotExpressionLocation(ctx));
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
                    throw new ArgumentException($"{strValue} is not a valid hex number");

                return new ValueExpression((object)result);
            });

        Parser<LogicalExpression> hexOctBinNumber;

        hexOctBinNumber = OneOf(hexNumber!, octalNumber!, binaryNumber!);

        char decimalSeparator = (extOptions != null) ? extOptions.GetDecimalSeparatorChar() : Parlot.Fluent.NumberLiterals.DefaultDecimalSeparator; // this method will return the default separator, if needed
        char decimalSeparator2 = (extOptions?.GetSecondaryDecimalSeparatorChar()) ?? '\0';
        char numGroupSeparator = (extOptions != null) ? extOptions.GetNumberGroupSeparatorChar() : Parlot.Fluent.NumberLiterals.DefaultGroupSeparator; // this method will return the default separator, if needed

        NumberOptions useNumberGroupSeparatorFlag = ((extOptions != null) && (numGroupSeparator != '\0')) ? NumberOptions.AllowGroupSeparators : NumberOptions.None;
        NumberOptions useUnderscoreFlag = acceptUnderscores ? NumberOptions.AllowUnderscore : NumberOptions.None;

        Parser<string>[] floatNumExclusions =
            (decimalSeparator2 != '\0')
            ? [Terms.Text(decimalSeparator.ToString()), Terms.Text(decimalSeparator2.ToString()), Terms.Text("E", true)]
            : [Terms.Text(decimalSeparator.ToString()), Terms.Text("E", true)];

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
                bool useDecimal = ((LogicalExpressionParserContext)ctx).Options.HasFlag(ExpressionOptions.DecimalAsDefault);
                if (useDecimal)
                    return new ValueExpression(val).SetLocation(new ParlotExpressionLocation(ctx));

                return new ValueExpression((double)val).SetLocation(new ParlotExpressionLocation(ctx));
            });

        var doubleNumber = Terms.Number<double>(NumberOptions.Float | useNumberGroupSeparatorFlag | useUnderscoreFlag | NumberOptions.RequireFractionalPartForDecimals, decimalSeparator, numGroupSeparator, decimalSeparator2)
            .Then<LogicalExpression>(static (ctx, val) =>
            {
                bool useDecimal = ((LogicalExpressionParserContext)ctx).Options.HasFlag(ExpressionOptions.DecimalAsDefault);
                if (useDecimal)
                {
                    if (val > MaxDecDouble)
                        return new ValueExpression(double.PositiveInfinity).SetLocation(new ParlotExpressionLocation(ctx));

                    if (val < MinDecDouble)
                        return new ValueExpression(double.NegativeInfinity).SetLocation(new ParlotExpressionLocation(ctx));

                    return new ValueExpression((decimal)val).SetLocation(new ParlotExpressionLocation(ctx));
                }

                return new ValueExpression(val);
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
                .And(Terms.AnyOf(decimalSeparators))
                .And(bigUIntNumberD)
                .And(ZeroOrOne(Terms.AnyOf("Ee")))
                .And(ZeroOrOne(bigIntNumberD))
                .When((_, val) => TryParseDecimal(val, acceptUnderscores) != null)
                .Then<LogicalExpression>(static (ctx, val) =>
                {
                    bool useDecimal = ((LogicalExpressionParserContext)ctx).Options.HasFlag(ExpressionOptions.DecimalAsDefault);

                    BigDecimal? value = TryParseDecimal(val, ((LogicalExpressionParserContext)ctx).AcceptUnderscores);

                    if (value == null)
                        return new ValueExpression();  // never happens - the When condition ensures that val can be parsed

                    if (useDecimal && (value >= decimal.MinValue && value <= decimal.MaxValue))
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

                    if (!useDecimal && (value >= decimal.MinValue && value <= decimal.MaxValue))
                    {
                        decimal decValue = (decimal)value;
                        if (value == decValue)
                            return new ValueExpression(decValue).SetLocation(new ParlotExpressionLocation(ctx));
                    }

                    return new ValueExpression(value).SetLocation(new ParlotExpressionLocation(ctx));
                });
        }

        var decimalOrDoubleNumber = (bigDecimalNumber is not null) ? OneOf(bigDecimalNumber, decimalNumber, doubleNumber) : OneOf(decimalNumber, doubleNumber);

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
        bool useCharsForOps = !options.HasFlag(ExpressionOptions.SkipLogicalAndBitwiseOpChars);
        bool useUnicodeForOps = options.HasFlag(ExpressionOptions.UseUnicodeCharsForOperations);
        bool useAssignments = options.HasFlag(ExpressionOptions.UseAssignments);

        var percentChar = Terms.Char('%'); // CultureInfo defines a percent character, but we are yet to see another character than '%'

        var comma = Terms.Char(',');
        var divided = useUnicodeForOps ? OneOf(Terms.Text("/"), Terms.Text(":"), Terms.Text("\u00F7")) : Terms.Text("/");
        var times = useUnicodeForOps ? OneOf(Terms.Text("*"), Terms.Text("\u00D7"), Terms.Text("\u2219")) : Terms.Text("*");
        var modulo = calculatePercent ? Terms.Text("mod", true) : OneOf(Terms.Text("%"), Terms.Text("mod", true));
        var intDivB = OneOf(Terms.Text("\\"), Terms.Text("div", true));
        var intDivP = Terms.Text("//");
        var minus = Terms.Text("-");
        var plus = Terms.Text("+");

        var equal = options.HasFlag(ExpressionOptions.UseCStyleAssignments) ? Terms.Text("==") : OneOf(Terms.Text("=="), Terms.Text("="));
        var notEqual = useUnicodeForOps ? OneOf(Terms.Text("<>"), Terms.Text("!="), Terms.Text("\u2260")) : OneOf(Terms.Text("<>"), Terms.Text("!="));
        var @in = useUnicodeForOps ? OneOf(Terms.Text("in", true), Terms.Text("\u2208")) : Terms.Text("in", true);
        var notIn = useUnicodeForOps ? OneOf(Terms.Text("not in", true), Terms.Text("\u2209")) : Terms.Text("not in", true);

        var like = Terms.Text("like", true);
        var notLike = Terms.Text("not like", true);

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

        var dotChar = Terms.Char('.');

        var statementEnd = semicolon;

        Parser<string>? root2 = useCharsForOps ? Terms.Text("\u221A") : null;
#if NET8_0_OR_GREATER
        Parser<string>? root3 = useCharsForOps ? Terms.Text("\u221B") : null;
#endif
        Parser<string>? root4 = useCharsForOps ? Terms.Text("\u221C") : null;

        var resultRefChar = Terms.Char('@');
        var atChar = Terms.Char('@');

        var letterIdentifier =
#if NET8_0_OR_GREATER
            Terms.Identifier(SearchValues.Create("_" + Character.AZ), SearchValues.Create("_" + Character.AlphaNumeric));
#else
            Terms.Identifier();
#endif

        var identifier = supportCurrency ? letterIdentifier : Terms.Identifier();
        // We don't let $ at the beginning of identifiers as it may be confused with currency

        Parser<string>? not;
        Parser<string>? and;
        Parser<string>? or;
        Parser<string>? xor;

        if (useCharsForOps)
        {
            and = useUnicodeForOps ? OneOf(Terms.Text("AND", true), Terms.Text("&&"), Terms.Text("\u2227")) : OneOf(Terms.Text("AND", true), Terms.Text("&&"));
            or = useUnicodeForOps ? OneOf(Terms.Text("OR", true), Terms.Text("||"), Terms.Text("\u2228")) : OneOf(Terms.Text("OR", true), Terms.Text("||"));
            not = useUnicodeForOps
                ? OneOf(Terms.Text("NOT", true).AndSkip(OneOf(Literals.WhiteSpace().Or(Not(AnyCharBefore(openParen))))), Terms.Text("!"), Terms.Text("\u00ac"))
                : OneOf(Terms.Text("NOT", true).AndSkip(OneOf(Literals.WhiteSpace().Or(Not(AnyCharBefore(openParen))))), Terms.Text("!"));
        }
        else
        {
            and = useUnicodeForOps ? OneOf(Terms.Text("AND", true), Terms.Text("\u2227")) : Terms.Text("AND", true);
            or = useUnicodeForOps ? OneOf(Terms.Text("OR", true), Terms.Text("\u2228")) : Terms.Text("OR", true);
            not = useUnicodeForOps
                ? OneOf(Terms.Text("NOT", true).AndSkip(OneOf(Literals.WhiteSpace().Or(Not(AnyCharBefore(openParen))))), Terms.Text("\u00ac"))
                : Terms.Text("NOT", true).AndSkip(OneOf(Literals.WhiteSpace().Or(Not(AnyCharBefore(openParen)))));
        }
        xor = useUnicodeForOps ? OneOf(Terms.Text("XOR", true), Terms.Text("\u2295"), Terms.Text("\u22BB")) : Terms.Text("XOR", true);

        var bitwiseAnd = useCharsForOps ? OneOf(Terms.Text("BIT_AND", true), Terms.Text("&")) : Terms.Text("BIT_AND", true);
        var bitwiseOr = useCharsForOps ? OneOf(Terms.Text("BIT_OR", true), Terms.Text("|"))  : Terms.Text("BIT_OR", true);
        var bitwiseXOr = useCharsForOps ? OneOf(Terms.Text("BIT_XOR", true), Terms.Text("^")) : Terms.Text("BIT_XOR", true);
        var bitwiseNot = useCharsForOps ? OneOf(Terms.Text("BIT_NOT", true), Terms.Text("~")) : Terms.Text("BIT_NOT", true);
        var returnParser = Terms.Text("return");

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
        var divAssign = Terms.Text("/=");
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

        // list => "(" (expression ("," expression)*)? ")"
        var populatedList =
            Between(openParen, Separated(comma.Or(semicolon)/*(decimalSeparator == ',' || decimalSeparator2 == ',' || numGroupSeparator == ',' ? semicolon : comma.Or(semicolon))*/, expressionOrBracedStatementSequence),
                    closeParen.ElseError("Parenthesis not closed."))
                .Then<LogicalExpression>(static (ctx, values) => new LogicalExpressionList(values).SetLocation(new ParlotExpressionLocation(ctx)));

        var emptyList = openParen.AndSkip(closeParen).Then<LogicalExpression>(static _ => new LogicalExpressionList());

        var list = OneOf(emptyList, populatedList);

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

        var booleanTrue = Terms.Text("true", true)
                .Then<LogicalExpression>(True);
        var booleanFalse = Terms.Text("false", true)
            .Then<LogicalExpression>(False);

        var theNull = Terms.Text("null", true)
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

        var rawStringValue = atChar.SkipAnd(Terms.Char('"')).SkipAnd(Literals.NoneOf("\"")).AndSkip(Terms.Char('"'))
            .Then<LogicalExpression>(static (ctx, value) =>
                new ValueExpression(value.ToString(), StringKind.RawDoubleQuote).SetLocation(new ParlotExpressionLocation(ctx)));

        var backQuoteStringValue = Terms.Char('`').SkipAnd(Literals.NoneOf("`")).AndSkip(Terms.Char('`'))
            .Then<LogicalExpression>(static (ctx, value) =>
                new ValueExpression(value.ToString(), StringKind.BackQuote).SetLocation(new ParlotExpressionLocation(ctx)));

        var stringValue = OneOf(singleQuotesStringValue, doubleQuotesStringValue, rawStringValue, backQuoteStringValue);

        var charIsNumber = Literals.Pattern(char.IsNumber);
        var charIsNumberWithWhitespace = Terms.Pattern(char.IsNumber);

        // Add proper date and time support

        SequenceAndSkip<LogicalExpression, char>? dateTime = null;

        if (!options.HasFlag(ExpressionOptions.DontParseDates))
        {
            DateTimeFormatInfo dateTimeFormat = extOptions?.GetFormat(typeof(DateTimeFormatInfo)) as DateTimeFormatInfo ?? cultureInfo?.DateTimeFormat ?? CultureInfo.CurrentCulture.DateTimeFormat;

            Sequence<TextSpan, TextSpan, TextSpan> dateDefinition;

            Parser<LogicalExpression> date;

            // The following block prepares the masks for the approach to parsing used by ncalc by default -
            // parsing of "x/y/z" in dates with the current culture info (which will likely not work in some locales).
            // So, these masks below fix the format to be "x/y/z" in the order used by the current culture.
            string[] ncalcDateMasks = new string[2];
            string[] ncalcDateTimeMasks = new string[2];
            string[] ncalcDateShortTimeMasks = new string[2];
            string[] ncalcDateTime12Masks = new string[4];
            string[] ncalcDateShortTime12Masks = new string[4];

            CultureInfo culture = cultureInfo ?? CultureInfo.CurrentCulture;

            string builtInDateSep = (cultureInfo ?? CultureInfo.CurrentCulture).DateTimeFormat.DateSeparator;
            string builtInTimeSep = (cultureInfo ?? CultureInfo.CurrentCulture).DateTimeFormat.TimeSeparator;

            string datePattern = culture.DateTimeFormat.ShortDatePattern;
            if (string.IsNullOrEmpty(datePattern))
            {
                ncalcDateMasks[0] = string.Join(builtInDateSep, "d", "M", "yyyy");
                ncalcDateMasks[1] = string.Join(builtInDateSep, "d", "M", "yy");
            }
            else
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

            // Define some masks for date-time values with both long and short time
            ncalcDateTimeMasks[0] = string.Join(" ", ncalcDateMasks[0], string.Join(builtInTimeSep, "H", "m", "s"));
            ncalcDateTimeMasks[1] = string.Join(" ", ncalcDateMasks[1], string.Join(builtInTimeSep, "H", "m", "s"));
            ncalcDateShortTimeMasks[0] = string.Join(" ", ncalcDateMasks[0], string.Join(builtInTimeSep, "H", "m"));
            ncalcDateShortTimeMasks[1] = string.Join(" ", ncalcDateMasks[1], string.Join(builtInTimeSep, "H", "m"));

            bool useSecondDate = false;
            bool onlyCustomDateTranslation = false;
            string customDateSep = builtInDateSep;

            if (extOptions != null)
            {
                customDateSep = extOptions.GetDateSeparator();
                if (customDateSep != builtInDateSep && !extOptions.Flags.HasFlag(AdvExpressionOptions.SkipBuiltInDateSeparator))
                    useSecondDate = true; // we use the second date separator when both custom separator and the default slash are enabled
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

            Sequence<TextSpan, TextSpan, TextSpan, string>? time12Definition = null;
            Sequence<TextSpan, TextSpan, TextSpan> timeDefinition;
            Sequence<string, TextSpan, TextSpan, TextSpan, TextSpan> timeSpanDefinition;
            Sequence<TextSpan, TextSpan, string>? shortTime12Definition = null;
            Sequence<TextSpan, TextSpan> shortTimeDefinition;
            Sequence<string, TextSpan, TextSpan, TextSpan> shortTimeSpanDefinition;

            bool use12HourTime = (extOptions == null) ? dateTimeFormat.ShortTimePattern.Contains("t") : extOptions.Use12HourTime();

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

            if (extOptions != null)
            {
                customTimeSep = extOptions.TimeSeparator;
                if (customTimeSep != builtInTimeSep && !extOptions.Flags.HasFlag(AdvExpressionOptions.SkipBuiltInTimeSeparator))
                    useSecondTime = true; // we use the second time separator when both custom separator and the default one are enabled and are different
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
                    if (TimeSpan.TryParse($"{time.Item3}{builtInTimeSep}{time.Item4}{builtInTimeSep}{time.Item5}", out var result))
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
                    if (TimeSpan.TryParse($"{time.Item3}{builtInTimeSep}{time.Item4}", out var result))
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
                            throw new FormatException(string.Format(errUnrecognizedPeriodIndicator, entry.Item2));
                    }

                    if (string.IsNullOrEmpty(prefix) && string.IsNullOrEmpty(suffix))
                    {
                        DateTime current = DateTime.UtcNow;
                        DateTime dt = current;
                        if (yearValue != 0)
                            dt = dt.AddYears(yearValue);
                        if (monthValue != 0)
                            dt = dt.AddMonths(monthValue);
                        if (weekValue != 0)
                            dt = dt.AddDays(weekValue * 7);
                        if (dayValue != 0)
                            dt = dt.AddDays(dayValue);
                        if (hourValue != 0)
                            dt = dt.AddHours(hourValue);
                        if (minuteValue != 0)
                            dt = dt.AddMinutes(minuteValue);
                        if (secondValue != 0)
                            dt = dt.AddSeconds(secondValue);
                        if (msecValue != 0)
                            dt = dt.AddMilliseconds(msecValue);
                        return new ValueExpression(dt - current).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                    else
                    {
                        if (!(string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix)))
                        {
                            throw new FormatException(string.Format(errDuplicateTimeRelationIndicators, prefix, suffix));
                        }

                        bool addTime = false;
                        bool pastTime = false;
                        DateTime dt = DateTime.Now; // people are interested in local time now, today, before, or after current moment

                        prefix = prefix?.ToLowerInvariant();
                        suffix = suffix?.ToLowerInvariant();

                        if (prefix != null && extOptions.PeriodNowIndicators.Contains(prefix))
                        {
                            addTime = false;  // ... and use dt as is
                        }
                        else
                        if (prefix != null && extOptions.PeriodTodayIndicators.Contains(prefix))
                        {
                            addTime = false;
                            dt = dt.Date;
                        }
                        else
                        if ((prefix != null && extOptions.PeriodPastIndicators.Contains(prefix)) || (suffix != null && extOptions.PeriodPastIndicators.Contains(suffix)))
                        {
                            addTime = true;
                            pastTime = true;
                        }
                        else
                        if ((prefix != null && extOptions.PeriodFutureIndicators.Contains(prefix)) || (suffix != null && extOptions.PeriodFutureIndicators.Contains(suffix)))
                        {
                            addTime = true;
                            pastTime = false;
                        }
                        else
                            throw new FormatException(string.Format(errUnrecognizedTimeRelationIndicator, prefix));

                        if (addTime)
                        {
                            if (pastTime)
                            {
                                yearValue = -yearValue;
                                monthValue = -monthValue;
                                weekValue = -weekValue;
                                dayValue = -dayValue;
                                hourValue = -hourValue;
                                minuteValue = -minuteValue;
                                secondValue = -secondValue;
                                msecValue = -msecValue;
                            }
                            if (yearValue != 0)
                                dt = dt.AddYears(yearValue);
                            if (monthValue != 0)
                                dt = dt.AddMonths(monthValue);
                            if (weekValue != 0)
                                dt = dt.AddDays(weekValue * 7);
                            if (dayValue != 0)
                                dt = dt.AddDays(dayValue);
                            if (hourValue != 0)
                                dt = dt.AddHours(hourValue);
                            if (minuteValue != 0)
                                dt = dt.AddMinutes(minuteValue);
                            if (secondValue != 0)
                                dt = dt.AddSeconds(secondValue);
                            if (msecValue != 0)
                                dt = dt.AddMilliseconds(msecValue);
                        }
                        return new ValueExpression(dt).SetLocation(new ParlotExpressionLocation(ctx));
                    }
                });
            }
            List<Parser<LogicalExpression>> timeParts = use12HourTime
                ? [dateAndTime12!, dateAndShortTime12!, dateAndTime, dateAndShortTime, date, time12!, shortTime12!, time, shortTime]
                : [dateAndTime, dateAndShortTime, date, time, shortTime];

            if (humaneTimeSpan != null)
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
            var eightHexSequence = Terms
                .Pattern(isHexDigit, 8, 8);

            var fourHexSequence = Terms
                .Pattern(isHexDigit, 4, 4);

            var twelveHexSequence = Terms
                .Pattern(isHexDigit, 12, 12);

            var thirtyTwoHexSequence = Terms
                .Pattern(isHexDigit, 32, 32);

            var guidWithHyphens = eightHexSequence
                    .AndSkip(minus)
                    .And(fourHexSequence)
                    .AndSkip(minus)
                    .And(fourHexSequence)
                    .AndSkip(minus)
                    .And(fourHexSequence)
                    .AndSkip(minus)
                    .And(twelveHexSequence)
                .Then<LogicalExpression>(static (ctx, g) =>
                        new ValueExpression(Guid.Parse(g.Item1.ToString() + g.Item2 + g.Item3 + g.Item4 + g.Item5)).SetLocation(new ParlotExpressionLocation(ctx)));

            Parser<LogicalExpression> guidWithoutHyphens;

            guidWithoutHyphens = thirtyTwoHexSequence
                .AndSkip(Not(decimalOrDoubleNumber))
                .Then<LogicalExpression>(static (ctx, g) => new ValueExpression(Guid.Parse(g.ToString()!)).SetLocation(new ParlotExpressionLocation(ctx)));

            guid = OneOf(guidWithHyphens, guidWithoutHyphens);
        }

        // primary => GUID | Percent | NUMBER | identifier | DateTime | string | resultReference | function | boolean | groupExpression | identifier | list ;

        List<Parser<LogicalExpression>> enabledParsers = [];

        if (guid != null)
            enabledParsers.Add(guid);
        enabledParsers.Add(hexOctBinNumber);
        if (currency != null)
            enabledParsers.Add(currency);
        enabledParsers.Add(intNumber);
        enabledParsers.Add(longNumber);
        if (bigIntNumber != null)
            enabledParsers.Add(bigIntNumber);
        enabledParsers.Add(decimalOrDoubleNumber);
        enabledParsers.Add(booleanTrue);
        enabledParsers.Add(booleanFalse);
        if (dateTime != null) // dateTime will be initialized unless options.HasFlag(ExpressionOptions.DontParseDates)
            enabledParsers.Add(dateTime);
        enabledParsers.Add(stringValue);
        enabledParsers.Add(functionOrResultRef);
        enabledParsers.Add(groupExpression);
        enabledParsers.Add(bracketedIdentifierExpression);
        enabledParsers.Add(identifierExpression);
        enabledParsers.Add(list);
        enabledParsers.Add(bracedExpressionOrStatementSequence);

        var primary = ((options.HasFlag(ExpressionOptions.SupportCStyleComments) || options.HasFlag(ExpressionOptions.SupportPythonComments)) ? ZeroOrMany(comment).SkipAnd(OneOf(enabledParsers.ToArray())).AndSkip(ZeroOrMany(comment)) : OneOf(enabledParsers.ToArray()));

        var indexedAccess = primary.And(ZeroOrOne(rangedIndex))
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
                    throw new NCalcParserException("Ranged index could not be parsed", ctx.Scanner.Cursor.Position);
            });

        // factorial => primary ("!")* ;
        // A factorial includes any primary
        var factorial = OneOf(indexedAccess/*, indexedAccess*/).And(ZeroOrMany(exclamationMark.AndSkip(Not(equal))))
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

        Parser<LogicalExpression> factorialOrPercent;

        if (extOptions?.Flags.HasFlag(AdvExpressionOptions.CalculatePercent) == true)
        {
            Parser<LogicalExpression>? numberPercent = factorial.And(ZeroOrOne(percentChar, '\0'))
                .Then<LogicalExpression>(static (ctx, x) =>
                {
                    if (x.Item2 == '\0')
                    {
                        // there is just a primary discovered
                        return x.Item1;
                    }
                    return new PercentExpression(x.Item1).SetLocation(new ParlotExpressionLocation(ctx));
                });
            Parser<LogicalExpression>? numberPercent2 = percentChar.And(factorial)
                .Then<LogicalExpression>(static (ctx, x) => new PercentExpression(x.Item2).SetLocation(new ParlotExpressionLocation(ctx)));
            factorialOrPercent = OneOf(numberPercent, numberPercent2);
        }
        else
            factorialOrPercent = factorial;

        // Either a factorial, primary, or exponential
        // exponential => factorial ( "**" factorial )* ;
        var exponential = factorialOrPercent.And(ZeroOrMany(exponent.And(factorial)))
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
                        for (int i = x.Item2.Count - 1; i > 0; i--)
                        {
                            result = new BinaryExpression(BinaryExpressionType.Exponentiation, x.Item2[i - 1].Item2,
                                x.Item2[i].Item2).SetLocation(new ParlotExpressionLocation(ctx));
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
            (returnParser, static (ctx, value) => new UnaryExpression(UnaryExpressionType.Return, value).SetLocation(new ParlotExpressionLocation(ctx))),
        ];
        if (root2 != null)
            unaryOps.Add((root2, static (ctx, value) => new UnaryExpression(UnaryExpressionType.SqRoot, value).SetLocation(new ParlotExpressionLocation(ctx))));
#if NET8_0_OR_GREATER
        if (root3 != null)
            unaryOps.Add((root3, static (ctx, value) => new UnaryExpression(UnaryExpressionType.CbRoot, value).SetLocation(new ParlotExpressionLocation(ctx))));
#endif
        if (root4 != null)
            unaryOps.Add((root4, static (ctx, value) => new UnaryExpression(UnaryExpressionType.FourthRoot, value).SetLocation(new ParlotExpressionLocation(ctx))));
        var unary = exponential.Unary(unaryOps.ToArray());

        List<(Parser<string>, Func<ParseContext, LogicalExpression, LogicalExpression, LogicalExpression>)>  multiplicativeList = [
            (intDivB, static (ctx, a, b) => new BinaryExpression(BinaryExpressionType.IntDivB, a, b).SetLocation(new ParlotExpressionLocation(ctx))),
            (divided, static  (ctx, a, b) => new BinaryExpression(BinaryExpressionType.Div, a, b).SetLocation(new ParlotExpressionLocation(ctx))),
            (times, static  (ctx, a, b) => new BinaryExpression(BinaryExpressionType.Times, a, b).SetLocation(new ParlotExpressionLocation(ctx))),
            (modulo, static  (ctx, a, b) => new BinaryExpression(BinaryExpressionType.Modulo, a, b).SetLocation(new ParlotExpressionLocation(ctx)))];
        if (!options.HasFlag(ExpressionOptions.SupportCStyleComments))
        {
            multiplicativeList.Insert(1, (intDivP, (ctx, a, b) => new BinaryExpression(BinaryExpressionType.IntDivP, a, b).SetLocation(new ParlotExpressionLocation(ctx))));
        }
        // multiplicative => unary ( ( "/" | "*" | "%" ) unary )* ;
        var multiplicative = unary.LeftAssociative(multiplicativeList.ToArray());

        // additive => multiplicative ( ( "-" | "+" ) multiplicative )* ;
        var additive = multiplicative.LeftAssociative(
            (plus, static (ctx, a, b) => new BinaryExpression(BinaryExpressionType.Plus, a, b).SetLocation(new ParlotExpressionLocation(ctx))),
            (minus, static (ctx, a, b) => new BinaryExpression(BinaryExpressionType.Minus, a, b).SetLocation(new ParlotExpressionLocation(ctx)))
        );

        // shift => additive ( ( "<<" | ">>" ) additive )* ;
        var shift = additive.LeftAssociative(
            (leftShift, static (ctx, a, b) => new BinaryExpression(BinaryExpressionType.LeftShift, a, b).SetLocation(new ParlotExpressionLocation(ctx))),
            (rightShift, static (ctx, a, b) => new BinaryExpression(BinaryExpressionType.RightShift, a, b).SetLocation(new ParlotExpressionLocation(ctx)))
        );

        // relational => shift ( ( ">=" | "<=" | "<" | ">" | "in" | "not in" ) shift )* ;
        var relational = shift.And(ZeroOrMany(OneOf(
                    greaterOrEqual.Then(BinaryExpressionType.GreaterOrEqual),
                    lessOrEqual.Then(BinaryExpressionType.LessOrEqual),
                    less.Then(BinaryExpressionType.Less),
                    greater.Then(BinaryExpressionType.Greater),
                    @in.Then(BinaryExpressionType.In),
                    notIn.Then(BinaryExpressionType.NotIn),
                    like.Then(BinaryExpressionType.Like),
                    notLike.Then(BinaryExpressionType.NotLike)
                )
                .And(shift)))
            .Then(ParseBinaryExpression);

        var equality = relational.And(ZeroOrMany(OneOf(
                    equal.Then(BinaryExpressionType.Equal),
                    notEqual.Then(BinaryExpressionType.NotEqual))
                .And(relational)))
            .Then(ParseBinaryExpression);

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

        // ternary => logical("?" logical ":" logical) ?
        var ternary = logical.And(ZeroOrOne(questionMark.SkipAnd(logical).AndSkip(colon).And(logical)))
            .Then(static (ctx, x) =>
                x.Item2.Item1 == null
                    ? x.Item1
                    : new TernaryExpression(x.Item1, x.Item2.Item1, x.Item2.Item2).SetLocation(new ParlotExpressionLocation(ctx)))
            .Or(logical);

        List<Parser<string>> operatorSequenceElements = [
            intDivB, divided, times, modulo, plus,
            minus, leftShift, rightShift, greaterOrEqual,
            lessOrEqual, greater, less, equal,
            notEqual];
        if (!options.HasFlag(ExpressionOptions.SupportCStyleComments))
            operatorSequenceElements.Insert(0, intDivP);

        var operatorSequence = ternary.LeftAssociative(
            (OneOrMany(OneOf(operatorSequenceElements.ToArray())),
                static (_, _) => throw new InvalidOperationException("Unknown operator sequence.")));

        List<Parser<LogicalExpression>> statements = [];

        if (options.HasFlag(ExpressionOptions.UseLoops))
        {
            var whileLoop = Terms.Text("while", caseInsensitive: true).SkipAnd(Terms.Text("(")).SkipAnd(expressionOrBracedStatementSequence).AndSkip(Terms.Text(")")).And(expressionOrBracedStatementSequence)
                .Then<LogicalExpression>(static (_, x) =>
                    new BinaryExpression(BinaryExpressionType.WhileLoop, x.Item1, x.Item2)
                );
            statements.Add(whileLoop);
        }

        if (options.HasFlag(ExpressionOptions.UseIfStatement))
        {
            var ifStatement = Terms.Text("if", caseInsensitive: true).SkipAnd(Terms.Text("(")).SkipAnd(expressionOrBracedStatementSequence).AndSkip(Terms.Text(")")).And(expressionOrBracedStatementSequence).And(ZeroOrOne(Terms.Text("else", caseInsensitive: true).SkipAnd(expressionOrBracedStatementSequence)))
                    .Then<LogicalExpression>(static (_, x) =>
                        new IfStatementExpression(x.Item1, x.Item2, (LogicalExpression?)x.Item3 ?? new ValueExpression())
                    );

            statements.Add(ifStatement);
        }

        enabledParsers.Clear();
        if (guid != null)
            enabledParsers.Add(guid);
        enabledParsers.Add(hexOctBinNumber);
        if (currency != null)
            enabledParsers.Add(currency);
        enabledParsers.Add(intNumber);
        enabledParsers.Add(longNumber);
        if (bigIntNumber != null)
            enabledParsers.Add(bigIntNumber);
        enabledParsers.Add(decimalOrDoubleNumber);
        enabledParsers.Add(booleanTrue);
        enabledParsers.Add(booleanFalse);
        enabledParsers.Add(theNull);
        if (dateTime != null) // dateTime will be initialized unless options.HasFlag(ExpressionOptions.DontParseDates)
            enabledParsers.Add(dateTime);
        enabledParsers.Add(stringValue);

        var paramName = letterIdentifier.And(ZeroOrOne(Terms.Char('=').SkipAnd(OneOf(enabledParsers.ToArray())))).Then<FunctionParameter>(static x => new FunctionParameter(x.Item1.ToString()!, x.Item2 != null, x.Item2));

        var populatedParamList =
            Between(openParen, Separated(comma.Or(semicolon), paramName),
                    closeParen.ElseError("Parenthesis not closed."));

        Parser<IReadOnlyList<FunctionParameter>>? emptyParamList = openParen.AndSkip(closeParen).Then<IReadOnlyList<FunctionParameter>>(static _ => []);

        var paramList = OneOf(emptyParamList, populatedParamList);

        Parser<LogicalExpression> funcBodyExpression = Terms.Text("=>").SkipAnd(expression)
            .Then<LogicalExpression>(static (ctx, x) => x.SetLocation(new ParlotExpressionLocation(ctx)));

        Parser<LogicalExpression> functionDecl = ZeroOrOne(comment).AndSkip(Terms.Text("fn")).And(letterIdentifier).And(paramList).And(OneOf(bracedExpressionOrStatementSequence, funcBodyExpression))
             .Then<LogicalExpression>(static (ctx, x) =>
             {
                 Function function = new(x.Item2.ToString()!, x.Item4)
                 {
                     Description = x.Item1.ToString()
                 };
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
                        "/=" or "\u00F7=" => BinaryExpressionType.DivAssignment,
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
                        result = x.Item1;
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

        expression.Parser = expressionOrAssignment;

        expressionOrBracedStatementSequence.Parser = OneOf(curlyBracedTopLevel, expressionOrAssignment);
        bracedExpressionOrStatementSequence.Parser = curlyBracedTopLevel;

        var expressionParser = OneOf(topLevel, curlyBracedTopLevel).AndSkip(ZeroOrMany(Literals.WhiteSpace(true))).Eof()
                .ElseError(InvalidTokenMessage);

        AppContext.TryGetSwitch("NCalc.EnableParlotParserCompilation", out var enableParserCompilation);

        return enableParserCompilation ? expressionParser.Compile() : expressionParser;
    }

    private static BigDecimal? TryParseDecimal((LogicalExpression, TextSpan, LogicalExpression, TextSpan, LogicalExpression) val, bool useUnderscores)
    {
        StringBuilder sb = new();
        if (val.Item1 != null)
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
        if (val.Item4.Length == 0)
        {
            if (val.Item5 != null)
                return null;
        }

        if (val.Item4.Length != 0)
        {
            if (val.Item5 == null)
                return null;
            sb.Append('E');
            sb.Append(val.Item5.ToString()); // fractional part
        }
        if (BigDecimal.TryParse(sb.ToString(), out BigDecimal result))
            return result;
        else
            return null;
    }

    private static Parser<LogicalExpression> GetOrCreateExpressionParser(CultureInfo cultureInfo, LogicalExpressionParserContext context)
    {
        if (context.Options == ExpressionOptions.None && Parsers.TryGetValue(cultureInfo, out var parser))
        {
            return parser;
        }

        var newParser = CreateExpressionParser(cultureInfo, context.Options, context.AdvancedOptions);
        if (context.Options == ExpressionOptions.None)
        {
            Parsers.TryAdd(cultureInfo, newParser);
        }

        return newParser;
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
        Parser<LogicalExpression> parserToUse;
        if (context.AdvancedOptions is not null)
            parserToUse = CreateExpressionParser(context.CultureInfo, context.Options, context.AdvancedOptions);
        else
            parserToUse = GetOrCreateExpressionParser(context.CultureInfo, context);

        if (parserToUse.TryParse(context, out var result, out var error))
            return result;

        string message;
        TextPosition position;
        if (error != null)
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

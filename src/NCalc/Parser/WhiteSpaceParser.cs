using NCalc;
using NCalc.Domain;

using Parlot;
using Parlot.Fluent;

namespace NCalc.Parser;

public class WhiteSpaceParser : Parser<TextSpan>
{
    bool _supportCStyleComments = false;
    bool _supportPythonComments = false;
    bool _parseDates = true;

    // The key under which the outcome of a date/time literal check is remembered per position in the parse memo
    private static readonly object DateTimeLiteralCheckKey = new();

    // The key under which the outcome of a doc comment check ("///" lines before "fn") is remembered per position in the parse memo
    private static readonly object DocCommentCheckKey = new();

    private Parser<TextSpan> _commentParser;

    public WhiteSpaceParser(ExpressionOptions expressionOptions) : base()
    {
        _supportCStyleComments = expressionOptions.HasFlag(ExpressionOptions.SupportCStyleComments);
        _supportPythonComments = expressionOptions.HasFlag(ExpressionOptions.SupportPythonComments);
        _parseDates = !expressionOptions.HasFlag(ExpressionOptions.DontParseDates);
        List<Parser<TextSpan>> parsers = [];
        if (_supportCStyleComments)
        {
            parsers.Add(Parsers.Literals.Comments("//"));
            parsers.Add(Parsers.Literals.Comments("/*", "*/"));
        }
        if (_supportPythonComments)
            parsers.Add(Parsers.Literals.Comments("#"));

        //_commentParser = Parsers.Capture(Parsers.OneOrMany(Parsers.OneOf(parsers.ToArray())));
        _commentParser = Parsers.OneOf(parsers.ToArray());
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Offset;

        int bufferLength = context.Scanner.Buffer.Length;
        string nextChars;
        int charsLeft;

        while (true)
        {
            context.Scanner.SkipWhiteSpaceOrNewLine();
            charsLeft = bufferLength - context.Scanner.Cursor.Offset;
            if (charsLeft <= 0)
                break;

            // Exclude doc comments: "///" lines directly before "fn" describe the function. Elsewhere "///" is an ordinary comment
            if (_supportCStyleComments && charsLeft >= 3)
            {
                nextChars = context.Scanner.Buffer.Substring(context.Scanner.Cursor.Offset, 3);
                if (nextChars.Equals("///", StringComparison.Ordinal) && IsDocCommentOfFunction(context, context.Scanner.Cursor.Offset))
                    break;
            }
            if (_supportPythonComments && _parseDates && IsDateTimeLiteralStart(context.Scanner.Buffer, context.Scanner.Cursor.Offset)
                && IsDateTimeLiteral(context))
                break;
            if (!_commentParser.Parse(context, ref result))
                break;
        }

        var end = context.Scanner.Cursor.Offset;

        if (start == end)
        {
            context.ExitParser(this);
            return false;
        }

        result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));

        context.ExitParser(this);
        return true;
    }

    /// <summary>
    /// Tells whether the "///" at the offset starts a block of doc comment lines that is followed by a function declaration ("fn").
    /// Other comments ("//" lines and "/* */" blocks) may stand between the lines and "fn", as the declaration skips them.
    /// The outcome is the same for every "///" line of the block, and is remembered for all of them, so that a long block is scanned
    /// once and not again from each of its lines.
    /// </summary>
    private static bool IsDocCommentOfFunction(ParseContext context, int offset)
    {
        var memo = (context as LogicalExpressionParserContext)?.ParseMemo;
        if (memo is not null && memo.TryGetValue((DocCommentCheckKey, offset), out var known))
            return (bool)known;

        string buffer = context.Scanner.Buffer;
        List<int>? lineStarts = memo is null ? null : [];
        int position = offset;
        bool? result = null;
        while (result is null)
        {
            if (string.CompareOrdinal(buffer, position, "//", 0, 2) == 0)
            {
                if (lineStarts is not null && string.CompareOrdinal(buffer, position, "///", 0, 3) == 0)
                    lineStarts.Add(position);
                int lineEnd = buffer.IndexOf('\n', position);
                if (lineEnd < 0)
                    result = false;
                else
                    position = lineEnd + 1;
            }
            else
            if (string.CompareOrdinal(buffer, position, "/*", 0, 2) == 0)
            {
                int commentEnd = buffer.IndexOf("*/", position + 2, StringComparison.Ordinal);
                if (commentEnd < 0)
                    result = false;
                else
                    position = commentEnd + 2;
            }
            else
            {
                result = position + 2 < buffer.Length
                    && string.CompareOrdinal(buffer, position, "fn", 0, 2) == 0
                    && !char.IsLetterOrDigit(buffer[position + 2]) && buffer[position + 2] != '_';
                break;
            }

            while (position < buffer.Length && char.IsWhiteSpace(buffer[position]))
                position++;
        }

        if (lineStarts is not null)
        {
            foreach (int lineStart in lineStarts)
                memo![(DocCommentCheckKey, lineStart)] = result.Value;
        }
        return result.Value;
    }

    /// <summary>
    /// Tells whether the '#' at the cursor starts a valid date or time literal, by parsing it with the date/time literal parser
    /// of the grammar. Anything else, e.g. "#see #3" or "#todo: fix#", is a comment. The outcome is remembered per position.
    /// </summary>
    private static bool IsDateTimeLiteral(ParseContext context)
    {
        // Without the grammar's literal parser (a context of another type), the shape of the text decides, as it did before
        if (context is not LogicalExpressionParserContext parserContext || parserContext.DateTimeLiteralParser is null)
            return true;

        // Whitespace skipped inside a literal that is being checked must leave a '#' alone; the outer check decides
        if (parserContext.IsCheckingDateTimeLiteral)
            return true;

        var start = context.Scanner.Cursor.Position;
        var key = (DateTimeLiteralCheckKey, start.Offset);
        if (parserContext.ParseMemo.TryGetValue(key, out var known))
            return (bool)known;

        bool isLiteral;
        parserContext.IsCheckingDateTimeLiteral = true;
        try
        {
            var literal = new ParseResult<LogicalExpression>();
            isLiteral = parserContext.DateTimeLiteralParser.Parse(context, ref literal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // An invalid date or time (e.g. #99:99#) is not a literal either
            isLiteral = false;
        }
        finally
        {
            parserContext.IsCheckingDateTimeLiteral = false;
            context.Scanner.Cursor.ResetPosition(start);
        }

        parserContext.ParseMemo[key] = isLiteral;
        return isLiteral;
    }

    /// <summary>
    /// Tells whether a '#' may start a date or time literal (#2001-01-01#, #now#) rather than a Python-style comment: the literal
    /// starts with a letter or a digit and is closed by another '#' on the same line, while a comment usually starts with "# ".
    /// </summary>
    private static bool IsDateTimeLiteralStart(string buffer, int offset)
    {
        if (offset + 1 >= buffer.Length || buffer[offset] != '#')
            return false;

        // A literal starts with a letter or a digit (#2001-01-01#, #now#, #1 month ago#) or with a minus and a digit (#-02:00#),
        // while decorated comments such as #* ... *# or #----# do not
        char first = buffer[offset + 1];
        if (!char.IsLetterOrDigit(first) && !(first == '-' && offset + 2 < buffer.Length && char.IsDigit(buffer[offset + 2])))
            return false;

        for (int i = offset + 1; i < buffer.Length && buffer[i] != '\n' && buffer[i] != '\r'; i++)
        {
            if (buffer[i] == '#')
                return true;
        }
        return false;
    }

    public override string ToString() => $"WhiteSpaceParser";
}
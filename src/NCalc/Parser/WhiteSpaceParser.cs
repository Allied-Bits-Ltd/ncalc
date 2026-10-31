using NCalc;

using Parlot;
using Parlot.Fluent;

namespace NCalc.Parser;

public class WhiteSpaceParser : Parser<TextSpan>
{
    bool _supportCStyleComments = false;
    bool _supportPythonComments = false;

    private Parser<TextSpan> _commentParser;

    public WhiteSpaceParser(ExpressionOptions expressionOptions) : base()
    {
        _supportCStyleComments = expressionOptions.HasFlag(ExpressionOptions.SupportCStyleComments);
        _supportPythonComments = expressionOptions.HasFlag(ExpressionOptions.SupportPythonComments);
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
            charsLeft = bufferLength - context.Scanner.Cursor.Offset - 1;
            if (charsLeft == 0)
                break;

            // Exclude doc comments
            if (_supportCStyleComments && charsLeft >= 3)
            {
                nextChars = context.Scanner.Buffer.Substring(context.Scanner.Cursor.Offset, 3);
                if (nextChars.Equals("///", StringComparison.Ordinal))
                    break;
            }
            if (_supportPythonComments && IsDateTimeLiteralStart(context.Scanner.Buffer, context.Scanner.Cursor.Offset))
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
    /// Tells whether a '#' starts a date or time literal (#2001-01-01#, #now#) rather than a Python-style comment: the literal
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
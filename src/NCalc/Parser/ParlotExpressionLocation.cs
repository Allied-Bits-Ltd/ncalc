using Parlot.Fluent;

namespace NCalc.Parser
{
    public class ParlotExpressionLocation : ExpressionLocation
    {
        public ParlotExpressionLocation(ParseContext context) : base()
        {
            if (context is null)
                SetLocation(0, -1, -1);
            else
                SetLocation(context.Scanner.Cursor.Offset, context.Scanner.Cursor.Position.Line, context.Scanner.Cursor.Position.Column);
        }
    }
}

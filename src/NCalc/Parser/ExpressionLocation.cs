namespace NCalc.Parser
{
    public class ExpressionLocation
    {
        public int Offset { get; private set; }

        public int Row { get; private set; }

        public int Column { get; private set; }

        public static ExpressionLocation Empty = new ExpressionLocation();

        public ExpressionLocation() : this(0, -1, -1) { }

        public ExpressionLocation(int offset, int row, int column)
        {
            Offset = offset;
            Row = row;
            Column = column;
        }

        protected void SetLocation(int offset, int row, int column)
        {
            Offset = offset;
            Row = row;
            Column = column;
        }
    }
}

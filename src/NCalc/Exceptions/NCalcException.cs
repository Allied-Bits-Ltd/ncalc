namespace NCalc.Exceptions;

public class NCalcException : Exception
{
    public NCalcException(string message) : base(message)
    {
    }

    protected NCalcException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
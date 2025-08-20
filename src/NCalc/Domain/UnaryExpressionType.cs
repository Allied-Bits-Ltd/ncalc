namespace NCalc.Domain;

public enum UnaryExpressionType
{
    Not,
    Negate,
    FromEnd,
    BitwiseNot,
    Positive,
    SqRoot,
#if NET8_0_OR_GREATER
    CbRoot,
#endif
    FourthRoot
}
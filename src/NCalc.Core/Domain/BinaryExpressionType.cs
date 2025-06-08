namespace NCalc.Domain;

public enum BinaryExpressionType
{
    And,
    Or,
    NotEqual,
    LessOrEqual,
    GreaterOrEqual,
    Less,
    Greater,
    Equal,
    Minus,
    Plus,
    Modulo,
    Div,
    Times,
    BitwiseOr,
    BitwiseAnd,
    BitwiseXOr,
    LeftShift,
    RightShift,
    Exponentiation,
    Factorial,
    In,
    NotIn,
    Like,
    NotLike,
    Unknown = -1
}
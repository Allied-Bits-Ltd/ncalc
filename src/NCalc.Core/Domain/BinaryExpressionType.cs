namespace NCalc.Domain;

public enum BinaryExpressionType
{
    StatementSequence,
    Assignment,
    And,
    Or,
    XOr,
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
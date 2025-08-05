namespace NCalc;

/// <summary>
/// Options used for both parsing and evaluation of an expression.
/// </summary>
[Flags]
public enum ExpressionOptions
{
    /// <summary>
    /// Specifies that no options are set.
    /// </summary>
    None = 0,

    /// <summary>
    /// Specifies case-insensitive matching for built-in functions.
    /// </summary>
    IgnoreCaseAtBuiltInFunctions = 1 << 1,

    /// <summary>
    /// No-cache mode. Ignores any pre-compiled expression in the cache.
    /// </summary>
    NoCache = 1 << 2,

    /// <summary>
    /// Treats parameters as arrays and returns a set of results.
    /// </summary>
    IterateParameters = 1 << 3,

    /// <summary>
    /// When using Round(), if a number is halfway between two others, it is rounded toward the nearest number that is away from zero.
    /// </summary>
    RoundAwayFromZero = 1 << 4,

    /// <summary>
    /// Specifies the use of CaseInsensitiveComparer for comparisons.
    /// </summary>
    CaseInsensitiveStringComparer = 1 << 5,

    /// <summary>
    /// Uses decimals instead of doubles as default floating point data type.
    /// </summary>
    DecimalAsDefault = 1 << 6,

    /// <summary>
    /// Defines a "null" parameter and allows comparison of values to null.
    /// </summary>
    AllowNullParameter = 1 << 7,

    /// <summary>
    /// Use ordinal culture on string compare.
    /// </summary>
    OrdinalStringComparer = 1 << 8,

    /// <summary>
    /// Allow calculation with <see cref="bool"/> values.
    /// </summary>
    AllowBooleanCalculation = 1 << 9,

    /// <summary>
    /// Check for arithmetic binary operation overflow.
    /// </summary>
    OverflowProtection = 1 << 10,

    /// <summary>
    /// Concat values as strings instead of arithmetic addition.
    /// </summary>
    StringConcat = 1 << 11,

    /// <summary>
    /// Parse single quoted strings as <see cref="char"/>.
    /// </summary>
    AllowCharValues = 1 << 12,

    /// <summary>
    /// Disables coercion of string values to other types.
    /// </summary>
    NoStringTypeCoercion = 1 << 13,

    /// <summary>
    /// Return the value instead of throwing an exception when the expression is null or empty.
    /// </summary>
    AllowNullOrEmptyExpressions = 1 << 14,

    /// <summary>
    /// Enables strict type matching, where comparisons between objects of different types will return false. This also applies to LIKE and NOT LIKE operations - if StrictTypeMatching is enabled, the operands must be strings.
    /// </summary>
    StrictTypeMatching = 1 << 15,

    /// <summary>
    /// Disables parsing of GUIDs for faster processing
    /// </summary>
    DontParseGuids = 1 << 16,

    /// <summary>
    /// Disables parsing of dates for faster parsing
    /// </summary>
    DontParseDates = 1 << 17,

    /// <summary>
    /// Support math operations (+/-) with DateTime and Timespan and between Timespans
    /// </summary>
    SupportTimeOperations = 1 << 18,

    /// <summary>
    /// Convert identifiers to lowercase before looking up in the parameter and functions dictionary
    /// </summary>
    LowerCaseIdentifierLookup = 1 << 19,

    /// <summary>
    /// Disables the use of the ampersand, `|`, `^`, `~` characters for logical and bitwise operations; opting for AND, OR, XOR, NOT, as well as BIT_AND, BIT_OR, BIT_XOR, and BIT_NOT instead. When this flag is enabled, `^` is used for powers (together with `**`)
    /// </summary>
    SkipLogicalAndBitwiseOpChars = 1 << 20,

    /// <summary>
    /// Enables the use of certain Unicode characters to denote some math operations
    /// </summary>
    UseUnicodeCharsForOperations = 1 << 21,

    /// <summary>
    /// When enabled, supports assignment statements and update of parameters. By default, the feature is disabled for performance.
    /// </summary>
    UseAssignments = 1 << 22,

    /// <summary>
    /// Enables the use of "=" for assignments and "==" for the equality operator. When not enabled, both "=" and "==" are used for the equality operator and assignment is done with ":="
    /// </summary>
    UseCStyleAssignments = 1 << 23,

    /// <summary>
    /// When enabled, supports sequences of statements separated by ';'
    /// </summary>
    UseStatementSequences = 1 << 24,

    /// <summary>
    /// When enabled and during the Divide operation, there is no remainder to the division, an integer type is used for a result; otherwise, the result is always float (double or extended)
    /// </summary>
    ReduceDivResultToInteger = 1 << 25,

    /// <summary>
    /// BigInteger is to be used as the last resort when parsing numbers. BigDecimal will be used when dividing two BigIntegers or two BigDecimals.
    /// </summary>
    UseBigNumbers = 1 << 26,

    /// <summary>
    /// Specifies that values expressed as hex, binary, or octal are treated as unsigned values. This has meaning only for the values above long.MaxValue.
    /// </summary>
    HexBinOctAreUnsigned = 1 << 27,

    /// <summary>
    /// Specifies that comparison of null values should take place (a non-null value has precedence) instead of shortcutting to false.
    /// </summary>
    CompareNullValues = 1 << 28,

    /// <summary>
    /// When set, null will be converted to zero in math expressions.
    /// </summary>
    TreatNullAsZero = 1 << 29,

    /// <summary>
    /// By default, an identifier may be included in curly braces or square brackets. Enabling this option removes the braces
    /// </summary>
    //NoBracesForIdentifiers = 1 << 30

    /// <summary>
    /// When set, the parser will recognize C-style line and block comments in expressions.
    /// </summary>
    SupportCStyleComments = 1 << 31,

    /// <summary>
    /// When set, the parser will recognize Python comments in expressions.
    /// </summary>
    SupportPythonComments = 1 << 32
}
# Values

A value is a terminal token representing a concrete element. This can be:

- Any built-in .NET numeric type
- A <xref:System.Numerics.BigInteger> or <xref:ExtendedNumerics.BigDecimal>
- Any floating point number, like <xref:System.Double>
- A <xref:NCalc.Domain.Percent>
- A <xref:NCalc.Domain.ComplexNumber> (can be converted to and from System.Numeric.Complex)
- A <xref:NCalc.Domain.Vector>  (can be converted to and from System.Numeric.Vector<T> and System.Numeric.Vector[1..4])
- A <xref:System.DateTime> or <xref:System.TimeSpan> (and <xref:System.DateTimeOffset> or `TimeOnly` values passed as parameters)
- A <xref:System.Boolean>
- A <xref:System.String>
- A <xref:System.Char>
- A <xref:System.Guid>
- `null`
- A <xref:NCalc.Domain.FunctionCall>
- An <xref:NCalc.Domain.Identifier> (parameter/variable)
- A <xref:NCalc.Domain.LogicalExpressionList>  (List of other expressions)

## Integers

They are represented using numbers. Numbers may be presented in decimal, hexadecimal, octal, and binary forms using a prefix:

```
123456
0xFFFF // hex
0o1777 // octal
0177   // octal, only when the AcceptCStyleOctals flag of the advanced options is set (otherwise, 177)
0b01010101 // binary
```

A minus sign before a number is the negation operator, so `-5` is the negated number 5; `-9223372036854775808` is `long.MinValue`.

Integer numbers are evaluated as <xref:System.Int32> or, if the value is too big, as <xref:System.Int64>. A decimal integer beyond the range of `long` is a <xref:System.Double>, unless the <xref:NCalc.ExpressionOptions.UseBigNumbers> flag is set in <xref:NCalc.ExpressionOptions>, in which case it is a <xref:System.UInt64> when it fits and a <xref:System.Numerics.BigInteger> otherwise. A hexadecimal, octal or binary literal beyond the range of `long` is a `ulong` (`0xFFFFFFFFFFFFFFFF` is `ulong.MaxValue`). With `UseBigNumbers`, `BigInteger` values may also be returned by certain math operations.

Without [Advanced Options](advanced_value_formats.md), a number cannot contain number group separators (`1,000` is not a number). With advanced options, it is possible to use a culture-specific or custom group separator.

Additionally, when the <xref:NCalc.AdvExpressionOptions.AcceptUnderscoresInNumbers> flag of [Advanced Options](advanced_value_formats.md) is set, a number may contain underscores for readability (`100_000`, `0xDEAD_BEEF`). As in C#, an underscore may only stand between two digits (or after the prefix of a hexadecimal, octal or binary number): `_1` is an identifier, and `1_` is an error.

## Floating point numbers

Use '.' (dot) to separate an integer and a fractional part. When [Advanced Options](advanced_value_formats.md) are used, it is possible to use a culture-specific or custom separator and even use two separators (e.g., use both ',' and '.').

Note, that the fractional part (the one after the dot) must be present for the number to be recognized as a floating point one: `123.` is an error, while `.123` and `123.0` are numbers. A number with a decimal separator or an exponent is a floating point number even when it is whole (`123.0` and `1e2` are doubles).

Floating point numbers are evaluated as follows:

* By default, as <xref:System.Double>.
* When the <xref:NCalc.ExpressionOptions.DecimalAsDefault> flag is set in <xref:NCalc.ExpressionOptions>, as <xref:System.Decimal>. A literal that `decimal` cannot hold, or holds with fewer significant digits than `double` (beyond its range, such as `1e30`, or below its resolution of 1E-28, such as `1e-30`), is a `double` instead.
* When the <xref:NCalc.ExpressionOptions.UseBigNumbers> flag is set, a literal that a `double` holds is a `double`, a literal that only a `decimal` holds exactly is a `decimal` (`12345678901234567890.5`), and a literal beyond these types (`1e400`) is an <xref:ExtendedNumerics.BigDecimal>. A literal with more integer digits than `MathHelper.MaxPowResultDigits` is the double infinity. With `UseBigNumbers`, `BigDecimal` values may also be returned by certain math operations.

Additionally, when [Advanced Options](advanced_value_formats.md) are used, a number may contain a currency symbol or identifier before or after the numeric value. Such a symbol is ignored in calculations. However, currency values are evaluated as <xref:System.Decimal> regardless of whether decimal is the default type for floating-point numbers.

### Common notation

```
123.456
.123
123.0
```

### Scientific notation

You can use the scientific notation, i.e., insert a letter "e" followed by an integer number to denote a power of ten (10^).
```
1.22e1
1e2
1e+2
1e-2
.1e-2
1e10
```

## Null

The `null` keyword is the null value. It is available only when the <xref:NCalc.ExpressionOptions.AllowNullParameter> flag (or <xref:NCalc.ExpressionOptions.UseTernaryLogic>) is set in <xref:NCalc.ExpressionOptions>; otherwise `null` is the name of a parameter. See [Handling of null values](operators.md#handling-of-null-values) for how operators treat null.

## Percent

When percent calculation is enabled in [Advanced Options](advanced_value_formats.md), the parser will recognize the '%' character and treat the parsed value as percent. Percent participates in expression evaluation. It can be the result of an operation (e.g., in operations like "5% + 2%"), in which case, an instance of <xref:NCalc.Domain.Percent> is returned. 

## Complex numbers

When complex numbers are enabled in [Advanced Options](advanced_value_formats.md), the parser recognizes a standalone "i" as a square root of -1 and any Xi, where X is a number (e.g., "3.5i" or "(2+3)i") as an imaginary number. The notation "3+5i" or "(2+4) + (4-1)i" or "t-5i" (here, t is a parameter/variable) produces a complex number, which can be returned as a result of an expression evaluation or can be manipulated similar to other numbers. The letter `i` is the imaginary unit only as a whole word or directly after a number, so names such as `index` and the `in` operator still work.

## Vectors

When vectors are enabled in [Advanced Options](advanced_value_formats.md), the parser recognizes a list of numbers in square brackets (e.g., "[1; 2; 5]" or "[3 4 7 9]" depending on how the list separator is set up) as a vector, which can be returned as a result of an expression evaluation or can be manipulated similar to other numbers. 

## DateTime

Must be enclosed between '#' (sharp) characters. 

```
#01/31/2008# // for en-US culture
#31.01.2008# // for de-DE culture
#08/08/2001 09:30:00# 
```
By default, NCalc reads DateTime values in the date and time format of the culture of the expression (the `CultureInfo` passed to the constructor of <xref:NCalc.Expression> or set in its `CultureInfo` property, which is the current culture of the thread at the moment the expression is created when none is given), always in the Gregorian calendar. When [Advanced Options](advanced_value_formats.md) are used, the format of date and time values can be customized to a large extent.

Additionally, when the <xref:NCalc.AdvExpressionOptions.ParseHumanePeriods> flag of [Advanced Options](advanced_value_formats.md) is set, it is possible to define dates relative to current moment in a humane form (e.g. #today#, #3 weeks ago# or #in 5 days#) as described in the [Advanced Value Formats and Operations](advanced_value_formats.md) topic. Such values are computed when the expression is evaluated, not when it is parsed.

Also, ISO 8601 / XML date and date-time values are supported in every culture:

```
#2001-01-01# // date
#2001-01-01Z# // date (UTC)
#2001-01-01T14:56:49# // datetime
#2001-01-01T14:56:49Z# // datetime
#2001-01-01T14:56:49.325Z# // datetime; 1 to 7 digits of the fraction of a second are kept exactly
```

A value with the `Z` designator has the `DateTimeKind.Utc` kind; a value without it has the `DateTimeKind.Unspecified` kind. The kind does not take part in comparisons: `#2001-01-01Z# = #2001-01-01#` is true.

## Time

Includes hours, minutes, and seconds (optionally with a fraction of a second, up to 7 digits), and optionally days before the hours. 
The value must be enclosed between sharps, and it is evaluated as a <xref:System.TimeSpan>.
```
#20:42:00#
#20:42#        // seconds may be omitted
#1.02:03:04#   // 1 day, 2 hours, 3 minutes and 4 seconds
#00:00:01.5#   // 1.5 seconds
#25:00:00#     // 25 hours (1.01:00:00)
#-1:30#        // a negative time span
#10:30 PM#     // the same as #22:30#, where the culture uses the 12-hour format
```

Hours may be 24 or more when no days are given; minutes and seconds must be below 60.

When [Advanced Options](advanced_value_formats.md) are used, the format of time values can be customized to a large extent.

Additionally, it is possible to define periods in a humane form (e.g. #5 weeks 3 days 28 hours#) as described in the [Advanced Value Formats and Operations](advanced_value_formats.md) topic.

## Booleans

Booleans can be either `true` or `false` (in any case, e.g. `True` or `TRUE`).

```
true
```

## Strings

Any characters between single or double quotes are evaluated as <xref:System.String>. 

```
'hello'
```

```
greeting("Chers")
```
You can escape special characters using a backslash("\\"). The parser will decode \\', \\", \\\\,\0, \a, \b, \f, \n, \r, \t, \v and convert them into the corresponding character. 
The parser will also decode unicode in hex notation escaped as \\u{abcd}, where {abcd} is exactly four (4) hex characters and as \\x{abcd}, where {abcd} is one to four characters.

Additionally, the parser will recognize raw strings, i.e., the strings with no escaping in them. Such strings may be enclosed in double quotes with the @ (at) before the opening double quote (@"raw string here") or in back quotes '\`' (\`raw string here\`). 
Of course, a raw string still may not contain a closing character ( " and \` respectively) in it. 
```
`This will be parsed as is without translating \t into a tab`
@"And this will also be parsed as is without translating \t into a tab"

@"But this string with "quotes" unfortunately won't work"
```

## Chars
If you use <xref:NCalc.ExpressionOptions.AllowCharValues>, single quoted strings with one character, including a single escaped character such as `'\n'` or `'A'`, are interpreted as <xref:System.Char>
```
var expression = new Expression("'g'", ExpressionOptions.AllowCharValues);
var result = expression.Evaluate();
Debug.Assert(result is char); // 'g' -> System.Char
```

## Guid
NCalc also supports <xref:System.Guid>, they can be parsed with or without hyphens.
```csharp
b1548bd5-2556-4d2a-9f47-bb8d421026dd
getUser(78b1941f4e7941c9bef656fad7326538)
```

A run of 32 hexadecimal characters without hyphens is a GUID only when it contains both digits and letters; a GUID with hyphens must not contain spaces (`a - b - c - d - e` is a subtraction). To use a name of the GUID shape as a parameter, put it in square brackets (`[abcdef11111111111111111111111111]`). Parsing of GUIDs can be turned off with the <xref:NCalc.ExpressionOptions.DontParseGuids> flag.

## Function

A function is made of a name followed by braces, containing optionally any value as arguments.

```
Abs(1)
```

```
doSomething(1, 'dummy')
```

Please read the [functions page](functions.md) for details.

## Parameters (variables)

A parameter (variable) is a name that can be optionally contained inside square brackets.

```
2 + x, 2 + [x]
```

Please read the [parameters page](parameters.md) for details.

## Lists

Lists are collections of expressions enclosed in parentheses. They are the equivalent of `List<LogicalExpression>` in .NET.
The elements of the list may be separated with a comma (",") or a semicolon (";"). A semicolon is recommended to avoid possible conflicts with a decimal separator or a number group separator.
```
('Chars'; secretOperation(); 3.14)
```

When [Advanced Options](advanced_value_formats.md) are used, the separator(s) that the parser should use to separate the values in the list is defined via the <xref:NCalc.AdvancedExpressionOptions.ListItemSeparator> property, which suports comma, semicolon, colon, and space, as well as their combinations, as separators.

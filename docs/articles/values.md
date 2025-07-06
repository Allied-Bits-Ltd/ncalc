# Values

A value is a terminal token representing a concrete element. This can be:

- An <xref:System.Int32> or <xref:System.Int64>
- A <xref:System.Numerics.BigInteger> or <xref:ExtendedNumerics.BigDecimal>
- Any floating point number, like <xref:System.Double>
- A <xref:NCalc.Domain.Percent>
- A <xref:System.DateTime> or <xref:System.TimeSpan>
- A <xref:System.Boolean>
- A <xref:System.String>
- A <xref:System.Char>
- A <xref:NCalc.Domain.Function>
- An <xref:NCalc.Domain.Identifier> (parameter)
- A <xref:NCalc.Domain.LogicalExpressionList>  (List of other expressions)

## Integers

They are represented using numbers. Numbers may be presented in decimal, hexadecimal, octal, and binary forms using a prefix:

```
123456
0xFFFF // hex
0o1777 // octal
0177   // octal, only when UseCStyleOctals option is enabled
0b01010101 // binary
```

Numbers in decimal format may include a leading sign ('-') character to indicate negative numbers.

Integer numbers are normally evaluated as <xref:System.Int32> or, if the value is too big, as <xref:System.Int64>. When the <xref:NCalc.ExpressionOptions.UseBigNumbers> flag in set in <xref:NCalc.ExpressionOptions>, <xref:System.Numerics.BigInteger> may be used in expressions and may be returned by certain math operations.

A number may contain number group separators. Comma (',') is the default separator, and when [Advanced Options](advanced_value_formats.md) are used, it is possible to use a culture-specific or custom separator.

Additionally, when [Advanced Options](advanced_value_formats.md) are used, a number may contain underscores for readability. Underscore support in binary, octal, and hex numbers is built-in, while support in decimals requires a patch in Parlot or a [custom branch of Parlot](https://github.com/Allied-Bits-Ltd/parlot/tree/ABCalc). 

## Floating point numbers

Use '.' (dot) to separate an interer and a fractional part. When [Advanced Options](advanced_value_formats.md) are used, it is possible to use a culture-specific or custom separator and even use two separators (e.g., use both ',' and '.').

Note, that the fractional part (the one after the dot) must be present for the number to be recognized as a floating point one.

Floating point numbers are evaluated either as <xref:System.Double> or, if the <xref:NCalc.ExpressionOptions.DecimalAsDefault> flag in set in <xref:NCalc.ExpressionOptions>, as <xref:System.Decimal>. When the <xref:NCalc.ExpressionOptions.UseBigNumbers> flag in set in <xref:NCalc.ExpressionOptions>, <xref:ExtendedNumerics.BigDecimal> may be used in expressions and may be returned by certain math operations.

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
1e+2
1e-2
.1e-2
1e10
```

## Percent

When percent calculation is enabled in [Advanced Options](advanced_value_formats.md), the parser will recognize the '%' character and treat the parsed value as percent. Percent participates in expression evaluation. It can be the result of an operation (e.g., in operations like "5% + 2%"), in which case, an instance of <xref:NCalc.Domain.Percent> is returned. 

## DateTime

Must be enclosed between '#' (sharp) characters. 

```
#2008/01/31# // for en-US culture
#08/08/2001 09:30:00# 
```
By default, NCalc uses current Culture to evaluate DateTime values. When [Advanced Options](advanced_value_formats.md) are used, the format of date and time values can be customized to a large extent.

Additionally, it is possible to define dates relative to current moment in a humane form (e.g. #today#, #3 weeks ago# or #in 5 days#) as described in the [Advanced Value Formats and Operations](advanced_value_formats.md) topic.

## Time

Includes Hours, minutes, and seconds. 
The value must be enclosed between sharps.
```
#20:42:00#
```

When [Advanced Options](advanced_value_formats.md) are used, the format of time values can be customized to a large extent.

Additionally, it is possible to define periods in a humane form (e.g. #5 weeks 3 days 28 hours#) as described in the [Advanced Value Formats and Operations](advanced_value_formats.md) topic.

## Booleans
Booleans can be either `true` or `false`.

```
true
```
## Strings

Any character between single or double quotes are evaluated as <xref:System.String>. 

```
'hello'
```

```
greeting("Chers")
```
You can escape special characters using \\, \', \n, \r, \t.

## Chars
If you use <xref:NCalc.ExpressionOptions.AllowCharValues>, single quoted strings are interpreted as <xref:System.Char>
```
var expression = new Expression("'g'", ExpressionOptions.AllowCharValues);
var result = expression.Evalutate();
Debug.Assert(result); // 'g' -> System.Char
```

## Guid
NCalc also supports <xref:System.Guid>, they can be parsed with or without hyphens.
```csharp
b1548bd5-2556-4d2a-9f47-bb8d421026dd
getUser(78b1941f4e7941c9bef656fad7326538)
```

## Function

A function is made of a name followed by braces, containing optionally any value as arguments.

```
Abs(1)
```

```
doSomething(1, 'dummy')
```

Please read the [functions page](functions.md) for details.

## Parameters

A parameter is a name that can be optionally contained inside brackets or double quotes.

```
2 + x, 2 + [x]
```

Please read the [parameters page](parameters.md) for details.

## Lists

Lists are collections of expressions enclosed in parentheses. They are the equivalent of `List<LogicalExpression>` at CLR.
```
('Chers', secretOperation(), 3.14)
```
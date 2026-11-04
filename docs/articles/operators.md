# Operators

Expressions can be combined using operators, each with a specific precedence priority. The precedence rules determine the order in which operations are performed in an expression. Below is the list of operators ordered by precedence in descending order:

1. **Primary**
2. **Factorial**
3. **Percent**
4. **Exponential**
5. **Unary**
6. **Multiplicative**
7. **Additive**
8. **Shift**
9. **Relational**
10. **Bitwise**
11. **Logical**
12. **Ternary**

Binary operators of the same precedence are evaluated from left to right, except the exponentiation (`**`) and the ternary operator (`?:`), which are right-associative: `2 ** 3 ** 2` is `2 ** 9`, and `a ? b : c ? d : e` is `a ? b : (c ? d : e)`.

Alphabetic operators and keywords (`and`, `or`, `xor`, `not`, `mod`, `div`, `in`, `like`, `bit_and` and the like, `true`, `false`, `null`) are recognized only as whole words and regardless of case: `trueValue` and `mod3` are identifiers.

## Handling of null values

The `null` keyword is available when the <xref:NCalc.ExpressionOptions.AllowNullParameter> flag (or <xref:NCalc.ExpressionOptions.UseTernaryLogic>, which implies it) is set; without it, `null` is read as the name of a parameter. A null value can also come from a parameter, a function or a handler.

* **Arithmetic, unary operators, percents and the built-in math functions** return `null` when an operand or argument is null (`null + 1` and `Abs(null)` are null). When the <xref:NCalc.ExpressionOptions.TreatNullAsZero> flag is set, null is used as zero in these operations instead (`null + 1` is 1).
* **Comparisons** compare null as a value of its own: `null = null` is true, `null = 1` is false and `null != 1` is true, while the ordering operators (`<`, `<=`, `>`, `>=`) are false when an operand is null. `TreatNullAsZero` does not apply to comparisons (`null = 0` is false). With the <xref:NCalc.ExpressionOptions.CompareNullValues> flag, null is ordered before any other value, so `null < 1` is true.
* **`in`, `like` and the ternary operator** return null when the value to look for, the string to match or the condition is null.
* **Logical operators** return null when an operand is null, unless <xref:NCalc.ExpressionOptions.UseTernaryLogic> is set (see [Logical](#logical)).

## Result types of arithmetic

The type of the result of an arithmetic operator depends on the types of its operands:

* Two integers give an integer of the wider type of the two (`int` and `long` give `long`). The product of two 32-bit integers is computed in 64 bits (`2147483647 * 2` is the `long` 4294967294), but `+` and `-` of two `int` values and all operations on two `long` values wrap around as unchecked C# arithmetic does (`2147483647 + 1` is -2147483648). Use <xref:NCalc.ExpressionOptions.OverflowProtection> to get a wider type or an exception instead, or <xref:NCalc.ExpressionOptions.UseBigNumbers> to get a `BigInteger` (see [Overflow Protection](overflow_protection.md)).
* A `ulong` combined with a signed integer is computed exactly; a result beyond the 64-bit range is a `BigInteger` (`ulong.MaxValue * -1` is -18446744073709551615). The product of two `ulong` values wraps around as in C#.
* An integer combined with a `double` or `float` gives a `double`; a `decimal` combined with an integer gives a `decimal`.
* A `decimal` combined with a `double` or `float` gives a `decimal` when the floating-point value fits in `decimal` with its significant digits, and a `double` otherwise (`1m + 1e300` is the double 1E+300).
* An operation with a `BigInteger` or `BigDecimal` operand promotes the other operand and gives a big number, whether or not `UseBigNumbers` is set. A `BigDecimal` result is narrowed to `double` only when no digits are lost, and a whole result to an integer type only when it fits.
* The `/` operator always gives a floating-point result for two integers (`7 / 2` is 3.5, `6 / 3` is the double 2), unless <xref:NCalc.ExpressionOptions.ReduceDivResultToInteger> is set, in which case an exact integer quotient is an integer (`6 / 3` is 2).
* With <xref:NCalc.ExpressionOptions.ReduceArithmeticResultType>, an integer result is reduced to the smallest integer type that holds it.
* NaN and infinities follow IEEE rules: `1 / 0.0` is +∞, `0 / 0.0` is NaN, and NaN is not equal to anything, including NaN.

Strings that hold a number are converted to a number in arithmetic (`'2' * 3` is 6), unless <xref:NCalc.ExpressionOptions.NoStringTypeCoercion> is set; such a string is read like a number literal, in the invariant notation or in the number format of the culture of the expression (a group separator is not accepted: `'1,5' + 0` throws unless the comma is the decimal separator of the culture). A string that is not a number throws <xref:NCalc.Exceptions.NCalcConversionException>. Booleans take part in arithmetic only with <xref:NCalc.ExpressionOptions.AllowBooleanCalculation> (true is 1 and false is 0).

## Primary

Primary are the first thing to be evaluated. They are direct values or a list of them.

### Values

Values include literals that are used in expressions.

**Examples:**
```csharp
42
"hello"
true
```

### Lists

Lists are used to group expressions.

**Examples:**
```csharp
2 * (3 + 2)
"foo" in ("foo", "bar", 5) 
secret_operation("my_db", 2) // Function arguments are actually a list!
```

The elements of the list may be separated with a comma (",") or a semicolon (";"). A semicolon is recommended to avoid possible conflicts with a decimal separator or a number group separator.

### Indexed Access to Lists and Strings

If an expression evaluates to a list or a string, an element or a range of elements of this list or string can be accessed using an index operator, where the index or the range may also be an expression or expressions. 
The C# "from end" syntax using a caret (^) is also supported. Any boundary of the range may be omitted. 

An element of a string is a `char`; a range of a list is a list, and a range of a string is a string. An index outside the list or string, or an inverted range, throws <xref:NCalc.Exceptions.NCalcParameterIndexException>.

**Examples:**
```csharp
(1; 2; 3)[1] // produces 2
'abcd'[1] // produces 'b' (a char)
(1; 2; 3)[1..2] // produces (2), a list of one element
(1; 2; 3)[0..2] // produces (1; 2)
'abcd'[1..3] // produces "bc"
'abcd'[^2..^1] // produces "c"
'abcd'[..^1] // produces "abc"
'abcd'[2..] // produces "cd"
'abcd'[..] // produces "abcd"
```

If an expression is a parameter (variable) that contains a list or a string, it is possible to update one of its elements using the index operator with an integer index, also from the end (it is not possible to update the range at the moment). Assignments require the <xref:NCalc.ExpressionOptions.UseAssignments> flag, and the sequences below also need <xref:NCalc.ExpressionOptions.UseStatementSequences>:
```
a := "abcd"; a[0] := "X" // single-character strings work like chars in this type of assignment
a := (1; 2; 3); a[1] := 'Y' // this is possible as our lists are lists of objects
a := (1; 2; 3); a[^1] := 7 // a is (1; 2; 7)
```

A value assigned to an element of a typed list or array (e.g. a `List<double>` passed as a parameter) is converted to the element type; a value that cannot be converted, or an index outside the list, throws <xref:NCalc.Exceptions.NCalcParameterIndexException>.

## Factorial

Factorial ('!') is a special post-operator that is applicable to a value or expression that evaluates to an integer value. The resulting expression is binary, and the number of exclamation marks determines the type of factorial (standard, double, triple etc.). 

* `!`: standard factorial
* `!!`: double factorial
* `!!!`: triple factorial
* `!....!`: some multifactorial

**Example**:
```
(1+2)!    // 6
5!!       // 15 (5 * 3 * 1)
20!!!
3! == 6   // true; only "!" immediately followed by "=" is the "!=" operator
```

The result is a `long`; `0!` is 1. A factorial that does not fit in `long` (such as `21!`) is a `double`, or, with <xref:NCalc.ExpressionOptions.UseBigNumbers>, an exact `BigInteger` whose number of digits is limited by `MathHelper.MaxPowResultDigits` (see [Limits](options.md#limits)). The factorial of a negative number or of a number with a fractional part (`3.5!`) throws <xref:NCalc.Exceptions.NCalcEvaluationException>.

## Percent

Percent is a post-operator that alters the way the operand is interpreted. A percent value is marked with a `%` character placed after a numeric value or expression.

Percent calculations must be enabled via the [AdvancedOptions](advanced_value_formats.md#percent-calculations) property of the ExpressionBase class. When percent calculations are enabled, the `%` character is used for them, and `mod` is used for modulo division.

**Example**:
```
(1+2)%      // 3%
5% + 2%     // 7%
100 * 5%    // 5
200 + 10%   // 220
200 - 5%    // 190
```

If the result of an operation is percent, an instance of <xref:NCalc.Domain.Percent> is returned as a result of the expression evaluation. The full list of operations is in [Percent Calculations](advanced_value_formats.md#percent-calculations).

## Exponential

Exponential operators perform exponentiation.

* `**` : Exponentiation
* `^` : Exponentiation (when the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag is set in  <xref:NCalc.ExpressionOptions>)

**Example:**
```csharp
2 ** 2           // 4
2 ** 3 ** 2      // 512, as 2 ** (3 ** 2)
2 ** -1          // 0.5; the exponent may have a sign
-2 ** 2          // -4, as -(2 ** 2)
(-2) ** 2        // 4
```

The operator gives the same result as the `Pow()` function. The result is a `double`, except that:

* with <xref:NCalc.ExpressionOptions.UseBigNumbers>, an integer raised to a non-negative integer power is an exact integer (`2 ** 200` is a `BigInteger`), and a big-number base gives a big-number result;
* with <xref:NCalc.ExpressionOptions.DecimalAsDefault> or a `decimal` base, an integer power is computed exactly and is an integer (for an integer base) or a `decimal` when it fits in `decimal`, and a `double` otherwise.

An exact power whose result would have more than `MathHelper.MaxPowResultDigits` digits (100,000 by default) throws <xref:NCalc.Exceptions.NCalcEvaluationException> before it is computed. Zero raised to a negative power is infinity.

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:
* `↑` (U+2191): Exponentiation (`⊑`, U+2291, is accepted too)

## Unary

Unary operators operate on a single operand.

* `!` : Logical NOT  (unless the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag is set in  <xref:NCalc.ExpressionOptions>)
* `not` : Logical NOT
* `-` : Negation
* `~` : Bitwise NOT (unless the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag is set in  <xref:NCalc.ExpressionOptions>)
* `bit_not` : Bitwise NOT

**Examples:**
```csharp
not true
!(1 != 2)
```

Negation keeps the type of its operand; negating the minimum value of a signed integer type gives the next wider type (`-(-2147483648)` is the `long` 2147483648). It also applies to complex numbers, vectors and percents. Logical NOT converts its operand to a boolean as the [logical operators](#logical) do.

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:

* `¬` (U+00AC) : Logical NOT
* `√` (U+221A) : Square root
* `∛` (U+221B) : Cube root
* `∜` (U+221C) : Fourth root

**Examples:**
```csharp
√4
∜(4*4)
```

The root operators use the options of the expression like the `Sqrt()` function does: with <xref:NCalc.ExpressionOptions.UseBigNumbers>, the root of a big number is a big number.

## Multiplicative

Multiplicative operators perform multiplication, division, and modulus operations.

* `*` : Multiplication
* `/` : Division
* `//` : Integer Division (Python-like - the exact quotient is rounded down (floored), so `-7 // 2` is -4; floating-point operands are accepted, and `7.5 // 2` is 3)
* `\` : Integer Division (Basic-like - floating-point operands are truncated first, then the quotient is truncated toward zero, so `-7 \ 2` is -3 and `7.5 \ 2` is 3)
* `div` : Integer Division (the same as `\`)
* `%` : Modulus (when percent calculation is disabled)
* `mod` : Modulus (always available; the only modulus operator when percent calculation is enabled in <xref:NCalc.AdvancedExpressionOptions>)

**Example:**
```csharp
1 * 2 % 3
```

Integer division is exact for all operand types: the quotient is computed from the exact operands, not rounded through `double`. Its result is an integer (`int`, `long` or, beyond that range, `BigInteger` with <xref:NCalc.ExpressionOptions.UseBigNumbers>); a quotient beyond the range of `long` without `UseBigNumbers` is the nearest `double` (`1e30 // 1` is 1E+30). An integer quotient of NaN or infinity is NaN or infinity.

The remainder (`%` and `mod`) has the sign of the dividend, as in C#: `-7 % 3` is -1. It keeps the fractional part for floating-point, `decimal` and big-number operands (`7.5 % 2` is 1.5).

**Division by zero** depends on the type of the operands:

* When the division is computed in `double` (a `double` or `float` operand, or `/` of two integers), the result follows IEEE rules: `5 / 0` is +∞, `0 / 0` is NaN, and `7.5 % 0.0` is NaN. With <xref:NCalc.ExpressionOptions.OverflowProtection>, an infinite result throws <xref:System.OverflowException> instead.
* Integer division (`//`, `\`, `div`), the remainder of integers, and any division of `decimal` and big-number values (including `/` with <xref:NCalc.ExpressionOptions.DecimalAsDefault> or <xref:NCalc.ExpressionOptions.UseBigNumbers> when an operand is a `decimal` or a big number) throw <xref:NCalc.Exceptions.NCalcEvaluationException> with the message "Division by zero." and a [DivideByZeroException](https://learn.microsoft.com/dotnet/api/system.dividebyzeroexception) as its `InnerException`. The divisor of `\` and `div` is truncated first, so a divisor that truncates to zero (`5 \ 0.5`) is a division by zero too.

A `decimal` result of `+`, `-`, `*` or `/` beyond the range of `decimal` throws <xref:NCalc.Exceptions.NCalcEvaluationException> ("The result of the multiplication is outside the range of decimal.", with an <xref:System.OverflowException> as its `InnerException`), with or without <xref:NCalc.ExpressionOptions.OverflowProtection>; with <xref:NCalc.ExpressionOptions.UseBigNumbers>, it is a big number instead.

When time operations are enabled using the <xref:NCalc.ExpressionOptions.SupportTimeOperations> flag in <xref:NCalc.ExpressionOptions>, it is possible to perform the following additional operations: 
* multiply a time period value (TimeSpan) by a number and obtain a time period as a result;
* divide a time period value (TimeSpan) by a number and obtain a time period as a result;
* divide a time period value (TimeSpan) by another time period and obtain their ratio as a `double`.

The result of multiplying or dividing a time period is rounded to the nearest tick. A result outside the range of `TimeSpan` (including a division by zero) throws <xref:NCalc.Exceptions.NCalcEvaluationException>.

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operators are also supported:
* `×` (U+00D7) : Multiplication
* `∙` (U+2219) : Multiplication
* `∶` (U+2236) : Division
* `÷` (U+00F7) : Division

When assignments are enabled using the <xref:NCalc.ExpressionOptions.UseAssignments> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:
* `*=` : Multiplication with assignment of the result to the left operand
* `/=` : Division with assignment of the result to the left operand

When Unicode characters are enabled, they can also be used for multiplication with assignment and division with assignment.

The non-AOT version of NCalc can perform multiplication, division, and modulus operations on any types which have these operations defined (overloaded). This includes non-numeric types.  The AOT version can handle only numbers (including BigInteger and BigDecimal) and multiplication and division of TimeSpan values by numbers.

## Additive

Additive operators perform addition and subtraction.

* `+` : Addition
* `-` : Subtraction

**Example:**
```csharp
1 + 2 - 3
```

When an operand of `+` is a string, the operator adds the numbers that the strings hold or concatenates the strings; see [String Concatenation](string_concatenation.md). A concatenated string may have at most `BuiltInFunctionHelper.MaxMakeResultLength` characters (10,000,000 by default).

When time operations are enabled using the <xref:NCalc.ExpressionOptions.SupportTimeOperations> flag in <xref:NCalc.ExpressionOptions>, it is possible to perform the following additional operations: 
* add a time period value (TimeSpan) to a date (DateTime) or another time period (TimeSpan) value;
* add ticks (100ns per tick) to a date (DateTime) or time period (TimeSpan) value;
* subtract a time period value (TimeSpan) from a date (DateTime) or another time period (TimeSpan) value;
* subtract a date value (DateTime) from another date (DateTime) value;
* subtract ticks (100ns per tick) from a date (DateTime) or time period (TimeSpan) value.

`DateTimeOffset` and `TimeOnly` values (passed as parameters) support the same operations. A result outside the range of the type throws <xref:NCalc.Exceptions.NCalcEvaluationException>.

When assignments are enabled using the <xref:NCalc.ExpressionOptions.UseAssignments> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:
* `+=` : Addition with assignment of the result to the left operand
* `-=` : Subtraction with assignment of the result to the left operand

The non-AOT version of NCalc can perform addition and subtraction operations on any types which have these operations defined (overloaded). This includes non-numeric types. The AOT version can handle numbers (including BigInteger and BigDecimal), dates and times (where the operations make sense), char variables, as well as add (concatenate)  strings.

## Shift

Shift operators shift the bits of an integer. They bind more tightly than the relational operators and more loosely than the additive ones, so `1 << 2 + 1` is `1 << 3`, and `1 << 2 > 3` compares `4` with `3`.

* `<<` : Left shift
* `>>` : Right shift

**Example:**
```csharp
2 >> 3   // 0
-8 >> 1  // -4
1 << 63  // 9223372036854775808 (ulong)
```

Shifts of negative values are arithmetic, as in C#. Unlike C#, the shift count is not masked to the width of the type: a left shift that moves all bits out gives 0 (`1 << 64` is 0); with <xref:NCalc.ExpressionOptions.UseBigNumbers> the result is a `BigInteger` (`1 << 64` is 18446744073709551616), and with <xref:NCalc.ExpressionOptions.OverflowProtection> such a shift throws <xref:System.OverflowException>. The shift count must be a non-negative integer; a negative or fractional count throws <xref:NCalc.Exceptions.NCalcEvaluationException>.

## Relational

Relational operators compare two values and return a boolean result.

### Equality and Inequality Operators

These operators compare two values to check equality or inequality.

* `==` : Equal to
* `=` : Equal to (unless the <xref:NCalc.ExpressionOptions.UseCStyleAssignments> flag in <xref:NCalc.ExpressionOptions> is set, in which case `=` is used for assignment)
* `!=`, `<>` : Not equal to

**Examples:**
```csharp
42 == 42            // true
"hello" == "world"  // false
10 != 5             // true
"apple" != "apple"  // false
```

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:
* `≠` (U+2260) : Not equal to

### Comparison Operators

These operators compare two values to determine their relative order.

* `<`  : Less than
* `<=` : Less than or equal to
* `>`  : Greater than
* `>=` : Greater than or equal to

**Examples:**
```csharp
3 < 5          // true
10 <= 10       // true
7 > 3          // true
8 >= 12        // false
```

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:
* `≤` (U+2264) : Less than or equal to
* `≥` (U+2265) : Greater than or equal to

### How values are compared

* **Numbers** of different types are compared by value, converted to the more precise type: `1 = 1.0` is true, and a `ulong`, a `decimal`, a `double` and a big number can be compared with each other. NaN is not equal to anything (`Sqrt(-1) = Sqrt(-1)` is false).
* **Strings** are compared using the culture of the expression, case-sensitively by default. <xref:NCalc.ExpressionOptions.CaseInsensitiveStringComparer> makes the comparison case-insensitive, and <xref:NCalc.ExpressionOptions.OrdinalStringComparer> makes it ordinal (`'a' < 'B'` is true with the culture and false ordinally).
* **A string and a value of another type**: the string is converted only when it is a single literal in NCalc syntax (a number such as `"1.7"`, `"-5"` or `"0x10"`, a date or time such as `"#2024-01-01#"`, a boolean or a GUID); otherwise both values are compared as strings. So `'1' == 1` is true and `'abc' == 1` is false. With <xref:NCalc.ExpressionOptions.NoStringTypeCoercion>, a string is never converted; a string and a number then are unequal and unordered (all comparisons except `!=` are false).
* **Values of incompatible types** (e.g. a date and a number, or two lists) throw <xref:NCalc.Exceptions.NCalcEvaluationException> ("Comparison of incomparable types was attempted"). When the <xref:NCalc.ExpressionOptions.CompareIncompatibleTypes> flag is set, such values are unequal and unordered instead: `=` and the ordering operators are false, and `!=` is true.
* With <xref:NCalc.ExpressionOptions.StrictTypeMatching>, values of different types are never equal (`1 = 1.0` and `'1' = 1` are false, `!=` is true); see [Strict Type Matching](strict_type_matching.md).
* **Complex numbers, vectors and percents** support `=` and `!=` (a complex number with no imaginary part equals the real number); percents can also be ordered. Ordering complex numbers or vectors throws <xref:NCalc.Exceptions.NCalcEvaluationException>.

See [Handling of null values](#handling-of-null-values) for comparisons with null.

### IN and NOT IN

The `IN` and `NOT IN` operators check whether a value is present or absent within a specified list or string.

* `IN` : Returns `true` if the left operand is found in the right operand (which can be a list or a string).
* `NOT IN` : Returns `true` if the left operand is not found in the right operand.

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:
* `∈` (U+2208) : Returns `true` if the left operand is found in the right operand (which can be a list or a string). 
* `∉` (U+2209) : Returns `true` if the left operand is not found in the right operand.

The right operand must be either a string or a list (`IEnumerable`). The items of a list are compared with the left operand as the `=` operator compares them, so `1 in ('1.0', 2)` is true, NaN is never found, and <xref:NCalc.ExpressionOptions.StrictTypeMatching> applies (`1 in (1.0, 2)` is then false). When the right operand is a string, the operator looks for a substring, using the case and culture options of string comparisons.

**Examples:**
```csharp
'Insert' IN ('Insert', 'Update')          // True
42 NOT IN (1, 2, 3)                       // True
'Sergio' IN 'Sergio is at Argentina'      // True
'Mozart' NOT IN ('Chopin', 'Beethoven')   // True
945 IN (202, 303, 945)                    // True
945 ∈ (202, 303, 945)                     // True
```

### LIKE and NOT LIKE

The `LIKE` and `NOT LIKE` operators compare a string against a pattern.

* `LIKE` : Checks if the string matches the specified pattern.
* `NOT LIKE` : Checks if the string does not match the specified pattern.

Default matching uses a wildcard matcher (not a regular expression, so no pattern can make it backtrack for a long time) and is used unless an application handles the MatchString event of <xref:NCalc.Expression> (MatchStringAsync of <xref:NCalc.AsyncExpression>) and returns the result of matching. This event is fired before the default matching, allowing applications to override the default matching mechanism. 

With default matching, patterns can include:
* `%` to match any sequence of characters, including line breaks.
* `_` to match any single character.

The whole string must match the pattern. Matching is case-sensitive unless <xref:NCalc.ExpressionOptions.CaseInsensitiveStringComparer> is set, and uses the culture of the expression. Other values are converted to strings, except with <xref:NCalc.ExpressionOptions.StrictTypeMatching>, where both operands must be strings or chars (otherwise <xref:NCalc.Exceptions.NCalcEvaluationException> is thrown).

**Examples:**
```csharp
'HelloWorld' LIKE 'Hello%'     // True
'Test123' NOT LIKE 'Test__'    // True ("__" matches two characters, and "123" has three)
'2024-08-28' LIKE '2024-08-__' // True
'abc' LIKE 'a%'                // True
```

## Bitwise

Bitwise operators perform bitwise operations on integers. The bitwise AND has the highest priority, followed by the bitwise XOR and then by the bitwise OR. The shift operators are described in [Shift](#shift).

By default, when the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag is not set in <xref:NCalc.ExpressionOptions>, the following operator symbols are used:

* `&` : Bitwise AND
* `^` : Bitwise XOR
* `|` : Bitwise OR

**Example:**
```csharp
6 & 3 | 8   // 10
-1 & 3      // 3
~5          // 18446744073709551610
```

The following operators are always available, and are the only bitwise operators when the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag is set in <xref:NCalc.ExpressionOptions>:

* `BIT_AND` : Bitwise AND
* `BIT_XOR` : Bitwise XOR
* `BIT_OR` : Bitwise OR

The operands are 64-bit integers in two's complement (a `BigInteger` operand gives a `BigInteger`). A non-negative result is a `ulong` and a negative result a `long`: `6 & 3` is the `ulong` 2, `-1 & -1` is the `long` -1, and `~5` is the `ulong` 18446744073709551610. A floating-point operand is accepted when it is a whole number (`4.0 | 1` is 5); a fractional operand, NaN or infinity throws <xref:NCalc.Exceptions.NCalcEvaluationException>.

When assignments are enabled using the <xref:NCalc.ExpressionOptions.UseAssignments> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported (regardless of the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag):

* `&=` : Bitwise AND with assignment of the result to the left operand
* `^=` : Bitwise XOR with assignment of the result to the left operand 
* `|=` : Bitwise OR with assignment of the result to the left operand

## Logical

Logical operators perform logical operations between expressions. By default, binary logic is used: an operand is converted to a boolean (a number is true when it is not zero, and NaN is true; a string must be `"true"` or `"false"`), and an operand that cannot be converted throws <xref:NCalc.Exceptions.NCalcConversionException>. A null operand gives a null result.
When the  <xref:NCalc.ExpressionOptions.UseTernaryLogic> flag is set in <xref:NCalc.ExpressionOptions>, logic operations are performed in Kleene’s Strong Three-Valued Logic, where null and non-boolean values are treated as Unknown, and null is returned for Unknown: `null and false` is false, `null or true` is true, and `null and true` is null.

`and` and `or` evaluate the right operand only when the left one does not decide the result.

* `and`, `&&` : Logical AND
* `xor` : Logical XOR
* `or`, `||` : Logical OR

**Examples:**
```csharp
true or false and true    // Evaluates to true
(1 == 1) || false        // Evaluates to true
```

*Note:* The `and` operator has the highest priority, followed by `xor` and then by `or`. Hence, in the example above, `false and true` is evaluated first. When the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag is set, `&&`, `||` and `!` are not available; use `and`, `or` and `not`.

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operators are also supported:
* `∧` (U+2227) : Logical AND
* `⊕` (U+2295) : Logical XOR
* `⊻` (U+22BB) : Logical XOR
* `∨` (U+2228) : Logical OR

## Ternary

The ternary operator works as a short form of an "if then else" statement in regular programming languages and takes the form of "x ? y : z" where `x` is the condition, `y` and `z` are values or expressions that evaluate to values. if `x` is true, the value of `y` is returned, otherwise the value of `z` is returned. Only the selected branch is evaluated, and the branches may have different types.

The condition is converted to a boolean as for the [logical operators](#logical); a null condition gives a null result. The operator is right-associative, so it can be chained without parentheses.

Examples:
```csharp
x > 0 ? x : 0 // Returns the larger of x and 0. 
x < 0 ? -1 : x = 0 ? 0 : 1 // Returns the sign of x.
```

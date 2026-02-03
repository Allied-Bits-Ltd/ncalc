# Operators

Expressions can be combined using operators, each with a specific precedence priority. The precedence rules determine the order in which operations are performed in an expression. Below is the list of operators ordered by precedence in descending order:

1. **Primary**
2. **Factorial**
3. **Percent**
4. **Exponential**
5. **Unary**
6. **Multiplicative**
7. **Additive**
8. **Relational**
9. **Bitwise**
10. **Logical**

## Handling of null values

In some cases, null values can appear in expressions. If the <xref:NCalc.ExpressionOptions.TreatNullAsZero> flag in <xref:NCalc.ExpressionOptions> is set, nulls are converted to zeros for all operations except for comparison and matchiing; for the latter operations, null takes place in comparison/matching. If `TreatNullAsZero` is not set and null is found in one of the values, `null` is immediately returned without evaluating the expression.

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

**Examples:**
```csharp
(1; 2; 3)[1] // produces 2
'abcd'[1] // produces 'b'
(1; 2; 3)[1..2] // produces 2
(1; 2; 3)[0..2] // produces (1; 2)
'abcd'[1..3] // produces "bc"
'abcd'[^2..^1] // produces "bc"
'abcd'[..^1] // produces "abc"
'abcd'[2..] // produces "cd"
'abcd'[..] // produces "abcd"
```

If an expression is a parameter (variable) that contains a list or a string, it is possible to update one of its elements using the index operator with an integer index (it is not possible to update the range at the moment):
```
a := "abcd"; a[0] := "X" // single-character strings work like chars in this type of assignment
a := (1; 2; 3); a[1] := 'Y' // this is possible as our lists are lists of objects
```

## Factorial

Factorial ('!') is a special post-operator that is applicable to a value or expression that evaluates to an integer value. The resulting expression is binary, and the number of exclamation marks determines the type of factorial (standard, double, triple etc.). 

* `!`: standard factorial
* `!!`: double factorial
* `!!!`: triple factorial
* `!....!`: some multifactorial

**Example**:
```
(1+2)!
20!!!
```

## Percent

Percent is a post-operator that alters the way the operand is interpreted. A percent value is marked with a `%` character placed after a numeric value or expression.

Percent calculations must be enabled via the [AdvancedOptions](advanced_value_formats.md) property of the ExpressionBase class. When percent calculations are enabled, the `%` character is used for them, and `mod` is used for modulo division.

**Example**:
```
(1+2)%
5% + 2%
100 * 5%
200 + 10%
200 - 5%
```

If the result of an operation is percent, an instance of <xref:NCalc.Domain.Percent> is returned as a result of the expression evaluation. 

## Exponential

Exponential operators perform exponentiation.

* `**` : Exponentiation
* `^` : Exponentiation (when the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag is set in  <xref:NCalc.ExpressionOptions>)

**Example:**
```csharp
2 ** 2
2 ^ 3
```
When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:
* `↑` (U+2291): Exponentiation

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

## Multiplicative

Multiplicative operators perform multiplication, division, and modulus operations.

* `*` : Multiplication
* `/` : Division
* `//` : Integer Division (Python-like - division may be performed on floating-point operands, and the result is truncated)
* `\\` : Integer Division (Basic-like - floating-point operands are truncated first)
* `div` : Integer Division (Basic-like - floating-point operands are truncated first)
* `%` : Modulus (when percent calculation is disabled)
* `mod` : Modulus (when percent calculation is enabled in <xref:NCalc.AdvancedExpressionOptions>)

**Example:**
```csharp
1 * 2 % 3
```

When time operations are enabled using the <xref:NCalc.ExpressionOptions.SupportTimeOperations> flag in <xref:NCalc.ExpressionOptions>, it is possible to perform the following additional operations: 
* multiply a time period value (TimeSpan) by a number and obtain a time period as a result;
* divide a time period value (TimeSpan) by a number and obtain a time period as a result.

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operators are also supported:
* `×` (U+00D7) : Multiplication
* `∙` (U+2219): Multiplication
* `:` : Division
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

When time operations are enabled using the <xref:NCalc.ExpressionOptions.SupportTimeOperations> flag in <xref:NCalc.ExpressionOptions>, it is possible to perform the following additional operations: 
* add a time period value (TimeSpan) to a date (DateTime) or another time period (TimeSpan) value;
* add ticks (100ns per tick) to a date (DateTime) or time period (TimeSpan) value;
* subtract a time period value (TimeSpan) from a date (DateTime) or another time period (TimeSpan) value;
* subtract a date value (DateTime) from another date (DateTime) value;
* subtract ticks (100ns per tick) from a date (DateTime) or time period (TimeSpan) value.

When assignments are enabled using the <xref:NCalc.ExpressionOptions.UseAssignments> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:
* `+=` : Addition with assignment of the result to the left operand
* `-=` : Subtraction with assignment of the result to the left operand

The non-AOT version of NCalc can perform addition and subtraction operations on any types which have these operations defined (overloaded). This includes non-numeric types. The AOT version can handle numbers (including BigInteger and BigDecimal), dates and times (where the operations make sense), char variables, as well as add (concatenate)  strings.

## Relational

Relational operators compare two values and return a boolean result.

### Equality and Inequality Operators

These operators compare two values to check equality or inequality.

* `=`, `==` : Equal to
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

When an attempt is made to compare the values that are not compatible (e.g., a string and a number), the outcome depends on whether the <xref:NCalc.ExpressionOptions.CompareIncompatibleTypes> flag is set in <xref:NCalc.ExpressionOptions>. If it is set, `false` is returned; otherwise, an exception is thrown.

### IN and NOT IN

The `IN` and `NOT IN` operators check whether a value is present or absent within a specified list or string.

* `IN` : Returns `true` if the left operand is found in the right operand (which can be a list or a string).
* `NOT IN` : Returns `true` if the left operand is not found in the right operand.

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported:
* `∈` (U+2208) : Returns `true` if the left operand is found in the right operand (which can be a list or a string). 
* `∉` (U+2209) : Returns `true` if the left operand is not found in the right operand.

The right operand must be either a string or a list (`IEnumerable`).

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

Default matching uses RegEx and is used unless an application handles the MatchString event of <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression> and returns the result of matching. This event is fired before the default matching, allowing applications to override the default matching mechanism. 

With default matching, patterns can include:
* `%` to match any sequence of characters.
* `_` to match any single character.

**Examples:**
```csharp
'HelloWorld' LIKE 'Hello%'     // True
'Test123' NOT LIKE 'Test__'    // False
'2024-08-28' LIKE '2024-08-__' // True
'abc' LIKE 'a%'                // True
```

## Bitwise

Bitwise operators perform bitwise operations on integers. The `and` operator has highest priority, followed by `xor` and then by `or`.

* `<<` : Left shift
* `>>` : Right shift

By default, when the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag is not set in <xref:NCalc.ExpressionOptions>, the following operator symbols are used:

* `&` : Bitwise AND
* `^` : Bitwise XOR
* `|` : Bitwise OR

**Example:**
```csharp
2 >> 3
```

When the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag is set in <xref:NCalc.ExpressionOptions>, the following operators are used:

* `BIT_AND` : Bitwise AND
* `BIT_XOR` : Bitwise XOR
* `BIT_OR` : Bitwise OR

When assignments are enabled using the <xref:NCalc.ExpressionOptions.UseAssignments> flag in <xref:NCalc.ExpressionOptions>, the following operations are also supported (regardless of the <xref:NCalc.ExpressionOptions.SkipLogicalAndBitwiseOpChars> flag):

* `&=` : Bitwise AND with assignment of the result to the left operand
* `^=` : Bitwise XOR with assignment of the result to the left operand 
* `|=` : Bitwise OR with assignment of the result to the left operand

## Logical

Logical operators perform logical comparisons between expressions.

* `and`, `&&` : Logical AND
* `xor` : Logical XOR
* `or`, `||` : Logical OR

**Examples:**
```csharp
true or false and true    // Evaluates to true
(1 == 1) || false        // Evaluates to true
```

*Note:* The `and` operator has highest priority, followed by `xor` and then by `or`. Hence, in the example above, `false and true` is evaluated first.

When Unicode Characters are enabled for operations using the <xref:NCalc.ExpressionOptions.UseUnicodeCharsForOperations> flag in <xref:NCalc.ExpressionOptions>, the following operators are also supported:
* `∧` (U+2229) : Logical AND
* `⊕` (U+2295) : Logical XOR
* `⊻` (U+22BB) : Logical XOR
* `∨` (U+2228) : Logical OR

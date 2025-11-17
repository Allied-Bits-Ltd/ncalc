NCalc is a fast and lightweight expression evaluator library for .NET, designed for flexibility and high performance. It
supports a wide range of mathematical and logical operations. NCalc can parse any expression and evaluate the result,
including static or dynamic parameters and custom functions. NCalc targets .NET 10, .NET 9, .NET 8, .NET Standard 2.0, and NET Framework
4.6.2 and later.

## Advanced features

This version of NCalc contains a number of advanced features compared to the original NCalc project, such as 

* Assignable parameters (variables) including shortcut operators (+=, etc.).
* Statement sequences (useful together with parameter assignments) which, in expressions, may be grouped using curly brackets (like in C-like languages).
* Support for indexed expressions (when an expression evaluates to a list a string, it is possible to access individual elements or a range of elements and assign new values of individual elements).
* Support for loops using the 'while' loop statement (it follows the regular C-like style) with 'break' and 'continue' flow control keywords.
* Support for the 'if' statement, which follows the regular C-like style.
* Support for C-Style (line and block) and Python-style comments.
* User-defined functions with named parameters (right in the expression, yes).
* The 'return' flow control keyword that lets one return a value without completely evaluating the expression (useful in complex expressions with multiple statements, conditions, or loops, as well as with user-defined functions). 
* Advanced date and time parsing, which takes into account culture settings (current or specific culture or custom separators) and supports times with or without seconds as well as 12-hour time.
* Parsing of humane period expressions like "3 weeks 2 days 5 hours" (period identifiers are customizable and multiple identifier per period are supported).
* Basic calculations with dates and time spans - one can add and subtract dates and times. Without these operations, date and time values are of little use (if only with custom functions).
* Currency support, which takes into account culture settings (current or specific culture or custom symbols) and produces decimal result from the currency value.
* Optional use of BigInteger and BigDecimal types for basic math operations and most built-in funcitons.
* Underscores in numbers and currency values. Modern programming languages support underscores for readability. Support is built-in with binary, octal, and hex numbers, while support in decimal numbers requires a [custom branch of Parlot](https://github.com/Allied-Bits-Ltd/parlot/tree/ABCalc). 
* Custom decimal and group separators in numbers and currency.
* An optional secondary decimal number separator (requires a [custom branch of Parlot](https://github.com/Allied-Bits-Ltd/parlot/tree/ABCalc) ). 
* Raw strings which are not parsed for escape sequences.
* C-Style octal literals.
* Result Reference character. A pseudo-function that would let a user application return some value, such as the result of a previous calculation. This is handy when an expression should include this result multiple times.
* Percent calculations.
* Factorials (both regular and any complex factorials on integers are supported).
* Logical XOR operations.
* Certain Unicode characters can be used as operators.
* Lowercase lookup for parameter and function names.
* Optional non-recursive evaluator for large and complex expressions.
* The possibility to reuse a pre-created parser or parsers when parsing multiple expressions.
* New flags in ExpressionOptions to skip date and GUID parsers in order to speed up parsing.
* Minor improvements in the asyncrhonous code (CancellationToken and ConfigureAwait(false) are present in all calls).
* The main projects have been combined into one project.

This version has its roots in the original NCalc project, which resides [here on github](https://github.com/ncalc/ncalc). Updates in the original project are brought to Allied Bits' NCalc unless they conflict with this version. 
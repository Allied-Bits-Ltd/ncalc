# Welcome to NCalc!

NCalc is a fast and lightweight expression evaluator library for .NET, designed for flexibility and high performance. It supports a wide range of mathematical and logical operations. NCalc can parse any expression and evaluate the result, including static or dynamic parameters and custom functions. NCalc targets .NET 10, .NET 9, .NET 8, .NET Standard 2.0, and NET Framework 4.6.2 and later.

<br>

To get started, click on this [link](articles/index.md).

## What NCalc offers

Over the years, several variants of NCalc have evolved from the original library. This variant of NCalc contains a number of advanced features compared to the original NCalc project, such as 

* Assignable parameters (variables) including shortcut operators (+=, etc.).
* Statement sequences (useful together with parameter assignments) which, in expressions, may be grouped using curly brackets (like in C-like languages).
* Support for indexed expressions (when an expression evaluates to a list a string, it is possible to access individual elements or a range of elements and assign new values to individual elements).
* Support for loops using the 'while' loop statement (it follows the regular C-like style) with 'break' and 'continue' flow control keywords.
* Support for the 'if' statement, which follows the regular C-like style.
* Support for C-Style (line and block) and Python-style comments.
* User-defined functions with named parameters (right in the expression, yes).
* Percent calculations.
* Factorials (both regular and any complex factorials on integers are supported).
* Ternary logic support in logical operations
* The 'return' flow control keyword that lets one return a value without completely evaluating the expression (useful in complex expressions with multiple statements, conditions, or loops, as well as with user-defined functions). 
* Advanced date and time parsing, which takes into account culture settings (current or specific culture or custom separators) and supports times with or without seconds as well as 12-hour time and ISO 8601 dates and times.
* Parsing of humane date expressions like "3 days ago" or "in 5 weeks" (identifiers are customizable and multiple identifiers are supported).
* Parsing of humane period expressions like "3 weeks 2 days 5 hours" (period identifiers are customizable and multiple identifiers per period are supported).
* Basic calculations with dates and time spans - one can add and subtract dates and times, multiply and divide time periods.
* Currency support, which takes into account culture settings (current or specific culture or custom symbols) and produces decimal result from the currency value.
* Optional use of BigInteger and BigDecimal types for basic math operations and most built-in funcitons.
* Underscores in numbers and currency values. Modern programming languages support underscores for readability. 
* Custom decimal and group separators in numbers and currency.
* An optional secondary decimal number separator. 
* Raw strings which are not parsed for escape sequences.
* C-Style octal literals.
* The Result Reference character - a pseudo-function that would let a user application return some value, such as the result of a previous calculation.
* Logical XOR operations.
* Certain Unicode characters can be used as operators.
* Optional non-recursive evaluator for large and complex expressions.
* The possibility to reuse a pre-created parser or parsers when parsing multiple expressions.
* The AOT-compatible version of the library.


## Useful Links

* [GitHub](https://github.com/Allied-Bits-Ltd/ncalc)
* [NuGet.Org](https://www.nuget.org/profiles/AlliedBits)

<br>

## Bugs and feature requests

Have a bug or a feature request?
Please first search for existing and closed issues.</br>
If your problem or idea is not addressed yet, [please open a new issue](https://github.com/Allied-Bits-Ltd/ncalc/issues/new).

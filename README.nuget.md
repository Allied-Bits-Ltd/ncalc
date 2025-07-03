NCalc is a fast and lightweight expression evaluator library for .NET, designed for flexibility and high performance. It
supports a wide range of mathematical and logical operations. NCalc can parse any expression and evaluate the result,
including static or dynamic parameters and custom functions. NCalc targets .NET 9, .NET 8, .NET Standard 2.0, and NET Framework
4.6.2 and later.

## Advanced features

This branch of NCalc contains a number of advanced features compared to the main NCalc project, such as 

* Assignable parameters including shortcut operators (+=, etc.).
* Statement sequences (useful together with parameter assignments).
* Advanced date and time parsing, which takes into account culture settings (current or specific culture or custom separators) and supports times with or without seconds as well as 12-hour time.
* Parsing of humane period expressions like "3 weeks 2 days 5 hours" (period identifiers are customizable and multiple identifier per period are supported).
* Basic calculations with dates and time spans - one can add and subtract dates and times. Without these operations, date and time values are of little use (if only with custom functions).
* Currency support, which takes into account culture settings (current or specific culture or custom symbols). The idea is that the parser should drop the currency symbol when the expression is copied by the user from some source and includes this currency symbol (don't force people remove manually what the parser can remove automatically).
* Optional use of BigInteger type for basic math operations and some funcitons.
* Underscores in numbers and currency . Modern programming languages support underscores for readability. Support is built-in with binary, octal, and hex numbers, while support in decimal numbers requires a [custom branch of Parlot](https://github.com/Allied-Bits-Ltd/parlot/tree/ABCalc). 
* Custom decimal and group separators in numbers and currency. Requires more attention to optionally support both comma and dot as separators, but this also requires modifying Parlot parser or using the alternative parsing for decimal and double numbers.
* An optional secondary decimal number separator (requires a [custom branch of Parlot](https://github.com/Allied-Bits-Ltd/parlot/tree/ABCalc) ). 
* C-Style octal literals.
* Result Reference character. A pseudo-function that would let a user application return some value, such as the result of a previous calculation. This is handy when an expression should include this result multiple times.
* Percent calculations.
* Factorials (both regular and any complex factorials on integers are supported).
* Logical XOR operations.
* Certain Unicode characters can be used as operators.
* Lowercase lookup for parameter and function names.
* The possibility to re-initialize the parser before parsing an expression.
* New flags in ExpressionOptions to skip date and GUID parsers in order to speed up parsing.

This version is and will be synchronized with the main NCalc project. The main NCalc project resides [here on github](https://github.com/ncalc/ncalc), and its packages are available [here](https://www.nuget.org/packages/NCalc.Core) and [here](https://www.nuget.org/packages/NCalc.Sync) on NuGet.

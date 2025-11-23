![NCalc](NCalc.png "NCalc")

# NCalc

[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/Allied-Bits-ltd/ncalc/build-test.yml "GitHub Actions Workflow Status")](https://img.shields.io/github/actions/workflow/status/Allied-Bits-ltd/ncalc/build-test.yml)

[![NuGet](https://img.shields.io/nuget/v/AlliedBits.NCalc.svg)](https://www.nuget.org/packages/AlliedBits.NCalc) [![downloads](https://img.shields.io/nuget/dt/AlliedBits.NCalc)](https://www.nuget.org/packages/AlliedBits.NCalc)

NCalc is a fast and lightweight expression evaluator library for .NET, designed for flexibility and high performance. It
supports a wide range of mathematical and logical operations. NCalc can parse any expression and evaluate the result,
including static or dynamic parameters and custom functions. NCalc targets .NET 10, .NET 9, .NET 8, .NET Standard 2.0, 
and NET Framework 4.6.2 and later.

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

This version borrows some updates of the main NCalc project, which resides [here on github](https://github.com/ncalc/ncalc).

## Docs

Need help or want to learn more? [Check NCalc docs](https://alliedbits.com/ncalc).

## Help

> [!IMPORTANT]
> If you need help, [please open an issue](https://github.com/Allied-Bits-Ltd/ncalc/issues/new/choose) and include the expression to help us better understand the problem.
> Providing this information will aid in resolving the issue effectively.

## Getting Started

```
dotnet add package AlliedBits.NCalc
...
using NCalc;
```

## Functionalities

### Simple Expressions

```c#
var expression = new Expression("2 + 3 * 5");
Debug.Assert(17 == expression.Evaluate());
```

**Evaluates .NET data types**

```c#
Debug.Assert(123456 == new Expression("123456").Evaluate()); // integers
Debug.Assert(new DateTime(2001, 01, 01) == new Expression("#01/01/2001#").Evaluate()); // date and times
Debug.Assert(123.456 == new Expression("123.456").Evaluate()); // floating point numbers
Debug.Assert(true == new Expression("true").Evaluate()); // booleans
Debug.Assert("azerty" == new Expression("'azerty'").Evaluate()); // strings
```

**Handles mathematical functional from System.Math**

```c#
Debug.Assert(0 == new Expression("Sin(0)").Evaluate());
Debug.Assert(2 == new Expression("Sqrt(4)").Evaluate());
Debug.Assert(0 == new Expression("Tan(0)").Evaluate());
```

**Evaluates custom functions**

```c#
var expression = new Expression("SecretOperation(3, 6)");
expression.Functions["SecretOperation"] = (args) => {
    return (int)args[0].Evaluate() + (int)args[1].Evaluate();
};

Debug.Assert(9 == expression.Evaluate());
```

**Handles unicode characters**

```c#
Debug.Assert("経済協力開発機構" == new Expression("'経済協力開発機構'").Evaluate());
Debug.Assert("Hello" == new Expression(@"'\u0048\u0065\u006C\u006C\u006F'").Evaluate());
Debug.Assert("だ" == new Expression(@"'\u3060'").Evaluate());
Debug.Assert("\u0100" == new Expression(@"'\u0100'").Evaluate());
```

**Define parameters, even dynamic or expressions**

```c#
var expression = new Expression("Round(Pow([Pi], 2) + Pow([Pi2], 2) + [X], 2)");

expression.Parameters["Pi2"] = new Expression("Pi * [Pi]");
expression.Parameters["X"] = 10;

expression.DynamicParameters["Pi"] = _ => {
    Console.WriteLine("I'm evaluating π!");
    return 3.14;
};

Debug.Assert(117.07 == expression.Evaluate());
```

**JSON Serialization**

NCalc for .NET 8 and later has built-in support for polymorphic JSON serialization using [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json).

```c#
const string expressionString = "{waterLevel} > 4.0";

var logicalExpression = LogicalExpressionFactory.Create(expressionString, ExpressionOptions.NoCache); //Created a BinaryExpression object.

var jsonExpression = JsonSerializer.Serialize(parsedExpression);

var deserializedLogicalExpression = JsonSerializer.Deserialize<LogicalExpression>(jsonExpression); //The object is still a BinaryExpression.

var expression = new Expression(deserializedLogicalExpression);

expression.Parameters = new Dictionary<string, object> {
    {"waterLevel", 4.0}
};

var result = expression.Evaluate();
```

**Caching**

NCalc automatically caches parsing of strings using a [`ConcurrentDictionary`](https://learn.microsoft.com/pt-br/dotnet/api/system.collections.concurrent.concurrentdictionary-2).
You can also use our [Memory Cache plugin](https://ncalc.github.io/ncalc/articles/plugins/memory_cache.html).

**Lambda Expressions**

```cs
var expression = new Expression("1 + 2");
Func<int> function = expression.ToLambda<int>();
Debug.Assert(function()); //3
```

## Related projects

### [Parlot](https://github.com/sebastienros/parlot) (Main project)
### [AlliedBits.Parlot](https://github.com/Allied-Bits-Ltd/parlot) (the fork with some improvements required for Allied Bits enhancements of NCalc)

Fast and lightweight parser creation tools by [Sébastien Ros](https://github.com/sebastienros) that NCalc uses at its
parser.

### [FastExpressionCompiler](https://github.com/dadhi/FastExpressionCompiler)

Fast Compiler for C# Expression Trees. Developed by [Maksim Volkov](https://github.com/dadhi)

### [PanoramicData.NCalcExtensions](https://github.com/panoramicdata/PanoramicData.NCalcExtensions)

Extension functions for NCalc to handle many general functions,  
including string functions, switch, if, in, typeOf, cast etc.  
Developed by David, Dan and all at [Panoramic Data](https://github.com/panoramicdata).

### [Jint](https://github.com/sebastienros/jint)

JavaScript Interpreter for .NET by [Sébastien Ros](https://github.com/sebastienros), the original author of NCalc library.  
Runs on any modern .NET platform as it supports .NET Standard 2.0 and .NET 4.6.1 targets (and up).

![NCalc](NCalc.png "NCalc")

# NCalc

[![GitHub Actions Workflow Status](https://github.com/Allied-Bits-ltd/ncalc/actions/workflows/build-test.yml "GitHub Actions Workflow Status")](https://img.shields.io/github/actions/workflow/status/Allied-Bits-ltd/ncalc/build-test.yml)

[![NuGet](https://img.shields.io/nuget/v/AlliedBits.NCalc.svg)](https://www.nuget.org/packages/AlliedBits.NCalc) [![downloads](https://img.shields.io/nuget/dt/AlliedBits.NCalc)](https://www.nuget.org/packages/AlliedBits.NCalc)

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

## Docs

Need help or want to learn more? [Check our docs](https://ncalc.github.io/ncalc) (documentation there does not include the advanced features listed above; those are documented in the .md files within the branch).

## Learn more

For additional information on the technique we used to create this framework please check these articles;
- [How to execute mathematical expressions in a string in .NET](https://www.jjconsulting.com.br/en-us/blog/programming/ncalc)
- [State of the Art Expression Evaluation](https://www.codeproject.com/Articles/18880/State-of-the-Art-Expression-Evaluation)

## Help

> [!IMPORTANT]
> If you need help, [please open an issue](https://github.com/Allied-Bits-Ltd/ncalc/issues/new/choose) and include the expression
> to help us better understand the problem.
> Providing this information will aid in resolving the issue effectively.

## Getting Started

```
dotnet add package AlliedBits.NCalc
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

JavaScript Interpreter for .NET by [Sébastien Ros](https://github.com/sebastienros), the author of NCalc library.  
Runs on any modern .NET platform as it supports .NET Standard 2.0 and .NET 4.6.1 targets (and up).

### [NCalcJS](https://github.com/thomashambach/ncalcjs)

A TypeScript/JavaScript port of NCalc.

### [NCalc101](https://ncalc101.magicsuite.net)

NCalc 101 is a simple web application that allows you to try out the NCalc expression evaluator, developed
by [Panoramic Data](https://github.com/panoramicdata).

### [JJMasterData](https://github.com/JJConsulting/JJMasterData/)

JJMasterData is a runtime form generator from database metadata. It uses NCalc to evaluate expressions used in field
visibility and other dynamic behaviors.

## NCalc versioning

The project uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) tool to manage versions.  
Each library build can be traced back to the original git commit.
Read more about [versioning here.](https://ncalc.github.io/ncalc/articles/new_release.html)

## Discord Server

If you want to speak with the NCalc core team, get support, or just get the latest NCalc news, [come to our discord server](https://discord.gg/TeJkmXbqFk).

## Star History

<a href="https://star-history.com/#ncalc/ncalc&Date">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/svg?repos=ncalc/ncalc&type=Date&theme=dark" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/svg?repos=ncalc/ncalc&type=Date" />
   <img alt="Star History Chart" src="https://api.star-history.com/svg?repos=ncalc/ncalc&type=Date" />
 </picture>
</a>

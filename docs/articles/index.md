# Intro

NCalc is a mathematical expression evaluator in .NET. NCalc can parse any expression or a group of expressions and evaluate the result, including static or dynamic parameters and custom functions.

## Project Description

NCalc is a mathematical expression evaluator in .NET. NCalc can parse any expression or a group of expressions and evaluate the result, including static or dynamic parameters and custom functions.

## Table of Contents
- [Operators](operators.md): Available standard operators and structures.
- [Values](values.md): Authorized values like types and functions.
- [Advanced Value Formats](advanced_value_formats.md): Advanced Value Formats and Operations
- [Functions](functions.md): List of already implemented functions.
- [Parameters/Variables](parameters.md): How to use parameters (variables) in expressions.
- [Handling Errors](handling_errors.md): How to handle errors.
- [Case Sensitivity](case_sensitivity.md): Options in how to handle case sensitivity.
- [Overflow Handling](overflow_protection.md): How to handle overflow with binary arithmetic operations
- [Async Support](async.md): How and when to use `async`.
- [Caching](caching.md): How caching works.
- [Improve performance](lambda_compilation.md): How to use compilation of expressions to CLR lambdas.
- [Dependency Injection](dependency_injection.md): Bring expressions to the next level with dependency injection.
- [Architecture](architecture.md): Check this article to learn how NCalc works.
- [Benchmarks](benchmarks.md): Check some numbers about the speed of some NCalc components.

## Functionalities

### Expressions and statements 

This branch of NCalc handles expressions which include variable assignment statements (which are also expressions as they return the assigned value) and groups of expressions. 
Expressions in a group are separated with a semicolon. One or several expressions may be wrapped with curly braces '{' and '}' to make a single expression, which is useful when a group of expressions should be treated as one expression (e.g., in function parameters).
To enable expression sequences, include the <xref:NCalc.ExpressionOptions.UseStatementSequences> flag in <xref:NCalc.ExpressionOptions> passed when creating an instance of the <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression> class:

```c#
var expression = new NCalc.Expression("{ 1; 2 }", ExpressionOptions.UseStatementSequences);
```

### Simple Expressions

```c#
var expression = new Expression("2 + 3 * 5");
Debug.Assert(17 == expression.Evaluate());
```

### Conditional Statements

This branch of NCalc supports if statements with a common C-style syntax.
To enable them, include the <xref:NCalc.ExpressionOptions.UseIfStatement> flag in <xref:NCalc.ExpressionOptions> passed when creating an instance of the <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression> class:

```c#
var expression = new NCalc.Expression("a = 1; if (a < 5) { a += 1; }; a", ExpressionOptions.UseIfStatement | ExpressionOptions.UseAssignments | ExpressionOptions.UseCStyleAssignments | ExpressionOptions.UseStatementSequences);

```

### Loops

This branch of NCalc supports `while` loops with a common C-style syntax.
To enable them, include the <xref:NCalc.ExpressionOptions.UseLoops> flag in <xref:NCalc.ExpressionOptions> passed when creating an instance of the <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression> class:

```c#
var expression = new NCalc.Expression("a = 1; while (a < 5) { a += 1; }", ExpressionOptions.UseLoops | ExpressionOptions.UseAssignments | ExpressionOptions.UseCStyleAssignments | ExpressionOptions.UseStatementSequences);

```

### Returning of Value

An expression always evaluates to some value. In complex expressions which include conditional statements and loops, it may be desired to return a value without evaluating the expression completely. For this, one can use a common `return` keyword followed by the value to return. 

### .NET Data Types

```c#
Debug.Assert(123456 == new Expression("123456").Evaluate()); // integers
Debug.Assert(new DateTime(2001, 01, 01) == new Expression("#01/01/2001#").Evaluate()); // datetime
Debug.Assert(123.456 == new Expression("123.456").Evaluate()); // floating point numbers
Debug.Assert(true == new Expression("true").Evaluate()); // booleans
Debug.Assert("azerty" == new Expression("'azerty'").Evaluate()); // strings
```

### Mathematical functional from [System.Math](https://learn.microsoft.com/en-us/dotnet/api/system.math?view=net-8.0)**

```c#
Debug.Assert(0 == new Expression("Sin(0)").Evaluate());
Debug.Assert(2 == new Expression("Sqrt(4)").Evaluate());
Debug.Assert(0 == new Expression("Tan(0)").Evaluate());
```

### Custom Functions

```c#
var expression = new Expression("SecretOperation(3, 6)");
expression.Functions["SecretOperation"] = (args) => {
    return (int)args[0].Evaluate() + (int)args[1].Evaluate();
};

Debug.Assert(9 == expression.Evaluate());
```

### Unicode Characters

```c#
Debug.Assert("経済協力開発機構" == new Expression("'経済協力開発機構'").Evaluate());
Debug.Assert("Hello" == new Expression(@"'\u0048\u0065\u006C\u006C\u006F'").Evaluate());
Debug.Assert("だ" == new Expression(@"'\u3060'").Evaluate());
Debug.Assert("\u0100" == new Expression(@"'\u0100'").Evaluate());
```

### Parameters (variables) - Static and Dynamic

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

### Lambda Expressions
```cs
var expression = new Expression("1 + 2");
Func<int> function = expression.ToLambda<int>();
Debug.Assert(function()); //3
```

### Comments

This branch of NCalc supports line and block C-style comments and Python line comments. 
To enable them, include the <xref:NCalc.ExpressionOptions.SupportCStyleComments> or <xref:NCalc.ExpressionOptions.SupportPythonComments> flag in <xref:NCalc.ExpressionOptions> passed when creating an instance of the <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression> class:

```c#
var expression = new NCalc.Expression("{ 1 /* this produces 1 as a result */ }", ExpressionOptions.SupportCStyleComments | ExpressionOptions.SupportPythonComments);
```

Note that when C-style comments are enabled, the "//" operator used in Python for integer division, cannot be used for this purpose. If C-style comments are disabled, this "//" integer division operator is supported.

### Recursive and non-recursive evaluation

By default, the evaluator works by recursively traversing the expression tree. Note that even linear expressions like "1+2+...+99+100" would be parsed into a tree with plenty of nodes (about 200 nodes ni the given example). This can cause a StackOverflow error.
If you need to evaluate such long or deeply nested expressions, you can enable the non-recursive evaluator by setting the <xref:NCalc.ExpressionOptions.UseNonRecursiveEvaluator> flag in <xref:NCalc.ExpressionOptions> passed when creating an instance of the <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression> class.
This non-recursive evaluator is slower than the recursive one, so it is disabled by default.
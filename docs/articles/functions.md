# Functions

NCalc supports built-in functions,  custom functions, handled by the application, and user-defined functions defined right in an expression.

## Built-in Functions

The framework includes a set of already implemented functions.

| Name                 | Description                                                                                                                                         | Usage                   | Result |
|----------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------|--------|
| Abs                  | Returns the absolute value of a specified number.                                                                                                   | Abs(-1)                 | 1d     |
| Acos                 | Returns the angle whose cosine is the specified number.                                                                                             | Acos(1)                 | 0d     |
| Asin                 | Returns the angle whose sine is the specified number.                                                                                               | Asin(0)                 | 0d     |
| Atan                 | Returns the angle whose tangent is the specified number.                                                                                            | Atan(0)                 | 0d     |
| Ceiling              | Returns the smallest integer greater than or equal to the specified number.                                                                         | Ceiling(1.5)            | 2d     |
| Cos                  | Returns the cosine of the specified angle.                                                                                                          | Cos(0)                  | 1d     |
| Exp                  | Returns e raised to the specified power.                                                                                                            | Exp(0)                  | 1d     |
| Floor                | Returns the largest integer less than or equal to the specified number.                                                                             | Floor(1.5)              | 1d     |
| IEEERemainder        | Returns the remainder resulting from the division of a specified number by another specified number.                                                | IEEERemainder(3; 2)     | -1d    |
| Ln                   | Returns the natural logarithm of a specified number.                                                                                                | Ln(1)                   | 0d     |
| Log                  | Returns the logarithm of a specified number.                                                                                                        | Log(1; 10)              | 0d     |
| Log2 (.NET 8+)       | Returns the base 2 logarithm of a specified number.                                                                                                 | Log2(1)                 | 0d     |
| Log10                | Returns the base 10 logarithm of a specified number.                                                                                                | Log10(1)                | 0d     |
| MakeList             | Creates a new list with the specified number of elements, optionally fille with some value.                                                         | MakeList(3)             | (null, null, null) |
|                      | The elements of this list can be set via a second parameter or later using an assignment with an index operator (list[x] := y)                      | MakeList(3; 1)          | (1, 1, 1)          |                    
| MakeStr[ing]         | Creates a string with the given number or repeated values. A values may be a character, a string, or an integer number (character code [1..65535]   | MakeStr(3; 'c')         | "ccc"  |                                           
| Max                  | Returns the larger of two specified numbers.                                                                                                        | Max(1; 2)               | 2      |
| Min                  | Returns the smaller of two numbers.                                                                                                                 | Min(1; 2)               | 1      |
| PercentDiff          | Returns the answer to the question "What is the difference between arguments in percent?"                                                           | %(80;60)                | -20%   |   
| PercentOf or %(a,b)  | Returns the answer to the question "What percent is a of b?"                                                                                        | %(50;75)                | 50%    |   
| Pow                  | Returns a specified number raised to the specified power.                                                                                           | Pow(3; 2)               | 9d     |
| Round                | Rounds a value to the nearest integer or specified number of decimal places.                                                                        | Round(3.222; 2)         | 3.22d  |
|                      | The mid number behaviour can be changed by using ExpressionOptions.RoundAwayFromZero during construction of the Expression object.                  |                         |        |
| Sign                 | Returns a value indicating the sign of a number.                                                                                                    | Sign(-10)               | -1     |
| Sin                  | Returns the sine of the specified angle.                                                                                                            | Sin(0)                  | 0d     |
| Sqrt                 | Returns the square root of a specified number.                                                                                                      | Sqrt(4)                 | 2d     |
| Tan                  | Returns the tangent of the specified angle.                                                                                                         | Tan(0)                  | 0d     |
| Truncate             | Calculates the integral part of a number.                                                                                                           | Truncate(1.7)           | 1      |

It also includes other general purpose ones.

| Name      | Description                                                                                          | Usage                                                | Result                                                                         |
|-----------|------------------------------------------------------------------------------------------------------|------------------------------------------------------|--------------------------------------------------------------------------------|
| in        | Returns whether an element is in a set of values.                                                    | in(1 + 1, 1, 2, 3)                                   | true                                                                           |
| iff       | Returns a value based on a condition.                                                                | iff(3 % 2 = 1, 'value is true', 'value is false')    | 'value is true'                                                                |
| if        | If is an alias to iff, accessible when the use of If as a statement is disabled.                     | if(3 % 2 = 0, 'value is true')                       | null                                                                           |
| ifs       | Returns a value based on evaluating a number of conditions, returning a default (when specified)     | ifs(foo > 50, "bar", foo > 75, "baz", "quux")        | if foo is between 50 and 75 "bar", foo greater than 75 "baz", otherwise "quux" |  
|           | or null (when no default is specified) if none of the conditions are true.                           | ifs(foo > 50, "bar", foo > 75, "baz")                | if foo is between 50 and 75 "bar", foo greater than 75 "baz", otherwise null   |  

By default, you can use comma (,) or semicolon (;) as argument separator. A semicolon is recommended to avoid a possible conflict of a comma-as-a-separator with a coma being a decimal separator or number group separator. 
You can change the argument separator using the <xref:NCalc.AdvancedExpressionOptions.ArgumentSeparator> property of the <xref:NCalc.AdvancedExpressionOptions> class, an instance of which you create and assign to the 
`AdvancedOptions` property of <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression>. 

When big numbers are not enabled or when they are enabled but the parameter is not a BigDecimal or BigInteger, the following applies: 
if the <xref:NCalc.ExpressionOptions.DecimalAsDefault> flag is set and the System.Math class has a backing function that accepts decimal arguments, the function casts the argument(s) to <xref:System.Decimal> before calling the backing function; otherwise, it casts the argument(s) to <xref:System.Double>.

## Custom Functions
Custom functions are created using the <xref:NCalc.ExpressionFunction> delegate. The parameters are <xref:NCalc.Expression> instances that can be lazy evaluated.
```csharp
expression.Functions["SecretOperation"] = (args) => {
    return (int)args[0].Evaluate() + (int)args[1].Evaluate();
};
```

## User-Defined Functions

A user can define a function right in the expression. Such a function can be called in the other parts of the expression. For user-defined functions to work, include the <xref:NCalc.ExpressionOptions.UseStatementSequences> flag into <xref:NCalc.ExpressionOptions> of an <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression>. The declaration syntax is similar to C# or Rust as shown in the below example:

```
// This is a description of a function with all mandatory parameters
fn sum3(a,b,c) { a + b + c }; sum3(1;2;3) // will return 6

// This is a description of a function with an optional parameter
fn sum3(a, b = 0, c = 3) => a + b + c; sum3(1;2) // will return 6 too
```

After an expression is parsed, user-defined functions are available in the <xref:NCalc.ExpressionBase`1.UserFunctions> dictionary. A <xref:NCalc.Domain.Function> object includes a description if it was included in the expression in the form of a comment immediately before the function declaration. 

To return a value from a function, one can use a common `return` keyword followed by the value to return.

## Using Event Handlers
You can also use event handlers to handle functions.
```csharp
expression.EvaluateFunction += delegate(string name, FunctionArgs args)
{
    if (name == "SecretOperation")
        args.Result = (int)args.Parameters[0].Evaluate() + (int)args.Parameters[1].Evaluate();
};
```

## Case Sensitivity
See [case_sensitivity](case_sensitivity.md) for more info.

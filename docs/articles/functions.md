# Functions

NCalc supports built-in functions,  custom functions, handled by the application, and user-defined functions defined right in an expression.

## Built-in Functions

The framework includes a set of already implemented functions. The suffix `d` in the Result column marks a `double`.

| Name                 | Description                                                                                                                                         | Usage                   | Result |
|----------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------|--------|
| Abs                  | Returns the absolute value of a specified number. An integer keeps its type (`Abs(long.MinValue)` is a `ulong`).                                    | Abs(-1)                 | 1      |
| Acos                 | Returns the angle whose cosine is the specified number.                                                                                             | Acos(1)                 | 0d     |
| Asin                 | Returns the angle whose sine is the specified number.                                                                                               | Asin(0)                 | 0d     |
| Atan                 | Returns the angle whose tangent is the specified number.                                                                                            | Atan(0)                 | 0d     |
| Atan2                | Returns the angle whose tangent is the quotient of two specified numbers (y, x).                                                                    | Atan2(0; -1)            | 3.141592653589793d |
| Ceiling              | Returns the smallest integer greater than or equal to the specified number. An integer is returned unchanged.                                       | Ceiling(1.5)            | 2d     |
| Cos                  | Returns the cosine of the specified angle.                                                                                                          | Cos(0)                  | 1d     |
| Exp                  | Returns e raised to the specified power.                                                                                                            | Exp(0)                  | 1d     |
| Floor                | Returns the largest integer less than or equal to the specified number. An integer is returned unchanged.                                           | Floor(1.5)              | 1d     |
| IEEERemainder        | Returns the remainder resulting from the division of a specified number by another specified number.                                                | IEEERemainder(3; 2)     | -1d    |
| Ln                   | Returns the natural logarithm of a specified number.                                                                                                | Ln(1)                   | 0d     |
| Log                  | Returns the logarithm of a specified number (the first argument) in a specified base (the second argument).                                         | Log(8; 2)               | 3d     |
| Log2 (.NET 8+)       | Returns the base 2 logarithm of a specified number.                                                                                                 | Log2(1)                 | 0d     |
| Log10                | Returns the base 10 logarithm of a specified number.                                                                                                | Log10(1)                | 0d     |
| MakeList             | Creates a new list with the specified number of elements, optionally filled with some value.                                                        | MakeList(3)             | (null, null, null) |
|                      | The elements of this list can be set via a second parameter or later using an assignment with an index operator (list[x] := y).                     | MakeList(3; 1)          | (1, 1, 1)          |
| MakeStr[ing]         | Creates a string with the given number or repeated values. A values may be a character, a string, or an integer number (character code [1..65535]). | MakeStr(3; 'c')         | "ccc"  |
| Max                  | Returns the larger of two specified numbers.                                                                                                        | Max(1; 2)               | 2      |
| Min                  | Returns the smaller of two numbers.                                                                                                                 | Min(1; 2)               | 1      |
| PercentDiff          | Returns the answer to the question "What is the difference between arguments in percent?"                                                           | PercentDiff(80;60)      | -25%   |   
| PercentOf or %(a,b)  | Returns the answer to the question "What percent is a of b?" (`%(a,b)` requires `AdvExpressionOptions.CalculatePercent`)                            | PercentOf(40;80)        | 50%    |   
| Pow                  | Returns a specified number raised to the specified power. The same as the `**` operator.                                                            | Pow(3; 2)               | 9d     |
| Round                | Rounds a value to the specified number of decimal places (the second argument is required; it may be 0 or negative).                                | Round(3.222; 2)         | 3.22d  |
|                      | The mid number behaviour can be changed by using ExpressionOptions.RoundAwayFromZero during construction of the Expression object.                  | Round(1234; -2)         | 1200   |
| Sign                 | Returns a value indicating the sign of a number.                                                                                                    | Sign(-10)               | -1     |
| Sin                  | Returns the sine of the specified angle.                                                                                                            | Sin(0)                  | 0d     |
| Sqrt                 | Returns the square root of a specified number.                                                                                                      | Sqrt(4)                 | 2d     |
| Tan                  | Returns the tangent of the specified angle.                                                                                                         | Tan(0)                  | 0d     |
| Truncate or Trunc    | Calculates the integral part of a number.                                                                                                           | Truncate(1.7)           | 1d     |

It also includes other general purpose ones.

| Name      | Description                                                                                          | Usage                                                | Result                                                                         |
|-----------|------------------------------------------------------------------------------------------------------|------------------------------------------------------|--------------------------------------------------------------------------------|
| in        | Returns whether an element is in a set of values.                                                    | in(1 + 1, 1, 2, 3)                                   | true                                                                           |
| iff       | Returns a value based on a condition.                                                                | iff(3 % 2 = 1, 'value is true', 'value is false')    | 'value is true'                                                                |
| if        | If is an alias to iff, accessible when the use of If as a statement is disabled.                     | if(3 % 2 = 0, 'value is true')                       | null                                                                           |
| ifs       | Returns a value based on evaluating a number of conditions, returning a default (when specified)     | ifs(foo > 50, "bar", foo > 75, "baz", "quux")        | if foo is between 50 and 75 "bar", foo greater than 75 "baz", otherwise "quux" |
|           | or null (when no default is specified) if none of the conditions are true.                           | ifs(foo > 50, "bar", foo > 75, "baz")                | if foo is between 50 and 75 "bar", foo greater than 75 "baz", otherwise null |

`iff`, `if` and `ifs` evaluate only the arguments they need, and convert their conditions to booleans as the [logical operators](operators.md#logical) do. `in` compares the first argument with the others as the `=` operator does.

By default, you can use comma (,) or semicolon (;) as argument separator. A semicolon is recommended to avoid a possible conflict of a comma-as-a-separator with a coma being a decimal separator or number group separator. 
You can change the argument separator using the <xref:NCalc.AdvancedExpressionOptions.ListItemSeparator> property of the <xref:NCalc.AdvancedExpressionOptions> class, an instance of which you create and assign to the 
`AdvancedOptions` property of <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression>. 

The names of built-in functions are case-sensitive (`abs(1)` is not found), unless the <xref:NCalc.ExpressionOptions.IgnoreCaseAtBuiltInFunctions> flag is set. A function name is resolved in this order: functions declared in the expression with `fn`, the `EvaluateFunction` handlers, the `Functions` dictionary, and finally the built-in functions; so a custom function with the name of a built-in function takes precedence over it.

### Arguments and results

* A built-in math function returns `null` when an argument is null.
* A wrong number of arguments throws <xref:NCalc.Exceptions.NCalcEvaluationException> (e.g. "Round() takes exactly 2 arguments"). An unknown function throws <xref:NCalc.Exceptions.NCalcFunctionNotFoundException>.
* Numeric strings are accepted as numbers; other values that cannot be converted to a number throw <xref:NCalc.Exceptions.NCalcConversionException> or <xref:NCalc.Exceptions.NCalcEvaluationException>.
* The trigonometric, hyperbolic, exponential and logarithmic functions, `Sqrt`, `Atan2` and `IEEERemainder` compute in `double` and return a `double` (out-of-domain arguments give NaN or an infinity, as in <xref:System.Math>: `Sqrt(-1)` is NaN and `Ln(0)` is -∞), except for big-number arguments with `UseBigNumbers` (see below).
* `Abs`, `Ceiling`, `Floor`, `Round`, `Sign` and `Truncate` keep the precision of `decimal` and big-number arguments, and `Abs`, `Ceiling`, `Floor` and `Round` return an integer argument as an integer. `Sign` returns an `int` (NaN for a NaN argument). When the <xref:NCalc.ExpressionOptions.DecimalAsDefault> flag is set, a `double` argument of these functions is computed as a `decimal` when `decimal` keeps its digits.
* `Max` and `Min` return the result in the type of the arithmetic on both arguments (`Max(3, 2.5)` is the double 3), return NaN when an argument is NaN, take a numeric string as a number, and throw <xref:NCalc.Exceptions.NCalcEvaluationException> for a number and a non-numeric value. They order values as the comparison operators do.
* `Pow` (and `**`): see [Exponential](operators.md#exponential) for the result type and the size limit of exact powers.
* `Round(x, n)` rounds the decimal (shortest round-trip) form of the number, so `Round(2.345, 2)` is 2.34, and a midpoint is rounded to the nearest even digit by default (`Round(2.5, 0)` is 2). With <xref:NCalc.ExpressionOptions.RoundAwayFromZero>, a midpoint is rounded away from zero (`Round(2.5, 0)` is 3). With <xref:NCalc.ExpressionOptions.UseSystemMathRound>, `Math.Round` is applied to the binary value instead. A negative number of places rounds to tens, hundreds and so on (`Round(1234, -2)` is 1200); more places than the type holds leave the value unchanged. An integer result that does not fit the type of the argument gets the next integer type (`Round(2147483647, -1)` is the long 2147483650).
* The factorial, exact powers, products of big numbers and the size of the results of `MakeList` and `MakeStr` are limited; see [Limits](options.md#limits).

**Big numbers.** When the <xref:NCalc.ExpressionOptions.UseBigNumbers> flag is set and an argument is a <xref:System.Numerics.BigInteger> or <xref:ExtendedNumerics.BigDecimal>, the transcendental functions (`Sqrt`, `Exp`, `Ln`, `Log`, `Log2`, `Log10`, the trigonometric and inverse trigonometric functions, and the root operators) are computed by NCalc with about `BigDecimal.Precision` significant digits (100 by default) and return a `BigDecimal`. Out-of-domain arguments give NaN or an infinity as <xref:System.Math> does. An argument of `Sin`, `Cos` or `Tan` with more than 10,000 integer digits, or an `Exp` whose result would exceed `MathHelper.MaxPowResultDigits` digits, throws <xref:NCalc.Exceptions.NCalcEvaluationException>. These functions are thread-safe and do not change `BigDecimal.AlwaysTruncate`.

When big numbers are not enabled or when they are enabled but the parameter is not a BigDecimal or BigInteger, the following applies: 
if the <xref:NCalc.ExpressionOptions.DecimalAsDefault> flag is set and the System.Math class has a backing function that accepts decimal arguments, the function casts the argument(s) to <xref:System.Decimal> before calling the backing function; otherwise, it casts the argument(s) to <xref:System.Double>. A `BigDecimal` beyond the range of `double` becomes an infinity of its sign in a function computed in `double`.

**Percents, complex numbers and vectors.**

* `Abs`, `Round`, `Ceiling`, `Floor` and `Truncate` of a percent return a percent (`Round(12.346%, 2)` is 12.35%); `Sign` of a percent is the sign of its value, and `Sqrt` of a percent is the square root of its fraction (`Sqrt(25%)` is 0.5). `PercentOf` and `PercentDiff` do not accept percent arguments.
* Complex numbers are accepted by `Abs` (the modulus), `Sqrt`, `Pow`, `Exp`, `Ln`, `Log`, the trigonometric functions and the rounding functions. A complex number whose imaginary part is zero is used as a real number, so `Sqrt(-1 + 0i)` is NaN like `Sqrt(-1)`. `Sign` and `IEEERemainder` of a complex number throw <xref:NCalc.Exceptions.NCalcEvaluationException>.
* `Abs`, `Floor`, `Ceiling`, `Truncate`, `Round`, `Sign`, `Sqrt`, `Pow` (with a scalar exponent) of a vector, and `Max` and `Min` of two vectors, are computed element-wise.

## Custom Functions
Custom functions are created using the <xref:NCalc.ExpressionFunction> delegate. The parameters are <xref:NCalc.Expression> instances that can be lazy evaluated.
```csharp
expression.Functions["SecretOperation"] = (args) => {
    return (int)args[0].Evaluate() + (int)args[1].Evaluate();
};
```

The arguments are evaluated with the cancellation token of the evaluation. Custom functions are looked up by name using the comparer of the `Functions` dictionary (see [Case Sensitivity](case_sensitivity.md)).

## User-Defined Functions

A user can define a function right in the expression. Such a function can be called in the other parts of the expression. For user-defined functions to work, include the <xref:NCalc.ExpressionOptions.UseStatementSequences> flag into <xref:NCalc.ExpressionOptions> of an <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression>. The declaration syntax is similar to C# or Rust as shown in the below example:

```
/// This is a description of a function with all mandatory parameters
fn sum3(a,b,c) { a + b + c }; sum3(1;2;3) // will return 6

/// This is a description of a function with an optional parameter
fn sum3(a, b = 0, c = 3) => a + b + c; sum3(1;2) // will return 6 too
```

After an expression is parsed (e.g. with a call to the <xref:NCalc.ExpressionBase`1.HasErrors(System.Threading.CancellationToken)> method), user-defined functions are available in the <xref:NCalc.ExpressionBase`1.UserFunctions> dictionary. A <xref:NCalc.Domain.Function> object includes a description if it was included in the expression in the form of a documentation comment ("///") immediately before the function declaration (other comments may stand between them). Documentation comments before a function declaration are recognized whether or not comments are enabled; elsewhere, `///` is an ordinary comment when the <xref:NCalc.ExpressionOptions.SupportCStyleComments> flag is set, and an error otherwise. Declaring two functions with the same name throws <xref:NCalc.Exceptions.NCalcParserException>.

To return a value from a function, one can use a common `return` keyword followed by the value to return. `break` and `continue` in a function body act only on the loops of that body.

### Scope of names in a function body

The body of a user-defined function is evaluated with the context of its caller:

- The body can call any function declared in the expression, whether it is declared before or after the calling function, including the function itself (recursion), as well as the functions declared in the body itself (they take precedence over the functions with the same names declared outside).
- A name is looked up first among the parameters of the function and the values assigned in the body. A name that the body does not define is looked up in the caller: the parameters of the calling function (when the function is called from the body of another user-defined function), then the parameters (`Parameters`), dynamic parameters (`DynamicParameters`), custom functions (`Functions`) and event handlers of the expression.
- Assignments in the body do not change the parameters of the expression: assigning a value to a name that is not a parameter of the function, including an indexed assignment (`l[0] := 9`) to a list of the caller, keeps the new value in the body. The `UpdateParameter` handlers are called as for other assignments. An assignment to a parameter of a function in whose body the function is declared changes the argument of that call. A function declared elsewhere (e.g. at the top level) reads the parameters of a function that calls it, but its assignments to such a name stay in its own body, and an indexed assignment changes a copy of the list.
- An indexed assignment to an argument (`fn g(a) { a[1] := 5; a }`) changes that element of a copy of the argument; the list of the caller is not changed.

```
fn fact(n) => if(n <= 1, 1, n * fact(n - 1)); fact(5) // will return 120

fn scale(x) => x * factor; scale(2) // with Parameters["factor"] = 10, will return 20

fn even(n) => if(n = 0, true, odd(n - 1)); fn odd(n) => if(n = 0, false, even(n - 1)); even(10) // will return true
```

The depth of recursion is limited: a call of a user-defined function nested deeper than `Expression.MaxUserFunctionCallDepth` (`AsyncExpression.MaxUserFunctionCallDepth` for <xref:NCalc.AsyncExpression>; 1000 by default) throws an <xref:NCalc.Exceptions.NCalcEvaluationException>, so endless recursion never crashes the process. The recursive evaluator may run out of stack before reaching that limit, which throws an <xref:NCalc.Exceptions.NCalcEvaluationException> too; use <xref:NCalc.ExpressionOptions.UseNonRecursiveEvaluator> for deep recursion.

## Using Event Handlers
You can also use event handlers to handle functions.
```csharp
expression.EvaluateFunction += delegate(string name, FunctionArgs args)
{
    if (name == "SecretOperation")
        args.Result = (int)args.Parameters[0].Evaluate() + (int)args.Parameters[1].Evaluate();
};
```

The handlers are called for every function call that is not a function declared with `fn`, before the `Functions` dictionary and the built-in functions are tried; a handler marks the function as handled by setting `args.Result`. <xref:NCalc.AsyncExpression> uses the `EvaluateFunctionAsync` event; all its handlers are awaited.

## Case Sensitivity
See [case_sensitivity](case_sensitivity.md) for more info.

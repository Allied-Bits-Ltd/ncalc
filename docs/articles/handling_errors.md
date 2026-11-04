# Handling Errors

When the expression has a syntax error, the evaluation will throw a <xref:NCalc.Exceptions.NCalcParserException>; errors found while the expression is evaluated throw a <xref:NCalc.Exceptions.NCalcEvaluationException> or one of its descendants. Both derive from <xref:NCalc.Exceptions.NCalcException>.

```c#
try
{
    new Expression("(3 + 2").Evaluate();
}
catch (NCalcParserException ex)
{
    Console.WriteLine("Error parsing the expression: {0}", ex.Message);
}
catch (NCalcEvaluationException ex)
{
    Console.WriteLine("Error evaluating the expression: {0}", ex.Message);
}
```

Though, you can also detect syntax errors before the evaluation by using the <xref:NCalc.ExpressionBase`1.HasErrors(System.Threading.CancellationToken)> method.

```c#
var expression = new Expression("a + b * (");
if(expression.HasErrors())
{
    Console.WriteLine(expression.Error);
}
```

When the token passed to <xref:NCalc.ExpressionBase`1.HasErrors(System.Threading.CancellationToken)> is cancelled before or while the expression is parsed, the method throws an <xref:System.OperationCanceledException>, as `Evaluate` does; a cancellation is not reported as an error of the expression (<xref:NCalc.ExpressionBase`1.Error> is not set).

The error is cleared before each parse, so an expression that failed to parse can be parsed again after its options are changed.

## Exception types

| Exception | Thrown when |
|-----------|-------------|
| <xref:NCalc.Exceptions.NCalcException> | The base class of the NCalc exceptions. Thrown directly for an empty expression (unless `AllowNullOrEmptyExpressions` is set), and by `ToString()`, parameter extraction and `ToLambda()` when an expression is nested too deeply. |
| <xref:NCalc.Exceptions.NCalcParserException> | The text cannot be parsed: a syntax error, an invalid literal (e.g. an invalid date, time span or hexadecimal number), a function declared twice, an invalid assignment target (`5 := 7`), an expression nested too deeply to be parsed, or a number group separator equal to the decimal separator. The message tells the position of the first character that could not be parsed, e.g. "Invalid token in expression at position (1:4)". |
| <xref:NCalc.Exceptions.NCalcEvaluationException> | The expression cannot be evaluated: a division by zero of integers, decimals or big numbers ("Division by zero.", with a <xref:System.DivideByZeroException> as `InnerException`), a `decimal` result beyond the range of `decimal` (with an <xref:System.OverflowException> as `InnerException`), a wrong number of arguments of a function, comparison of incomparable types, a result beyond a [limit](options.md#limits), a factorial of a negative or fractional number, a bitwise operation on a fractional number, an unsupported operation on complex numbers or vectors, a date or time result out of range, an expression nested too deeply for the recursive evaluator, too deep recursion of `fn` functions, or `break`/`continue` outside a loop. |
| <xref:NCalc.Exceptions.NCalcConversionException> | (derives from `NCalcEvaluationException`) A value cannot be converted to the type an operation needs, e.g. `'abc' + 1`, or `'x' and true`. |
| <xref:NCalc.Exceptions.NCalcParameterNotDefinedException> | (derives from `NCalcEvaluationException`) A parameter is not found in `Parameters` or `DynamicParameters` and no `EvaluateParameter` handler provides it. |
| <xref:NCalc.Exceptions.NCalcFunctionNotFoundException> | (derives from `NCalcEvaluationException`) A function is not declared, not in `Functions`, not handled and not built in. |
| <xref:NCalc.Exceptions.NCalcParameterIndexException> | (derives from `NCalcEvaluationException`) An index is outside a list or string, a range is invalid, or a value assigned by index cannot be converted to the element type. |

Evaluation errors carry the location of the failing part of the expression where it is known.

Some errors are reported with .NET exceptions:

* <xref:System.OverflowException> when <xref:NCalc.ExpressionOptions.OverflowProtection> is set and an integer result does not fit in 64 bits or a floating-point result is infinite (see [Overflow Protection](overflow_protection.md)).
* <xref:System.OperationCanceledException> when the cancellation token passed to `Evaluate`, `EvaluateAsync` or `HasErrors` is cancelled.
* <xref:System.InvalidOperationException> when an arithmetic operator is applied to types that do not support it (e.g. `true + 1` without `AllowBooleanCalculation`, or a date and a time span without `SupportTimeOperations`).
* Exceptions thrown by custom functions, handlers and dynamic parameters are passed through.

With `EvaluateAsync()`, a parse error and an already cancelled token are reported through the returned task.

Floating-point operations do not throw for division by zero or out-of-domain arguments: they return infinity or NaN, as `System.Math` does (see [Multiplicative](operators.md#multiplicative)). With `OverflowProtection`, an operator whose floating-point result is infinite throws <xref:System.OverflowException>.

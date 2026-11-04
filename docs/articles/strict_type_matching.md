# Strict Type Matching

With `StrictTypeMatching`, values of different types are never equal: `=` and `==` return `false` and `!=` returns `true` when the types of the operands differ, and the values are not converted to a common type.

## Usage Example

```csharp
var expression = new Expression("'1' == 1", ExpressionOptions.StrictTypeMatching);
Debug.Assert(false.Equals(expression.Evaluate())); //Without ExpressionOptions.StrictTypeMatching, this would be true.
```

Numbers of different numeric types are different types too:
```csharp
var expression = new Expression("1 == 1.0", ExpressionOptions.StrictTypeMatching);
Debug.Assert(false.Equals(expression.Evaluate())); //Without ExpressionOptions.StrictTypeMatching, this would be true.
```

The option also applies to:

* `in` and `not in`, and the `in()` function: `1 in (1.0, 2)`, `'1' in (1, 2)` and `1 in '123'` are false;
* `like` and `not like`: both operands must be strings or chars, otherwise <xref:NCalc.Exceptions.NCalcEvaluationException> is thrown;
* the ordering operators (`<`, `<=`, `>`, `>=`), which return `false` for operands of different types.

Without the option, values of different types are converted for comparison as described in [How values are compared](operators.md#how-values-are-compared).

# String concatenation

If the <xref:NCalc.ExpressionOptions.StringConcat> option specified the result will be a string concatenation. 
Otherwise, it will try to perform an arithmetic addition if possible. When the arithmetic operation fails, if both values are
strings then the result will be a string concatenation.

## Example

With this option enabled, string concatenation will always happen, also for two numbers: `1 + 2` is `"12"`.

```csharp
var expression = new Expression($"5 + '1'", ExpressionOptions.StringConcat);
var result = expression.Evaluate();
Debug.Assert("51".Equals(result)); // 51
```

## Summary

| Expression   | `StringConcat` Enabled | `StringConcat` Disabled |
|--------------|------------------------|-------------------------|
| `'L' + 'eo'` | `'Leo'`                | `'Leo'`                 |
| `1 + '2'`    | `'12'`                 | `3`                     |
| `'1' + '2'`  | `'12'`                 | `3`                     |
| `'1' + 'x'`  | `'1x'`                 | `'1x'`                  |
| `1 + 'x'`    | `'1x'`                 | <xref:NCalc.Exceptions.NCalcConversionException> |
| `1 + 2`      | `'12'`                 | `3`                     |

Without the option, a string that holds a number is read like a number literal (see [Result types of arithmetic](operators.md#result-types-of-arithmetic)), and a string and a value that is not a string can only be added when the string holds a number. With <xref:NCalc.ExpressionOptions.NoStringTypeCoercion>, strings are not converted to numbers, so `+` with a string operand concatenates (`'1' + '2'` and `1 + '2'` are `"12"`).

A string produced by concatenation may have at most `BuiltInFunctionHelper.MaxMakeResultLength` characters (10,000,000 by default); a longer result throws <xref:NCalc.Exceptions.NCalcEvaluationException>.

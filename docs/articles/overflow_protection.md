# Overflow Protection

To handle binary arithmetic operation overflow you should use <xref:NCalc.ExpressionOptions.OverflowProtection>.

With this option, an integer operation whose result does not fit in the type of its operands is computed in a wider type when there is one:
`int.MaxValue + 1` gives `2147483648` (a `long`). A result that does not fit in 64 bits throws <xref:System.OverflowException>
(with <xref:NCalc.ExpressionOptions.UseBigNumbers>, it is a `BigInteger` instead):

```c#
try
{
    var expression = new Expression($"{long.MaxValue} + 1", ExpressionOptions.OverflowProtection);
    expression.Evaluate();
}
catch (OverflowException ex) { }
```

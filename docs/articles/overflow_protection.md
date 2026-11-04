# Overflow Protection

To handle binary arithmetic operation overflow you should use <xref:NCalc.ExpressionOptions.OverflowProtection>.

Without this option, integer arithmetic follows unchecked C# arithmetic: `+` and `-` of two `int` values and all operations on two `long` values wrap around (`2147483647 + 1` is -2147483648, `9223372036854775807 * 2` is -2). The product of two 32-bit integers is computed in 64 bits, so it does not wrap.

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

The option also applies to:

* the product of two `ulong` values, which wraps around without the option;
* the left shift: a shift that moves set bits out of 64 bits (`1 << 64`) throws instead of giving 0;
* the conversion of a result to the type of a typed lambda (`ToLambda<int>()`).

A `ulong` combined with a signed integer, and the products of two `uint`, `ushort` or `byte` values, are computed exactly with or without the option. Floating-point and `decimal` arithmetic is not affected: a `double` overflows to infinity, and a `decimal` result beyond the range of `decimal` throws <xref:System.OverflowException> with or without the option.

With <xref:NCalc.ExpressionOptions.UseBigNumbers>, integer arithmetic never overflows: a result beyond 64 bits is a `BigInteger`, limited in size by `MathHelper.MaxPowResultDigits` (see [Limits](options.md#limits)).

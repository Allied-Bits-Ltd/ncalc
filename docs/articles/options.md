# Expression Options

The behavior of the parser and of the evaluator is controlled by the <xref:NCalc.ExpressionOptions> flags, passed to the constructor of <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression> (or set in an [expression context](expression_context.md)), by the [advanced options](advanced_value_formats.md), and by a few static settings described in [Limits](#limits).

```c#
var expression = new Expression("0.1 + 0.2", ExpressionOptions.DecimalAsDefault | ExpressionOptions.OverflowProtection);
```

All flags are off by default (`ExpressionOptions.None`). An expression parsed with one set of options, culture and advanced options is cached separately from the same text parsed with another set, so options can be changed between evaluations; see [Caching](caching.md).

## Flags

### Numbers and arithmetic

| Flag | Effect |
|------|--------|
| `DecimalAsDefault` | Floating-point literals are `decimal` instead of `double` (literals that `decimal` cannot hold with their digits stay `double`); math functions use the `decimal` overloads of `System.Math` where they exist. See [Values](values.md#floating-point-numbers). |
| `UseBigNumbers` | Integer literals beyond `long` are `ulong` or `BigInteger`, floating-point literals beyond `double` are `BigDecimal`; integer arithmetic that overflows gives a `BigInteger`, and exact powers and factorials are computed. Functions of big numbers are computed with big-number precision. |
| `OverflowProtection` | Integer arithmetic that overflows its type is computed in a wider type, or throws <xref:System.OverflowException> beyond 64 bits. See [Overflow Protection](overflow_protection.md). |
| `ReduceDivResultToInteger` | `/` of two integers gives an integer when the quotient is a whole number (`6 / 3` is the `int` 2 instead of the `double` 2). |
| `ReduceArithmeticResultType` | The integer result of an arithmetic operation is reduced to the smallest type that holds it. |
| `AllowBooleanCalculation` | Booleans take part in arithmetic as 1 and 0 (`true + 1` is 2); without it, such an operation fails. |
| `RoundAwayFromZero` | `Round()` rounds midpoints away from zero instead of to the nearest even digit. |
| `UseSystemMathRound` | `Round()` applies `System.Math.Round` to the binary value of a double instead of rounding its decimal form. |
| `HexBinOctAreUnsigned` | Hexadecimal, binary and octal literals are treated as unsigned values (relevant for values above `long.MaxValue`). |
| `TreatNullAsZero` | Null is used as zero in arithmetic, unary operators, percents and math functions (not in comparisons). |
| `SupportTimeOperations` | Enables arithmetic with dates and time spans; see [Additive](operators.md#additive) and [Multiplicative](operators.md#multiplicative). |

### Comparisons and logic

| Flag | Effect |
|------|--------|
| `CaseInsensitiveStringComparer` | Strings are compared, searched by `in` and matched by `like` ignoring case. |
| `OrdinalStringComparer` | Strings are compared ordinally instead of with the culture of the expression. |
| `NoStringTypeCoercion` | Strings are never converted to numbers or other types in comparisons and arithmetic. |
| `StrictTypeMatching` | Values of different types are never equal; see [Strict Type Matching](strict_type_matching.md). |
| `CompareIncompatibleTypes` | Values that cannot be compared are unequal and unordered instead of throwing an exception. |
| `AllowNullParameter` | Enables the `null` keyword and comparisons with null. |
| `CompareNullValues` | Null takes part in ordering comparisons (null is less than any other value) instead of making them false. |
| `UseTernaryLogic` | Logical operators use Kleene's three-valued logic, where null and non-boolean values are Unknown; implies `AllowNullParameter`. |

### Syntax

| Flag | Effect |
|------|--------|
| `UseAssignments` | Enables assignments (`:=`, `+=` and the like) to parameters. |
| `UseCStyleAssignments` | `=` is the assignment and `==` the only equality operator. |
| `UseStatementSequences` | Enables sequences of statements separated by `;`, blocks in curly braces and functions declared with `fn`. |
| `UseIfStatement` | Enables the `if (...) { } else { }` statement; `if` is then no longer an alias of the `iff()` function. |
| `UseLoops` | Enables `while` loops with `break` and `continue`. |
| `SupportCStyleComments` | Enables `//` line comments and `/* */` block comments; `//` is then not the integer division operator. |
| `SupportPythonComments` | Enables `#` line comments. A `#` that starts a valid date or time literal closed on the same line is still a literal. |
| `SkipLogicalAndBitwiseOpChars` | Disables `&`, `\|`, `^`, `~`, `!`, `&&` and `\|\|`; use `and`, `or`, `not`, `bit_and`, `bit_or`, `bit_xor`, `bit_not`, and `^` for powers. |
| `UseUnicodeCharsForOperations` | Enables Unicode operator characters such as `×`, `÷`, `≤`, `√` and `∧`; see [Operators](operators.md). |
| `AllowCharValues` | A single-quoted string of one character is a `char`. |
| `DontParseDates` | Disables date and time literals (`#...#`), which makes parsing faster. |
| `DontParseGuids` | Disables GUID literals, which makes parsing faster. |
| `AllowNullOrEmptyExpressions` | An empty expression evaluates to an empty string instead of throwing <xref:NCalc.Exceptions.NCalcException>. |

### Names and lookup

| Flag | Effect |
|------|--------|
| `IgnoreCaseAtBuiltInFunctions` | Built-in function names are matched ignoring case. |
| `LowerCaseIdentifierLookup` | Parameter and function names are converted to lowercase before they are looked up. See [Case Sensitivity](case_sensitivity.md). |
| `IterateParameters` | Parameters that are lists are iterated, and the result is a list of results; see [Parameters](parameters.md#multi-valued-parameters). |

### Evaluation and caching

| Flag | Effect |
|------|--------|
| `UseNonRecursiveEvaluator` | Uses the non-recursive evaluator; see [Choosing an evaluator](#choosing-an-evaluator). |
| `NoCache` | The parsed expression is neither taken from nor stored in the cache. |
| `AvoidDynamicFunctions` | Does not use dynamic code for operators on non-numeric types (required for AOT; the NCalc.AOT assembly behaves this way always). |

## Choosing an evaluator

NCalc has four evaluators that give the same results: synchronous (<xref:NCalc.Expression>) and asynchronous (<xref:NCalc.AsyncExpression>), each in a recursive and a non-recursive variant.

* Use <xref:NCalc.AsyncExpression> when functions, parameters or handlers do asynchronous work (I/O); see [Async Support](async.md). It is not faster than the synchronous evaluator.
* The recursive evaluator (the default) is the fastest. It checks the remaining stack space before evaluating each node, and throws <xref:NCalc.Exceptions.NCalcEvaluationException> when an expression is nested too deeply for the stack of the thread (a long chain of operators such as a sum of a few hundred terms is nested as deeply as it is long), instead of crashing the process with a `StackOverflowException`.
* The non-recursive evaluator (`UseNonRecursiveEvaluator`) is slower but evaluates very long and deeply nested expressions. Arguments of functions, bodies of `fn` functions and expressions used as parameter values are evaluated as separate expressions; when they are nested so deeply that the stack is nearly used up, the evaluation continues on another thread (a new thread with a stack of `LogicalExpressionParser.DeepExpressionStackSize` bytes, or a thread-pool thread in the asynchronous evaluator) while the calling thread waits. Handlers and functions called from that part then run on the other thread: thread-local values and locks held by the caller are not available there. The culture, the UI culture and `AsyncLocal` values flow to the other thread. Set `LogicalExpressionParser.DeepExpressionStackSize` to 0 to keep the evaluation on the calling thread; such expressions then throw <xref:NCalc.Exceptions.NCalcEvaluationException>.

The parser handles deep nesting in a similar way: an expression nested too deeply for the stack of the calling thread is parsed again on a thread with a stack of `DeepExpressionStackSize` bytes, and one that is still too deep throws <xref:NCalc.Exceptions.NCalcParserException>.

## Limits

The following static settings protect an application from expressions that would run for a very long time or use a lot of memory. They are process-wide; set them once at startup.

| Setting | Default | Meaning |
|---------|---------|---------|
| `Expression.MaxLoopIterations`, `AsyncExpression.MaxLoopIterations` | 65536 | The total number of iterations of all `while` loops of one evaluation, including nested loops and loops in function arguments, `fn` bodies and expressions used as parameter values. When it is reached, the loops stop silently and the evaluation continues. |
| `Expression.MaxUserFunctionCallDepth`, `AsyncExpression.MaxUserFunctionCallDepth` | 1000 | The maximum depth of nested calls of functions declared with `fn`; a deeper call throws <xref:NCalc.Exceptions.NCalcEvaluationException>. |
| `Expression.CheckExecutionStack`, `AsyncExpression.CheckExecutionStack` | `true` | Whether the recursive evaluator checks the stack. Clear it only when all expressions are known to be small. |
| `MathHelper.MaxPowResultDigits` | 100,000 | The maximum number of digits of an exact power, a factorial with `UseBigNumbers`, a product or quotient of big numbers, the quotient of `%` and `//` of big numbers, a sum of big decimals with very different exponents, an integer converted from a huge `BigDecimal`, and the result of big-number `Exp`. A larger result throws <xref:NCalc.Exceptions.NCalcEvaluationException> before it is computed. |
| `BuiltInFunctionHelper.MaxMakeResultLength` | 10,000,000 | The maximum number of elements of a list created by `MakeList()` and of characters of a string created by `MakeStr()` or by concatenation with `+`. A larger result throws <xref:NCalc.Exceptions.NCalcEvaluationException>. |
| `LogicalExpressionParser.DeepExpressionStackSize` | 16 MB | The stack size of the threads used for deeply nested expressions (see above); 0 turns them off. |
| `BigDecimal.Precision` (ExtendedNumerics) | 100 | The number of significant digits of big-number divisions and transcendental functions. |
| `ComplexNumber.Tolerance` | 1E-10 | The tolerance with which complex numbers are compared and treated as real numbers. |

The static field `ExpressionBase<T>.UseNonRecursiveEvaluator` (e.g. `Expression.UseNonRecursiveEvaluator = true`) turns on the non-recursive evaluator for all expressions of that class, as if the flag were set.

## Culture

Each expression has a culture: the `CultureInfo` passed to its constructor or set in its `CultureInfo` property, or the current culture of the thread at the moment the expression is created. The culture of the expression, not the current culture of the thread at evaluation time, is used to:

* read date and time literals (always in the Gregorian calendar) and, with [advanced options](advanced_value_formats.md), number separators and currency symbols where they come from the culture;
* compare strings and match them with `like` and `in`;
* read numbers from strings in arithmetic and comparisons;
* format values in `ToString()` of the expression.

The results of arithmetic do not depend on the culture. For predictable results in server applications, pass an explicit culture (for example `CultureInfo.InvariantCulture`) to the constructor.

## Thread safety

* An <xref:NCalc.Expression> or <xref:NCalc.AsyncExpression> instance holds mutable state (its parameters, its handlers, the parsed tree and the error of the last parse); do not evaluate one instance on several threads at the same time. Create an instance per evaluation or per thread; parsing is cheap because parsed expressions and parsers are cached.
* The parse cache and the parsers are shared and thread-safe, and parsed expression trees are not changed by evaluation.
* The static settings in [Limits](#limits) are process-wide; changing them while other threads evaluate expressions affects those evaluations.
* Handlers and custom functions may be called on another thread when the non-recursive evaluator continues a deeply nested evaluation there (see [Choosing an evaluator](#choosing-an-evaluator)).

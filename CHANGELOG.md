# 9.1.0 (Allied Bits)

* Deeply nested or very long expressions no longer crash the process with an uncatchable `StackOverflowException`. When the stack runs low, the parser throws `NCalcParserException` and the recursive evaluators (sync and async) throw `NCalcEvaluationException`; use `ExpressionOptions.UseNonRecursiveEvaluator` to evaluate very long expressions. The evaluator check can be turned off with `Expression.CheckExecutionStack`/`AsyncExpression.CheckExecutionStack` when all expressions are known to be small.
* Fixed a regression in the negation operation: the result of a unary minus was always converted to `BigDecimal`, so expressions like `-2 * 3` failed and `-2 + 3` produced a `float`. Negation now keeps the operand's type; negating the minimum value of a signed integer type produces the next wider type.
* Arithmetic with a `BigInteger` or `BigDecimal` operand and an operand of another numeric type no longer requires `ExpressionOptions.UseBigNumbers`: the other operand is promoted and the result is computed. Previously, such operations could fail with "not implemented", return a wrong value (e.g. `5UL + BigDecimal(-36)` gave 0), or loop forever retrying after an overflow (e.g. `BigDecimal % ulong.MaxValue`).
* The `%` operator with a `BigDecimal` operand (and the element-wise `%` of vectors) now keeps the fractional part of the remainder, like `double` and `decimal` do: `7.5 % 2` is 1.5 instead of 1.
* `BigDecimal` results are no longer narrowed to `float`. A fractional result is converted to `double` only when no digits are lost and stays `BigDecimal` otherwise; an integer result beyond the 64-bit range stays `BigInteger`. `Floor()` of a big number is now exact without `UseBigNumbers` too, so `//` with a big operand no longer loses precision.
* The parsed-expression cache no longer returns an expression parsed under different settings. The cache key now includes the expression options, the culture and the advanced options in addition to the expression text; previously, the same text evaluated with, e.g., `DecimalAsDefault` or another date order received the result parsed with the settings of the first evaluation. Custom `ILogicalExpressionCache` implementations receive the composite key as the `expression` string.
* Functions declared with `fn` now work when the parsed expression is taken from the cache (previously, the second evaluation of the same text failed with "Function not found") and when the expression is evaluated with a pre-created parser. Parsing the same expression into a context more than once (e.g. `HasErrors()` followed by `Evaluate()`) no longer fails with "An item with the same key has already been added".
* JSON serialization (System.Text.Json) now supports statement sequences, `if` statements and function declarations, and expressions with assignments, which failed with `NotSupportedException` or an object-cycle `JsonException` before. Functions declared in a deserialized expression are registered when an `Expression` is created from it. The `CultureInfo` property of expression nodes is no longer written to JSON; it could not be serialized and was never read back.
* A string compared with a value of another type (comparison operators and `in()`) is now converted only when it is a single number, date or time, boolean or GUID literal in NCalc syntax, optionally with one sign (e.g. `"1.7"`, `"-5"`, `"0x10"`, `"#2024-01-01#"`); any other string is compared as a string. Previously, the string was evaluated as a full NCalc expression, so `x == 2017` with `x = "2020-01-02"` was true (2020 − 1 − 2), and a parameter value could run functions and loops. The conversion uses a dedicated literal-only parser that is created once per combination of settings, so it is also about 100 times faster (the full expression parser was built for every such comparison), and such strings no longer go into the parsed-expression cache.
* A chain of three or more `**` operators no longer loses operands: `2**2**2**2` is 65536 (right-associative) instead of 16. Previously, only the first three operands were used.
* Alphabetic keywords and operators (`true`, `false`, `null`, `return`, `not`, `and`, `or`, `xor`, `mod`, `div`, `in`, `like`, `BIT_*`, `if`, `else`, `while`, `fn`) are now recognized only as whole words. Previously, they matched the beginning of a longer identifier, so `returnValue * 2` was parsed as `return (Value * 2)`, `trueValue` and `true_flag` failed to parse, and `2 mod3` was accepted as `2 mod 3`.
* With `ParseComplexNumbers`, the imaginary unit `i` is now recognized only as a whole word or a number suffix (`i`, `2i`). Previously, it took the first letter of identifiers and of the `in` operator, so `2 in (1, 2)`, `index * 2` or `info` failed to parse.
* The non-recursive evaluator (`UseNonRecursiveEvaluator`) no longer loops forever when the condition of a ternary operator is null; the result is null, as with the recursive evaluator.
* `ExpressionOptions.SupportPythonComments` no longer switches on other options. Its value was `1 << 31` evaluated as a 32-bit integer, which became `0xFFFFFFFF80000000` in the 64-bit enum and also set `UseLoops`, `UseIfStatement`, `UseNonRecursiveEvaluator`, `CompareIncompatibleTypes`, `AvoidDynamicFunctions`, `ReduceArithmeticResultType`, `UseTernaryLogic` and `UseSystemMathRound`. The value is now `0x80000000`. **Breaking:** the value of a public constant changed; code compiled against an earlier version keeps the old value until it is recompiled.
* `AsyncExpression` evaluates `in` and `not in` correctly. The asynchronous evaluator passed the operands in the wrong order, so `2 in (1, 2, 3)` threw "Comparison of incomparable types" and `'a' in 'abc'` was false.
* Deeply nested expressions can be parsed. All binary operators are parsed by a single loop instead of a nested parser per precedence level, which about doubles the nesting a stack can hold, and an expression that is still nested too deeply is parsed again on a thread with a larger stack (16 MB by default, set by `LogicalExpressionParser.DeepExpressionStackSize`; 0 turns it off). About 3,000–5,000 levels of parentheses, function calls or ternaries are now accepted instead of about 60–160. Serialization (`ToString()`), parameter and function extraction and lambda compilation throw `NCalcException` when the stack runs low instead of crashing the process.
* Infinity and doubles beyond the range of `long` are no longer converted to `long.MaxValue` / `long.MinValue` when a result is reduced to an integer type. `1 / 0.0` is `+∞` (previously 9223372036854775807), `1e20 % 3e20` is `1E+20`, `1e30 // 1` is `1E+30`, and with `ReduceArithmeticResultType`, `1e20 + 1` stays `1E+20`. Whole-number results within the range of `long` are still reduced as before.
* Arithmetic on a `long` or `ulong` and a `float` no longer truncates the `float` to an integer: `0L + 1.5f` is 1.5 instead of 2, and `10L * 0.25f` is 2.5 instead of 0. The result no longer depends on the order of the operands. Such pairs are computed in `double`, as a `float` cannot hold a 64-bit integer precisely, so `ulong.MaxValue / 1.5f` no longer returns null and `0L + float.MaxValue` no longer overflows.
* Exact integer powers (with `UseBigNumbers`, `DecimalAsDefault` or a big-number base) are computed with `BigInteger.Pow` and are limited in size: a power whose result would have more than `MathHelper.MaxPowResultDigits` digits (100,000 by default) throws `NCalcEvaluationException` before it is computed. Previously, short expressions such as `2 ** 2000000` or `Pow(1.5, 100000)` ran for minutes; `2 ** 200000` now takes under a second instead of about 7 seconds. Checking whether a big-number result is a whole number no longer goes through its string form, which took seconds for numbers with tens of thousands of digits.
* `BigDecimal` values are converted to `double`, `float`, `decimal` and the integer types without going through ExtendedNumerics' explicit conversions, which format the number with the culture current at the first use of `BigDecimal` but parse it with the current culture: after a culture change (as in web applications with per-request cultures), 1.5 was read as 15. Converting a `BigDecimal` with a fractional part to `int` now truncates it instead of throwing `FormatException`.
* All evaluators (sync and async, recursive and non-recursive) multiply and divide the same way: complex numbers can be divided (`(1+2i)/(3-4i)` failed with `NCalcConversionException` in every evaluator), and vectors and timespans can be divided and timespans multiplied in the asynchronous and non-recursive evaluators too. Multiplication in those evaluators no longer narrows the result type unless `ReduceArithmeticResultType` is set, as in the synchronous recursive evaluator (`2147483647 * 2` is an `Int64`, not a `UInt32`).
* `LogicalExpression.ToString()` adds the parentheses that the precedence and associativity of operators require, so the text parses back to the same expression: `(1 + 2) * 3`, `1 - (2 - 3)`, `(-2) ** 2`, `(2 ** 3) ** 2` and nested ternary operators lost their parentheses and changed their meaning or could not be parsed.
* `ToString()` of a function call no longer cuts off the last character of an argument that does not end with a space: `Max(abc, 1)` became `Max(ab; 1)` and `Max(a, b)` became `Max(; b)`.
* `LIKE` and `NOT LIKE` use a wildcard matcher instead of a regular expression, so a pattern such as `'%a%a%a%a%a%b'` can no longer occupy the CPU for minutes (catastrophic backtracking); `%` and `_` now also match line breaks. The same matcher is used by compiled lambdas.
* `0!` is 1, a negative factorial throws `NCalcEvaluationException`, and a factorial that does not fit in `long` (such as `21!`) becomes a `double` instead of wrapping to a negative number; with `UseBigNumbers` it is exact and limited by `MathHelper.MaxPowResultDigits`.
* Reducing an arithmetic result to a smaller type no longer throws or wraps: `Pow(2, 63)` with `DecimalAsDefault` or `UseBigNumbers` and `1 << 63` give `9223372036854775808` (`ulong`), and `(byte)0 - (byte)1`, `(ushort)0 - (ushort)1` and `(sbyte)0 - 1u` give `-1`. The product of two unsigned values (`ushort`, `uint`) is no longer truncated.
* Division and modulo by zero of integers, decimals and big numbers throw `NCalcEvaluationException` ("Division by zero.", with the .NET `DivideByZeroException` as `InnerException`) instead of "Operator is not implemented"; a `float` divided by a `decimal` no longer throws `InvalidCastException`.
* Integer division (`//` and `div`) of `BigDecimal` values gives an integer: `div` no longer throws `ArgumentException`, and `//` no longer returns a `double`.
* Values that cannot be compared (a string and a number with `NoStringTypeCoercion`, or incompatible types with `CompareIncompatibleTypes`) are unequal and unordered: all comparisons except `!=` are false. Previously `'5' < 1` and `1 < '5'` were both true, and `CompareIncompatibleTypes` ordered values by their hash codes.
* Numbers of different kinds are compared by value: a `decimal` with a `double` beyond the `decimal` range or infinity, a `ulong` above `long.MaxValue` with a `long` or `int`, and a `BigInteger` or `BigDecimal` with a fractional number, a decimal, infinity or a string no longer throw. `MathHelper.Compare` (and `BoxedNumberComparer`) no longer inverts the result when only the right operand is a big number, and NaN equals NaN in it.
* Comparisons, `in` and `LIKE` use the culture of the expression instead of the culture of the current thread, so their results no longer depend on the server's regional settings; `x in (...)` compares the items as `x = ...` does.
* Complex numbers, vectors and percentages support `==` and `!=` (a complex number with no imaginary part equals the real number), and percentages support ordering and `Max`/`Min`; ordering complex numbers or vectors throws `NCalcEvaluationException`.
* ISO date-time literals keep fractions of a second of 4 to 7 digits exactly (`.1234` was read as `.1233`, and the 7 digits of the round-trip format were rejected).
* The time literal `#25:00:00#` is 25 hours instead of 25 days; minutes and seconds above 59, and hours above 23 together with days (`#1.25:00:00#`), are rejected.
* Relative date and time literals (`#now#`, `#today#`, `#2 days ago#`, `#1 month#`) are computed when the expression is evaluated, not when it is parsed, so a cached expression no longer returns the time of its first parsing.
* With `SupportPythonComments`, date and time literals such as `#2001-01-01#` can be used: a `#` that starts with a letter or a digit and is closed by another `#` on the same line is a literal, not a comment.
* 64-bit hexadecimal, octal and binary literals no longer wrap to negative values: `0xFFFFFFFFFFFFFFFF` is `ulong.MaxValue`, not `-1`.
* A run of 32 hexadecimal characters is a GUID only when it mixes digits and letters and ends there: a 32-digit number is a number and a 32-letter name is an identifier. The groups of a hyphenated GUID cannot be separated by spaces, so `a - b - c - d - e` is a subtraction.
* The non-recursive evaluators no longer hang on a list or statement sequence that contains the same node twice, and evaluate long expressions in linear time (100,000 terms took about 100 seconds).
* The evaluators give the same results: the non-recursive ones no longer evaluate the right operand when the left one is null, `not` with `UseTernaryLogic` returns its result, `TreatNullAsZero` applies to the result of a statement sequence, and list slices evaluate their expression elements; the asynchronous ones treat a null value as unequal to another value and apply `TreatNullAsZero` to unary operators, percents and statement sequences.
* `ComplexNumber.ToString()` and `Vector.ToString()` format their parts with the requested culture (the invariant culture by default) instead of the culture that was current at the first use of `BigDecimal`.
* An exponent may have a prefix operator, a percent or an imaginary unit: `2 ** -x`, `2 ** 10%` and `2 ** 3i` are parsed (`2 ** -3 ** 2` is `2 ** -(3 ** 2)`, as in Python).
* With `CurrencySymbolType.FromCulture` and the separators of the advanced options, the configured culture is used instead of the culture of the current thread.
* Parsers are built once per combination of settings and reused, so parsing an expression that is not cached takes microseconds instead of about a millisecond.
* The asynchronous evaluator returns null for `xor` with a null operand, as the synchronous one does.
* An `Expression` used as the value of a parameter gets the handlers of the parent expression only for its evaluation (they were added again on every evaluation), and an expression that refers to itself through its parameters throws `NCalcEvaluationException` instead of crashing the process.
* All handlers of the asynchronous events are awaited; only the last one was, so the results of the others were lost.
* Compiled lambdas (`ToLambda`) give the same results as `Evaluate` in more cases: `not` converts its operand to a boolean, the branches of `?:` and `if` may have different numeric types, `if` takes 2 or 3 arguments, the product of two 32-bit integers does not wrap around, `'a' in 'abc'` is a substring test, `Ln`, `Log2` and `Trunc` are available, `Round` works with `DecimalAsDefault`, options combined with other options are recognized, and `null` is the null value with `AllowNullParameter`.
* `Log2()` takes one argument (it required two); `Trunc`, `Log2`, `MakeStr`/`MakeString` and `MakeList` with a value are available in `AsyncExpression` too.
* `MakeStr()` accepts only a string, a character or a character code, and `MakeList()`/`MakeStr()` throw `NCalcEvaluationException` for sizes beyond `BuiltInFunctionHelper.MaxMakeResultLength` (10,000,000 by default) instead of `OverflowException` or an attempt to allocate gigabytes.
* `Round()` of a complex number or a vector keeps the number of decimal places and the rounding mode.
* `Log(x, b)` with a big-number argument no longer truncates a fractional base to an integer.
* `decimal` and `BigDecimal` arguments of `Abs`, `Ceiling`, `Floor`, `Round`, `Sign`, `Truncate` and `Pow` keep their precision instead of going through `double`, also when `DecimalAsDefault` and `UseBigNumbers` are not set.
* `Pow()` with a `decimal` base returns a `double` instead of throwing `OverflowException` when the result does not fit in `decimal` (or is NaN); a complex base can be raised to any power, and a real base to a complex power.
* Unary minus works for vectors, complex numbers and percents (`-[1;2]`, `-10%`), and `Abs()` for percents.
* `LogicalExpression.ToString()` writes the text in the dialect of the options the expression was parsed with (`==`, `bit_and`, `mod`) and with the function name of an `fn` declaration; numbers are written with the decimal separator of the parser and in a form that is read back exactly (`1e-30` was written as `0`), and dates in ISO form, whatever the culture of the current thread.
* JSON serialization of the expression tree keeps bracketed identifiers, quoted strings and GUID values.
* Setting `CultureInfo` or `AdvancedOptions` of an expression no longer changes the culture of an `AdvancedExpressionOptions` instance shared with other expressions; `AdvancedExpressionOptions.Clone()` creates a copy.
* `IterateParameters` no longer replaces the list parameters with their last elements (a second evaluation returned a single value), does not iterate the characters of a string, handles `return`, and observes the cancellation token.
* `Evaluate` no longer writes a `null` entry into the parameters (a read-only dictionary threw) or the static `UseNonRecursiveEvaluator` setting into `Options`.
* Percent operations without dynamic code (as in NCalc.AOT) return the same types as with it.
* Adding an expression to the cache no longer scans the whole cache, so caching many distinct expressions takes linear time instead of quadratic.
* `BoxedNumberComparer` compares values as the `=` operator does, and its hash codes are equal for equal values (`1`, `1.0` and `1m`), so it works in dictionaries, sets and `Distinct()`.
* `in` and `in()` compare the values as `=` does: `1 in ('1.0', 2)` is true, NaN is not found in a list of doubles, and a substring is found with the case and culture options (`'a' in 'XAY'` with `CaseInsensitiveStringComparer`).
* With `StrictTypeMatching`, `!=` is true for operands of different types (it was false, as all other comparisons).
* A Python-style comment may end the text (`1 + 2 #`).
* A factorial may be compared: `3! == 6` and `3! = 6` are parsed (only `!` followed directly by `=` is the `!=` operator).
* Parse errors report the position of the first character that could not be parsed instead of (1:1), and a missing argument in a function call is reported as such.
* NCalc exceptions are thrown instead of `InvalidOperationException` (`1 + + 2`), `ArgumentException` (a function declared twice, invalid hex, octal and binary literals, comparison of two lists), `FormatException` (`iff('x', 1, 2)`) and `ArgumentOutOfRangeException` (assignment to a negative index).
* `-9223372036854775808` is `long.MinValue` instead of a double.
* `[..]` selects the whole value, and the invalid ranges `[]`, `[^]`, `[^..1]` and `[1..^]` are parse errors (the `^` was ignored).
* The up arrow `↑` (U+2191) is the exponentiation operator with `UseUnicodeCharsForOperations`, as documented (`⊑` is still accepted); an ISO date may have the `Z` designator (`#2001-01-01Z#`); documentation examples are corrected.
* The conditional operator is right-associative and needs no parentheses: `a ? b : c ? d : e`.
* `Round()` with a negative number of digits rounds to tens, hundreds and so on for all types (`Round(1234, -2)` is 1200), and more digits than a type holds leave the value unchanged instead of throwing.
* The built-in math functions return null for a null argument (`Abs(null)` was 0, `Exp(null)` 1, `Log(null, 2)` -infinity, and `Pow(null, 2)` threw `ArgumentNullException`).
* `Max()` and `Min()` return the result in the type of the arithmetic on both arguments (`Max(3, 2.5)` is 3.0), return NaN for a NaN argument, take a numeric string as a number, and throw for a number and a non-numeric value.
* Shift counts are not masked to the width of the type: `1 << 64` is 0 (a big integer with `UseBigNumbers`, an overflow with `OverflowProtection`), and `MathHelper.RightShift` shifts right.
* Parsing no longer shares the nodes of `true`, `false` and `null` between expressions, and `ToString()` no longer writes options into the expression tree.
* `GetParameterNames()` and `GetFunctionNames()` return each name once, do not report the parameters of user-defined functions, and return an empty list for an empty expression with `AllowNullOrEmptyExpressions`.
* Parameter and function names are compared ordinally, independently of the culture.
* Cancellation during parsing throws `OperationCanceledException` instead of being reported as a syntax error.
* `ToString()` handles a factorial with a long step, GUID values, and quotes in strings that were not read from text.
* An ISO date-time without the `Z` designator has `DateTimeKind.Unspecified` instead of `DateTimeKind.Utc`.
* NCalc.MemoryCache sets the size of the entries (`LogicalExpressionMemoryCacheOptions.EntrySize`), so it works with an `IMemoryCache` that has a `SizeLimit`, and its keys cannot collide with other keys in a shared cache.
* The BigDecimal versions of `Asin`, `Acos`, `Atan`, `Ln`, `Log`, `Log10`, `Sin`, `Cos`, `Tan`, `Exp` and the other transcendental functions have about `BigDecimal.Precision` digits and take milliseconds instead of growing to tens of thousands of digits for seconds or minutes.
* Division with `UseBigNumbers` no longer depends on the current culture: after the thread culture changed (e.g. from sk-SK to en-US), `1.0 / 4.0` gave `25` instead of `0.25` because the quotient was converted with ExtendedNumerics' culture-dependent conversions. Decimal literals are also parsed to the same type in every culture.
* Integer division of or by a `ulong` above `long.MaxValue` now works: `a // 2` and `a \ 2` with `a = ulong.MaxValue` give `9223372036854775807` (previously `null` or `OverflowException`), and `1 // a` gives `0`.
* `ulong` arithmetic no longer throws `OverflowException` when the result fits: `ulong.MaxValue * 1` is `ulong.MaxValue`, `2^63 (ulong) + (-1L)` is `9223372036854775807`, and `Max(ulong.MaxValue, -1)` / `Min(...)` work. Without `OverflowProtection`, a `ulong` product wraps around as in C#.
* With `UseBigNumbers`, integer `+` and `-` are promoted like `*` and `**` instead of wrapping around: `int.MaxValue + 1` is `2147483648`, `ulong.MaxValue + 1` is `18446744073709551616` and `1UL - 2UL` is `-1`.
* `MathHelper.MaxPowResultDigits` can no longer be bypassed: `10 ** 200000`, `100 ** 150000` and `Pow(10, 200000)` throw `NCalcEvaluationException` (the trailing zeros of the base were not counted), and a negative shift count is rejected for `BigInteger` operands as it is for the other integer types, so `a >> -400000` no longer builds a huge number.
* Time arithmetic (`SupportTimeOperations`): dividing or multiplying a `TimeSpan` by any number, including `decimal` and `BigInteger`, rounds to the nearest tick (`FromTicks(10) / 3` is 3 ticks, `FromHours(1) / 7` works instead of failing), and results outside the range of `TimeSpan` or `DateTime` throw `NCalcEvaluationException` instead of wrapping around (`TimeSpan.MaxValue + 1` gave `TimeSpan.MinValue`) or throwing .NET exceptions.
* Percent arithmetic (`CalculatePercent`) gives the same result with and without `AvoidDynamicFunctions` (and in NCalc.AOT): `200 + 10%` with a `BigInteger` or `BigDecimal` operand, and with `float` operands under `DecimalAsDefault`, no longer fails or returns a different type.
* `Percent` now has value equality (`new Percent(1.5).Equals(new Percent(1.5))` is true, and 10% of an `int` equals 10% of a `double`) with a matching hash code, and `ToString()` uses the invariant culture (`"1.5%"`), like `ComplexNumber` and `Vector`; `ToString(format, provider)` formats with the given culture.
* Shifts of negative values are arithmetic, as in C#: `-1 << 1` is `-2` and `-8 >> 1` is `-4` (previously `OverflowException`). A `BigInteger` shift count is accepted (`1 << b` threw `InvalidCastException`).
* `BoxedNumberComparer.GetHashCode` is consistent with `Equals` for numbers of different types (`0.1f` and `0.1m`, `0.30000000000000004` and `0.3m`, `'a'` and `97`) and no longer throws for a `BigDecimal` beyond the range of `double` (`1E400`). `ComplexNumberToleranceComparer` and `VectorToleranceComparer` return hash codes that are consistent with their tolerance-based equality, so a `HashSet` no longer keeps two values that are equal within the tolerance.
* Comparing, `Max`/`Min`, integer division and remainder of real complex numbers no longer throw `InvalidCastException`.
* Integer edge cases: `3.5!` throws `NCalcEvaluationException` instead of returning `6`; `NaN \ 1` is `NaN` and `1e30 \ 1` is `1e30` (previously `0` and `long.MaxValue`); `long.MinValue % -1` is `0` and `long.MinValue \ -1` is `9223372036854775808` instead of throwing; negating `9223372036854775808` (`ulong`) gives `long.MinValue`.
* `Round`, `Ceiling`, `Floor` and `Truncate` accept percentages and return a percentage (`Round(12.346%, 2)` is 12.35%), as `Abs` already did; `Sign` of a percentage is the sign of its value, and `Sqrt` of a percentage is the square root of its fraction (`Sqrt(25%)` is 0.5). Previously these functions threw conversion errors.
* `Round` with an extreme number of digits no longer gives wrong results or BCL exceptions: `Round(15, -2147483648)` and `Round(0.12345, -2147483647)` are 0. An integer result that does not fit the type of the argument gets the next integer type instead of BigInteger (`Round(2147483647, -1)` is the long 2147483650).
* `Pow(x, y)` and `x ** y` with an infinite exponent return infinity or zero for decimal and big-number bases and with `DecimalAsDefault` or `UseBigNumbers`, instead of throwing `OverflowException`.
* `Max` and `Min` order values as the comparison operators do: a complex number with no imaginary part is a real number (`Max(c, 2)`), a boolean counts as 1 or 0 with `AllowBooleanCalculation` (`Max(true, 5)` is 5), NaN always wins and an infinity can be compared with a big number. Values that cannot be ordered (complex numbers with an imaginary part, vectors, incomparable types) throw `NCalcEvaluationException` instead of being ordered by type name or throwing a BCL exception. Two percentages are compared by their values.
* `Ceiling` and `Floor` return an integer argument unchanged, and `Abs` of an integer is exact and keeps the integer type, so that a long such as 9007199254740993 no longer loses digits through double; `Abs(long.MinValue)` is 9223372036854775808 (ulong), as for negation. Note that `Abs(-1)` and `Ceiling(7)` now return integers instead of doubles.
* `Sign` of NaN returns NaN instead of throwing `ArithmeticException`.
* Parsing time no longer grows exponentially with the nesting depth of lists and of parentheses whose content is parsed more than once. Previously, about 135 characters such as `((((1, 0), 1), 2), …)` nested 24 levels deep, or 40 parentheses around `#abc#`, kept the parser busy for minutes; such text is now parsed (or rejected) in milliseconds, with every set of options.
* With `ExpressionOptions.SupportPythonComments`, a comment that contains a second `#` no longer breaks parsing: `1 + 2 #see #3`, `1 + 2 #todo: fix#` and `1 #a#` followed by a line break are 3. A `#` starts a date or time literal only when the text up to the next `#` is a valid literal (e.g. `#2024-01-02#`, `#10:30#`); anything else, and everything when `ExpressionOptions.DontParseDates` is set, is a comment to the end of the line.
* Time literals of 24 hours or more (`#25:00:00#`, `#24:00#`, `#48:30#`, `#-25:00:00#`) are now also read when advanced options are set; previously they failed to parse with any `AdvancedExpressionOptions`. The time separator of the advanced options now follows `TimeSeparatorType` like the date separator does: the custom `TimeSeparator` is used only with `SeparatorType.Custom`.
* A culture that was customized but kept its standard name (e.g. `en-GB` with the short date pattern changed to `M/d/yyyy`) no longer shares the parser, or a cached parsed expression, with the standard culture of that name. Previously, `#1/2/2024#` gave the date of whichever culture was used first, also with `NoCache`. Parsers are now keyed by the formats of the culture (date and time patterns and separators, number and currency separators), so creating a new instance of the same culture for each expression still reuses one parser.
* Advanced options whose period indicator lists differ only in where the commas are (e.g. `["days,x"]` and `["days", "x"]`) no longer share a parser, so `#2 x#` is two days with the second setting.
* A string compared with a value of another type is no longer converted when it contains a comment: with comment support enabled, `'5 # five' = 5` and `'/*x*/ 5' = 5` were true.
* A 32-character number with an exponent is no longer read as a GUID: `100000000000000000000000000000e1` is 1E+30, and `+ 1` after it works. A run of 32 hexadecimal characters that starts with a letter and contains digits is still a GUID without hyphens; write an identifier of that shape in brackets (`[abcdef11111111111111111111111111]`).
* `ToString()` writes more constructs in a form that parses back to the same expression under the options of the expression: `not` instead of `!` when `SkipLogicalAndBitwiseOpChars` is set (`!True and False` could not be parsed); parentheses around an indexed expression that is not a name, a function call, a list or a string (`(s[1..4])[0]` was written as `s[1..4][0]`, a parse error); `(x)i` for the imaginary part of a non-number (`x i` was written as `xi`, which is read as a parameter named `xi`); a `ulong` above `long.MaxValue` in hexadecimal (`0xFFFFFFFFFFFFFFFF`, which was written in decimal digits and read back as a `double`); infinities as `1e400` and `-1e400` and NaN as `(1e400 - 1e400)` (`Infinity` was read as a parameter).
* `ToString()` writes the items of lists and function arguments with the separator the parser accepts: with a list separator that allows a semicolon, the items are separated by `;` (`Max(1; 2)` was written as `Max(1, 2)`, which is read as `Max(1,0, 2)` when the decimal separator is a comma); a comma that is also the decimal separator gets a space before it (`Max(1 , 2)`); with `ListItemSeparatorKind.Space`, an item that starts with a sign and an item followed by a parenthesis or a bracket are parenthesized (`Max(1 (-2))` was written as `Max(1 -2)`, a subtraction).
* A time span literal may have a fraction of a second (`#00:00:01.5#`, `#-1.02:03:04.25#`, up to 7 digits), and `ToString()` writes a time span with a fraction in this form (`#1 second 500 ms#` was written as `#00:00:01.5000000#`, which could not be parsed). A negative time span literal keeps its sub-millisecond ticks.
* JSON serialization keeps the options of the expression nodes (the expression options, and the flags, the list separator and the decimal separator of the advanced options), so a deserialized expression is written by `ToString()` in its dialect: `x == 5` with `UseCStyleAssignments` was written back as `x = 5` (an assignment), `6 bit_and 3` and `7 mod 3` as `6 & 3` and `7 % 3`, which cannot be parsed under those options.
* JSON serialization of values: a dictionary is written with a type tag, so a dictionary with the keys `Mantissa` and `Exponent` or `$type` and `$value` is read back as a dictionary (it was read as a `BigDecimal` or a tagged number); an array of numbers, strings, dates and other scalar types keeps its type (`int[]` was read back as `List<object>`); `DateTimeOffset` values are supported (they threw `NotSupportedException`). JSON written by earlier versions is still read.
* `Expression.AdvancedOptions` always stores a copy of the assigned options, which gets the culture of the expression. Previously the instance was copied only when its culture differed from that of the expression, so whether a later change of the assigned instance applied to the expression depended on the culture. Change the options through the property (`e.AdvancedOptions.Flags |= ...`). A parsed expression keeps a snapshot of the options it was parsed with: changing the options afterwards no longer changes `ToString()` of an expression that was already parsed (and may be cached).
* With `UseNonRecursiveEvaluator`, a very long argument of a function, of `if()` or of a function declared with `fn` (e.g. `Abs(1 + 1 + … + 1)` with 30,000 terms) no longer fails with "The expression is nested too deeply to be processed".
* With `UseNonRecursiveEvaluator`, `xor` no longer evaluates its right operand when the left one is null (or, with `UseTernaryLogic`, not a boolean), so `n xor f()` with `n = null` does not call `f()`, as in the recursive evaluator.
* The asynchronous non-recursive evaluator now calls `MatchStringAsync` handlers for `like` and `not like`; previously, it ignored them and used the built-in matching.
* `return null` now returns null: `return null; 5` and `fn f() { return null; 7 }; f()` gave 5 and 7 (0 with `TreatNullAsZero`).
* Cancellation is now honoured by the synchronous recursive evaluator (in operators, lists, statement sequences and `while` loops) and inside the arguments of built-in and custom functions in all evaluators: `Max(Foo() + g(1), 0)` stops when `Foo` cancels the token. The arguments passed to `EvaluateFunction` handlers and custom functions use the token of the evaluation, also when evaluated with `Evaluate()` or `FunctionArgs.EvaluateParameters()`; `EvaluateParameters(CancellationToken)` and `EvaluateParametersAsync(CancellationToken)` overloads were added.
* An `Expression` used as the value of a parameter is evaluated with its own parameters first, then those of the outer expression, and is no longer changed: previously, the outer expression's parameters were copied into the child permanently and overwrote its own values (a child with `x = 5` computing `x * 2` gave 2 inside a parent with `x = 1`). A child with read-only `Parameters` now works, a handler shared by both expressions (or by a shared context) is called once, and a synchronous `Expression` can be the value of a parameter of an `AsyncExpression`. An `AsyncExpression` used as a parameter of a synchronous `Expression` throws `NCalcEvaluationException` with an explanation instead of failing in an operator.
* With `IterateParameters`, the arguments of functions no longer iterate the list parameters again: `Abs(x[0])` and `if(true, x[0], 0)` with `x = [[1, 2], [3, 4]]` give `[1, 3]` like `x[0]`, instead of throwing.
* An expression that failed to parse no longer keeps failing after the options are changed: `Error` is cleared before each parse, so `1 /* c */ + 2` evaluates to 3 once `SupportCStyleComments` is set.
* The error message for an inverted range such as `lst[2..1]` shows the actual upper bound instead of the text `{upperBound}`.
* `ToLambda()` on a null expression with `AllowNullOrEmptyExpressions` returns a function that gives the default value of the result type instead of throwing `NullReferenceException`.
* `EvaluateAsync()` reports a parse error (e.g. of an empty expression) and an already cancelled token through the returned task instead of throwing from the call.
* `GetParameterNames()` removes the parameter names of `fn` functions the way the evaluator looks them up: in `fn f(x) => X + x; f(1)`, `X` is reported (it is another parameter) unless `LowerCaseIdentifierLookup` is set. `ParameterExtractionVisitor` got a constructor that takes the case sensitivity.
* `ToLambda()` computes operators and built-in functions with the same routines as `Evaluate()`, with the culture and options of the expression, so a compiled lambda gives the same values. Previously many results differed without an error, for example `1 << 40` gave 256, `~0` gave -1, `'a' < 'B'` compared ordinally, `1 = 1.0` with StrictTypeMatching gave True, `null ? 1 : 2` gave 2, `false ? 0.5 : 9007199254740993` lost precision, `Pow(2, 0.5)` with DecimalAsDefault gave 1, `Round(2.345, 2)` gave 2.35 and `Max(Sqrt(-1), 1)` gave 1.
* `ToLambda()` handles expressions that used to throw when compiled or run, such as `1 in (1.0, 2.0)`, `'1' in (1, 2)`, `'5' + 1`, `1 and 0`, `Round(1250, -2)`, `a // b`, `a \ b`, the sum of two byte parameters, `10% * 10%` with CalculatePercent, complex numbers, vectors and `null` with a context object.
* `ToLambda<object>()` returns values of the types that `Evaluate()` returns (for example `Min(3, 2)` and `if(true, 1, 0.0)` give the integer 1 or 2, not a double). A typed lambda such as `ToLambda<double>()` still converts the result to the requested type; with OverflowProtection a value that does not fit throws `OverflowException`.
* Built-in functions in lambdas are found ignoring case only with IgnoreCaseAtBuiltInFunctions, as in `Evaluate()` (`abs(-2)` is not found without it), and `if`, `ifs` and `in` evaluate only the arguments they need.
* `ToLambda()` throws `NotSupportedException` for what a lambda cannot do instead of compiling it into something that gives a different result: assignments and compound assignments without a context object (for example `x := 5; x * 2` gave 2), `while` loops, `return`, indexes and function declarations. With a context object (`ToLambda<TContext, TResult>()`), assignments still update its members, and later reads see the new value.

* An integer literal stays an integer when whitespace and a word starting with "e", or a separator, follow it: `if (true) 5 else 6` gives 5 as an int, not a double. With a comma as both the decimal and the list separator, `Max(2147483647 , 1)` reads both arguments as integers.
* JSON keeps the options of a list or statement sequence at the root of an expression and the trailing `;` of a sequence. `x == 1; 2` with C-style assignments no longer comes back from JSON as the assignment `x = 1; 2`, and `(7 mod 3, 1)` with CalculatePercent no longer comes back as `(7 % 3; 1)`. Such a list is written as `{"$type": "statements", "Options": ..., "EndsWithSeparator": true, "Items": [...]}`; lists without options are written as before, and JSON written by earlier versions is still read.
* Relative date and time literals stay relative in ToString() and JSON: `#today#`, `#2 days ago#` and `#yesterday# < #today#` are written as they were written, not as the date of the moment of writing, and a deserialized `#today#` gives the current date when evaluated.
* Date literals no longer depend on the calendar of the thread culture: with a Thai or Saudi thread culture, `#1/2/2024#` is 2 January 2024 (it was the Buddhist year 1481 or a parse error). Expressions without advanced options are no longer cached per thread culture.
* Only a variable or an indexed variable can be assigned to. `5 = x` with C-style assignments, `Abs(x) := 7`, `(x + 1) := 4` and `5 += 1` are parse errors instead of silently returning the right-hand side.
* ToString() keeps the types of unsigned and currency literals: with HexBinOctAreUnsigned, `0xFF - 0x100` is written in hexadecimal instead of as `255 - 256` (which gave -1), and with AcceptCurrencySymbol, `$0.1 + $0.2 = $0.3` is written with the currency symbol, so it stays a decimal comparison and is still true.
* ToString() keeps `.0` on literals inside a computed range or from-end boundary: `s[^((2147483647.0 + 1) / 268435456)]` is no longer written with an int that overflows.
* With the Space list separator, ToString() parenthesizes items that start with `!`, `~`, `not` and other prefix operators: `(3 (!false))` is no longer written as `(3 !False)`, which read as a factorial.
* Time span literals work under cultures whose time separator is "." (fi-FI): `#10.30#`, `#10.30.00#`, `#1.10.30.00#` and the `#10:30:00#` form that ToString() writes.
* `fn` declarations round-trip through ToString(): parameters are separated by `;` (or `,`) whatever the list separator, default values keep the decimal separator (`b = 1,5`) and `null`, and descriptions are written as `///` lines.
* With SupportCStyleComments, `///` that does not describe a function is an ordinary comment: `1 + 2 /// c` gives 3.
* The errors "Unknown operator sequence" and "Invalid DateTime format" tell the position. docs/articles/operators.md: logical AND is U+2227 `∧`, and the shift operators are listed at their real precedence (between additive and relational).
* Percent arithmetic (`AdvExpressionOptions.CalculatePercent`) gives the same results in NCalc and NCalc.AOT (and with `AvoidDynamicFunctions`): both now use one implementation that converts the operands to the result type (`double`, or `decimal` with `DecimalAsDefault` or a decimal operand) before applying the formula. Previously NCalc multiplied in the integer type of the operands, so `300000000 * 10%` gave -12949672.96 (30000000 now) and threw `OverflowException` with `OverflowProtection`. Also `1e308 * 10%` is 1E+307 instead of infinity, `200 / 0%` is infinity and `0 / 0%` is NaN in both packages, a percent of a time span works in both (`t + 10%` with t = 1 hour is 01:06:00), and `true + 10%` reports that the operator is not implemented instead of a message about `UseDecimalAsDefault`.
* The bitwise operators `&`, `|`, `^`, `~` and the assignments `&=`, `|=`, `^=` accept negative integers and use two's complement, as the shift operators and `BigInteger` operands do: `-1 & 3` is 3, `-8 | 1` is -7, `~(-1)` is 0. Previously they threw `OverflowException` (also in `ToLambda()`, where `-1 & 255` gave 255 before 9.1.0). Non-negative operands give the same `ulong` results as before (`~5` is 18446744073709551610). A fractional operand, NaN or infinity now throws `NCalcEvaluationException` instead of being truncated silently (`1.5 & 3` gave 1); whole floating-point values are accepted (`4.0 | 1` is 5).
* A `ulong` combined with a signed integer of any width is computed exactly: `5UL - 7` is -2 (it wrapped around to 18446744073709551614 unless the subtrahend was a `long`), also with `OverflowProtection`, and `5 - 9223372036854775808UL` is -9223372036854775803 instead of an `OverflowException` with `OverflowProtection`. A result beyond the 64-bit range is a `BigInteger` whatever the options (`ulong.MaxValue * -1` and `0 - ulong.MaxValue` are -18446744073709551615, as `-ulong.MaxValue` is); a product of two `ulong` values still wraps around as in C# and overflows with `OverflowProtection`.
* `%` and `//` of big decimals (and `%` of vectors) are exact for any size of the quotient. Previously the quotient was rounded to `BigDecimal.Precision` (100) digits, so `10^206 % 7` gave 6 instead of 2 and `1e300 % 1.5` gave 1E+300 (with `UseBigNumbers`).
* `MathHelper.MaxPowResultDigits` can no longer be bypassed: a factorial of a number beyond the range of `double` (`(2**1100)!`) and a power of a fraction with many decimal places (`0.1 ** 3000000`) throw `NCalcEvaluationException` at once instead of running for minutes. The limit also applies to products of big numbers (the digits of the factors are added up, so `fn sq(x) => x * x; sq(sq(2 ** 300000))` or squaring in a loop throws instead of building a value with millions of digits), to sums of big decimals with very different exponents, and to the quotient of `%` and `//` of big numbers. Strings concatenated with `+` may have at most `BuiltInFunctionHelper.MaxMakeResultLength` characters, as the results of `MakeStr()`.
* A `decimal` combined with a `double` or `float` beyond the range of `decimal` (or NaN, or infinity) is computed in `double` (in `BigDecimal` with `UseBigNumbers`) instead of throwing `OverflowException`: `1m + 1e300` is 1E+300, and `Max(1m, 1e300)` is 1E+300, consistent with `1m < 1e300`. Comparing a decimal with exactly `(double)decimal.MaxValue` (2^96) no longer throws "Comparison of incomparable types".
* With `DecimalAsDefault`, a big-number result is narrowed to `decimal` only when no digits are lost, as it is narrowed to `double` otherwise: `BigInteger 5 / BigDecimal 1E+40` is 5E-40 instead of 0, and `BigDecimal 1 / BigDecimal 3` keeps its 100 digits.
* Negating a `ulong` above 2^63 gives the negative `BigInteger` instead of throwing `OverflowException` (`-a` with a = `ulong.MaxValue` is -18446744073709551615).
* `BoxedNumberComparer` always gives equal hash codes to the values it considers equal, so `HashSet`, `Dictionary` and `Distinct()` with it no longer keep equal numbers apart (e.g. `8297108.5f` and `8297108.5`, `123456749` and `123456752f`). Numbers are hashed by their value as a `float`; because `=` converts to the more precise type and is therefore not transitive, the comparer reports the rare pairs that `=` considers equal only after rounding and whose `float` values differ as unequal.
* The error message for negating an unsupported type no longer contains a stray `$` ("Unsupported type Char in the negate operation").
* `Pow` and `**` with a NaN or infinite base no longer throw NotFiniteNumberException or OverflowException under DecimalAsDefault or UseBigNumbers; they give NaN, infinity or 0 as Math.Pow does (`Sqrt(-1) ** 2` is NaN, `Pow(Exp(1000), -1)` is 0).
* Zero raised to a negative power is infinity under DecimalAsDefault and UseBigNumbers and for decimal or big-number zeros, as without the options (`Pow(0, -1)` and `0 ** -1` gave DivideByZeroException).
* With DecimalAsDefault, Abs, Ceiling, Floor, Round, Sign and Truncate compute a NaN, an infinity or a double beyond the range of decimal as a double instead of throwing OverflowException (`Abs(Sqrt(-1))` is NaN, `Sign(Exp(1000))` is 1, `Ceiling(d)` with d = 1e30 is 1E+30).
* Big-number transcendental functions (UseBigNumbers with a BigInteger or BigDecimal argument) give correct values where ExtendedNumerics did not, and NaN or infinity where System.Math does: `Exp(-100)` is 3.72E-44 (it was a large negative number), `Atan(1)` is π/4 (it was 0.7866), `Sin(2)`, `Sin(100)`, `Cos(0.3)` and `Tan(1)` have the right sign, `Tan(0)` is 0, `Ln(-1)`, `Log(8, -2)`, `Sqrt(-4)` and `Asin(2)` are NaN, `Ln(0)` is -∞, `Asin(1)` is π/2 and `Acos(0)` is π/2 (they threw .NET exceptions). Sin, Cos and Tan of arguments beyond the range of int (`Sin(10 ** 30)`) are reduced exactly instead of throwing OverflowException; an argument with more than 10,000 integer digits throws NCalcEvaluationException. `Exp(10 ** 400)` throws NCalcEvaluationException once the result would exceed MathHelper.MaxPowResultDigits digits instead of running indefinitely, `Exp(-(10 ** 400))` is 0, and large arguments such as `Exp(100000)` take milliseconds instead of minutes. `Log(8, 10 ** 400)` is computed with big numbers instead of giving NaN. Other errors of ExtendedNumerics are reported as NCalcEvaluationException.
* `Round(x, n)` with more than 15 places rounds small doubles: `Round(1.23E-17, 16)` is 0 and `Round(1.5E-16, 16)` is 2E-16 (the value was returned unchanged).
* `Truncate` accepts vectors (`Truncate([1.5; -2.5])` is [1; -2]), and `Truncate` of a BigDecimal returns a BigDecimal, as Floor and Ceiling do (it returned a BigInteger). New method `Vector.Truncate`.
* `PercentOf` and `PercentDiff` divide integers by zero as the `/` operator does: `PercentOf(10, 0)` is ∞% instead of DivideByZeroException. A percent argument (`PercentOf(10%, 200)`), `Sign` of a complex number and `IEEERemainder` of a complex number throw NCalcEvaluationException instead of InvalidOperationException.
* Errors in the conditions of `ifs` and `if` report the location of the condition in AsyncExpression too, and errors thrown inside a built-in function (`1 + Round(1.5, 'x')`) have the same location with UseNonRecursiveEvaluator as without it.
* An indexed assignment to an argument of a function declared with `fn` changes that element of the argument instead of replacing the whole argument with the assigned value: `fn g(a) { a[1] := 5; a }; g(lst)` with lst = [1, 2, 3] returns [1, 5, 3] (it returned 5), and `fn g(s) { s[0] := 'X'; s }; g('abc')` returns "Xbc". As with other assignments to arguments, the change is local to the function: the list of the caller is not changed.
* `return` in an argument of a function returns from the whole expression, or from the `fn` function in whose body it is, as `return` elsewhere does: `Abs(return -3) + 100` returns -3 (it returned 103), and `Abs(return a)` inside a `while` loop leaves the loop.
* With `UseNonRecursiveEvaluator`, nested function calls (`Abs(Abs(...))`, `if(true, if(...), 0)`, calls of `fn` functions) are no longer limited to a few hundred levels (about 260 in the synchronous and 100 in the asynchronous evaluator). When the stack of the thread is nearly used up, the synchronous evaluator continues on a new thread with a stack of `LogicalExpressionParser.DeepExpressionStackSize` bytes (16 MB by default; 0 turns this off), and the asynchronous one on a thread-pool thread. If the nesting is still too deep, the error no longer advises `UseNonRecursiveEvaluator` when that option is in use.
* The recursive evaluators stop as soon as the cancellation token is cancelled during an operand, as the non-recursive ones do: in `cancel() + p1`, `cancel() ? p1 : 0` or `-cancel()`, where `cancel()` cancels the token, `p1` is no longer evaluated and `OperationCanceledException` is thrown. The asynchronous evaluator checks the token before each item of a list, as the synchronous one does.
* A negative index from the end (`s[^-1]`) throws `NCalcParameterIndexException` instead of `ArgumentOutOfRangeException`. A value assigned by index to an element of a typed list or array is converted to the element type (`d[0] := 1` with a `List<double>` stores 1.0; it threw `ArgumentException`), and a value that cannot be converted (`a[0] := 'x'` with an `int[]`) throws `NCalcParameterIndexException` instead of `InvalidCastException`.
* A handler that an `Expression` used as a parameter value shares with the outer expression is called once also when the outer expression has other handlers too (it was called twice).
* An `Expression` used as a parameter value can call the functions in the `Functions` of the outer expression (its own come first), as it can use the outer `Parameters` and `DynamicParameters`. Functions declared with `fn` in the outer expression remain invisible to it.
* `IterateParameters` no longer writes the current elements into `Parameters`, so read-only parameter dictionaries work (they threw `NotSupportedException`), and a dictionary in the parameters is one value rather than a list of key/value pairs (`x * 2` with x = [1, 2] and another parameter holding a dictionary of one entry threw "IEnumerable parameters must have the same number of items").
* `Expression.MaxLoopIterations` (and `AsyncExpression.MaxLoopIterations`) limits the iterations of all loops of one evaluation together, including nested loops and loops in function arguments, in `fn` bodies and in expressions used as parameter values; previously it applied to each loop separately, so two nested loops could run 65,536² iterations. As before, the loops stop silently when the limit is reached. The asynchronous non-recursive evaluator now uses `AsyncExpression.MaxLoopIterations` like the asynchronous recursive one (it used `Expression.MaxLoopIterations`).
* `ToLambda()` no longer terminates the process with a `StackOverflowException` for long or deeply nested expressions.
  * A chain of operators such as `1 + 2 + 3 + ...` or `a and b and c ...` is compiled without nesting, so a sum of thousands of terms compiles.
  * An expression nested more than 64 levels deep (parentheses, function calls, ternaries) is compiled with the System.Linq compiler instead of FastExpressionCompiler. FastExpressionCompiler does not check the stack, and it crashed at about 150–250 levels on a 1 MB stack.
  * Above 128 levels, `ToLambda()` throws `NCalcException`. Use `Evaluate()` for such expressions.
* `ToLambda<TContext, TResult>`: when an argument of a context method is computed (an operator, a built-in function or another method), the overload is now chosen when the lambda runs, by the type of the value.
  * Previously, the first declared overload was chosen, so with `F(int)` declared before `F(double)`, `F(5 / 2)` called `F(int)` with 2. Now it calls `F(double)` with 2.5, whatever the order of the declarations.
  * An argument is converted to another numeric type only without loss. `Twice(4 / 2)` passes 2 to `int Twice(int)`, but `Twice(2.9 * 1)`, `Twice(3000000000 * 1)` and `Twice(1 / 0)` throw `NCalcEvaluationException`; previously they passed 2, 1705032704 and an arbitrary value.
  * A literal argument that needs such a conversion (`Twice(2.0)`), or a value passed to an `object` parameter (`O(1)`), now works; it threw `MissingMethodException` before.
* `ToLambda<TContext, TResult>`: a value assigned to a typed member of the context is converted without loss, and the assignment gives the stored value.
  * `x := 4 / 2` stores 2 in an `int` member and gives 2.
  * `x := 2.7`, `x := 3000000000` and `x += 0.5` throw `NCalcEvaluationException`. Previously they stored 2, -1294967296 and 1, and the assignment gave the unconverted value.
  * `null` is stored only in a nullable or reference member; it was stored as 0 before.
* `ToLambda()` of a statement sequence whose statement other than the last is a list or `in()` (e.g. `(1, 2); 3` or `in(1, 2, 3); 4`) no longer gives a delegate that throws `InvalidProgramException`.
* Decimal literals and decimal parameter values keep their scale in lambdas: with `DecimalAsDefault`, `2.0` gives `2.0` as `Evaluate()` does, not `2`.
* A lambda uses the parameters and options that the expression has when `ToLambda()` is called.
  * Changing them later no longer partly affects an existing lambda.
  * For example, with `x = 3` and the parameter `e1` = `x * 10`, `x + e1` gives 33 even after `x` is set to 5; it gave 53 before.
* JSON serialization of deep expressions no longer crashes the process: a tree deeper than `JsonSerializerOptions.MaxDepth` (64 by default) fails with a `JsonException`, as before 9.1, and so does a tree too deep for the stack of the thread with a larger `MaxDepth`. Writing and reading take linear time (each level copied its whole subtree again, and reading skipped over it).
* A JSON document whose nodes lack required members (e.g. a binary node without operands), have unknown operator or value types, contain a malformed tagged value (`{"$type":"Int32","$value":"abc"}`), or a relative date literal or string text that does not match its value is rejected with a `JsonException` when it is read, instead of failing later with a .NET exception or being written by `ToString()` as a different expression. A relative date literal out of the range of dates (`#2147483647 months ago#`) throws `NCalcEvaluationException` instead of `ArgumentOutOfRangeException`.
* With `AcceptCurrencySymbol`, JSON keeps the currency symbol and currency decimal separator, so `ToString()` of a deserialized expression writes `$0.1` instead of the currency symbol of the current culture.
* A `///` description of a function is kept when other comments (`//` lines, `/* */` blocks) stand between it and `fn`; long blocks of `///` lines are parsed in linear time.
* With advanced options, date literals are read in the Gregorian calendar (not the Buddhist, Persian or Um Al-Qura calendar of the thread or options culture), and with `DateSeparatorType.BuiltIn` in the date order of the options' culture (the expression culture) instead of the thread culture, as without advanced options. **Behaviour change** for applications whose thread culture and expression culture have different date orders and that use BuiltIn date separators.
* Where the date and time separators are both `.` (fi-FI, da-DK) and the separators come from the culture, `#10.30.00#` is a time span and `#1.10.30#` the date 2030-10-01 (previously today's date at that time of day); an invalid date such as `#29.02.2023#` is reported as an invalid date, not an invalid time span.
* The error of an invalid assignment target (`5 := 7`) tells the position, and `Expression.Error` (and the `NCalcParserException` thrown by `Expression`) carries the parser's message (e.g. "Unknown operator sequence at position (1:6)") instead of "Error parsing the expression.".
* The digit limit of exact powers and of products of big numbers (`MathHelper.MaxPowResultDigits`) no longer counts the decimal places of a fractional number twice: `1.5 ** 50000` with `UseBigNumbers`, `Pow(1.000001, 10000)` with `DecimalAsDefault` and `Pow(1 + rate, 1825)` with a decimal `rate` work again (they were rejected as having about twice their real number of digits). Powers and products whose decimal places exceed the limit (`0.1 ** 3000000`) are still rejected.
* Without `UseBigNumbers`, `Pow()` and `**` with a decimal base (or a fractional base with `DecimalAsDefault`) return a decimal when the result fits in it and a double otherwise, as documented: `Pow(2.5m, 80)` is a double instead of a `BigDecimal`, `Pow(2.5m, 100000)` is infinity and `Pow(1.0000001m, 1000000)` is 1.10517091261432 instead of throwing the digit limit. `Pow(1.5, 20000)` with `DecimalAsDefault` is therefore infinity (use `UseBigNumbers` for the exact value).
* A division that is computed in double because a decimal is combined with a double beyond the range of decimal keeps the double quotient (`2.5m / 1e300` is 2.5E-300 instead of 0), and a quotient below the resolution of decimal (1E-28) or float is no longer narrowed to 0 (`1e-20 / 1e20` with `DecimalAsDefault` is 1E-40).
* With `UseBigNumbers` or a `BigInteger`/`BigDecimal` operand, `*`, `/`, `//`, `%` and the percent operators with NaN or an infinity are computed in double, as `+` and `-` are, instead of throwing `NotFiniteNumberException` or `OverflowException` (`Sqrt(-1) * 2` is NaN). `NaN + 10%` with `UseBigNumbers` is NaN again instead of an `InvalidOperationException`.
* Division of big numbers is limited by `MathHelper.MaxPowResultDigits` like multiplication: a quotient with more digits (`a / (1 / a)` for a = 10 ** 60000, `(10 ** 99999) / 0.1`) throws `NCalcEvaluationException` instead of doubling the size of the value at each step.
* A `ulong` combined with a signed integer is computed exactly also when the result is beyond the 64-bit range: it is a `BigInteger` whatever the options (`ulong.MaxValue * -1` and `0 - ulong.MaxValue` are -18446744073709551615, as `-ulong.MaxValue` is; `ulong.MaxValue * 2` is 36893488147419103230), instead of wrapping around or throwing `OverflowException` with `OverflowProtection`. A product of two `ulong` values still wraps around as in C# (and overflows with `OverflowProtection`).
* `TimeSpan / TimeSpan` (a double), `TimeSpan + TimeSpan`, `TimeSpan - TimeSpan` and `DateTime - DateTime` give the same results in NCalc.AOT, with `AvoidDynamicFunctions` and on .NET Framework as in NCalc; NCalc.AOT threw `InvalidOperationException`.
* Bitwise operators with a `BigInteger` operand follow the rules of the other integers: a whole floating-point operand is accepted (`BigInteger 12 & 4.0` is 4) and a fraction, NaN or infinity throws `NCalcEvaluationException` instead of an `NCalcConversionException`; a fractional shift count of a `BigInteger` (`bi << 1.5`) throws instead of being truncated.
* A percent of a large decimal no longer overflows in the intermediate product: `70000000000000000000000000000m * 10%` is 7000000000000000000000000000 and `70000000000000000000000000000m + 10%` is 77000000000000000000000000000; a result beyond the range of decimal throws `NCalcEvaluationException` instead of `OverflowException`.
* With `UseBigNumbers`, a percent whose double result would lose digits keeps the exact value: `ulong.MaxValue + 10%` is 20291418481080506776.5 (`BigDecimal`), as `ulong.MaxValue * 10%` is.
* The product of two `uint` (or `ushort`, `byte`) values is computed in the next unsigned type also with `OverflowProtection`: `uint.MaxValue * uint.MaxValue` is 18446744065119617025 instead of an `OverflowException`.
* `BigDecimal` values with a huge exponent (e.g. a parameter 1E+1000000) no longer make comparisons, `=`, `in`, sums and `BoxedNumberComparer` run for seconds; converting such a value to an integer (`Floor`, `Truncate`, `Ceiling`, `&`, `!`) throws `NCalcEvaluationException` when the integer would have more than `MathHelper.MaxPowResultDigits` digits, instead of running for seconds (or throwing `OverflowException`).
* Big-number Asin and Acos are computed with the reduced arctangent (asin x = atan(x / sqrt(1 − x²))); Asin(0.7) and Acos(±0.7) were wrong in the 7th digit, and each call is several times faster.
* With DecimalAsDefault, Abs, Ceiling, Floor, Round, Sign and Truncate compute a double or float in double when decimal cannot keep its digits: Sign(Exp(-100)) is 1 instead of 0, Round(1E-30, 30) is 1E-30 instead of 0.
* With UseBigNumbers, Log of a big number with a fractional or sub-1 base (Log(10 ** 400, 2.5)) returns the double logarithm instead of ±Infinity.
* The factorial of a complex number throws NCalcEvaluationException instead of InvalidOperationException; the factorial of a BigInteger or decimal beyond the range of long is Infinity without UseBigNumbers (as for a double), instead of an OverflowException.
* Round clamps a number of places beyond the range of int (a long, BigInteger, decimal or double) instead of throwing OverflowException; NaN places throw NCalcEvaluationException.
* After a big-number Sin/Cos/Tan of a huge argument, later big-number trigonometric calls are no longer about 6 times slower.
* Big-number Sin, Tan, Asin and Atan of arguments below 10^-(BigDecimal.Precision / 2) return the argument (Cos 1, Csc and Cot 1/x) instead of 0 or an "undefined" error.
* BigInteger and BigDecimal values can be conditions (if, iff, ifs, ?:, not, and, or, xor, while): non-zero is true.
* ToLambda reports a wrong number of arguments of a built-in function ("Pow() takes exactly 2 arguments") as the evaluators do, instead of an error about an argument.
* Round of a double to 15 or fewer places reads all digits of the double (its shortest round-trip text), as for more places: Round(123456.78901234567, 11) no longer changes the value. UseSystemMathRound is unchanged.
* Sign, Sqrt and Pow (with a scalar exponent) of a vector, and Max and Min of two vectors, are computed element-wise.
* MathHelper.ConvertToInt and ConvertToLong reject NaN and out-of-range doubles and floats on every target framework (OverflowException, or NCalcConversionException in the overloads with an operation name, which now also wrap OverflowException); the result no longer depends on the framework's cast.
* `MaxLoopIterations` applies to one evaluation and to its parts (function arguments, bodies of user functions, expressions used as parameter values) only. Previously, work started during an evaluation (a task, a timer, a thread pool item) kept the budget of that evaluation, so an unrelated evaluation there later ran its loops 0 times, and unrelated expressions evaluated by a handler shared the budget of the running evaluation and could lose iterations when they ran in parallel. Every other evaluation, also one started by a handler, now has a budget of its own; parts of an evaluation that run in parallel share its budget safely.
* With `UseNonRecursiveEvaluator`, nested calls of `fn` functions, of `EvaluateFunction` handlers and `Functions` that evaluate their arguments, and chains of expressions used as parameter values are no longer limited to a few hundred levels (sync and async); the evaluation continues on a thread with a large stack as for built-in functions. The error for an expression that is still too deep no longer advises `UseNonRecursiveEvaluator` when it is in use.
* The asynchronous non-recursive evaluator propagates an error or `return` out of deeply nested calls in linear time (1,000 levels: about 0.2 s instead of 5 s). An exception that comes out of more than 32 nested evaluations keeps its type, but its stack trace shows only the outer part.
* With `UseNonRecursiveEvaluator`, handlers called from deeply nested parts of an expression may run on another thread while the calling thread waits (documented). `LogicalExpressionParser.DeepExpressionStackSize = 0` keeps the evaluation on the calling thread, now in the asynchronous evaluator too; such expressions then throw `NCalcEvaluationException`.
* ToLambda chooses an overload of a context method by one rule whatever the form of the arguments: a literal, a typed member and a computed value of the same type call the same overload, independently of the order of the declarations (the lowest conversion cost, then the more derived class, then the more specific parameters). The overload is still bound when the lambda is compiled when the argument types decide it.
* ToLambda considers the overloads of the base classes of the context object too, so a base-class overload is called when the derived class's same-named method cannot take the value; with equal conversion cost the derived class's method wins.
* ToLambda converts an integer to an enum member or parameter, and an enum to an integer member or parameter, as a cast does, when the value fits the underlying type.
* ToLambda converts a double, float or BigDecimal to a decimal parameter or member only when the decimal has the same value (a double converts to the decimal of its shortest round-trip form); values such as 1e-30 or 123456789.123456789 are refused instead of being rounded.
* Typed lambdas (ToLambda<int>, <long>, <long?> and other integer types, and enums) of BigDecimal values truncate the value as a cast of a double does, independently of the current culture, instead of throwing FormatException, InvalidCastException or InvalidOperationException; a value out of the range of the type throws OverflowException.

* Integer division is exact for all operand types and options: `//` floors the exact quotient and `\` divides the truncated operands exactly, instead of rounding the quotient first. With `UseBigNumbers`, `9223372036854775807 // 3` is 3074457345618258602 (was 3074457345618258432); with `DecimalAsDefault`, `6.9999999999999999999999999999 // 7` is 0 and `-0.0000000000000000000000000001 // 3` is -1 (were 1 and 0); `79228162514264337593543950334 \ 7` is 11318308930609191084791992904 (was …905).
* Dividing two integers with `/` when an operand is beyond 2^53 no longer loses digits because the dividend was rounded to double: the result has the same type as before, but its value comes from the exact integers (a double result is the nearest double, and a whole quotient is an integer with `ReduceDivResultToInteger`). With `ReduceDivResultToInteger`, `9223372036854775807 / 1` is 9223372036854775807 (was 9223372036854775808) and `ulong.MaxValue / 255` is 72340172838076673; with `DecimalAsDefault` and `UseBigNumbers`, `9223372036854775807 / 7` is 1317624576693539401m. With `UseBigNumbers`, an integer divided by a double is computed from the exact integer, and a numeric string beyond the double range in `/` keeps its exact value. An integer divided by a decimal or big-number percent keeps its digits too, and so does, with `UseBigNumbers`, an integer beyond 2^53 divided by any percent (`long.MaxValue / 10%` is 92233720368547758070). Integers that a double holds give the same results as before.
* A decimal combined with a double or float: a non-zero value whose significant digits do not fit in the 28 decimal places of decimal (such as 1e-29, 1.234e-27 or 1e-300; 1e-28 still fits) is computed and compared in double (`BigDecimal` with `UseBigNumbers`), so `1m / 1e-300` is 1E+300 instead of `DivideByZeroException` and `1e-29 = 0m` is false; a whole double or float keeps all its digits, so `16777216f = 16777216m` and `9007199254740992.0 = 9007199254740992m` are true and `0m + 16777216f` is 16777216. Fractional values still use 15 (float: 7) significant digits, so `0.1 = 0.1m` stays true.
* Numeric strings in arithmetic are read like number literals, as comparisons, `in`, `Max` and `Min` read them: `'1e3' * 2` works with `DecimalAsDefault`, `'12345678901234567891' - 12345678901234567890` is 1 with `UseBigNumbers`, a string beyond the decimal range no longer throws `OverflowException`, and a group separator is no longer dropped (`'1,5' + 0` throws `NCalcConversionException` instead of giving 15). Strings in the format of the culture (`'2,0'` with a comma culture) still work. A numeric string is still a double (decimal with `DecimalAsDefault`), so result types do not change; with `UseBigNumbers` it keeps its exact big type only when a double cannot hold its value, and in `/` it is read before the division, so `a / '0.1'` gives the same result and type as `a / b` with the double 0.1 (`7L / '0.1'` is the long 70, as `7L / 0.1` was already).
* `ReduceDivResultToInteger` applies with `UseBigNumbers` also without `ReduceArithmeticResultType`: `6 / 3` is the integer 2 (only an exact integer quotient of operands that are not big numbers is reduced).
* With `OverflowProtection`, the difference of two `ulong` values above `long.MaxValue` is computed exactly when it fits in `long` (`18446744073709551614 - 18446744073709551615` is -1) instead of throwing a conversion `OverflowException`; a difference beyond the range of `long` throws the arithmetic `OverflowException`.
* The OverflowProtection documentation example is corrected: `int.MaxValue + 1` gives 2147483648 (a `long`); `long.MaxValue + 1` throws `OverflowException`.
* The remainder of two decimals no longer throws `OverflowException` when the quotient is beyond the decimal range: `decimal.MaxValue % 1.0000000000000000000000000001m` is 0.0771837485735662406456049673.
* `StrictTypeMatching` applies to `in` and `not in` as to `=`: `1 in (1.0, 2)`, `'1' in (1, 2)` and `1 in '123'` are false.
* The root operators √, ∛ and ∜ use the options of the expression, so with UseBigNumbers they give the same big-number results as Sqrt, Cbrt and Fthrt instead of a double (or ∞ for big numbers beyond the range of double).
* Big-number Sqrt (and the public Cbrt, Fthrt, Sinh, Tanh, Coth, Sech, Csch and Arccot) keep about BigDecimal.Precision significant digits for small results: Sqrt(4E-300) is 2E-150 instead of 0, Coth(1E-120) and Csch(1E-120) are about 1E+120 instead of an error; Coth(0) and Csch(0) are +∞.
* A BigDecimal beyond the range of double in a function computed in double (Atan2, IEEERemainder, Pow with a fractional exponent, and the transcendental functions without UseBigNumbers) is an infinity of its sign, as a BigInteger of the same size, instead of throwing OverflowException.
* The public double Cot, Csc, Sec, Coth and Csch return ±∞ for a zero divisor and Arcsec and Arccsc return NaN for |x| < 1, as System.Math does, instead of throwing ArgumentOutOfRangeException; conversion errors of Arcsec and Arccsc name these functions; the big-number Arccsc(±1) is ±π/2.
* The public Max and Min overloads with a BigInteger or BigDecimal second argument accept fractions, NaN and infinities as the first argument and order them as the built-in Max and Min. A null first argument throws `ArgumentNullException`.
* A power whose result has exactly MathHelper.MaxPowResultDigits digits (2 ** 332190) is accepted, and one with more digits is rejected before it is computed.
* Big-number Sinh, Cosh, Tanh, Coth, Sech and Csch of large arguments (and the complex hyperbolic and trigonometric functions that use them) have about BigDecimal.Precision correct digits; Sinh(300) was wrong from the 8th digit.
* Big-number Sinh, Cosh, Tanh, Coth, Sech and Csch of large arguments (and the complex hyperbolic and trigonometric functions that use them) have about BigDecimal.Precision correct digits; Sinh(300) was wrong from the 8th digit. They are computed from NCalc's own exponential, as Exp is, so a result beyond MathHelper.MaxPowResultDigits digits throws NCalcEvaluationException as Exp does.
* A percent of a time span accepts any number: `t * 12.5%`, `t + 0.5%`, `t / 12.5%` and percents given as `decimal`, `float`, `BigInteger` or `BigDecimal` work (they threw a conversion or "not implemented" exception; only integer percents worked). Integer, decimal and big-number percents are applied exactly, rounded to the nearest tick.
* With `UseBigNumbers`, a percent operation involving a `double` or `float` is computed exactly from the shortest round-trip value of the double (and the exact value of an integer operand): `d * 10%` is no longer rounded to 15 significant digits, `l + 0.5%` with l = 9007199254740993 is 9052235251014697.965, and the result is a `double` when a double holds it exactly and a `BigDecimal` otherwise (`1e308 + 100%` is 2E+308). A `double` with a `BigInteger` or `BigDecimal` percent (or the reverse) is no longer rounded to 15 digits without the option either.
* `DateTimeOffset ± TimeSpan`, `DateTimeOffset - DateTimeOffset` and `TimeOnly - TimeOnly` work in NCalc.AOT and with `AvoidDynamicFunctions`, and a result outside the range of the type (`DateTimeOffset.MaxValue + t`) throws `NCalcEvaluationException` "outside the range of DateTimeOffset" instead of "Operator '+' is not implemented".
* An imaginary part smaller than `ComplexNumber.Tolerance` is no longer dropped before arithmetic: `1e-11i * 1e11` is `1i` (was 0) and `(1e-11i) / (1e-11i)` is 1 (was NaN). In arithmetic, a complex number is used as a real number when its imaginary part is zero or at most `ComplexNumber.Tolerance` times its real part (so `Exp(pi * i) + 1` is still 0); a purely imaginary number stays complex. The absolute tolerance still applies to `IsReal`, `IsZero` and equality.
* `Vector.Magnitude`, `Normalize()`, `AngleBetween()`, `Distance()`, `MaxByMagnitude()` and `MinByMagnitude()` work for vectors created without `UseBigNumbers` (they threw `InvalidCastException`).
* The bitwise and shift operators of vectors apply the rules of the scalar operators to each component: a fractional component, a negative or fractional shift count, or a left shift whose result exceeds `MathHelper.MaxPowResultDigits` digits throws `NCalcEvaluationException` (`[1.5;2] & [3;3]` gave `[1, 2]`, `[8;16] << -1` gave `[4, 8]`).
* `Pow` and `**` of a complex number with a whole exponent (up to 64 in magnitude) are exact: `(1+2i) ** 2 == -3+4i` is true.
* Complex, vector and time operations throw `NCalcEvaluationException` instead of .NET exceptions: division of a complex number or a vector by zero, `%` of a vector by zero, `Ln`/`Log` of complex zero and invalid logarithm bases, `Exp()` of a complex number with a huge real part, `//` and `%` of complex numbers, NaN or infinite vector components and scalars, `//` of vectors, `TimeSpan / 0` with `SupportTimeOperations`, and `TimeSpan.MaxValue + t` without it. A complex number with no imaginary part beyond the range of `double` keeps its value as `BigDecimal` (`Abs(1e200i * 1e200i + 1i)` threw `OverflowException`).
* Vector functions (`Abs`, `Floor`, `Ceiling`, `Truncate`, `Sqrt`, `Pow`, `Round`) use the options of the evaluation, not the options stored in the vector: with `UseBigNumbers`, `Sqrt([2;3])` and `Sqrt()` of a vector created by the host have big-number precision.
* With `DecimalAsDefault`, a number literal with significant digits below the resolution of decimal (1E-28) is read as a double when decimal would keep fewer of its digits, as a literal beyond the range of decimal is: `1e-30 > 0` is true (it was 0), `1.234e-27` keeps its digits (it was 0.0000000000000000000000000012). Literals that decimal holds, or holds with at least 16 significant digits, and literals below the range of double (`1e-400`) stay decimal.
* With `UseBigNumbers`, an exponent literal without a decimal separator is a big number like the same literal with one: `1e400` is 1E+400 (it was infinity), `1E+1000 / 1E+999` is 10. A literal with more integer digits (or leading zeros) than `MathHelper.MaxPowResultDigits` is the double infinity or 0, with or without a separator: `1.0e9999999` is infinity at once (it was a big number after more than a minute of parsing). A big-number literal with many digits, such as `12345678901234567890.5`, keeps all of them.
* With a secondary decimal separator equal to the group separator (`SecondaryDecimalSeparator = ","` with the built-in group separator), `1,5` is 1.5 also alone, as an argument or as a list item (it was 15).
* A decimal separator makes a number fractional only when a digit follows it: with a comma as both the decimal and the list separator, `Max(9007199254740993, 1)` and `(1, 2)` read integers (the first was a double, and with `UseBigNumbers` the list was the single number 1.2). With `UseBigNumbers` a number no longer contains whitespace (`1 .5` was 1.5). Range bounds such as `s[1..3]` are integers.
* A custom time separator `-` or `+` is no longer read as a UTC offset: `#2024/01/02 10-30-05#` is 10:30:05 (it was 05:30 local time), and `#10-30-00#` is a time span.
* A number group separator equal to the decimal separator (e.g. `NumberGroupSeparatorType.FromCulture` with de-DE and the built-in decimal separator) is rejected with an `NCalcParserException` when the expression is parsed, instead of reading `0.5` as 5.
* With `AcceptUnderscoresInNumbers`, an underscore separates digits only between two digits, as in C#: `_1` and `__2` are identifiers again (they were the numbers 1 and 2), and `1_`, `1_.5` or `1e_5` are rejected.
* With `AllowCharValues`, a one-character literal written with an escape sequence (`'\n'`, `'\u0041'`, `'\''`) is a char, like `'A'`.
* With the custom date separator `-` in the DMY or MDY order, date-only literals such as `#01-02-2024#` are read in that order, as with `/` (they were rejected as invalid ISO dates). Where `-` is both the date and the time separator, three numbers are a date when they make a valid date, otherwise a time span (`#10-30-05#` is 10:30:05 in DMY).
* A custom evaluation visitor (`IEvaluationVisitorFactory`, `IAsyncEvaluationVisitorFactory`, `WithEvaluationVisitorFactory`) also evaluates the arguments of functions (`if()`, `ifs()`, built-in and custom functions) and the bodies and arguments of `fn` functions, not only the top level of the expression. With `UseNonRecursiveEvaluator`, the visitor's `Visit(..., ExpressionTask, ...)` overloads are used.
* `break` and `continue` in the body of an `fn` function outside a loop of that body no longer act on a loop of the caller; they raise the same "may be used only inside the loop body" error as outside any loop.
* Indexed assignment accepts from-end indices (`l[^1] := 7`, `l[^1] += 1`, `s[^1] := 'z'`) as reading `l[^1]` does; an index outside the list or string raises NCalcParameterIndexException.
* After an asynchronous evaluation whose deeply nested calls continued on thread pool threads (`UseNonRecursiveEvaluator`), the caller continues on a fresh stack: it resumed with the stack of the thread nearly used up, so its next evaluation could fail as nested too deeply.
* The big-number transcendental functions (`Ln`, `Log`, `Log2`, `Log10`, `Exp`, `Sqrt`, `∛`, `∜`, `Asin`, `Acos`, `Atan`, the reciprocal and hyperbolic functions; UseBigNumbers with a BigInteger or BigDecimal argument) no longer set the process-wide `BigDecimal.AlwaysTruncate` while they run, which truncated the BigDecimal results of every other thread to `BigDecimal.Precision` digits meanwhile (`1.5 ** 400` lost 300 digits while another thread computed `Ln(x)`) and overwrote a value the application had set. They are computed by NCalc in fixed point and take milliseconds instead of up to seconds; the logarithms of numbers below 10^-100 are computed (they threw NCalcEvaluationException).
* The body of a function declared with `fn` can call any function declared in the expression (before or after it), including itself, so recursive and mutually recursive functions work; names that the body does not define are read from the caller: the arguments of a calling function, then the parameters, dynamic parameters, custom functions and handlers of the expression. Assignments in the body (also indexed ones to a list of the caller) do not change the parameters of the expression; only a function declared in the body of another function assigns to the arguments of that function, as before; a function declared elsewhere reads the arguments of a calling function, but its assignments to them (also indexed ones) stay in its own body.
* Recursion of `fn` functions is limited by the new `Expression.MaxUserFunctionCallDepth` / `AsyncExpression.MaxUserFunctionCallDepth` (default 1000): a deeper call throws `NCalcEvaluationException`, also with `UseNonRecursiveEvaluator`, so endless recursion never crashes the process.
* `HasErrors(token)` throws `OperationCanceledException` when the token is cancelled while the expression is parsed, as `Evaluate` does, instead of reporting the cancellation as a parser error in `Error`.
* Division by zero of integers, decimals and big numbers throws `NCalcEvaluationException` ("Division by zero.", with the .NET `DivideByZeroException` as `InnerException`) for `/`, `//`, `div`, `\`, `%` and the division by a percent, in all evaluators; floating-point divisions keep their IEEE results (`5 / 0` is ∞, `7.5 % 0.0` is NaN). `\` truncates its divisor first, so a divisor that truncates to zero is a division by zero also for a dividend beyond the range of `long`: `1e30 \ 1e-30` gave ∞ (or `OverflowException` with `OverflowProtection`).
* An integer quotient of `\` or `//` beyond the range of `long` that is returned as a `double` is the nearest double to the exact quotient: `1e20 \ 7` is 1.4285714285714287E+19 (was 1.4285714285714285E+19).
* An integer divided by an integer percent with an operand beyond 2^53 is the nearest double to the exact quotient (the dividend was converted to double first); with `DecimalAsDefault`, an integer divided by a numeric string is exact (`9223372036854775807 / '1.5'` is 6148914691236517204.6666666667, was …333.33); percent arithmetic converts between `decimal` and `double`/`float` with the same rule as the other arithmetic (`16777216f + 7%` with `DecimalAsDefault` is 17951621.12, was 17951625.4; `1e-30f + 7%` is 1.07E-30, was 0).
* `BoxedNumberComparer.Equals` and `GetHashCode` treat NaN as equal to NaN, consistent with its `Compare` and with `double.Equals`, so NaN can be found in a set or dictionary built with the comparer; the `=`, `!=` and `in` operators keep `NaN = NaN` false.
* A currency group separator equal to the currency decimal separator (`AdvancedExpressionOptions.CurrencyNumberGroupSeparator` / `CurrencyDecimalSeparator`, custom or from the culture) is rejected with `NCalcParserException`, as an equal number group and decimal separator already is; it was accepted silently, which made currency literals such as `$0.5` ambiguous.
* Time-only 12-hour literals are parsed: with a culture or options that use the 12-hour format, `#10:30 PM#`, `#10:30:15 AM#` and `#10:30 p#` are the `TimeSpan` of the 24-hour literal (`#22:30#`), `#12:00 AM#` is midnight and `#12:00 PM#` is noon. They were a parse error ("Invalid TimeSpan format").
* Conversions of `double` and `float` values to `BigDecimal` (big-number literals, `UseBigNumbers` arithmetic, complex numbers, comparisons and hash codes) no longer depend on the current culture: the conversion of ExtendedNumerics formats the value with the current culture and parses it with the culture current at its first use, so with cultures such as fa-IR expressions like `-1.5e40 + 1` with `UseBigNumbers` failed to parse or evaluate. `BigDecimal` values in `ToString()` of expressions, complex numbers and vectors are formatted by NCalc from the mantissa and exponent.
* The big-number transcendental functions keep about `BigDecimal.Precision` significant digits also when the result is small: logarithms of numbers near 1, `Atan`, `Asin` and `Arccot` of small arguments, and `Acos`, `Arcsec` and `Arccsc` near ±1 had `BigDecimal.Precision` decimal places (`Ln(1 + 1E-30)` kept 70 digits, `Arccsc(1 + 1E-151)` 50). They no longer run out of memory for arguments such as `3E-2000000000`, `Log` of a tiny negative number is NaN (was -∞), and big-number `Cot(0)` and `Csc(0)` are +∞ like `Coth(0)` and `Csch(0)` (they threw).
* With `UseBigNumbers`, the remainder `%` of a `long` or `ulong` beyond 2^53 and a `double` or `float` is computed exactly, as `/`, `//` and `\` compute such pairs: `9007199254740993 % 7.5` is 3 (was 2, from the integer rounded to double) and `long.MaxValue % 2.25` is 0.25; a zero `double` divisor is a division by zero, as for `/`.
* With `UseBigNumbers`, a `float` that becomes a `BigDecimal` keeps its own value (its shortest text) also when it is paired with a `double` in `//` and `\` and in percent operations: `7.5 // 1e-30f` is 7500000000000000000000000000000 (was 7499999976216923325417965053695, from 1e-30f read as 1.0000000031710769E-30). `Vector.FromVector2/3/4` read float components the same way (3.7f is 3.7).
* A percent divided by a number (`a% / b`, `a /= b` with a percent) gets the value from the exact integers when an integer percent or divisor is beyond 2^53, as `a / b%` does: `9007199254740995% / 3` is 3002399751580331.5% (was 3002399751580332%).
* No conversion of a `double` or `float` to `BigDecimal` depends on the current culture any more. ExtendedNumerics formats the number with the current culture and parses it with the culture current at its first use, so mixing cultures such as fa-IR and en-US threw FormatException or read another number. This affected complex-number tolerance comparisons (`c = c`), complex operators with doubles, `Sqrt`/`Log`/`Pow` of complex numbers, vector tolerance comparisons and conversions from `System.Numerics` vectors, `MathHelper.Hypot` and the `MathHelper.Add/Subtract/Multiply/Divide(BigDecimal, object)` helpers, and the tagged `BigDecimal` values of JSON.

# 9.0.6.5  (Allied Bits)

* Updated Parlot references.
* The .NET 4.6.2 target was replaced by .NET 4.7.2 to follow Parlot target update.

# 9.0.6.4 (Allied Bits)

* Fixed the leftover issue of the old NCalc - serialization of a float or double value did not work properly due to some quirky code. The code has been rewritten to operate properly.

# 9.0.6.3 (Allied Bits)

* Fixed an `ExecutionEngineException` on full-AOT platforms (e.g. iOS): the default logger factory no longer builds a `Microsoft.Extensions.Logging` `LoggerFactory` at startup unless a logging switch (`NCalc.Logging.EnableConsole`/`NCalc.Logging.EnableTrace`) is enabled. When no switch is set it returns `NullLoggerFactory.Instance`, avoiding the DI/lambda machinery that Mono full-AOT cannot compile and tries to JIT. This affects both the regular and the AOT assemblies.

# 9.0.6.2 (Allied Bits)

* Fixed a regression, where two string values were not compared properly.

# 9.0.6.1 (Allied Bits)

* Improved the comparison mechanism behind the "in" and "not in" operations when an object is checked against an array of objects. Now, this mechanism accounts for all numeric types and properly handles string coercion.

# 9.0.6 (Allied Bits)

* Fixed the `Pow` function and the exponentiation operation so that they are not limited by bitness of the `double` type. 

# 9.0.5 (Allied Bits)

* The `Round` function was re-implemented to apply the `MidpointRounding` mode only to midpoint values and not to everything as it is implemented in the System.Math namespace. This behavior is optional - you can use System.Math implementation by setting the 'UseSystemMathRound' option in the expression options.
* Added a workaround of a bug in `BigDecimal.GetFractionalPart` method that returns zero ([the issue is reported here](https://github.com/AdamWhiteHat/BigDecimal/issues/92)).

# 9.0.4.4 (Allied Bits)

* Setting a variable in a function body did not fire the `UpdateParameter` event of the expression.

# 9.0.4.3 (Allied Bits)

* "return X" did not work from inside the while loop (did not break the loop).
* Slightly improved error reporting in the case of a conflict between the operand types in the percent operations.

# 9.0.4.2 (Allied Bits)

* In rare cases, operations with an integer number and an integer percent did not work right due to integer division taking place.

# 9.0.4.1 (Allied Bits)

* Improved parsing of big decimals so that they don't get in the way of other expressions. 

# 9.0.3 (Allied Bits)

* Fixed comparison of a BigInteger or BigDecimal with a second value of a different type.
* The negation operation has been rewritten to not use subtraction. 

# 9.0.2 (Allied Bits)

* Fixed the handling of slice access to lists (e.g., "(1; 2; 3; 4)[^2..^1]").
* Fixed the handling of indexed access when the index was from end (e.g., "abcde"[^1]).
* Corrected the integer division to explicitly use Truncate for Basic-like division and Floor for Python-like division.

# 9.0.1 (Allied Bits)

* Added JSON serialization of complex numbers and vectors.
* Complex numbers in the Ni form were not recognized by the parser when parsing of percent was enabled. Fixed.

# 9.0 (Allied Bits)

* Added parsing of and operations with vectors (Vector class).
* Added parsing of and operations with complex numbers (ComplexNumber class).
* (IMPORTANT) The `AdvancedExpressionOptions.ArgumentSeparator` property has been renamed to `ListItemSeparator` to reflect the nature of the property. The same for the type.
* Fixed the separators in the SerializationVisitor - now, the value for a separator is derived from AdvancedExpressionOptions.ListItemSeparator when AdvancedExpressionOptions is set.

# 8.9.1 (Allied Bits)

* Added the ExtraIdentifierChars property to the AdvancedExpressionOptions class. This property lets one specify additional characters accepted in identifiers (useful if your identifiers may contain Unicode characters). The default value of the property contains the Unicode characters for greek letters Pi and Phi, and to use them, it suffices to assign the AdvancedOptions property of an Expression or AsyncExpression instance.

# 8.9 (Allied Bits)

* (IMPORTANT) Changed the way ValueExpression is serialized to JSON. The new way works correctly for deserialization, but the format is different from the one used before.
* (IMPORTANT) Reworked the handling of the LowerCaseIdentifierLookup expression option; now, it works as an "ignore case" flag with case-insensitive comparison of function and parameter names and does not require parameter names and function names in the lists to be in lowercase. Previously, lookup of functions did not work when function names contained uppercase letters and lower-case identifier matching was enabled.
* If, during the type conversion, an InvalidCastException error happens, it gets converted to an NCalcConversionException that carries plenty of additional information about the operation that failed.

# 8.8.1 (Allied Bits)

* The "return" keyword now grabs the rest of the expression until the end of it for evaluation. I.e., where the statement "... { return 2 + 2 }" returned 2,  now, 4 is returned.

# 8.8 (Allied Bits)

* Added optional support for ternary logic (Kleene’s Strong Three-Valued Logic) in logic operations.

# 8.7.7 (Allied Bits)

* When Unicode characters were enabled, a colon ":" was erroneously used for division (a Ratio character, \u2236, must have been used) and this got in the way of the ternary operator.
* Fixed the [inherited] wrong order of logical and bitwise operations. Now, a strong precedence order (bit_and, bit_xor, bit_or, and, xor, or) is used.
* Fixed the parsing of a date-only value in ISO format (e.g. #2026-02-02#)

# 8.7.6.2 (Allied Bits)

* Added optional parameters for dynamic expression parameters and functions to the constructor of the ExpresssionContext class.

# 8.7.6.1 (Allied Bits)

* Added the `MathHelper.IsBoxedNumberZeroOrOne` method
* Fixed `MathHelper.IsBoxedNumberOne` that could return wrong result for a double value of one.

# 8.7.6 (Allied Bits)

* When big numbers were used, a certain combination of operand types (a BigInteger and a floating point in any order) during math operations could fail to convert to a common big number type.
* Division could work improperly when two integer numbers were divided.
* Improved conversion of types in math operations - now, an attempt is made to make a result to be of the type of  initial operand(s) after an operation which required the change of types of the operands.
* Now, a string is parsed using `Expression` during comparison between a string and a non-string.

# 8.7.5.1 (Allied Bits)

* Fixed a failing test - it was missing an option.

# 8.7.5 (Allied Bits)

* (IMPORTANT) Reworked the Left Shift and Right Shift operations so that they validate parameters and allow only integers. Also, fixed Left Shift to produce correct results when the operation overflows 64 bits and Big numbers are enabled.

# 8.7.4 (Allied Bits)

* (IMPORTANT) Reworked the handling of comments to have them treated as a whitespace. Additionally, a user function may be annotated not by a regular comment, but by a documentation comment in C# style. Consequently, regular comments don't work as function documentation anymore.
* Now, it is possible to use the `return` keyword in any expression and not just in functions. 
* The code related to comparison of incompatible types has been simplified. 
* The `Truncate` built-in function now truncates DateTime to a date-only DateTime and TimeSpan to a Timespan with the number of days.

# 8.7.3.1 (Allied Bits)

* The `BoxedNumberComparer` class now implements IEqualityComparer interface.

# 8.7.3 (Allied Bits)

* Humane dates parser now recognizes 'yesterday' and 'tomorrow' (and of course their localized values) in addition to 'now' and 'today'.
* Added the `MathHelper.IsBoxedPositiveNumber()` and `MathHelper.IsBoxedNegativeNumber()` methods.
* Added an overloaded `EvaluationHelper.Compare()` method that returns int.
* Added the `BoxedNumberComparer` class used for comparing boxed numbers.

# 8.7.2.3 (Allied Bits)

* Made EvaluationHelper.Compare() more usable from outside of the package.
* Added the MathHelper.GetBoxedNumberAsDouble() and MathHelper.GetBoxedNumberAsDecimal() methods.

# 8.7.2.2 (Allied Bits)

* In AdvancedExpressionOptions, made some methods public so that they can be used by users of the package.

# 8.7.2.1 (Allied Bits)

* Improved reporting of a parsing error which occurs if a non-numeric string (i.e., the one that cannot be parsed into a number) is passed to an arithmetic operation function or to Min/Max method. Now, NCalcConversionException is thrown instead of FormatException.

# 8.7.2 (Allied Bits)

* Added support for addition and subtraction of ticks to/from a DateTime or a TimeSpan.
* Addressed the possible integer overflow during multiplication.
* Improved and fixed some border cases in math and comparison operations.

# 8.7.1.1 (Allied Bits)
* Fixed the chaos with assembly versions.

# 8.7.1 (Allied Bits)
* Added parsing of ISO 8601 date and date-time values (those used in XML).

# 8.7.0.1 (Allied Bits)
* Fixed the nuget package, in which the AOT assembly conflicted with the regular one.

# 8.7.0 (Allied Bits)
* Added AOT (ahead-of-time compilation) support via a separate assembly.
* Added an option to disable the use of dynamic functions for arithmetic in the regular assembly.
* NCalc now can multiply and divide a TimeSpan by a number as well as multiply a number by a Timespan (the result is a TimeSpan). 
* Removed an [artificial] limitation when arithmetic operations could be performed only on numbers, chars, strings, and dates and times (where applicable). Now, it is up to a caller to verify whether a type can be a part of the arithmetic operation - NCalc will throw InvalidOperationException, if it cannot be (this part of the behavior does not differ from previous versions).

# 8.6.5.1 (Allied Bits)
* Updated the version in the reference to Parlot to 1.5.6

# 8.6.5 (Allied Bits)
* Extended cancellation token support to the parsing stage - now, it is possible to specify and use a cancellation token not just while evaluating an expression but also when just parsing it.

# 8.6.4 (Allied Bits)
* Added .NET 10 target.

# 8.6.3 (Allied Bits)
* Added spaces as an option for a separator of list elements. Also, added some coombinations of characters (e.g., ColonOrSpace) as options.
* Slightly improved parsing of decimalsand doubles

# 8.6.2 (Allied Bits)
* Added protection against overflow by upscaling the parameters of basic math operations and repeating the operation in the case when an operation causes an overflow error.

# 8.6.1 (Allied Bits)
* Fixed the issue where values of different types were not always properly converted, which could lead to the type incompatibility error during a basic math operation.

# 8.6.0 (Allied Bits)
* Added a way to re-use the parser when multiple expressions must be evaluated.
* Removed the now useless cache of parsers. Also, a default parser is not created anymore as it was almost always useless.
* Added the `ArgumentSeparator` property to the AdvancedExpressionOptions class. This property lets one control the character used as an argument separator in function calls.

# 8.5.0.1 (Allied Bits)
* Fixed the behavior of assignments which should assign null if it is in the right operand.
* Fixed the comparison of an empty string with a numeric value: by default, an exception is thrown (as before), but if the "CompareIncompatibleTypes" flag is included in ExpressionOptions, comparison will evaluate to `false`.

# 8.5.0 (Allied Bits)
* Added support for user-defined functions.
* Added support for a return statement that can return a value from the expression (or a function).
* Fixed ExpressionOptions.(UseIfStatement,UseLoops,UseNonRecursiveEvaluator) which overlapped with other options due to the overflow of a 32-bit enumeration.

# 8.1.0 (Allied Bits)
* Added a non-recursive evaluator to help with a StackOverflow error with long or deep expressions.

# 8.0.2 (Allied Bits)
* Added parsing of "raw" strings which are not parsed for escape sequences.
* SerializationVisitor improved to provide the original value of a string for ValueExpression. If the original string included escaped characters, SerializationVisitor will return that original string and not a decoded one.

# 8.0.1 (Allied Bits)
* The Index operator can now be used to access elements of strings (i.e., individual characters and substrings).
* Added support for ranges as indexes (see documentation for details and examples).

# 8.0.0 (Allied Bits)
* Added support for C-Style (line and block) and Python-style comments.
* Added support for indexed expressions (when an expression evaluates to a list, it is possible to access individual elements).
* Implemented assignment of values to indexed expressions (if they evaluate to a variable).
* Sequences of expressions and statements can now be grouped using curly brackets (this is needed when a group of expressions or statements is used as a parameter of a function).
* Implemented the optional 'while' loop statement (it follows the regular C-like language style) with 'break' and 'continue' flow control keywords.
* Implemented the optional 'if' statement (it follows the regular C-like language style).
* Renamed the if() built-in function to iff(). If() is still supported unless the if() statement is enabled.
* Added the MakeList(size) built-in function.
* Added location of a logical expression in the source expression as a property of LogicalExpression class and its descendants; this expression is used when throwing some exceptions and can be used for simplifying diagnostics, logging, and error messages.
* Added the MatchString event for custom string matching.
* Added Log2 to built-in functions.
* Fixed statement sequences (the last statement was skipped).
* Added the missing pieces to the serialization visitor (not all binary operations were serialized).

# 7.0.1 (Allied Bits)
* Added the MatchString event to the expression classes for customized matching of strings. 
* Added the TreatNullAsZero flag to ExpressionOptions.
* Updated all unary, binary, and ternary functions so that they properly handle null values (either convert them to 0 or return null immediately depending on the TreatNullAsZero flag). 
* Fixed bugs in the handling of percent operations.

# 7.0.0 (Allied Bits)
* Increased the version to expose the difference in functionality between ncalc/ncalc and this package.
* Combined the main projects into one NCalc project.
* Built-in trigonometry functions now call the implementations in BigDecimal in the case the argument is BigDecimal or BigInteger and big numbers are enabled.
* Compare method now handles double and float NaN values before other comparisons.
* Added the PercentOf and PercentDiff functions, also modified the parser to recognize %(a,b) as the PercentOf function.
* Added CancellationToken to the async versions of the methods and events. 

# 5.6.0-beta (Allied Bits)
* Added parsing of a BigDecimal (when UseBigNumbers is set).
* Added integer division operations (Basic-style and Python-style)
* Added the "div" operator for integer division
* Added support for the BigDecimal type; renamed the UseBigInteger option to UseBigNumber
* Added an option to properly compare null values (a null value is smaller than a non-null value)

# 5.6.0-alpha (Allied Bits)
* Assignable parameters including shortcut operators (+=, etc.).
* Statement sequences (useful together with parameter assignments).
* Advanced date and time parsing, which takes into account culture settings (current or specific culture or custom separators) and supports times with or without seconds as well as 12-hour time.
* Parsing of humane period expressions like "3 weeks 2 days 5 hours" (period identifiers are customizable and multiple identifier per period are supported).
* Basic calculations with dates and time spans - one can add and subtract dates and times. Without these operations, date and time values are of little use (if only with custom functions).
* Currency support, which takes into account culture settings (current or specific culture or custom symbols). The idea is that the parser should drop the currency symbol when the expression is copied by the user from some source and includes this currency symbol (don't force people remove manually what the parser can remove automatically).
* Optional use of BigInteger type for basic math operations and some funcitons.
* Underscores in numbers and currency. Modern programming languages support underscores for readability. Support is built-in with binary, octal, and hex numbers, while support in decimal numbers requires a [custom branch of Parlot](https://github.com/Allied-Bits-Ltd/parlot/tree/ABCalc). 
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

# 5.4.1
* Update Parlot version by @gumbarros in https://github.com/ncalc/ncalc/pull/397

# 5.4.0
* Update NCalc.Core.csproj by @sebastienros in https://github.com/ncalc/ncalc/pull/381
* Update GH actions to latest by @sebastienros in https://github.com/ncalc/ncalc/pull/382
* Add binary, octal and hexadecimal number support. by @slobber in https://github.com/ncalc/ncalc/pull/388
* Update parameters.md by @gmkado in https://github.com/ncalc/ncalc/pull/385
* Documentation - Broken Links & Formatting by @Jason-Barratt in https://github.com/ncalc/ncalc/pull/389
* Use CultureInfo in string concatenation by @ridago in https://github.com/ncalc/ncalc/pull/392
* Fix culture info test by @ridago in https://github.com/ncalc/ncalc/pull/393
* Added `IEvaluationVisitorFactory` for more granular control over evaluation. by @gumbarros in https://github.com/ncalc/ncalc/pull/395
* Update 3rd party dependencies by @gumbarros in https://github.com/ncalc/ncalc/pull/396

## Breaking Changes
* Removed `IEvaluationService`, please use `IEvaluationVisitorFactory`. Check [this article](https://ncalc.github.io/ncalc/articles/overriding_evaluation.html) for more info.

# 5.3.1
* Small code quality improvements by @gumbarros in https://github.com/ncalc/ncalc/pull/365
* Fix Directory.Build.props inheritance by @sebastienros in https://github.com/ncalc/ncalc/pull/373
* Update packages by @Bykiev in https://github.com/ncalc/ncalc/pull/366
* Small code quality improvements by @gumbarros in https://github.com/ncalc/ncalc/pull/376
* Added issue #372 unit test by @gumbarros in https://github.com/ncalc/ncalc/pull/377

# 5.3.0
* Improve SerializationVisitor performance by @Bykiev in https://github.com/ncalc/ncalc/pull/353
* Remove nullable disable by @Bykiev in https://github.com/ncalc/ncalc/pull/355
* Improve performance of math functions by @Bykiev in https://github.com/ncalc/ncalc/pull/356
* Lambdas - use BigDecimal in Pow function by @Bykiev in https://github.com/ncalc/ncalc/pull/357
* Round - use int32 as digits parameter type by @Bykiev in https://github.com/ncalc/ncalc/pull/359
* Lambdas - add support for DecimalAsDefault by @Bykiev in https://github.com/ncalc/ncalc/pull/358
* Update to .NET 9, C# 13 and update all external dependencies to latest version. by @gumbarros in https://github.com/ncalc/ncalc/pull/361
* Update NCalc logo by @gumbarros in https://github.com/ncalc/ncalc/pull/363
* Added JSON polymorphism support by @gumbarros in https://github.com/ncalc/ncalc/pull/364

## Breaking Changes
* Remove obsolete `Expressions` property from `Function` class by @gumbarros in https://github.com/ncalc/ncalc/pull/362

# 5.2.11
* Update packages by @Bykiev in https://github.com/ncalc/ncalc/pull/346
* Fix culture support with NoStringTypeCoercion by @Bykiev in https://github.com/ncalc/ncalc/pull/347
* Added `ExpressionOptions.StrictTypeMatching` by @gumbarros in https://github.com/ncalc/ncalc/pull/351

# 5.2.10
* Issue 335 parsing of large decimal literals by @kierantop in https://github.com/ncalc/ncalc/pull/340
* Improve number parser readability by @gumbarros in https://github.com/ncalc/ncalc/pull/341
* Fix exponentiation at NCalc.Async by @gumbarros in https://github.com/ncalc/ncalc/pull/343

# 5.2.9
* Fix nested functions at `FunctionExtractionVisitor` by @gumbarros in https://github.com/ncalc/ncalc/pull/336
* Fix logical operator priority by @gumbarros in https://github.com/ncalc/ncalc/pull/338

# 5.2.8
* Fix `in` operator when there is an empty string by @gumbarros in https://github.com/ncalc/ncalc/pull/330

# 5.2.7
* Use `Microsoft.Extensions.Logging` instead of hard-coded logging by @gumbarros in https://github.com/ncalc/ncalc/pull/328
* `ExpressionOptions.NoStringTypeCoercion` should respect `in` operator by @gumbarros in https://github.com/ncalc/ncalc/pull/329

# 5.2.6
* Allow objects to be used with the `in` operator by @gumbarros in https://github.com/ncalc/ncalc/pull/325
* Fix ternary operator at NCalcAsync by @gumbarros in https://github.com/ncalc/ncalc/pull/326
* Code Quality: Remove exponentiation duplicated logic by @gumbarros in https://github.com/ncalc/ncalc/pull/327

# 5.2.5
* Added `Like` operator by @gumbarros in https://github.com/ncalc/ncalc/pull/324

# 5.2.4
* Add `ExpressionOptions.NoStringTypeCoercion` by @alexwarren in https://github.com/ncalc/ncalc/pull/321
* Add `ExpressionOptions.AllowNullOrEmptyExpressions` by @gumbarros in https://github.com/ncalc/ncalc/pull/322

# 5.2.3
* Roslynator - PrivateAssets="all" by @Bykiev in https://github.com/ncalc/ncalc/pull/319
* Fix compiler warnings in NCalc.Tests by @axunonb in https://github.com/ncalc/ncalc/pull/318

# 5.2.2
* Fix number parsing by @Bykiev in https://github.com/ncalc/ncalc/pull/316

# 5.2.1
* Fix inner parameter and functions at extraction visitors by @gumbarros in https://github.com/ncalc/ncalc/pull/306
* Fix ExpressionOptions.IterateParameters by @Bykiev in https://github.com/ncalc/ncalc/pull/309

# 5.2.0
* Added `FunctionExtractionVisitor` by @gumbarros in https://github.com/ncalc/ncalc/pull/290
* Re-use `LogicalExpressionList` logic for `Function` parameters by @gumbarros in https://github.com/ncalc/ncalc/pull/291
* Added `EvaluationHelper` and `in` operator should respect string comparer by @gumbarros in https://github.com/ncalc/ncalc/pull/292
* Small refactoring of MathHelper.ConvertToHighestPrecision by @Bykiev in https://github.com/ncalc/ncalc/pull/293
* Code cleanup by @Bykiev in https://github.com/ncalc/ncalc/pull/294
* Added `ExpressionBase` by @gumbarros in https://github.com/ncalc/ncalc/pull/297
* ifs improvement by @Bykiev in https://github.com/ncalc/ncalc/pull/298
* Update Parlot package by @Bykiev in https://github.com/ncalc/ncalc/pull/299
* Update Parlot (1.0.2) by @Bykiev in https://github.com/ncalc/ncalc/pull/300
* Add `ValueType.Guid` by @gumbarros in https://github.com/ncalc/ncalc/pull/301
* Breaking Change: Use `ValueTask` instead of `Task` at `NCalc.Async` by @gumbarros in https://github.com/ncalc/ncalc/pull/302

# 5.1.0
* Fix typo in MathHelper by @Bykiev in https://github.com/ncalc/ncalc/pull/273
* Small refactoring of Expression.IterateParameters by @Bykiev in https://github.com/ncalc/ncalc/pull/274
* Added `ShouldHandleBinaryExpression` unit test by @gumbarros in https://github.com/ncalc/ncalc/pull/278
* Add new `ExpressionOptions.StringConcat` to concat values as string by @Bykiev in https://github.com/ncalc/ncalc/pull/276
* Added `ExpressionOptions.AllowCharValues` by @gumbarros in https://github.com/ncalc/ncalc/pull/279
* Remove appveyor and use GH Actions with Coverlet by @gumbarros in https://github.com/ncalc/ncalc/pull/284
* Small performance improvement for `BinaryExpression` by @gumbarros in https://github.com/ncalc/ncalc/pull/283
* Move benchmarks to a separate workflow by @gumbarros in https://github.com/ncalc/ncalc/pull/285
* Improve string_concatenation.md docs by @gumbarros in https://github.com/ncalc/ncalc/pull/281
* Move event handlers to `ExpressionContext` by @gumbarros in https://github.com/ncalc/ncalc/pull/286
* Add support for Parlot parser compilation via AppContext switch by @Bykiev in https://github.com/ncalc/ncalc/pull/288
* Update Parlot parser by @Bykiev in https://github.com/ncalc/ncalc/pull/289
* Added `LogicalExpressionList` and `in` operator by @gumbarros in https://github.com/ncalc/ncalc/pull/287

# 5.0.0
* Overflow protection by @Bykiev in https://github.com/ncalc/ncalc/pull/256
* Consolidate NETStandard.Library package version by @Bykiev in https://github.com/ncalc/ncalc/pull/257
* Add OverflowProtection to `LambdaExpressionVisitor` by @gumbarros in https://github.com/ncalc/ncalc/pull/259
* Improve CI with `DOTNET_NOLOGO` and `DOTNET_CLI_TELEMETRY_OPTOUT` by @gumbarros in https://github.com/ncalc/ncalc/pull/260
* Fix treating an expression with whitespace in fractional part as valid by @Bykiev in https://github.com/ncalc/ncalc/pull/262
* Added `IDictionary<string,ExpressionFunction>` and `IDictionary<string,ExpressionParameter>` support by @gumbarros in https://github.com/ncalc/ncalc/pull/254
* Use decimal with exponentiation when DecimalAsDefault is used by @Bykiev in https://github.com/ncalc/ncalc/pull/269
* Add NOTRACE for the entire solution at Release by @gumbarros in https://github.com/ncalc/ncalc/pull/268
* Fix `AsyncFunctionArgs` regression by @gumbarros in https://github.com/ncalc/ncalc/pull/271
* Added `Id` property to `Identifier` by @gumbarros in https://github.com/ncalc/ncalc/pull/266
* Visitor pattern is now stateless with generics by @gumbarros in https://github.com/ncalc/ncalc/pull/272

## Breaking Changes
- `NCalcAsync` now uses `AsyncExpressionContext`
- `ExpressionContext` is now a `record` instead of a `class`, allowing support for shallow cloning
- `IEvaluationVisitor` is removed, please use `IEvaluationService` for an easier to implement interface
- `ILogicalExpressionVisitor` is now `ILogicalExpressionVisitor<T>`, where `<T>` is the return of the visitor
- `IAsyncLogicalExpressionVisitor` is removed, please use `ILogicalExpressionVisitor<Task<object?>>`
- `AdvancedExpression` and `AsyncAdvancedExpression` are removed, please use the respective constructors at `Expression` and `AsyncExpression` to prevent unnecessary casting.

# 4.3.3
* Add `MemberNotNullWhen` attribute to `HasErrors` by @gmcchessney in https://github.com/ncalc/ncalc/pull/250
* Fix tests by @Bykiev in https://github.com/ncalc/ncalc/pull/251
* Fix parsing fractional zero by @Bykiev in https://github.com/ncalc/ncalc/pull/253
* Refactor MathHelper by @Bykiev in https://github.com/ncalc/ncalc/pull/255

# 4.3.2
* Fix handling new lines in expression by @Bykiev in https://github.com/ncalc/ncalc/pull/234
* Add support UInt64 for binary operators by @Bykiev in https://github.com/ncalc/ncalc/pull/237
* Fix parsing expression by @Bykiev in https://github.com/ncalc/ncalc/pull/241
* Re-added `HasErrors` method to `NCalc.Async` by @gumbarros in https://github.com/ncalc/ncalc/pull/245
* Require braces to be closed by a brace of the same type by @gumbarros in https://github.com/ncalc/ncalc/pull/246
* Make unclosed brace cause a parsing exception by @gmcchessney in https://github.com/ncalc/ncalc/pull/243

# 4.3.1
* Fix handling new lines in expression by @Bykiev in https://github.com/ncalc/ncalc/pull/234

# 4.3.0
* Added `async` support by @gumbarros in https://github.com/ncalc/ncalc/pull/207
* Remove unused Parlot rule by @Bykiev in https://github.com/ncalc/ncalc/pull/221
* Inline `TypeHelper.IsReal` by @gumbarros in https://github.com/ncalc/ncalc/pull/225
* Allow whitespace at end of expression by @gumbarros and @Bykiev in https://github.com/ncalc/ncalc/pull/224
* Re-added Benchmark project by @gumbarros in https://github.com/ncalc/ncalc/pull/220
* Run Benchmark at CI by @gumbarros in https://github.com/ncalc/ncalc/pull/228
* Fixed not operator behavior by @gumbarros and @Bykiev in https://github.com/ncalc/ncalc/pull/227

## Breaking Changes
* `Expression` is now `AsyncExpression` at `NCalcAsync`, related classes are also prefixed with Async to prevent naming collisions
* Removed obsolete `HasOption` extension method from `ExpressionOptions`, please use `HasFlag`
* Removed obsolete `CaseInsensitiveComparer` enum member, please use `CaseInsensitiveStringComparer`

# 4.2.1
* [Fix treating NOT as unary in function name](https://github.com/ncalc/ncalc/pull/211) by [Andrey Bykiev](https://github.com/Bykiev)
* [Fix GetParametersNames() method inifinte loop with unary operators](https://github.com/ncalc/ncalc/pull/212) by [Andrey Bykiev](https://github.com/Bykiev)
* [Fix parsing floating-point numbers](https://github.com/ncalc/ncalc/pull/215) by [Andrey Bykiev](https://github.com/Bykiev)
* [Fix handling invalid expression with comma](https://github.com/ncalc/ncalc/pull/217) by [Sébastien Ros](https://github.com/sebastienros), [Andrey Bykiev](https://github.com/Bykiev)

# 4.2
* [Improve Parlot error handling](https://github.com/ncalc/ncalc/pull/181) by [Andrey Bykiev](https://github.com/Bykiev)
* [Fix OverflowException with double values](https://github.com/ncalc/ncalc/pull/188) by [Andrey Bykiev](https://github.com/Bykiev), [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Fix double value precision loss](https://github.com/ncalc/ncalc/pull/188) by [Andrey Bykiev](https://github.com/Bykiev), [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Add new ExpressionOptions.AllowBooleanCalculation and ExpressionOptions.OrdinalStringComparer options. ExpressionOptions.CaseInsensitiveComparer is now obsolete, please use ExpressionOptions.CaseInsensitiveStringComparer instead](https://github.com/ncalc/ncalc/pull/188) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Add support for compilation of expressions to CLR lambdas](https://github.com/ncalc/ncalc/pull/188) by [Gustavo Mauricio de Barros](https://github.com/gumbarros). Credits to [Sebastian Klose](https://github.com/sklose) and [NCalc2 project](https://github.com/sklose/NCalc2) contributors
* [HasOption method from ExpressionOptions is now obsolete, please use HasFlag](https://github.com/ncalc/ncalc/pull/196) by [Gustavo Barros](https://github.com/gumbarros)
* [AOT apps now work again with NCalc](https://github.com/ncalc/ncalc/pull/200) by [Gustavo Barros](https://github.com/gumbarros)

## Breaking Changes
* [Do not convert external function name to lower case when ExpressionOptions.IgnoreCase option is used](https://github.com/ncalc/ncalc/pull/179) by [Andrey Bykiev](https://github.com/Bykiev)
* [Add support for using null with operators](https://github.com/ncalc/ncalc/pull/184) by [Andrey Bykiev](https://github.com/Bykiev)
* [Exceptions need to be handled as NCalcEvaluationException instead of ArgumentException and added TypeHelper](https://github.com/ncalc/ncalc/pull/182) by [Gustavo Barros](https://github.com/gumbarros)
* [Use IOptions instead of IOptionsSnapshot at LogicalExpressionMemoryCache](https://github.com/ncalc/ncalc/pull/187) by [Gustavo Barros](https://github.com/gumbarros)
* [Changed the logic of ExpressionOptions.DecimalAsDefault. When this option is specified, all function parameters are expected to be decimal](https://github.com/ncalc/ncalc/pull/188) by [Gustavo Barros](https://github.com/gumbarros)
* [Fully removed LogicalExpressionVisitor, please use ILogicalExpressionVisitor](https://github.com/ncalc/ncalc/pull/188) by [Gustavo Barros](https://github.com/gumbarros)

# 4.1
* [Remove excessive check for casing](https://github.com/ncalc/ncalc/pull/149) by [Andrey Bykiev](https://github.com/Bykiev)
* [Add support for comparison with null parameters](https://github.com/ncalc/ncalc/pull/156) by [Andrey Bykiev](https://github.com/Bykiev)
* [Fix support for TimeSpan and DateTime with hours, minutes and seconds](https://github.com/ncalc/ncalc/issues/158) by [Gustavo Barros](https://github.com/gumbarros)
* [Add Dependency Injection support with IMemoryCache plugin](https://github.com/ncalc/ncalc/issues/154) by [Gustavo Barros](https://github.com/gumbarros)
* [Add support for using semicolon as argument separator](https://github.com/ncalc/ncalc/pull/162) by [Andrey Bykiev](https://github.com/Bykiev)
* [Fix invalid token handling](https://github.com/ncalc/ncalc/pull/166) by [Andrey Bykiev](https://github.com/Bykiev)
* [Add support fo curly braces as alternative to square brackets](https://github.com/ncalc/ncalc/pull/169) by [Andrey Bykiev](https://github.com/Bykiev)
* [Re-added ANTLR as a plugin](https://github.com/ncalc/ncalc/pull/176) by [Gustavo Barros](https://github.com/gumbarros)

# 4.0
* [Parlot is used instead of Antlr for parsing](https://github.com/ncalc/ncalc/issues/137) by [Andrey Bykiev](https://github.com/Bykiev), [Gustavo Mauricio de Barros](https://github.com/gumbarros) and [Sébastien Ros](https://github.com/sebastienros)
* [`GetParametersNames` no longer adds same parameter more than one time to the result](https://github.com/ncalc/ncalc/issues/141) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [xUnit is now used for unit tests](https://github.com/ncalc/ncalc/issues/138) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [New DocFX website with articles and public API](https://github.com/ncalc/ncalc/issues/143) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)

## Breaking Changes
* .NET Framework 4.6.1 no longer supported, please update to .NET Framework 4.6.2 or higher
* Renamed `EvaluateOptions` enum to `ExpressionOptions`
* Renamed `EvaluateOptionsExtensions` class to `ExpressionOptionsExtensions`
* Renamed `Expression.OriginalExpression` property  to `Expression.ExpressionString`
* Renamed `Expression.ParsedExpression` property to `Expression.LogicalExpression`
* Renamed `Numbers` static class to `MathHelper`
* Removed `Expression.Compile` static method, please use `LogicalExpressionFactory.Create`
* Removed unused `BinaryExpressionType.Unknown` enum value
* `Expression.Error` property now stores an `Exception` object instead of a `string`
* `Expression.GetParametersNames` method now returns a `List<String>` instead of a `string[]`

# 3.13.1
* [CompareUsingMostPreciseType is now public again](https://github.com/ncalc/ncalc/commit/c3eb2778c7e83ef191b8f647cdd98f802f6af3bf) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Fixed `BitwiseXOr` behavior](https://github.com/ncalc/ncalc/pull/134) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)

# 3.13

* [Performance: Use pattern matching instead of TypeCode](https://github.com/ncalc/ncalc/pull/126) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Fix boolean comparison](https://github.com/ncalc/ncalc/pull/123) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Test for inner exception type instead of exception message](https://github.com/ncalc/ncalc/pull/127) by [axunonb](https://github.com/axunonb)
* [Update project to create unsigned and signed versions of NCalcSync](https://github.com/ncalc/ncalc/pull/129) by [axunonb](https://github.com/axunonb)

# 3.12

* [Allow using decimal as default floating point type](https://github.com/ncalc/ncalc/pull/118) by [Luca Schimweg](https://github.com/lucaschimweg)
* [Use correct CLR types at GetMostPreciseType](https://github.com/ncalc/ncalc/pull/116/) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Performance improvements: Optimize built-in functions name checking and use ConcurrentDictionary<string, WeakReference<LogicalExpression>> at caching](https://github.com/ncalc/ncalc/pull/114) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Options were not propagating to EvaluationVisitor](https://github.com/ncalc/ncalc/pull/111) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)

# 3.11

* [Evaluate function and parameters only once](https://github.com/ncalc/ncalc/pull/108) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Performance improvements: CA1860 and CA1834](https://github.com/ncalc/ncalc/pull/105) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Add GetParametersNames method to Expression.cs](https://github.com/ncalc/ncalc/pull/104) by [Gustavo Mauricio de Barros](https://github.com/gumbarros),
  [Added Expression.GetParameters() method](https://github.com/ncalc/ncalc/pull/104) by [Rodion Mostovoi](https://github.com/rodion-m)

# 3.10

* [Make EvaluationVisitor sub-class friendly](https://github.com/ncalc/ncalc/pull/92) by [Nick](https://github.com/thetreatment)
* [Add support for ifs(cond, val, default)](https://github.com/ncalc/ncalc/pull/91) by [Justin Baugh](https://github.com/baughj)
* [Added EvaluationVisitor at ctor](https://github.com/ncalc/ncalc/pull/90) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Updated ANTLR runtime to v4.13.1](https://github.com/ncalc/ncalc/issues/81)

# 3.9

* [.NET 8 + C#12 + ReSharper refactors](https://github.com/ncalc/ncalc/pull/88) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)
* [Added EvaluateOptions.CaseInsensitiveComparer](https://github.com/ncalc/ncalc/pull/86) by [Gustavo Mauricio de Barros](https://github.com/gumbarros)

# 3.8

* [CompareUsingMostPreciseType chooses first option, not best](https://github.com/ncalc/ncalc/issues/76) by [@ThomasHambach](https://github.com/ThomasHambach)
* [EvaluationVisitor.Result should be protected, not private](https://github.com/ncalc/ncalc/issues/79) by [Oleksandr Kovaliv](https://github.com/Kizuto3)

# 3.7

* [Add parameterless constructor to ValueExpression](https://github.com/ncalc/ncalc/pull/61) by [@ThomasHambach](https://github.com/ThomasHambach)
* [Add Min and Max to unit tests](https://github.com/ncalc/ncalc/pull/63) by by [@ThomasHambach](https://github.com/ThomasHambach)
* [Update from Antlr3.Runtime to Antlr4.Runtime.Standard 4.12.0](https://github.com/ncalc/ncalc-async/pull/18) by [@markcanary](https://github.com/markcanary)

# 3.6

* [Add arithmetic support for multiple operators and types](https://github.com/ncalc/ncalc/pull/59) by [Justin Baugh](https://github.com/baughj)
* [Using OrdinalIgnoreCase instead of ToLower() checks](https://github.com/ncalc/ncalc/pull/55) by [Andrey Bykiev](https://github.com/Bykiev)

# 3.5

* [Added Atan2 built-in function](https://github.com/ncalc/ncalc/pull/53) by [YuJiaHao](https://github.com/YuJiaHao)

# 3.4

* [Improved custom CultureInfo support](https://github.com/ncalc/ncalc/pull/52) by [rholek](https://github.com/rholek)

# 3.2

* [Disable tracing for Release builds](https://github.com/ncalc/ncalc/issues/51) by [Shifty15](https://github.com/Shifty15)

# 3.1

* [CultureInfo support](https://github.com/ncalc/ncalc/pull/46) by [Shifty15](https://github.com/Shifty15)

# 3.0

Several syntax changes to the grammar:

* [Exponentiation operator `**`](https://github.com/ncalc/ncalc/issues/36)
* [Case insensitive operators and key words (e.g. `AND` or `True`)](https://github.com/ncalc/ncalc/issues/37)
* [Support numbers with trailing dot (e.g. `47.`)](https://github.com/ncalc/ncalc/issues/21)
* [Support for positive sign (e.g. `+5`)](https://github.com/ncalc/ncalc/issues/11)

While these changes in themselves wouldn't introduce compatibility issues with previously valid statements, code that relies on statements with these constructs being invalid would be affected. The grammar also had to be regenerated with a new version of ANTLR with some fixes to it since it was clear that the generated source code had been modified manually. Manual review indicates that the regenerated grammar is identical, but because of both these reasons this is released as a new major version.

* [Bugfix: invalid tokens were skipped silently without any errors](https://github.com/ncalc/ncalc/issues/22). Expressions like `"4711"` would ignore the `"` (since that is not the string character in the NCalc syntax) and parse it as the number `4711`, but now an `EvaluationException` is thrown as for other syntax issues. This may affect existing expressions, but since they were always incorrect and now give an exception rather than silently getting a new value it does not merit a new major release.
* [Major bugfix: long integers are now treated as integers](https://github.com/ncalc/ncalc/issues/18). Previous versions converted them to single-precision floats, which caused data loss on large numbers. Since this affects the results of existing expressions, it requires a new major release.
* [New builtin function `Ln()`](https://github.com/ncalc/ncalc/pull/14)

# 2.0

Initial public release of the .NET Core version.

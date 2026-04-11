# CLAUDE.md - NCalc Project Guide

## Project Overview

NCalc is a mathematical and logical expression evaluator library for .NET. It parses string expressions into an AST (Abstract Syntax Tree), then evaluates them using the Visitor pattern. The library supports sync/async evaluation, lambda compilation, DI integration, and an extensive set of built-in functions.

**Package ID**: `AlliedBits.NCalc`

## Build & Test

```bash
# Build the main library (multi-targeted: net462, netstandard2.0, net8.0, net9.0, net10.0)
dotnet build src/NCalc/NCalc.csproj

# Run the full test suite (xUnit v3, net10.0)
dotnet test test/NCalc.Tests/NCalc.Tests.csproj

# Run tests by category
dotnet test test/NCalc.Tests/NCalc.Tests.csproj --filter "Category=Serialization"
dotnet test test/NCalc.Tests/NCalc.Tests.csproj --filter "Category=Math"

# Force a clean rebuild (useful if build caching causes stale binaries)
dotnet build --no-incremental

# Run benchmarks
dotnet run --project test/NCalc.Benchmarks/NCalc.Benchmarks.csproj -c Release
```

## Project Structure

```
src/
  NCalc/                          # Core library (multi-targeted)
    Domain/                       # AST node types (LogicalExpression hierarchy)
    Visitors/                     # Visitor implementations (evaluation, serialization, extraction)
    Parser/                       # Parlot-based expression parser
    Helpers/                      # Static utilities (MathHelper, TypeHelper, etc.)
    Factories/                    # Factory interfaces and implementations
    Handlers/                     # Event handler delegates and args classes
    Cache/                        # WeakReference-based expression cache
    Exceptions/                   # NCalc-specific exception types
    Reflection/                   # Reflection utilities (.NET targets only)
  NCalc.AOT/                      # AOT-compatible variant (net9.0, net10.0 only)
  Plugins/
    NCalc.MemoryCache/            # Microsoft.Extensions.Caching.Memory cache plugin
    NCalc.Antlr/                  # Alternative ANTLR-based parser plugin
test/
  NCalc.Tests/                    # Main test suite
  NCalc.Tests.AOT/                # AOT-specific tests
  NCalc.Benchmarks/               # BenchmarkDotNet performance tests
  NCalc.Play/                     # Playground/example project
```

## Architecture

### Expression Evaluation Flow

```
String Expression
    -> LogicalExpressionParser (Parlot)
    -> LogicalExpression AST (cached via ILogicalExpressionCache)
    -> EvaluationVisitor / AsyncEvaluationVisitor
    -> Result (object?)
```

### Visitor Pattern

All AST nodes inherit from `LogicalExpression`. Each node implements:
- `Accept<T>(ILogicalExpressionVisitor<T>)` for recursive evaluation
- `AcceptNoRecurse<T>(ILogicalExpressionNoRecurseVisitor<T>)` for stack-safe evaluation of deep expressions

**Visitor implementations** (4 evaluation variants must stay in sync):
- `EvaluationVisitor.cs` (sync recursive)
- `EvaluationVisitor.NoRecurse.cs` (sync non-recursive)
- `AsyncEvaluationVisitor.cs` (async recursive)
- `AsyncEvaluationVisitor.NoRecurse.cs` (async non-recursive)

When adding a new operator or type dispatch to evaluation, all 4 variants must be updated.

### AST Node Types

- **Values**: `ValueExpression`, `Identifier`
- **Operations**: `BinaryExpression` (45+ types), `UnaryExpression`, `TernaryExpression`
- **Functions**: `FunctionCall`, `FunctionExpression`
- **Collections**: `LogicalExpressionList`, `VectorExpression`
- **Special**: `ImaginaryNumberExpression`, `PercentExpression`, `ExpressionGroup`, `StatementSequence`

### JSON Serialization

- `LogicalExpression` uses `[JsonPolymorphic]` / `[JsonDerivedType]` for polymorphic System.Text.Json serialization (guarded by `#if NET`)
- `ObjectValueJsonConverter` (in `Domain/ObjectValueJsonConverter.cs`) handles `ValueExpression.Value` (an `object?`), supporting scalars, BigDecimal, ComplexNumber, Vector, and collections
- BigDecimal serializes as `{"Mantissa": ..., "Exponent": ...}`
- ComplexNumber and Vector use a `{"$type": "TypeName", ...}` discriminator pattern
- Other scalars use `{"$type": "TypeName", "$value": "stringValue"}` tagged format

### Multi-Targeting & AOT

- **Regular NCalc**: `net462`, `netstandard2.0`, `net8.0`, `net9.0`, `net10.0`
- **NCalc.AOT**: `net9.0`, `net10.0` only; defines `AOT_COMPILATION` constant; no FastExpressionCompiler or reflection
- `#if NET` guards modern .NET features (JSON polymorphism, `[JsonConstructor]`, etc.)
- `PolySharp` provides polyfills for `net462`/`netstandard2.0` targets

## Code Conventions

### Style
- 4-space indentation, Allman brace style
- File-scoped namespaces (`namespace NCalc.Domain;`)
- `System` usings first, then external packages, then internal NCalc namespaces
- Nullable reference types enabled project-wide (`<Nullable>enable</Nullable>`)
- `sealed` on domain/leaf classes
- Primary constructors (C# 12) used for simple AST nodes

### Naming
- Interfaces: `I*` prefix (e.g., `ILogicalExpressionVisitor`)
- Exceptions: `NCalc*` prefix (e.g., `NCalcException`)
- Options enums: `*Options` suffix with `[Flags]` and bit-shift notation
- Type alias `NCalcVector = NCalc.Domain.Vector` used where `System.Numerics` is also imported

### Helpers
- `MathHelper` handles arithmetic, comparison, type conversion with overflow protection
- `MathHelper.ConvertToBigDecimal()` is the safe way to convert `object?` MathHelper returns to `BigDecimal` (handles both `double` and `BigDecimal`)
- `MathHelperOptions` is a runtime-only struct (wraps `CultureInfo` + `ExpressionOptions`) that should NOT be serialized; visitors reconstruct it from `LogicalExpression.Options` and `LogicalExpression.CultureInfo`

## Testing

- **Framework**: xUnit v3 with `Microsoft.Testing.Platform`
- **Categories**: `[Trait("Category", "...")]` - Math, Serialization, Evaluations, Advanced, etc.
- **Test data**: `TheoryData<T1, T2>` subclasses in `test/NCalc.Tests/TestData/`
- **Base class**: `TestBase` provides `CheckResult()` helper and common option sets
- **Cancellation**: Always use `TestContext.Current.CancellationToken` in test methods

## Key Dependencies

- **AlliedBits.Parlot** - Parser combinator library for expression parsing
- **FastExpressionCompiler** - Lambda compilation (not in AOT variant)
- **ExtendedNumerics.BigDecimal** - Arbitrary-precision decimal arithmetic
- **Roslynator** - Code analysis (warnings treated as errors)

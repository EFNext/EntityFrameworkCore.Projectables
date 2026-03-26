# How It Works

Understanding the internals of EF Core Projectables helps you use it effectively and debug issues when they arise. The library has two main components: a **build-time source generator** and a **runtime query interceptor**.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    BUILD TIME                           │
│                                                         │
│  Your C# code with [Projectable] members                │
│            │                                            │
│            ▼                                            │
│  ┌───────────────────────────────────┐                  │
│  │  Roslyn Source Generator          │                  │
│  │  (ProjectionExpressionGenerator)  │                  │
│  │  - Scans for [Projectable]        │                  │
│  │  - Parses member bodies           │                  │
│  │  - Generates Expression<>         │                  │
│  │    companion classes              │                  │
│  └───────────────────────────────────┘                  │
│            │                                            │
│            ▼                                            │
│  Auto-generated *.g.cs files with Expression<> trees    │
└─────────────────────────────────────────────────────────┘

┌───────────────────────────────────────────────────────────┐
│                   RUNTIME                                 │
│                                                           │
│  LINQ query using projectable member                      │
│            │                                              │
│            ▼                                              │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  ProjectableExpressionReplacer (ExpressionVisitor)  │  │
│  │  - Walks the LINQ expression tree                   │  │
│  │  - Detects calls to [Projectable] members           │  │
│  │  - Loads generated Expression<> via reflection      │  │
│  │  - Substitutes the call with the expression         │  │
│  └─────────────────────────────────────────────────────┘  │
│            │                                              │
│            ▼                                              │
│  Expanded expression tree (no [Projectable] calls)        │
│            │                                              │
│            ▼                                              │
│  Standard EF Core SQL translation → SQL query             │
└───────────────────────────────────────────────────────────┘
```

## Build Time: The Source Generator

### `ProjectionExpressionGenerator`

This is the entry point for the Roslyn incremental source generator. It implements `IIncrementalGenerator` for high-performance, incremental code generation.

**Pipeline:**
1. **Filter** — Uses `ForAttributeWithMetadataName` to efficiently find all `MemberDeclarationSyntax` nodes decorated with `[ProjectableAttribute]`.
2. **Interpret** — Calls `ProjectableInterpreter.GetDescriptor()` to extract all the information needed to generate code.
3. **Generate** — Produces a static class with an `Expression<Func<...>>` factory method.

### `ProjectableInterpreter`

Reads the attribute arguments, resolves the member's type information (namespace, generic parameters, containing classes), and extracts the expression body.

**Key tasks:**
- Resolves `NullConditionalRewriteSupport`, `UseMemberBody`, `ExpandEnumMethods`, and `AllowBlockBody` from the attribute.
- Determines the correct parameter list for the generated lambda (including the implicit `@this` parameter for instance members and extension methods).
- Dispatches to `BlockStatementConverter` for block-bodied members.

### `BlockStatementConverter`

Converts block-bodied method statements into expression-tree-compatible forms:

| Statement                            | Converted to                    |
|--------------------------------------|---------------------------------|
| `if (cond) return A; else return B;` | `cond ? A : B`                  |
| `switch (x) { case 1: return "a"; }` | `x == 1 ? "a" : ...`            |
| `var v = expr; return v + 1;`        | Inline substitution: `expr + 1` |
| Multiple early `return`              | Nested ternary chain            |

### Expression Rewriters

After the body is extracted, several rewriters transform the expression syntax:

| Rewriter                      | Purpose                                                          |
|-------------------------------|------------------------------------------------------------------|
| `ExpressionSyntaxRewriter`    | Rewrites `?.` operators based on `NullConditionalRewriteSupport` |
| `DeclarationSyntaxRewriter`   | Adjusts member declarations for the generated class              |
| `VariableReplacementRewriter` | Inlines local variables into the return expression               |

### Generated Code

For a property like:

```csharp
public class Order
{
    [Projectable]
    public decimal GrandTotal => Subtotal + Tax;
}
```

The generator produces something like:

```csharp
// Auto-generated — not visible in IntelliSense
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class Order__GrandTotal
{
    public static Expression<Func<Order, decimal>> Expression()
        => @this => @this.Subtotal + @this.Tax;
}
```

The class name is deterministic, based on namespace + class name + member name.

### `ProjectionExpressionClassNameGenerator`

Generates a stable, unique class name for each projectable member. Handles generics, overloads (via parameter type names), and nested classes.

## Runtime: The Query Interceptor

### How Queries Are Intercepted

When `UseProjectables()` is called, the library registers custom implementations of EF Core's internal query infrastructure. Depending on the [Compatibility Mode](/reference/compatibility-mode):

**Full mode** — registers a `CustomQueryCompiler` that wraps EF Core's default compiler. Before compiling any query, it calls `ProjectableExpressionReplacer.Replace()` on the raw LINQ expression.

**Limited mode** — registers a `CustomQueryTranslationPreprocessor` (via `CustomQueryTranslationPreprocessorFactory`). This runs inside EF Core's own query pipeline after the query is accepted, so the expanded query benefits from EF Core's query cache.

### `ProjectableExpressionReplacer`

Inherits from `ExpressionVisitor`. Its `Visit` method walks the LINQ expression tree and looks for:

- **Property accesses** that correspond to `[Projectable]` properties.
- **Method calls** that correspond to `[Projectable]` methods.

For each hit, it:
1. Calls `ProjectionExpressionResolver.FindGeneratedExpression()` to locate the auto-generated expression class via reflection.
2. Uses `ExpressionArgumentReplacer` to substitute the lambda parameters with the actual arguments from the call site.
3. Replaces the original call node with the inlined expression body.

The replacement is done recursively — if the inlined expression itself contains projectable calls, they are also expanded.

### `ProjectionExpressionResolver`

Discovers the auto-generated companion class by constructing the expected class name (using the same naming logic as the generator) and reflecting into the assembly.

```csharp
// Roughly equivalent to:
var type = assembly.GetType("Order__GrandTotal");
var method = type.GetMethod("Expression");
var expression = (LambdaExpression)method.Invoke(null, null);
```

### `ExpressionArgumentReplacer`

Replaces the `@this` parameter (and any method arguments) in the retrieved lambda with the actual expressions from the call site. This is standard expression tree parameter substitution.

## Tracking Behavior Handling

The replacer also manages EF Core's tracking behavior. When a projectable member is used in a `Select` projection, the replacer wraps the expanded query in a `AsNoTracking()` call if necessary, ensuring consistent behavior with and without projectables.

## Summary

| Phase   | Component                            | Responsibility                               |
|---------|--------------------------------------|----------------------------------------------|
| Build   | `ProjectionExpressionGenerator`      | Source gen entry point, orchestration        |
| Build   | `ProjectableInterpreter`             | Extract descriptor from attribute + syntax   |
| Build   | `BlockStatementConverter`            | Block body → expression conversion           |
| Build   | `ExpressionSyntaxRewriter`           | `?.` handling, null-conditional rewrite      |
| Runtime | `CustomQueryCompiler`                | Full mode: expand before EF Core             |
| Runtime | `CustomQueryTranslationPreprocessor` | Limited mode: expand inside EF Core pipeline |
| Runtime | `ProjectableExpressionReplacer`      | Walk and replace projectable calls           |
| Runtime | `ProjectionExpressionResolver`       | Locate generated expression via reflection   |
| Runtime | `ExpressionArgumentReplacer`         | Substitute parameters in lambda              |


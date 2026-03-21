# Introduction

**EntityFrameworkCore.Projectables** is a library that lets you write C# properties and methods — decorated with a simple `[Projectable]` attribute — and use them directly inside any EF Core LINQ query. The library takes care of translating those members into the SQL query, keeping your codebase DRY and your queries efficient.

## The Problem It Solves

When using EF Core, you often need to express the same business logic in two places:

1. **In-memory** — as a regular C# property or method on your entity.
2. **In queries** — duplicated inline as a LINQ expression so EF Core can translate it to SQL.

```csharp
// ❌ Without Projectables — logic duplicated
class Order
{
    // C# property (in-memory use)
    public decimal GrandTotal => Subtotal + Tax;

    // Must be duplicated inline in every LINQ query
}

var totals = dbContext.Orders
    .Select(o => new {
        GrandTotal = o.Items.Sum(i => i.Price) + (o.Items.Sum(i => i.Price) * o.TaxRate)
    })
    .ToList();
```

With Projectables, you write the logic once:

```csharp
// ✅ With Projectables — write once, use everywhere
class Order
{
    [Projectable] public decimal Subtotal => Items.Sum(i => i.Price);
    [Projectable] public decimal Tax => Subtotal * TaxRate;
    [Projectable] public decimal GrandTotal => Subtotal + Tax;
}

var totals = dbContext.Orders
    .Select(o => new { o.GrandTotal })  // Inlined into SQL automatically
    .ToList();
```

## How It Works

Projectables has two components that work together:

### 1. Source Generator (build time)

When you compile your project, a Roslyn source generator scans for members decorated with `[Projectable]` and generates a **companion expression tree** for each one. For example, the `GrandTotal` property above generates something like:

```csharp
// Auto-generated — hidden from IntelliSense
public static Expression<Func<Order, decimal>> GrandTotal_Expression()
    => @this => @this.Items.Sum(i => i.Price) + (@this.Items.Sum(i => i.Price) * @this.TaxRate);
```

### 2. Runtime Interceptor (query time)

At query execution time, a custom EF Core query pre-processor walks your LINQ expression tree. Whenever it encounters a call to a `[Projectable]` member, it **replaces it with the generated expression tree**, substituting the actual parameters. The resulting expanded expression tree is then handed off to EF Core for normal SQL translation.

```
LINQ query
    → [Projectables interceptor replaces member calls with expressions]
    → Expanded expression tree
    → EF Core SQL translation
    → SQL query
```

## Comparison with Similar Libraries

| Feature                       | Projectables     | Expressionify | LinqKit |
|-------------------------------|------------------|---------------|---------|
| Source generator based        | ✅                | ✅             | ❌       |
| Works with entity methods     | ✅                | ✅             | Partial |
| Works with extension methods  | ✅                | ✅             | ✅       |
| Composable projectables       | ✅                | ❌             | Partial |
| Constructor projections       | ✅                | ❌             | ❌       |
| Pattern matching support      | ✅                | ❌             | ❌       |
| Method overloads support      | ✅                | ❌             | ❌       |
| Block-bodied members          | ✅ (experimental) | ❌             | ❌       |
| Enum method expansion         | ✅                | ❌             | ❌       |
| Null-conditional rewriting    | ✅                | ❌             | ❌       |
| Roslyn analyzers & code fixes | ✅                | ❌             | ❌       |
| Limited/cached mode           | ✅                | ❌             | ❌       |

## EF Core Version Compatibility

| Library Version | EF Core Version | Notable Additions                                                                                                |
|-----------------|-----------------|------------------------------------------------------------------------------------------------------------------|
| v1.x            | EF Core 3.1, 5  | Initial release                                                                                                  |
| v2.x, v3.x      | EF Core 6, 7    | Null-conditional rewriting, enum expansion                                                                       |
| v6.x+           | EF Core 6+      | Block-bodied members, constructor projections, pattern matching, method overloads, Roslyn analyzers & code fixes |

## Next Steps

- [Quick Start →](/guide/quickstart)
- [Constructor Projections →](/guide/projectable-constructors)
- [Analyzers & Code Fixes →](/reference/diagnostics)
- [Learn how it works internally →](/advanced/how-it-works)


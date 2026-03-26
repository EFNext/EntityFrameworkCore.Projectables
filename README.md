# EntityFrameworkCore.Projectables
Flexible projection magic for EF Core

[![NuGet version (EntityFrameworkCore.Projectables)](https://img.shields.io/nuget/v/EntityFrameworkCore.Projectables.Abstractions.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables.Abstractions/)
[![.NET](https://github.com/EFNext/EntityFrameworkCore.Projectables/actions/workflows/build.yml/badge.svg)](https://github.com/EFNext/EntityFrameworkCore.Projectables/actions/workflows/build.yml)

## NuGet packages
- EntityFrameworkCore.Projectables.Abstractions [![NuGet version](https://img.shields.io/nuget/v/EntityFrameworkCore.Projectables.Abstractions.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables.Abstractions/) [![NuGet](https://img.shields.io/nuget/dt/EntityFrameworkCore.Projectables.Abstractions.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables.Abstractions/)
- EntityFrameworkCore.Projectables [![NuGet version](https://img.shields.io/nuget/v/EntityFrameworkCore.Projectables.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables/) [![NuGet](https://img.shields.io/nuget/dt/EntityFrameworkCore.Projectables.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables/)

> Starting with V2 of this project we're binding against **EF Core 6**. If you're targeting **EF Core 5** or **EF Core 3.1** then you can use the latest v1 release. These are functionally equivalent.

## Getting started
1. Install the package from [NuGet](https://www.nuget.org/packages/EntityFrameworkCore.Projectables/)
2. Enable Projectables in your DbContext by adding: `dbContextOptions.UseProjectables()`
3. Mark properties, methods, or constructors with `[Projectable]`.
4. Read the **[documentation](https://efnext.github.io)** for guides, reference, and recipes.

### Example

```csharp
class Order
{
    public decimal TaxRate { get; set; }
    public ICollection<OrderItem> Items { get; set; }

    [Projectable] public decimal Subtotal => Items.Sum(item => item.Product.ListPrice * item.Quantity);
    [Projectable] public decimal Tax => Subtotal * TaxRate;
    [Projectable] public decimal GrandTotal => Subtotal + Tax;
}

public static class UserExtensions
{
    [Projectable]
    public static Order GetMostRecentOrder(this User user) =>
        user.Orders.OrderByDescending(x => x.CreatedDate).FirstOrDefault();
}

var result = dbContext.Users
    .Where(u => u.UserName == "Jon")
    .Select(u => new { u.GetMostRecentOrder().GrandTotal })
    .FirstOrDefault();
```

The properties are **inlined into SQL** — no client-side evaluation, no N+1.

### How it works

There are two components: a **Roslyn source generator** that emits companion `Expression<TDelegate>` trees for each `[Projectable]` member at compile time, and a **runtime interceptor** that walks your LINQ queries and substitutes those expressions before EF Core translates them to SQL.

## Features (v6.x+)

| Feature                                         | Docs                                                                       |
|-------------------------------------------------|----------------------------------------------------------------------------|
| Properties & methods                            | [Guide →](https://efnext.github.io/guide/projectable-properties)           |
| Extension methods                               | [Guide →](https://efnext.github.io/guide/extension-methods)                |
| Constructor projections                         | [Guide →](https://efnext.github.io/guide/projectable-constructors)         |
| Method overloads                                | Fully supported                                                            |
| Pattern matching (`switch`, `is`)               | [Reference →](https://efnext.github.io/reference/pattern-matching)         |
| Block-bodied members (experimental)             | [Advanced →](https://efnext.github.io/advanced/block-bodied-members)       |
| Null-conditional rewriting                      | [Reference →](https://efnext.github.io/reference/null-conditional-rewrite) |
| Enum method expansion                           | [Reference →](https://efnext.github.io/reference/expand-enum-methods)      |
| `UseMemberBody`                                 | [Reference →](https://efnext.github.io/reference/use-member-body)          |
| Roslyn analyzers & code fixes (EFP0001–EFP0012) | [Reference →](https://efnext.github.io/reference/diagnostics)              |
| Limited/Full compatibility mode                 | [Reference →](https://efnext.github.io/reference/compatibility-mode)       |

## FAQ

#### Is this specific to a database provider?
No. The interceptor hooks into EF Core's query compilation pipeline before any provider-specific translation, so it works with SQL Server, PostgreSQL, SQLite, Cosmos DB, and any other EF Core provider.

#### Are there performance implications?
Two compatibility modes are available: **Full** (default) expands every query before handing it to EF Core; **Limited** expands once and caches the result. Limited mode often outperforms plain EF Core on repeated queries. See the [Compatibility Mode docs](https://efnext.github.io/reference/compatibility-mode).

#### Can I compose projectables?
Yes — a `[Projectable]` member can call other `[Projectable]` members. They are recursively inlined into the final SQL.

#### How does this relate to [Expressionify](https://github.com/ClaveConsulting/Expressionify)?
Expressionify has overlapping features and a similar approach but a narrower scope. Projectables adds constructor projections, pattern matching, block-bodied members, enum expansion, and a richer diagnostics layer.

#### How does this relate to LinqKit/LinqExpander?
[LinqKit](https://github.com/scottksmith95/LINQKit) and similar libraries predate source generators. Projectables (and Expressionify) are superior approaches for modern .NET because the source generator does the heavy lifting at compile time with no runtime reflection.

#### What .NET and EF Core versions are supported?
- v1.x → EF Core 3.1 / 5
- v2.x–v3.x → EF Core 6 / 7
- v6.x+ → EF Core 6+ (current; targets `net8.0` and `net10.0`)

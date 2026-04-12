# ReadmeSample

A runnable sample illustrating **every feature** from the [Features table](../../README.md) of EntityFrameworkCore.Projectables, using a **local SQLite database** created automatically on startup.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- No database server required — SQLite is embedded.

## Running the sample

```bash
dotnet run --project samples/ReadmeSample
```

The `ReadmeSample.db` file is recreated automatically on every run (`EnsureDeleted` / `EnsureCreated`).
Each section prints the generated SQL followed by the query results.

---

## Project structure

```
ReadmeSample/
├── Program.cs                        # Entry point — 11 numbered feature demos
├── ApplicationDbContext.cs           # SQLite DbContext with UseProjectables()
├── Entities/
│   ├── User.cs                       # User with an Orders collection
│   ├── Order.cs                      # Order entity — most features live here
│   ├── OrderItem.cs                  # Order line item (composite primary key)
│   ├── Product.cs                    # Product with optional Supplier navigation
│   ├── Supplier.cs                   # Optional supplier (for null-conditional demo)
│   └── OrderStatus.cs               # Enum + GetDisplayName() (for enum expansion demo)
├── Dtos/
│   └── OrderSummaryDto.cs           # DTO with a [Projectable] constructor
└── Extensions/
    └── UserExtensions.cs            # [Projectable] extension methods on User
```

---

## Features demonstrated

All features from the [root README features table](../../README.md#features-v6x) are covered.

### Feature 1 — Properties & methods

**Properties** compose each other recursively — `GrandTotal` inlines `Subtotal` and `Tax`:

```csharp
[Projectable] public decimal Subtotal   => Items.Sum(item => item.Product.ListPrice * item.Quantity);
[Projectable] public decimal Tax        => Subtotal * TaxRate;
[Projectable] public decimal GrandTotal => Subtotal + Tax;
```

**Methods** accept parameters and are equally inlined into SQL:

```csharp
[Projectable]
public decimal GetDiscountedTotal(decimal discountPct) => GrandTotal * (1 - discountPct);
```

### Feature 2 — Extension methods

The extension method body is inlined as a correlated subquery:

```csharp
// Extensions/UserExtensions.cs
[Projectable]
public static Order? GetMostRecentOrder(this User user) =>
    user.Orders.OrderByDescending(x => x.CreatedDate).FirstOrDefault();
```

### Feature 3 — Constructor projections

Mark a constructor with `[Projectable]` to project a DTO entirely in SQL — no client-side mapping:

```csharp
// Dtos/OrderSummaryDto.cs
public OrderSummaryDto() { }   // required parameterless ctor (EFP0008 ensures its presence)

[Projectable]
public OrderSummaryDto(Order order)
{
    Id            = order.Id;
    UserName      = order.User.UserName;
    GrandTotal    = order.GrandTotal;      // other [Projectable] members are recursively inlined
    StatusName    = order.StatusDisplayName;
    PriorityLabel = order.PriorityLabel;
}

// Usage
dbContext.Orders.Select(o => new OrderSummaryDto(o));
```

### Feature 4 — Method overloads

Both overloads of `GetMostRecentOrderForUser` are independently supported; each generates its own expression class:

```csharp
[Projectable]
public static Order? GetMostRecentOrder(this User user) => …;

[Projectable]
public static Order? GetMostRecentOrderForUser(this User user, bool includeUnfulfilled) => …;
```

### Feature 5 — Pattern matching (`switch`, `is`)

Switch expressions are rewritten into SQL `CASE WHEN` expressions:

```csharp
[Projectable]
public string PriorityLabel => GrandTotal switch
{
    >= 100m => "High",
    >= 30m  => "Medium",
    _       => "Low",
};
```

Generated SQL:
```sql
CASE WHEN GrandTotal >= 100 THEN 'High'
     WHEN GrandTotal >= 30  THEN 'Medium'
     ELSE 'Low' END
```

### Feature 6 — Block-bodied members (experimental)

`if`/`else` block bodies are converted to ternary expressions, producing identical SQL to a switch expression.
`AllowBlockBody = true` acknowledges the experimental nature and suppresses warning **EFP0001**:

```csharp
[Projectable(AllowBlockBody = true)]
public string GetShippingCategory()
{
    if (GrandTotal >= 100m)
        return "Express";
    else if (GrandTotal >= 30m)
        return "Standard";
    else
        return "Economy";
}
```

### Feature 7 — Null-conditional rewriting

`Supplier?.Name` uses the null-conditional operator, which cannot be expressed in an `Expression<T>` directly.
`NullConditionalRewriteSupport.Ignore` strips the `?.` — EF Core handles nullability via a `LEFT JOIN`:

```csharp
// Entities/Product.cs
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
public string? SupplierName => Supplier?.Name;
```

Generated SQL:
```sql
SELECT p.Name, s.Name AS SupplierName
FROM Products p
LEFT JOIN Suppliers s ON p.SupplierId = s.Id
```

> Use `NullConditionalRewriteSupport.Rewrite` for explicit `CASE WHEN NULL` guards (safer for Cosmos DB).

### Feature 8 — Enum method expansion

`GetDisplayName()` is a plain C# method — not `[Projectable]`. With `ExpandEnumMethods = true`, the generator
evaluates it **at compile time** for every enum value and bakes the results into a SQL `CASE` expression.
The method never runs at query time:

```csharp
// Entities/OrderStatus.cs
public static string GetDisplayName(this OrderStatus status) => status switch
{
    OrderStatus.Pending   => "Pending Review",
    OrderStatus.Fulfilled => "Fulfilled",
    OrderStatus.Cancelled => "Cancelled",
    _                     => status.ToString(),
};

// Entities/Order.cs
[Projectable(ExpandEnumMethods = true)]
public string StatusDisplayName => Status.GetDisplayName();
```

Generated SQL:
```sql
CASE WHEN Status = 0 THEN 'Pending Review'
     WHEN Status = 1 THEN 'Fulfilled'
     WHEN Status = 2 THEN 'Cancelled' END
```

### Feature 9 — `UseMemberBody`

`UseMemberBody` replaces the annotated member's expression source with another member's body.
Useful when the public member has a different in-memory implementation but you want a clean SQL expression:

```csharp
// Private EF-compatible expression
private bool IsHighValueOrderImpl => GrandTotal >= 50m;

// The generator uses IsHighValueOrderImpl's body — the own body is ignored
[Projectable(UseMemberBody = nameof(IsHighValueOrderImpl))]
public bool IsHighValueOrder => IsHighValueOrderImpl;
```

### Feature 10 — Compatibility mode

Configured in `ApplicationDbContext.OnConfiguring`:

```csharp
// Full (default) — expands every query on each invocation; maximum compatibility
optionsBuilder.UseProjectables();

// Limited — expands once then caches; better performance for repeated queries
optionsBuilder.UseProjectables(p => p.CompatibilityMode(CompatibilityMode.Limited));
```

| Mode      | Expansion timing          | Query cache | Performance         |
|-----------|---------------------------|-------------|---------------------|
| `Full`    | Every invocation          | Per query   | Baseline            |
| `Limited` | First invocation, cached  | Reused      | ✅ Often faster than vanilla EF |

### Feature 11 — Roslyn analyzers & code fixes (EFP0001–EFP0012)

Compile-time only — not demonstrated at runtime. Diagnostics are reported directly in the IDE:

| Code       | When triggered                                          | Fix available                    |
|------------|---------------------------------------------------------|----------------------------------|
| `EFP0001`  | Block-bodied member without `AllowBlockBody = true`     | Add `AllowBlockBody = true`      |
| `EFP0002`  | `?.` used without configuring `NullConditionalRewriteSupport` | Choose Ignore or Rewrite  |
| `EFP0008`  | DTO class missing parameterless constructor             | Insert parameterless constructor |
| `EFP0012`  | Factory method can be a constructor                     | Convert to `[Projectable]` ctor  |

See the [Diagnostics Reference](https://efnext.github.io/reference/diagnostics) for the full list.

---

## How it works

1. The **Roslyn Source Generator** (`EntityFrameworkCore.Projectables.Generator`) inspects every `[Projectable]`-annotated member at compile time and emits a companion `Expression<TDelegate>` property.
2. The **runtime interceptor** (`UseProjectables()`) hooks into EF Core's query compilation pipeline and substitutes those expression trees in place of the annotated member calls before SQL translation.

The final SQL contains each member's body **inlined directly** — no C# method calls at runtime, no client-side evaluation, no N+1.

---

## Environment

| Setting          | Value                                        |
|------------------|----------------------------------------------|
| .NET TFM         | `net10.0`                                    |
| C# language      | 14.0                                         |
| Database         | SQLite (`ReadmeSample.db`, local file)       |
| EF Core provider | `Microsoft.EntityFrameworkCore.Sqlite` 10.x  |
| Nullable         | enabled                                      |

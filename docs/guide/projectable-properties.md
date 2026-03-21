# Projectable Properties

Projectable properties let you define computed values on your entities using standard C# expression-bodied properties, and have those computations automatically translated into SQL when used in LINQ queries.

## Defining a Projectable Property

Add `[Projectable]` to any **expression-bodied property**:

```csharp
using EntityFrameworkCore.Projectables;

public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }

    [Projectable]
    public string FullName => FirstName + " " + LastName;
}
```

## Using Projectable Properties in Queries

Once defined, projectable properties can be used in **any part of a LINQ query**:

### In `Select`

```csharp
var names = dbContext.Users
    .Select(u => u.FullName)
    .ToList();
```

Generated SQL (SQLite):
```sql
SELECT (COALESCE("u"."FirstName", '') || ' ') || COALESCE("u"."LastName", '')
FROM "Users" AS "u"
```

### In `Where`

```csharp
var users = dbContext.Users
    .Where(u => u.FullName.Contains("Jon"))
    .ToList();
```

### In `GroupBy`

```csharp
var grouped = dbContext.Users
    .GroupBy(u => u.FullName)
    .Select(g => new { Name = g.Key, Count = g.Count() })
    .ToList();
```

### In `OrderBy`

```csharp
var sorted = dbContext.Users
    .OrderBy(u => u.FullName)
    .ToList();
```

### In multiple clauses at once

```csharp
var query = dbContext.Users
    .Where(u => u.FullName.Contains("Jon"))
    .GroupBy(u => u.FullName)
    .OrderBy(u => u.Key)
    .Select(u => u.Key);
```

Generated SQL (SQLite):
```sql
SELECT (COALESCE("u"."FirstName", '') || ' ') || COALESCE("u"."LastName", '')
FROM "Users" AS "u"
WHERE ('Jon' = '') OR (instr((COALESCE("u"."FirstName", '') || ' ') || COALESCE("u"."LastName", ''), 'Jon') > 0)
GROUP BY (COALESCE("u"."FirstName", '') || ' ') || COALESCE("u"."LastName", '')
ORDER BY (COALESCE("u"."FirstName", '') || ' ') || COALESCE("u"."LastName", '')
```

## Composing Projectable Properties

Projectable properties can reference **other projectable properties**. The entire chain is expanded into the final SQL:

```csharp
public class Order
{
    public decimal TaxRate { get; set; }
    public ICollection<OrderItem> Items { get; set; }

    [Projectable] public decimal Subtotal => Items.Sum(item => item.Product.ListPrice * item.Quantity);
    [Projectable] public decimal Tax => Subtotal * TaxRate;        // uses Subtotal
    [Projectable] public decimal GrandTotal => Subtotal + Tax;     // uses Subtotal + Tax
}
```

All three properties are inlined transitively in the generated SQL.

## Block-Bodied Properties (Experimental)

In addition to expression-bodied properties (`=>`), you can use **block-bodied properties** with `AllowBlockBody = true`:

```csharp
[Projectable(AllowBlockBody = true)]
public string Category
{
    get
    {
        if (Score > 90)
            return "Excellent";
        else if (Score > 70)
            return "Good";
        else
            return "Average";
    }
}
```

See [Block-Bodied Members](/advanced/block-bodied-members) for the full feature documentation.

## Important Rules

- The property **must be expression-bodied** (using `=>`) unless `AllowBlockBody = true` is set.
- The expression must be translatable by EF Core — it can only use members that EF Core understands (mapped columns, navigation properties, and other `[Projectable]` members).
- Properties **cannot be overloaded** — each property name must be unique within its type.
- The property body has access to `this` (the entity instance) and its navigation properties.

## Nullable Properties

If your expression uses the null-conditional operator (`?.`), you need to configure `NullConditionalRewriteSupport`. See [Null-Conditional Rewrite](/reference/null-conditional-rewrite) for details.


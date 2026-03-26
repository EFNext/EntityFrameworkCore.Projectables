# Scoring and Classification with Pattern Matching

This recipe shows how to use C# pattern matching — `switch` expressions and `is` patterns — inside `[Projectable]` members to compute scores, grades, tiers, and labels directly in SQL.

## Grading with Relational Patterns

Classic grading logic maps numeric ranges to labels. Pattern matching makes this readable and the generator translates it to a SQL `CASE` expression:

```csharp
public class Student
{
    public int Score { get; set; }

    [Projectable]
    public string Grade => Score switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        _     => "F"
    };

    [Projectable]
    public bool IsPassing => Score >= 60;

    [Projectable]
    public bool IsHonors => Score >= 90;
}
```

Generated SQL:
```sql
SELECT CASE
    WHEN [s].[Score] >= 90 THEN N'A'
    WHEN [s].[Score] >= 80 THEN N'B'
    WHEN [s].[Score] >= 70 THEN N'C'
    WHEN [s].[Score] >= 60 THEN N'D'
    ELSE N'F'
END AS [Grade]
FROM [Students] AS [s]
```

## Customer Tiers with `and` Patterns

Use `and` to express range bands cleanly:

```csharp
public class Customer
{
    public int LifetimeOrderCount { get; set; }
    public decimal LifetimeSpend { get; set; }

    [Projectable]
    public string Tier => LifetimeSpend switch
    {
        >= 10_000              => "Platinum",
        >= 5_000 and < 10_000  => "Gold",
        >= 1_000 and < 5_000   => "Silver",
        _                      => "Bronze"
    };

    [Projectable]
    public bool IsLoyalty => LifetimeOrderCount >= 10;
}
```

```csharp
// Segment customers for a marketing campaign
var segments = dbContext.Customers
    .GroupBy(c => c.Tier)
    .Select(g => new { Tier = g.Key, Count = g.Count() })
    .ToList();
```

## Risk Scoring with Guards

Use `when` guards for conditions that can't be expressed with a pattern alone:

```csharp
public class Loan
{
    public int CreditScore { get; set; }
    public decimal DebtToIncomeRatio { get; set; }

    [Projectable]
    public string RiskCategory => CreditScore switch
    {
        >= 750 when DebtToIncomeRatio < 0.3m  => "Low",
        >= 700                                 => "Medium",
        >= 600                                 => "High",
        _                                      => "Very High"
    };
}
```

## `is` Patterns for Boolean Flags

Use `is` patterns for concise boolean properties:

```csharp
public class Product
{
    public int Stock { get; set; }
    public decimal Price { get; set; }
    public int ReorderPoint { get; set; }

    [Projectable]
    public bool IsInStock => Stock is > 0;

    [Projectable]
    public bool NeedsReorder => Stock is >= 0 and <= ReorderPoint;

    [Projectable]
    public bool IsBudget => Price is > 0 and < 25;

    [Projectable]
    public bool HasNoStock => Stock is 0;
}
```

## Combining Classification with Aggregation

Compose projectable properties to build richer query results:

```csharp
public class Order
{
    public decimal GrandTotal { get; set; }
    public DateTime CreatedDate { get; set; }

    [Projectable]
    public string ValueBand => GrandTotal switch
    {
        >= 1000 => "High",
        >= 250  => "Medium",
        _       => "Low"
    };

    [Projectable]
    public bool IsRecent => CreatedDate >= DateTime.UtcNow.AddDays(-30);
}
```

```csharp
// Breakdown of recent orders by value band
var breakdown = dbContext.Orders
    .Where(o => o.IsRecent)
    .GroupBy(o => o.ValueBand)
    .Select(g => new
    {
        Band = g.Key,
        Count = g.Count(),
        Total = g.Sum(o => o.GrandTotal)
    })
    .OrderBy(x => x.Band)
    .ToList();
```

## Property Patterns for Multi-Field Classification

Use property patterns to match on multiple fields simultaneously:

```csharp
public class Employee
{
    public int YearsOfService { get; set; }
    public string Department { get; set; }

    [Projectable]
    public static bool IsEligibleForBonus(this Employee e) =>
        e is { YearsOfService: >= 2, Department: "Sales" or "Engineering" };
}
```

## Tips

- **Use `_` as the default arm** — always include a discard arm to avoid generating a ternary chain with no final fallback.
- **Keep arms ordered from most to least specific** — the generator emits a ternary chain in arm order; put the most restrictive cases first.
- **Avoid positional patterns** — deconstruct patterns (`(x, y) =>`) are not supported (EFP0007). Use property patterns (`{ X: x, Y: y }`) instead.
- **Compose with filters** — classification properties work perfectly in `Where`, `GroupBy`, and `OrderBy` just like any other projectable.

See also: [Pattern Matching reference](/reference/pattern-matching), [Diagnostics EFP0007](/reference/diagnostics#efp0007).


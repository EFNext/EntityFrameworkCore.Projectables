# Collection Aggregates

This recipe shows how to expose reusable aggregation computations — counts, sums, averages, and existence checks on navigation collections — as `[Projectable]` properties that translate to efficient SQL subqueries.

## The Pattern

Define aggregate properties directly on the entity. EF Core translates them to correlated subqueries or `COUNT`/`SUM` expressions inline in the outer query.

## Counts

```csharp
public class Customer
{
    public ICollection<Order> Orders { get; set; }
    public ICollection<Review> Reviews { get; set; }

    [Projectable]
    public int OrderCount => Orders.Count();

    [Projectable]
    public int CompletedOrderCount => Orders.Count(o => o.CompletedDate != null);

    [Projectable]
    public int ReviewCount => Reviews.Count();
}
```

```csharp
var summary = dbContext.Customers
    .Select(c => new
    {
        c.Id,
        c.OrderCount,
        c.CompletedOrderCount,
        c.ReviewCount
    })
    .ToList();
```

Generated SQL (simplified):
```sql
SELECT
    [c].[Id],
    (SELECT COUNT(*) FROM [Orders] WHERE [CustomerId] = [c].[Id]) AS [OrderCount],
    (SELECT COUNT(*) FROM [Orders] WHERE [CustomerId] = [c].[Id] AND [CompletedDate] IS NOT NULL) AS [CompletedOrderCount],
    (SELECT COUNT(*) FROM [Reviews] WHERE [CustomerId] = [c].[Id]) AS [ReviewCount]
FROM [Customers] AS [c]
```

## Sums and Totals

```csharp
public class Order
{
    public ICollection<OrderItem> Items { get; set; }
    public decimal TaxRate { get; set; }

    [Projectable]
    public decimal Subtotal => Items.Sum(i => i.UnitPrice * i.Quantity);

    [Projectable]
    public decimal TaxAmount => Subtotal * TaxRate;

    [Projectable]
    public decimal GrandTotal => Subtotal + TaxAmount;

    [Projectable]
    public int TotalUnits => Items.Sum(i => i.Quantity);
}
```

Projectable aggregates compose naturally:

```csharp
// Sort by computed total — no data fetched to memory
var topOrders = dbContext.Orders
    .OrderByDescending(o => o.GrandTotal)
    .Take(10)
    .Select(o => new { o.Id, o.Subtotal, o.TaxAmount, o.GrandTotal })
    .ToList();
```

## Existence Checks

Boolean `Any()` checks are useful as filter predicates:

```csharp
public class Customer
{
    public ICollection<Order> Orders { get; set; }
    public ICollection<SupportTicket> SupportTickets { get; set; }

    [Projectable]
    public bool HasOrders => Orders.Any();

    [Projectable]
    public bool HasOpenTickets => SupportTickets.Any(t => t.ResolvedDate == null);

    [Projectable]
    public bool HasRecentOrder =>
        Orders.Any(o => o.CreatedDate >= DateTime.UtcNow.AddDays(-30));
}
```

```csharp
// Customers who have ordered recently but also have open support tickets
var atRisk = dbContext.Customers
    .Where(c => c.HasRecentOrder && c.HasOpenTickets)
    .ToList();
```

## Averages

```csharp
public class Product
{
    public ICollection<Review> Reviews { get; set; }

    [Projectable]
    public double? AverageRating => Reviews.Any()
        ? Reviews.Average(r => (double)r.Rating)
        : null;

    [Projectable]
    public int ReviewCount => Reviews.Count();
}
```

## Min / Max

```csharp
public class Customer
{
    public ICollection<Order> Orders { get; set; }

    [Projectable]
    public DateTime? FirstOrderDate => Orders.Any()
        ? Orders.Min(o => o.CreatedDate)
        : null;

    [Projectable]
    public DateTime? LastOrderDate => Orders.Any()
        ? Orders.Max(o => o.CreatedDate)
        : null;

    [Projectable]
    public decimal? LargestOrderTotal => Orders.Any()
        ? Orders.Max(o => o.GrandTotal)
        : null;
}
```

## Combining with Filters

Aggregate properties work in `Where`, `OrderBy`, and `GroupBy`:

```csharp
// High-value customers (> 5 orders AND lifetime spend > $1000)
var highValue = dbContext.Customers
    .Where(c => c.OrderCount > 5 && c.LifetimeSpend > 1000)
    .OrderByDescending(c => c.LifetimeSpend)
    .ToList();

// Tier distribution report
var tiers = dbContext.Customers
    .GroupBy(c => c.OrderCount switch
    {
        0      => "No Orders",
        1      => "First Order",
        <= 5   => "Occasional",
        <= 20  => "Regular",
        _      => "VIP"
    })
    .Select(g => new { Tier = g.Key, Count = g.Count() })
    .ToList();
```

## Conditional Aggregates

Add `if`/`else` logic inside a block-bodied projectable to conditionally return aggregates:

```csharp
public class Supplier
{
    public bool IsPreferred { get; set; }
    public ICollection<Product> Products { get; set; }

    [Projectable(AllowBlockBody = true)]
    public decimal TotalStockValue
    {
        get
        {
            if (IsPreferred)
                return Products.Sum(p => p.StockQuantity * p.PreferredPrice);
            else
                return Products.Sum(p => p.StockQuantity * p.ListPrice);
        }
    }
}
```

## Tips

- **Avoid N+1** — aggregate projectables work best when used directly in a `Select` or `Where` at the top-level query. Accessing them inside nested `Select` on a collection may generate additional round-trips.
- **Guard nullable aggregates** — wrap `Min`, `Max`, `Average` in an `Any()` check or use `?? default` to avoid null-reference issues in the generated SQL.
- **Compose freely** — `GrandTotal` depending on `Subtotal` is a first-class pattern. The generator inlines transitively.
- **Use Limited mode** — aggregate projections in repeated queries benefit from [Limited compatibility mode](/reference/compatibility-mode) caching.

See also: [Computed Entity Properties](/recipes/computed-properties), [Reusable Query Filters](/recipes/reusable-query-filters).


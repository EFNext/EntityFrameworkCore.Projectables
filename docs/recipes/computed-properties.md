# Computed Entity Properties

This recipe shows how to define reusable computed properties on your entities and use them across multiple query operations — all translated to SQL without any duplication.

## The Pattern

Define computed values as `[Projectable]` properties directly on your entity. These properties can then be used in `Select`, `Where`, `GroupBy`, `OrderBy`, and any combination thereof.

## Example: Order Totals

```csharp
public class Order
{
    public int Id { get; set; }
    public decimal TaxRate { get; set; }
    public DateTime CreatedDate { get; set; }
    public ICollection<OrderItem> Items { get; set; }

    // Building blocks
    [Projectable]
    public decimal Subtotal => Items.Sum(item => item.Product.ListPrice * item.Quantity);

    [Projectable]
    public decimal Tax => Subtotal * TaxRate;

    // Composed from other projectables
    [Projectable]
    public decimal GrandTotal => Subtotal + Tax;
}
```

### Use in Select

```csharp
var summaries = dbContext.Orders
    .Select(o => new OrderSummaryDto
    {
        Id = o.Id,
        Subtotal = o.Subtotal,   // ✅ Inlined into SQL
        Tax = o.Tax,             // ✅ Inlined into SQL
        GrandTotal = o.GrandTotal // ✅ Inlined into SQL
    })
    .ToList();
```

### Use in Where

```csharp
// Only load high-value orders
var highValue = dbContext.Orders
    .Where(o => o.GrandTotal > 1000)
    .ToList();
```

### Use in OrderBy

```csharp
// Sort by computed value
var ranked = dbContext.Orders
    .OrderByDescending(o => o.GrandTotal)
    .Take(10)
    .ToList();
```

### All Together

```csharp
var report = dbContext.Orders
    .Where(o => o.GrandTotal > 500)
    .OrderByDescending(o => o.GrandTotal)
    .GroupBy(o => o.CreatedDate.Year)
    .Select(g => new
    {
        Year = g.Key,
        Count = g.Count(),
        TotalRevenue = g.Sum(o => o.GrandTotal)
    })
    .ToList();
```

All computed values are evaluated **in the database** — no data is fetched to memory for filtering or aggregation.

## Example: User Profile

```csharp
public class User
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public DateTime BirthDate { get; set; }
    public DateTime? LastLoginDate { get; set; }

    [Projectable]
    public string FullName => FirstName + " " + LastName;

    [Projectable]
    public int Age => DateTime.Today.Year - BirthDate.Year
        - (DateTime.Today.DayOfYear < BirthDate.DayOfYear ? 1 : 0);

    [Projectable]
    public bool IsActive => LastLoginDate != null
        && LastLoginDate >= DateTime.UtcNow.AddDays(-30);
}
```

```csharp
// Find active adult users, sorted by name
var results = dbContext.Users
    .Where(u => u.IsActive && u.Age >= 18)
    .OrderBy(u => u.FullName)
    .Select(u => new { u.FullName, u.Age })
    .ToList();
```

## Example: Product Catalog

```csharp
public class Product
{
    public decimal ListPrice { get; set; }
    public decimal DiscountRate { get; set; }
    public int StockQuantity { get; set; }
    public int ReorderPoint { get; set; }

    [Projectable]
    public decimal SalePrice => ListPrice * (1 - DiscountRate);

    [Projectable]
    public decimal SavingsAmount => ListPrice - SalePrice;

    [Projectable]
    public bool NeedsReorder => StockQuantity <= ReorderPoint;
}
```

```csharp
// Products on sale that need restocking
var reorder = dbContext.Products
    .Where(p => p.NeedsReorder && p.SalePrice < 50)
    .OrderBy(p => p.StockQuantity)
    .Select(p => new
    {
        p.Id,
        p.SalePrice,
        p.SavingsAmount,
        p.StockQuantity
    })
    .ToList();
```

## Tips

- **Compose freely** — projectables can call other projectables. Build from simple to complex.
- **Use Limited mode** in production for repeated queries — computed properties are cached after the first execution.
- **Keep it pure** — projectable properties should be pure computations (no side effects). Everything must be translatable to SQL.
- **Avoid N+1** — if a projectable property references navigation properties, make sure to structure your queries so EF Core can generate a single efficient query.


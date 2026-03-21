# Reusable Query Filters

This recipe shows how to define reusable filtering logic as projectable extension methods or properties, and compose them across multiple queries without duplicating LINQ expressions.

## The Pattern

Define your filtering criteria as `[Projectable]` members that return `bool`. Use them in `Where()` clauses exactly as you would any other property. EF Core translates the expanded expression to a SQL `WHERE` clause.

## Example: Active Entity Filter

```csharp
public class User
{
    public bool IsDeleted { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public DateTime? EmailVerifiedDate { get; set; }

    [Projectable]
    public bool IsActive =>
        !IsDeleted
        && EmailVerifiedDate != null
        && LastLoginDate >= DateTime.UtcNow.AddDays(-90);
}
```

```csharp
// Reuse everywhere
var activeUsers = dbContext.Users.Where(u => u.IsActive).ToList();
var activeAdmins = dbContext.Users.Where(u => u.IsActive && u.IsAdmin).ToList();
var activeCount = dbContext.Users.Count(u => u.IsActive);
```

Generated SQL (simplified):
```sql
SELECT * FROM [Users]
WHERE [IsDeleted] = 0
  AND [EmailVerifiedDate] IS NOT NULL
  AND [LastLoginDate] >= DATEADD(day, -90, GETUTCDATE())
```

## Example: Parameterized Filter as Extension Method

Extension methods are ideal for filters that accept parameters:

```csharp
public static class OrderExtensions
{
    [Projectable]
    public static bool IsWithinDateRange(this Order order, DateTime from, DateTime to) =>
        order.CreatedDate >= from && order.CreatedDate <= to;

    [Projectable]
    public static bool IsHighValue(this Order order, decimal threshold) =>
        order.GrandTotal >= threshold;

    [Projectable]
    public static bool BelongsToRegion(this Order order, string region) =>
        order.ShippingAddress != null && order.ShippingAddress.Region == region;
}
```

```csharp
var from = DateTime.UtcNow.AddMonths(-1);
var to = DateTime.UtcNow;

var recentHighValueOrders = dbContext.Orders
    .Where(o => o.IsWithinDateRange(from, to))
    .Where(o => o.IsHighValue(500m))
    .ToList();
```

## Example: Composing Multiple Filters

Build complex filters by composing simpler ones:

```csharp
public class Order
{
    [Projectable]
    public bool IsFulfilled => FulfilledDate != null;

    [Projectable]
    public bool IsRecent => CreatedDate >= DateTime.UtcNow.AddDays(-30);

    // Composed from simpler projectables
    [Projectable]
    public bool IsRecentFulfilledOrder => IsFulfilled && IsRecent;
}

public static class OrderExtensions
{
    [Projectable]
    public static bool IsEligibleForReturn(this Order order) =>
        order.IsFulfilled
        && order.FulfilledDate >= DateTime.UtcNow.AddDays(-30)
        && !order.HasOpenReturnRequest;
}
```

## Example: Global Query Filters

Projectable properties work in EF Core's global query filters (configured in `OnModelCreating`):

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Soft-delete global filter using a projectable property
    modelBuilder.Entity<Order>()
        .HasQueryFilter(o => !o.IsDeleted);
    
    // Tenant isolation filter
    modelBuilder.Entity<Order>()
        .HasQueryFilter(o => o.TenantId == _currentTenantId);
}
```

::: info
When using global query filters with Projectables, ensure that `UseProjectables()` is configured on your `DbContext`. The library includes a convention (`ProjectablesExpandQueryFiltersConvention`) that ensures global filters referencing projectable members are also expanded correctly.
:::

## Example: Specification Pattern

Projectables pair naturally with the Specification pattern:

```csharp
public static class OrderSpecifications
{
    [Projectable]
    public static bool IsActive(this Order order) =>
        !order.IsCancelled && !order.IsDeleted;

    [Projectable]
    public static bool IsOverdue(this Order order) =>
        order.IsActive()
        && order.DueDate < DateTime.UtcNow
        && !order.IsFulfilled;

    [Projectable]
    public static bool RequiresAttention(this Order order) =>
        order.IsOverdue()
        || order.HasOpenDispute
        || order.PaymentStatus == PaymentStatus.Failed;
}
```

```csharp
// Dashboard: count orders requiring attention
var attentionCount = await dbContext.Orders
    .Where(o => o.RequiresAttention())
    .CountAsync();

// Alert users with overdue orders
var overdueUserIds = await dbContext.Orders
    .Where(o => o.IsOverdue())
    .Select(o => o.UserId)
    .Distinct()
    .ToListAsync();
```

## Tips

- **Keep filters pure** — filter projectables should only read data, never modify it.
- **Compose at the projectable level** — compose filters inside projectable members rather than chaining multiple `.Where()` calls for more reusable building blocks.
- **Name clearly** — use names that express business intent (`IsEligibleForRefund`) rather than technical details (`HasRefundDateNullAndStatusIsComplete`).
- **Prefer entity-level properties for entity-specific filters**, and extension methods for cross-entity or parameterized filters.
- **Use Limited mode** — parameterized filter methods are a perfect use case for [Limited compatibility mode](/reference/compatibility-mode), which caches the expanded query after the first execution.


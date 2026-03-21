# Enum Display Names in Queries

This recipe shows how to project human-readable labels from enum values — such as names from `[Display]` attributes — directly into SQL queries using `ExpandEnumMethods`.

## The Problem

You have an enum with display-friendly labels:

```csharp
public enum OrderStatus
{
    [Display(Name = "Pending Review")]
    Pending = 0,

    [Display(Name = "Approved & Processing")]
    Approved = 1,

    [Display(Name = "Rejected")]
    Rejected = 2,

    [Display(Name = "Shipped")]
    Shipped = 3
}
```

And a helper extension method:

```csharp
public static class OrderStatusExtensions
{
    public static string GetDisplayName(this OrderStatus status)
    {
        var field = typeof(OrderStatus).GetField(status.ToString());
        var attr = field?.GetCustomAttribute<DisplayAttribute>();
        return attr?.Name ?? status.ToString();
    }
}
```

The problem: `GetDisplayName` uses reflection — EF Core cannot translate this to SQL.

## The Solution with `ExpandEnumMethods`

Use `ExpandEnumMethods = true` on the projectable member that calls `GetDisplayName`:

```csharp
public class Order
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }

    [Projectable(ExpandEnumMethods = true)]
    public string StatusLabel => Status.GetDisplayName();
}
```

The source generator evaluates `GetDisplayName` for each enum value at **compile time** and bakes the results into the expression tree as string constants:

```csharp
// Generated expression equivalent:
Status == OrderStatus.Pending  ? "Pending Review"        :
Status == OrderStatus.Approved ? "Approved & Processing" :
Status == OrderStatus.Rejected ? "Rejected"              :
Status == OrderStatus.Shipped  ? "Shipped"               :
null
```

Which translates to:

```sql
SELECT CASE
    WHEN [o].[Status] = 0 THEN N'Pending Review'
    WHEN [o].[Status] = 1 THEN N'Approved & Processing'
    WHEN [o].[Status] = 2 THEN N'Rejected'
    WHEN [o].[Status] = 3 THEN N'Shipped'
END AS [StatusLabel]
FROM [Orders] AS [o]
```

## Using StatusLabel in Queries

```csharp
// Project enum labels into a DTO
var orders = dbContext.Orders
    .Select(o => new OrderDto
    {
        Id = o.Id,
        StatusLabel = o.StatusLabel   // Translated to CASE in SQL
    })
    .ToList();

// Group by display name
var statusCounts = dbContext.Orders
    .GroupBy(o => o.StatusLabel)
    .Select(g => new { Status = g.Key, Count = g.Count() })
    .ToList();

// Filter on the computed label (less efficient — prefer filtering on the enum value directly)
var pending = dbContext.Orders
    .Where(o => o.StatusLabel == "Pending Review")
    .ToList();
```

## Adding More Computed Properties

```csharp
public class Order
{
    public OrderStatus Status { get; set; }

    [Projectable(ExpandEnumMethods = true)]
    public string StatusLabel => Status.GetDisplayName();

    [Projectable(ExpandEnumMethods = true)]
    public bool IsProcessing => Status.IsInProgress();  // Custom bool extension

    [Projectable(ExpandEnumMethods = true)]
    public int StatusSortOrder => Status.GetSortOrder();
}

public static class OrderStatusExtensions
{
    public static string GetDisplayName(this OrderStatus status) { /* ... */ }

    public static bool IsInProgress(this OrderStatus status) =>
        status is OrderStatus.Approved or OrderStatus.Shipped;

    public static int GetSortOrder(this OrderStatus status) =>
        status switch {
            OrderStatus.Pending  => 1,
            OrderStatus.Approved => 2,
            OrderStatus.Shipped  => 3,
            OrderStatus.Rejected => 99,
            _                    => 0
        };
}
```

## Nullable Enum Properties

If the enum property is nullable, wrap the call in a null-conditional and configure the rewrite:

```csharp
public class Order
{
    public OrderStatus? OptionalStatus { get; set; }

    [Projectable(
        ExpandEnumMethods = true,
        NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
    public string? OptionalStatusLabel => OptionalStatus?.GetDisplayName();
}
```

## Enum on Navigation Property

```csharp
public class Order
{
    public Customer Customer { get; set; }

    [Projectable(ExpandEnumMethods = true)]
    public string CustomerTierLabel => Customer.Tier.GetDisplayName();
}
```

## Best Practices

- **Filter on the enum value** (not the label) for best SQL performance: `Where(o => o.Status == OrderStatus.Pending)`.
- **Use labels only for projection** (`Select`) — translating `WHERE StatusLabel = 'Pending Review'` is less efficient than `WHERE Status = 0`.
- If your enum changes frequently, regenerate — the display name values are baked in at compile time.


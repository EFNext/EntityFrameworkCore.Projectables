# Extension Methods

Projectable extension methods let you define query logic outside of your entity classes — useful for keeping entities clean, applying logic to types you don't own, or grouping related query helpers.

## Defining a Projectable Extension Method

Add `[Projectable]` to any extension method in a **static class**:

```csharp
using EntityFrameworkCore.Projectables;

public static class UserExtensions
{
    [Projectable]
    public static Order GetMostRecentOrder(this User user, DateTime? cutoffDate) =>
        user.Orders
            .Where(x => cutoffDate == null || x.CreatedDate >= cutoffDate)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefault();
}
```

## Using Extension Methods in Queries

```csharp
var result = dbContext.Users
    .Where(u => u.UserName == "Jon")
    .Select(u => new {
        GrandTotal = u.GetMostRecentOrder(DateTime.UtcNow.AddDays(-30)).GrandTotal
    })
    .FirstOrDefault();
```

The extension method is fully inlined — including any nested projectable members like `GrandTotal`.

## Extension Methods on Non-Entity Types

You don't need to restrict projectable extension methods to entity types. They work on **any type** that EF Core can work with in queries:

```csharp
// On int
public static class IntExtensions
{
    [Projectable]
    public static int Squared(this int i) => i * i;
}

// On string
public static class StringExtensions
{
    [Projectable]
    public static bool ContainsIgnoreCase(this string source, string value) =>
        source.ToLower().Contains(value.ToLower());
}
```

Usage in queries:

```csharp
var squaredScores = dbContext.Players
    .Select(p => new { p.Name, SquaredScore = p.Score.Squared() })
    .ToList();

var results = dbContext.Products
    .Where(p => p.Name.ContainsIgnoreCase("widget"))
    .ToList();
```

## Extension Methods with Multiple Parameters

```csharp
public static class OrderExtensions
{
    [Projectable]
    public static bool IsHighValueOrder(this Order order, decimal threshold, bool includeTax = false) =>
        (includeTax ? order.GrandTotal : order.Subtotal) > threshold;
}

var highValue = dbContext.Orders
    .Where(o => o.IsHighValueOrder(500, includeTax: true))
    .ToList();
```

## Chaining Extension Methods

Extension methods can call other projectable members (properties, methods, or other extension methods):

```csharp
public static class UserExtensions
{
    [Projectable]
    public static decimal TotalSpentThisMonth(this User user) =>
        user.Orders
            .Where(o => o.CreatedDate.Month == DateTime.UtcNow.Month)
            .Sum(o => o.GrandTotal);  // GrandTotal is [Projectable] on Order

    [Projectable]
    public static bool IsVipCustomer(this User user) =>
        user.TotalSpentThisMonth() > 1000;  // Calls another [Projectable] extension
}
```

## Extension Methods on Nullable Types

Extension methods on nullable entity types work naturally:

```csharp
public static class UserExtensions
{
    [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
    public static string GetFullAddress(this User? user) =>
        user?.Location?.AddressLine1 + " " + user?.Location?.AddressLine2;
}
```

See [Null-Conditional Rewrite](/reference/null-conditional-rewrite) for details on handling nullable navigation.

## Important Rules

- Extension methods **must be in a static class**.
- The `this` parameter represents the entity instance in the generated expression.
- **Method overloading is not supported** — each method name must be unique within its declaring static class.
- Default parameter values are supported but the caller must explicitly provide all arguments in LINQ queries (EF Core does not support optional parameters in expression trees).


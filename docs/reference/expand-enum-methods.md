# Expand Enum Methods

The `ExpandEnumMethods` option allows you to call ordinary C# methods on enum values inside a projectable member and have those calls translated to SQL `CASE` expressions. Without this option, calling a non-projectable method on an enum value would fail SQL translation.

## The Problem

You have an enum with a helper method:

```csharp
public enum OrderStatus { Pending, Approved, Rejected }

public static class OrderStatusExtensions
{
    public static string GetDisplayName(this OrderStatus status) =>
        status switch {
            OrderStatus.Pending  => "Pending Review",
            OrderStatus.Approved => "Approved",
            OrderStatus.Rejected => "Rejected",
            _                    => status.ToString()
        };
}
```

If you try to use `GetDisplayName()` inside a projectable member without `ExpandEnumMethods`, the generator cannot produce a valid expression tree because `GetDisplayName` is not a database function.

## The Solution

Set `ExpandEnumMethods = true` on the projectable member that calls the enum method:

```csharp
public class Order
{
    public OrderStatus Status { get; set; }

    [Projectable(ExpandEnumMethods = true)]
    public string StatusName => Status.GetDisplayName();
}
```

The generator enumerates all values of `OrderStatus` and produces a chain of ternary expressions:

```csharp
// Generated expression equivalent
Status == OrderStatus.Pending  ? GetDisplayName(OrderStatus.Pending)  :
Status == OrderStatus.Approved ? GetDisplayName(OrderStatus.Approved) :
Status == OrderStatus.Rejected ? GetDisplayName(OrderStatus.Rejected) :
null
```

EF Core then translates this to a SQL `CASE` expression:

```sql
SELECT CASE
    WHEN [o].[Status] = 0 THEN N'Pending Review'
    WHEN [o].[Status] = 1 THEN N'Approved'
    WHEN [o].[Status] = 2 THEN N'Rejected'
END AS [StatusName]
FROM [Orders] AS [o]
```

## Supported Return Types

| Return type       | Default fallback value    |
|-------------------|---------------------------|
| `string`          | `null`                    |
| `bool`            | `default(bool)` → `false` |
| `int`             | `default(int)` → `0`      |
| Other value types | `default(T)`              |
| Nullable types    | `null`                    |

## Examples

### Boolean Return

```csharp
public static bool IsApproved(this OrderStatus status) =>
    status == OrderStatus.Approved;

[Projectable(ExpandEnumMethods = true)]
public bool IsStatusApproved => Status.IsApproved();
```

Generated SQL:
```sql
SELECT CASE
    WHEN [o].[Status] = 0 THEN CAST(0 AS bit)
    WHEN [o].[Status] = 1 THEN CAST(1 AS bit)
    WHEN [o].[Status] = 2 THEN CAST(0 AS bit)
    ELSE CAST(0 AS bit)
END AS [IsStatusApproved]
FROM [Orders] AS [o]
```

### Integer Return

```csharp
public static int GetSortOrder(this OrderStatus status) => (int)status;

[Projectable(ExpandEnumMethods = true)]
public int StatusSortOrder => Status.GetSortOrder();
```

Generated SQL:
```sql
SELECT CASE
    WHEN [o].[Status] = 0 THEN 0
    WHEN [o].[Status] = 1 THEN 1
    WHEN [o].[Status] = 2 THEN 2
    ELSE 0
END AS [StatusSortOrder]
FROM [Orders] AS [o]
```

### Methods with Additional Parameters

Additional parameters are passed through to each branch of the expanded ternary:

```csharp
public static string Format(this OrderStatus status, string prefix) =>
    prefix + status.ToString();

[Projectable(ExpandEnumMethods = true)]
public string FormattedStatus => Status.Format("Status: ");
```

### Nullable Enum Types

If the enum property is nullable, the expansion is wrapped in a null check:

```csharp
public class Order
{
    public OrderStatus? OptionalStatus { get; set; }

    [Projectable(ExpandEnumMethods = true)]
    public string? OptionalStatusName => OptionalStatus?.GetDisplayName();
}
```

### Enum on Navigation Properties

```csharp
public class Order
{
    public Customer Customer { get; set; }

    [Projectable(ExpandEnumMethods = true)]
    public string CustomerTierName => Customer.Tier.GetDisplayName();
}
```

## Limitations

- The method being expanded **must be deterministic** — it will be evaluated at code-generation time for each enum value.
- All enum values must produce valid SQL-translatable results.
- The enum type must be known at compile time (no dynamic enum types).
- Only the outermost enum method call on the enum property is expanded; nested calls may require multiple projectable members.

## Comparison with `[Projectable]` on the Extension Method

You might wonder: why not just put `[Projectable]` on `GetDisplayName` itself?

| Approach                                | When to use                                                                                                             |
|-----------------------------------------|-------------------------------------------------------------------------------------------------------------------------|
| `[Projectable]` on the extension method | The method body is a simple expression EF Core can translate (e.g., `== OrderStatus.Approved`).                         |
| `ExpandEnumMethods = true`              | The method body is complex or references non-EF-translatable code (e.g., reads a `[Display]` attribute via reflection). |

`ExpandEnumMethods` evaluates the method at **compile time** for each enum value and bakes the results into the expression tree, so the method body doesn't need to be translatable at all.


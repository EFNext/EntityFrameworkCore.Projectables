# Nullable Navigation Properties

This recipe covers how to work with optional (nullable) navigation properties in projectable members, using `NullConditionalRewriteSupport` to safely handle `?.` operators.

## The Challenge

Navigation properties can be nullable — either because the relationship is optional, or because the related entity isn't loaded. Using `?.` in a projectable body without configuration produces **error EFP0002**, because expression trees cannot represent the null-conditional operator directly.

## Choosing a Strategy

| Strategy          | Best For                                                                             |
|-------------------|--------------------------------------------------------------------------------------|
| `Ignore`          | SQL Server / databases with implicit null propagation; navigation is usually present |
| `Rewrite`         | Cosmos DB; client-side evaluation scenarios; maximum correctness                     |
| Manual null check | Complex multi-level nullable chains where you want full control                      |

## Strategy 1: `Ignore`

Strips the `?.` — `A?.B` becomes `A.B`. In SQL, NULL propagates implicitly in most expressions.

```csharp
public class User
{
    public Address? Address { get; set; }

    [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
    public string? CityName => Address?.City;
}
```

Generated expression: `Address.City`

Generated SQL (SQL Server):
```sql
SELECT [a].[City]
FROM [Users] AS [u]
LEFT JOIN [Addresses] AS [a] ON [u].[AddressId] = [a].[Id]
```

If `Address` is `NULL`, SQL returns `NULL` for `City` — which matches the expected C# behavior.

**Use when:** You're on SQL Server (or a database with implicit null propagation), and you're confident that `NULL` will propagate correctly for your use case.

## Strategy 2: `Rewrite`

Rewrites `A?.B` as `A != null ? A.B : null` — generates explicit null checks in the expression.

```csharp
public class User
{
    public Address? Address { get; set; }

    [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
    public string? CityName => Address?.City;
}
```

Generated expression: `Address != null ? Address.City : null`

Generated SQL (SQL Server):
```sql
SELECT CASE WHEN [a].[Id] IS NOT NULL THEN [a].[City] END
FROM [Users] AS [u]
LEFT JOIN [Addresses] AS [a] ON [u].[AddressId] = [a].[Id]
```

**Use when:** You need explicit null handling, you're targeting Cosmos DB, or you want maximum semantic equivalence to C# code.

## Multi-Level Nullable Chains

For deeply nested nullable navigation:

```csharp
public class User
{
    public Address? Address { get; set; }
}

public class Address
{
    public City? City { get; set; }
}

public class City
{
    public string? PostalCode { get; set; }
}

// Ignore: strips all ?.
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
public string? PostalCode => Address?.City?.PostalCode;
// → Address.City.PostalCode

// Rewrite: explicit null check at each level
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
public string? PostalCode => Address?.City?.PostalCode;
// → Address != null
//     ? Address.City != null
//       ? Address.City.PostalCode
//       : null
//     : null
```

## Strategy 3: Manual Null Checks

For maximum control, write the null check explicitly — no `NullConditionalRewriteSupport` needed:

```csharp
[Projectable]
public string? CityName =>
    Address != null ? Address.City : null;

[Projectable]
public string? PostalCode =>
    Address != null && Address.City != null
        ? Address.City.PostalCode
        : null;
```

This approach is verbose but gives you precise control over the generated expression.

## Extension Methods on Nullable Entity Parameters

When an extension method's `this` parameter is nullable:

```csharp
public static class UserExtensions
{
    [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
    public static string? GetFullAddress(this User? user) =>
        user?.Address?.AddressLine1 + ", " + user?.Address?.City;
}
```

## Combining with Other Options

Null-conditional rewrite is compatible with other `[Projectable]` options:

```csharp
[Projectable(
    NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore,
    ExpandEnumMethods = true)]
public string? ShippingStatusLabel =>
    ShippingInfo?.Status.GetDisplayName();
```

## Practical Recommendation

```
Is the property on SQL Server?
  → Yes, and null propagation is acceptable: use Ignore (simpler SQL)
  → Yes, but you need explicit null behavior: use Rewrite
  → No (Cosmos DB, in-memory, or client-side eval): use Rewrite or manual check
```

::: tip
Start with `Ignore` for SQL Server projects. Switch to `Rewrite` if you observe unexpected nullability behavior in query results.
:::


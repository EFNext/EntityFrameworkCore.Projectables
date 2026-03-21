# Null-Conditional Rewrite

Expression trees — the representation EF Core uses internally — cannot directly express the null-conditional operator (`?.`). If your projectable member contains `?.`, the source generator needs to know how to handle it.

## The Problem

Consider this projectable property:

```csharp
[Projectable]
public string? FullAddress =>
    Location?.AddressLine1 + " " + Location?.City;
```

This is valid C# code, but it **cannot be converted to an expression tree as-is**. The null-conditional operator is syntactic sugar that cannot be represented directly in an `Expression<Func<T, TResult>>`.

By default (with `NullConditionalRewriteSupport.None`), the generator will report **error EFP0002** and refuse to generate code.

## The `NullConditionalRewriteSupport` Options

Configure the strategy on the `[Projectable]` attribute:

```csharp
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
```

---

### `None` (Default)

```csharp
[Projectable]  // NullConditionalRewriteSupport.None is the default
public string? FullAddress => Location?.AddressLine1 + " " + Location?.City;
```

The generator **rejects** any use of `?.`. This produces error EFP0002:

```
error EFP0002: 'FullAddress' has a null-conditional expression exposed but is not configured 
to rewrite this (Consider configuring a strategy using the NullConditionalRewriteSupport 
property on the Projectable attribute)
```

**Use when:** Your projectable members never use `?.` — this is the safest default that prevents accidental misuse.

---

### `Ignore`

```csharp
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
public string? FullAddress => Location?.AddressLine1 + " " + Location?.City;
```

The null-conditional operators are **stripped** — `A?.B` becomes `A.B`.

Generated expression is equivalent to:
```csharp
Location.AddressLine1 + " " + Location.City
```

**Behavior in SQL:** The result is `NULL` if any operand is `NULL`, because SQL's null propagation works implicitly in most expressions. This is consistent with how most SQL databases handle null values.

**Use when:**
- You're using SQL Server or another database where null propagation in expressions works as expected.
- You know the navigation property will not be null in practice (or null is an acceptable result when it is).
- You want simpler, shorter SQL output.

**Generated SQL example (SQL Server):**
```sql
SELECT ([u].[AddressLine1] + N' ') + [u].[City]
FROM [Users] AS [u]
```

---

### `Rewrite`

```csharp
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
public string? FullAddress => Location?.AddressLine1 + " " + Location?.City;
```

The null-conditional operators are **rewritten as explicit null checks** — `A?.B` becomes `A != null ? A.B : null`.

Generated expression is equivalent to:
```csharp
(Location != null ? Location.AddressLine1 : null)
+ " " +
(Location != null ? Location.City : null)
```

**Use when:**
- You need **explicit null handling** in the generated expression.
- You're targeting Cosmos DB or another provider that evaluates expressions client-side.
- You want the expression to behave identically to the original C# code.

**Trade-off:** The generated SQL can become significantly more complex, especially with deeply nested null-conditional chains.

**Generated SQL example (SQL Server):**
```sql
SELECT 
    CASE WHEN [u].[LocationId] IS NOT NULL THEN [l].[AddressLine1] ELSE NULL END
    + N' ' +
    CASE WHEN [u].[LocationId] IS NOT NULL THEN [l].[City] ELSE NULL END
FROM [Users] AS [u]
LEFT JOIN [Locations] AS [l] ON [u].[LocationId] = [l].[Id]
```

## Comparison Table

| Option    | `?.` allowed      | Expression generated     | SQL complexity |
|-----------|-------------------|--------------------------|----------------|
| `None`    | ❌ (error EFP0002) | —                        | —              |
| `Ignore`  | ✅                 | `A.B`                    | Simple         |
| `Rewrite` | ✅                 | `A != null ? A.B : null` | Higher         |

## Practical Recommendation

- **SQL Server + navigations you control:** Use `Ignore` — SQL Server's null semantics match C#'s null-conditional in most cases.
- **Cosmos DB or client-side evaluation:** Use `Rewrite` — you need explicit null checks.
- **Unsure:** Start with `Rewrite` for correctness, optimize to `Ignore` if SQL complexity is an issue.

## Example: Navigation Property Chain

```csharp
public class User
{
    public Address? Location { get; set; }
}

public class Address
{
    public string? AddressLine1 { get; set; }
    public string? City { get; set; }
}

// Option 1: Ignore (simpler SQL)
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
public static string? GetFullAddress(this User? user) =>
    user?.Location?.AddressLine1 + " " + user?.Location?.City;
// → user.Location.AddressLine1 + " " + user.Location.City

// Option 2: Rewrite (explicit null checks, safer)
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
public static string? GetFullAddress(this User? user) =>
    user?.Location?.AddressLine1 + " " + user?.Location?.City;
// → (user != null ? (user.Location != null ? user.Location.AddressLine1 : null) : null)
//   + " " +
//   (user != null ? (user.Location != null ? user.Location.City : null) : null)
```


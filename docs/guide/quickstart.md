# Quick Start

This guide walks you through a complete end-to-end example — from installing the NuGet packages to seeing the generated SQL.

## Prerequisites

- .NET 8 to .NET 10
- EF Core 6 or later (any provider)

## Step 1 — Install the Packages

Projectables is split into **two NuGet packages**:

| Package                                         | Purpose                                                          |
|-------------------------------------------------|------------------------------------------------------------------|
| `EntityFrameworkCore.Projectables.Abstractions` | `[Projectable]` attribute + Roslyn source generator + code fixes |
| `EntityFrameworkCore.Projectables`              | EF Core runtime interceptor (`UseProjectables()`)                |

In most single-project setups both packages go in the same project.

### .NET CLI

```bash
dotnet add package EntityFrameworkCore.Projectables.Abstractions
dotnet add package EntityFrameworkCore.Projectables
```

### Package Manager Console

```powershell
Install-Package EntityFrameworkCore.Projectables.Abstractions
Install-Package EntityFrameworkCore.Projectables
```

### PackageReference (`.csproj`)

```xml
<ItemGroup>
  <PackageReference Include="EntityFrameworkCore.Projectables.Abstractions" Version="*" />
  <PackageReference Include="EntityFrameworkCore.Projectables" Version="*" />
</ItemGroup>
```

> **Tip:** Replace `*` with the [latest stable version](https://www.nuget.org/packages/EntityFrameworkCore.Projectables/).

## Step 2 — Define Your Entities

Add `[Projectable]` to any property or method whose body you want EF Core to translate to SQL:

```csharp
using EntityFrameworkCore.Projectables;

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; }
    public ICollection<Order> Orders { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public decimal TaxRate { get; set; }

    public User User { get; set; }
    public ICollection<OrderItem> Items { get; set; }

    // Mark computed properties with [Projectable]
    [Projectable] public decimal Subtotal => Items.Sum(item => item.Product.ListPrice * item.Quantity);
    [Projectable] public decimal Tax => Subtotal * TaxRate;
    [Projectable] public decimal GrandTotal => Subtotal + Tax;
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int Quantity { get; set; }
    public Product Product { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public decimal ListPrice { get; set; }
}
```

The source generator runs at **compile time** and emits a companion `Expression<TDelegate>` for each `[Projectable]` member — no runtime reflection.

## Step 3 — Enable Projectables on Your DbContext

Call `UseProjectables()` when configuring your `DbContextOptions`. The extension method is in the `Microsoft.EntityFrameworkCore` namespace and is included in the `EntityFrameworkCore.Projectables` package.

### With DI (`AddDbContext`)

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseProjectables());
```

### With `OnConfiguring`

```csharp
public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Order> Orders { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer("your-connection-string")
            .UseProjectables();
    }
}
```

## Step 4 — Use Projectable Members in Queries

Now you can use `GrandTotal`, `Subtotal`, and `Tax` **directly in any LINQ query**:

```csharp
// In a Select projection
var orderSummaries = dbContext.Orders
    .Select(o => new {
        o.Id,
        o.Subtotal,
        o.Tax,
        o.GrandTotal
    })
    .ToList();

// In a Where clause
var highValueOrders = dbContext.Orders
    .Where(o => o.GrandTotal > 1000)
    .ToList();

// In an OrderBy
var sortedOrders = dbContext.Orders
    .OrderByDescending(o => o.GrandTotal)
    .ToList();
```

## Step 5 — Check the Generated SQL

Use `ToQueryString()` to inspect the SQL EF Core generates:

```csharp
var query = dbContext.Orders
    .Where(o => o.GrandTotal > 1000)
    .OrderByDescending(o => o.GrandTotal);

Console.WriteLine(query.ToQueryString());
```

The `GrandTotal` property composes `Subtotal` (which is also `[Projectable]`) — both are fully inlined into SQL:

```sql
SELECT [o].[Id], [o].[UserId], [o].[CreatedDate], [o].[TaxRate]
FROM [Orders] AS [o]
WHERE (
    COALESCE(SUM([p].[ListPrice] * CAST([oi].[Quantity] AS decimal(18,2))), 0.0) +
    COALESCE(SUM([p].[ListPrice] * CAST([oi].[Quantity] AS decimal(18,2))), 0.0) * [o].[TaxRate]
) > 1000.0
ORDER BY (
    COALESCE(SUM([p].[ListPrice] * CAST([oi].[Quantity] AS decimal(18,2))), 0.0) +
    COALESCE(SUM([p].[ListPrice] * CAST([oi].[Quantity] AS decimal(18,2))), 0.0) * [o].[TaxRate]
) DESC
```

All computation happens in the database — no data is loaded into memory for filtering or sorting.

## Adding Extension Methods

You can also define projectable extension methods — useful for logic that doesn't belong on the entity itself:

```csharp
public static class UserExtensions
{
    [Projectable]
    public static Order GetMostRecentOrder(this User user, DateTime? cutoffDate = null) =>
        user.Orders
            .Where(x => cutoffDate == null || x.CreatedDate >= cutoffDate)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefault();
}
```

Use it in a query just like any regular method:

```csharp
var result = dbContext.Users
    .Where(u => u.UserName == "Jon")
    .Select(u => new {
        GrandTotal = u.GetMostRecentOrder(DateTime.UtcNow.AddDays(-30)).GrandTotal
    })
    .FirstOrDefault();
```

## Next Steps

- [Projectable Properties in depth →](/guide/projectable-properties)
- [Projectable Methods →](/guide/projectable-methods)
- [Extension Methods →](/guide/extension-methods)
- [Full [Projectable] attribute reference →](/reference/projectable-attribute)


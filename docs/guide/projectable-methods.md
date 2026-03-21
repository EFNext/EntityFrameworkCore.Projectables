# Projectable Methods

Projectable methods work like projectable properties but accept parameters, making them ideal for reusable query fragments that vary based on runtime values.

## Defining a Projectable Method

Add `[Projectable]` to any **expression-bodied method** on an entity:

```csharp
public class Order
{
    public int Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsFulfilled { get; set; }
    public decimal TaxRate { get; set; }
    public ICollection<OrderItem> Items { get; set; }

    [Projectable]
    public decimal Subtotal => Items.Sum(item => item.Product.ListPrice * item.Quantity);

    [Projectable]
    public bool IsRecentOrder(int days) =>
        CreatedDate >= DateTime.UtcNow.AddDays(-days) && IsFulfilled;
}
```

## Using Projectable Methods in Queries

```csharp
// Pass runtime values as arguments
var recentOrders = dbContext.Orders
    .Where(o => o.IsRecentOrder(30))
    .ToList();

// Use in Select
var summary = dbContext.Orders
    .Select(o => new {
        o.Id,
        IsRecent = o.IsRecentOrder(7),
        o.Subtotal
    })
    .ToList();
```

The method argument (`30` or `7`) is captured and translated into the generated SQL expression.

## Methods with Multiple Parameters

```csharp
public class Product
{
    public decimal ListPrice { get; set; }
    public decimal DiscountRate { get; set; }

    [Projectable]
    public decimal DiscountedPrice(decimal additionalDiscount, int quantity) =>
        ListPrice * (1 - DiscountRate - additionalDiscount) * quantity;
}

// Usage
var prices = dbContext.Products
    .Select(p => new {
        p.Id,
        FinalPrice = p.DiscountedPrice(0.05m, 10)
    })
    .ToList();
```

## Composing Methods and Properties

Projectable methods can call projectable properties and vice versa:

```csharp
public class Order
{
    [Projectable] public decimal Subtotal => Items.Sum(i => i.Price);
    [Projectable] public decimal Tax => Subtotal * TaxRate;

    // Method calling projectable properties
    [Projectable]
    public bool ExceedsThreshold(decimal threshold) => (Subtotal + Tax) > threshold;
}

var highValue = dbContext.Orders
    .Where(o => o.ExceedsThreshold(500))
    .ToList();
```

## Block-Bodied Methods (Experimental)

Methods can also use traditional block bodies when `AllowBlockBody = true`:

```csharp
[Projectable(AllowBlockBody = true)]
public string GetStatus(decimal threshold)
{
    if (GrandTotal > threshold)
        return "High Value";
    else if (GrandTotal > threshold / 2)
        return "Medium Value";
    else
        return "Standard";
}
```

See [Block-Bodied Members](/advanced/block-bodied-members) for full details.

## Important Rules

- Methods must be **expression-bodied** (`=>`) unless `AllowBlockBody = true`.
- **Method overloading is not supported** — each method name must be unique within its type.
- Parameters are passed through to the generated expression as closures and resolved at query time.
- Parameter types must be supported by EF Core (primitive types, enums, and other EF-translatable types).

## Difference from Extension Methods

Instance methods are defined directly on the entity. For query logic that doesn't belong on the entity, or that applies to types you don't own, use [Extension Methods](/guide/extension-methods) instead.


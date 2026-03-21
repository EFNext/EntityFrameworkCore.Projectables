# `[Projectable]` Attribute

The `ProjectableAttribute` is the entry point for this library. Place it on any property or method to tell the source generator to produce a companion expression tree for it.

## Namespace

```csharp
using EntityFrameworkCore.Projectables;
```

## Target

| Target            | Supported |
|-------------------|-----------|
| Properties        | ✅         |
| Methods           | ✅         |
| Extension methods | ✅         |
| Constructors      | ✅ (v6.x+) |
| Indexers          | ❌         |

The attribute can be inherited by derived types (`Inherited = true`).

## Properties

### `NullConditionalRewriteSupport`

**Type:** `NullConditionalRewriteSupport`  
**Default:** `NullConditionalRewriteSupport.None`

Controls how null-conditional operators (`?.`) in the member body are handled by the source generator.

```csharp
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
public string? FullAddress => Location?.AddressLine1 + " " + Location?.City;
```

| Value            | Behavior                                                                                                                                               |
|------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------|
| `None` (default) | Null-conditional operators are **not allowed** — the generator raises error EFP0002.                                                                   |
| `Ignore`         | Null-conditional operators are **stripped** — `A?.B` becomes `A.B`. Safe for databases where null propagates implicitly (SQL Server).                  |
| `Rewrite`        | Null-conditional operators are **rewritten** as explicit null checks — `A?.B` becomes `A != null ? A.B : null`. Safer but may increase SQL complexity. |

See [Null-Conditional Rewrite](/reference/null-conditional-rewrite) for full details.

---

### `UseMemberBody`

**Type:** `string?`  
**Default:** `null`

Tells the generator to use a **different member's body** as the source for the generated expression tree. Useful when the projectable member's body is not directly available (e.g., interface implementation, abstract member).

```csharp
public class Order
{
    // The actual computation is defined here
    private decimal ComputeGrandTotal() => Subtotal + Tax;

    // The projectable member delegates to it
    [Projectable(UseMemberBody = nameof(ComputeGrandTotal))]
    public decimal GrandTotal => ComputeGrandTotal();
}
```

See [Use Member Body](/reference/use-member-body) for full details.

---

### `ExpandEnumMethods`

**Type:** `bool`  
**Default:** `false`

When set to `true`, method calls on enum values inside this projectable member are expanded into a **chain of ternary expressions** — one branch per enum value. This allows enum helper methods (like display name lookups) to be translated to SQL `CASE` expressions.

```csharp
[Projectable(ExpandEnumMethods = true)]
public string StatusName => Status.GetDisplayName();
```

See [Expand Enum Methods](/reference/expand-enum-methods) for full details.

---

### `AllowBlockBody`

**Type:** `bool`  
**Default:** `false`

Enables **block-bodied member** support (experimental). Without this flag, using a block body with `[Projectable]` produces warning EFP0001. Setting this to `true` suppresses the warning.

```csharp
[Projectable(AllowBlockBody = true)]
public string Category
{
    get
    {
        if (Score > 100) return "High";
        else if (Score > 50) return "Medium";
        else return "Low";
    }
}
```

See [Block-Bodied Members](/advanced/block-bodied-members) for full details.

---

## Complete Example

```csharp
public class Order
{
    public OrderStatus Status { get; set; }
    public decimal TaxRate { get; set; }
    public Address? ShippingAddress { get; set; }
    public ICollection<OrderItem> Items { get; set; }

    // Simple computed property
    [Projectable]
    public decimal Subtotal => Items.Sum(i => i.Price * i.Quantity);

    // Composing projectables
    [Projectable]
    public decimal GrandTotal => Subtotal * (1 + TaxRate);

    // Handling nullable navigation
    [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
    public string? ShippingLine => ShippingAddress?.AddressLine1 + ", " + ShippingAddress?.City;

    // Enum expansion
    [Projectable(ExpandEnumMethods = true)]
    public string StatusLabel => Status.GetDisplayName();

    // Block-bodied (experimental)
    [Projectable(AllowBlockBody = true)]
    public string Priority
    {
        get
        {
            if (GrandTotal > 1000) return "High";
            if (GrandTotal > 500) return "Medium";
            return "Normal";
        }
    }
}
```


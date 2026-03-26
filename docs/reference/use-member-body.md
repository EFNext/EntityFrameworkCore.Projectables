# Use Member Body

The `UseMemberBody` option on `[Projectable]` tells the source generator to use a **different member's body** as the source expression, instead of the annotated member's own body. This lets you maintain a separate in-memory implementation while supplying a clean expression for EF Core.

## Delegating to a Method or Property Body

Point `UseMemberBody` at another method or property that has the **same return type and parameter signature**. The generator uses the target member's body instead:

```csharp
public class Entity
{
    public int Id { get; set; }

    // EF-side: generates an expression from ComputedImpl
    [Projectable(UseMemberBody = nameof(ComputedImpl))]
    public int Computed => Id;            // original body is ignored

    // In-memory implementation (different algorithm)
    private int ComputedImpl => Id * 2;
}
```

The generated expression is `(@this) => @this.Id * 2`, so `Computed` projects as `Id * 2` in SQL.

> [!NOTE]
> When delegating to a regular method or property body the target member must be declared in the **same source file** as the `[Projectable]` member so the generator can read its body.

## Using an `Expression<Func<...>>` Property as the Body

For even more control you can supply the body as a typed `Expression<Func<…>>` property. This lets you write the expression once and reuse it from both the `[Projectable]` member and any runtime code that needs the expression tree directly:

```csharp
public class Entity
{
    public int Id { get; set; }

    [Projectable(UseMemberBody = nameof(Computed4))]
    public int Computed3 => Id;   // body is replaced at compile time

    // The expression tree is picked up by the generator and by the runtime resolver
    private static Expression<Func<Entity, int>> Computed4 => x => x.Id * 3;
}
```

Unlike regular method/property delegation, `Expression<Func<…>>` backing properties may be declared in a **different file** — for example in a separate part of a `partial class`:

```csharp
// File: Entity.cs
public partial class Entity
{
    public int Id { get; set; }

    [Projectable(UseMemberBody = nameof(IdDoubledExpr))]
    public int Computed => Id;
}

// File: Entity.Expressions.cs
public partial class Entity
{
    private static Expression<Func<Entity, int>> IdDoubledExpr => @this => @this.Id * 2;
}
```

## Instance Methods and Parameter Alignment

For instance methods the generator automatically aligns lambda parameter names with the method's own parameter names, so you are free to choose any names in the lambda. Using `@this` for the receiver is conventional:

```csharp
public class Entity
{
    public int Value { get; set; }

    [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
    public bool IsPositive() => Value > 0;

    // Any receiver name works; @this is conventional
    private static Expression<Func<Entity, bool>> IsPositiveExpr => @this => @this.Value > 0;
}
```

If the lambda parameter names differ from the method's parameter names the generator renames them automatically:

```csharp
// Lambda uses (c, t) but method parameter is named threshold — generated code uses threshold
private static Expression<Func<Entity, int, bool>> ExceedsThresholdExpr =>
    (c, t) => c.Value > t;
```

## Static Extension Methods

`UseMemberBody` works equally well on static extension methods. Name the lambda parameters to match the method's parameter names:

```csharp
public static class FooExtensions
{
    [Projectable(UseMemberBody = nameof(NameEqualsExpr))]
    public static bool NameEquals(this Foo a, Foo b) => a.Name == b.Name;

    private static Expression<Func<Foo, Foo, bool>> NameEqualsExpr =>
        (a, b) => a.Name == b.Name;
}
```

The generated expression is `(Foo a, Foo b) => a.Name == b.Name`.

## Use Cases

### Interface Members

Interface members cannot have bodies. Use `UseMemberBody` to delegate to a default implementation or a helper:

```csharp
public class Order
{
    public decimal TaxRate { get; set; }
    public ICollection<OrderItem> Items { get; set; }

    private decimal ComputeGrandTotal() =>
        Items.Sum(i => i.Price * i.Quantity) * (1 + TaxRate);

    [Projectable(UseMemberBody = nameof(ComputeGrandTotal))]
    public decimal GrandTotal => ComputeGrandTotal();
}
```

### Reusing Bodies Across Multiple Members

```csharp
public class Order
{
    private bool IsEligibleForDiscount() =>
        Items.Count > 5 && TotalValue > 100;

    [Projectable(UseMemberBody = nameof(IsEligibleForDiscount))]
    public bool CanApplyDiscount => IsEligibleForDiscount();

    [Projectable(UseMemberBody = nameof(IsEligibleForDiscount))]
    public bool ShowDiscountBadge => IsEligibleForDiscount();
}
```

## Diagnostics

| Code        | Severity | Cause                                                                              |
|-------------|----------|------------------------------------------------------------------------------------|
| **EFP0010** | ❌ Error  | The name given to `UseMemberBody` does not match any member on the containing type |
| **EFP0011** | ❌ Error  | A member with that name exists but its type or signature is incompatible           |

See the full [Diagnostics Reference](/reference/diagnostics) for fix guidance.

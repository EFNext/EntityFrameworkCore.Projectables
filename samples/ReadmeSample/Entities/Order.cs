using EntityFrameworkCore.Projectables;

namespace ReadmeSample.Entities;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? FulfilledDate { get; set; }
    public decimal TaxRate { get; set; }
    public OrderStatus Status { get; set; }

    public User User { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = [];

    // ── Feature 1: Projectable properties (compose each other recursively) ──────

    /// <summary>Sum of (unit price × quantity) for all items — inlined into SQL.</summary>
    [Projectable] public decimal Subtotal => Items.Sum(item => item.Product.ListPrice * item.Quantity);

    /// <summary>Tax amount (Subtotal × TaxRate) — composed from another [Projectable].</summary>
    [Projectable] public decimal Tax => Subtotal * TaxRate;

    /// <summary>Total including tax — composed from two [Projectable] properties.</summary>
    [Projectable] public decimal GrandTotal => Subtotal + Tax;

    /// <summary>True when the order has been fulfilled — usable in .Where() filters.</summary>
    [Projectable] public bool IsFulfilled => FulfilledDate != null;

    // ── Feature 1 (method): Projectable method with a parameter ─────────────────

    /// <summary>Grand total after applying a percentage discount — demonstrates a [Projectable] method.</summary>
    [Projectable]
    public decimal GetDiscountedTotal(decimal discountPct) => GrandTotal * (1 - discountPct);

    // ── Feature 5: Pattern matching — switch expression ──────────────────────────

    /// <summary>
    /// Priority label derived from GrandTotal using a switch expression.
    /// The generator rewrites this into SQL CASE WHEN expressions.
    /// </summary>
    [Projectable]
    public string PriorityLabel => GrandTotal switch
    {
        >= 100m => "High",
        >= 30m  => "Medium",
        _       => "Low",
    };

    // ── Feature 6: Block-bodied member (experimental) ────────────────────────────

    /// <summary>
    /// Shipping category determined via an if/else block body.
    /// AllowBlockBody = true acknowledges the experimental nature (suppresses EFP0001).
    /// The block is converted to a ternary expression — identical SQL to the switch above.
    /// </summary>
    [Projectable(AllowBlockBody = true)]
    public string GetShippingCategory()
    {
        if (GrandTotal >= 100m)
            return "Express";
        else if (GrandTotal >= 30m)
            return "Standard";
        else
            return "Economy";
    }

    // ── Feature 8: Enum method expansion ─────────────────────────────────────────

    /// <summary>
    /// Human-readable status label.
    /// ExpandEnumMethods = true makes the generator enumerate every OrderStatus value at
    /// compile time and bake the results in as a SQL CASE expression — the GetDisplayName()
    /// method itself never runs at runtime.
    /// </summary>
    [Projectable(ExpandEnumMethods = true)]
    public string StatusDisplayName => Status.GetDisplayName();

    // ── Feature 9: UseMemberBody ──────────────────────────────────────────────────

    // Private EF-compatible expression — the actual body EF Core will use.
    private bool IsHighValueOrderImpl => GrandTotal >= 50m;

    /// <summary>
    /// UseMemberBody delegates the expression source to IsHighValueOrderImpl.
    /// The annotated member's own body is ignored by the generator; the target
    /// member's body is used as the expression tree instead.
    /// </summary>
    [Projectable(UseMemberBody = nameof(IsHighValueOrderImpl))]
    public bool IsHighValueOrder => IsHighValueOrderImpl;
}

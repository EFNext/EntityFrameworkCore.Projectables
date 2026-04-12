using EntityFrameworkCore.Projectables;

namespace ReadmeSample.Entities;

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal ListPrice { get; set; }

    // Optional supplier — foreign key is nullable so the join is a LEFT JOIN in SQL.
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>
    /// Null-conditional rewriting (NullConditionalRewriteSupport.Ignore):
    /// the ?. operator is stripped and EF Core handles nullability via the LEFT JOIN.
    /// </summary>
    [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
    public string? SupplierName => Supplier?.Name;
}

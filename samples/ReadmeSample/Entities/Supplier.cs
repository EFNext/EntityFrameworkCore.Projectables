namespace ReadmeSample.Entities;

/// <summary>Optional supplier linked to a product — used to demonstrate null-conditional rewriting.</summary>
public class Supplier
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}


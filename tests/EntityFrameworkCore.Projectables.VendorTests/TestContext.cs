using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.VendorTests;

/// <summary>Order entity used in vendor-compatibility tests.</summary>
public class Order
{
    public int Id { get; set; }
    public string? CustomerName { get; set; }
    public decimal Total { get; set; }
    public bool IsCompleted { get; set; }

    /// <summary>
    /// A computed projectable property. Having at least one [Projectable] writable
    /// property on the entity ensures that <c>CustomQueryCompiler</c> is exercised
    /// (it expands the projectable reference and potentially adds a Select wrapper).
    /// </summary>
    [Projectable]
    public bool IsLargeOrder => Total > 100;
}

public class TestDbContext : DbContext
{
    // Keep the connection open for the lifetime of the context so the in-memory
    // SQLite database is not destroyed between operations.
    readonly SqliteConnection _connection;

    public TestDbContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(_connection);
        optionsBuilder.UseProjectables();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>();
    }

    public void SeedData()
    {
        Orders.AddRange(
            new Order { CustomerName = "Alice", Total = 50m, IsCompleted = false },
            new Order { CustomerName = "Bob", Total = 150m, IsCompleted = true },
            new Order { CustomerName = "Charlie", Total = 200m, IsCompleted = true });
        SaveChanges();
    }

    public override void Dispose()
    {
        base.Dispose();
        _connection.Dispose();
    }
}

using Microsoft.EntityFrameworkCore;
using ReadmeSample.Entities;

namespace ReadmeSample;

public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ReadmeSample;Trusted_Connection=True");

        // Feature 10: Compatibility mode
        // Full (default) — expands every query on each invocation; maximum compatibility.
        // Limited        — expands once, then caches; better performance for repeated queries.
        //   Switch with: optionsBuilder.UseProjectables(p => p.CompatibilityMode(CompatibilityMode.Limited));
        optionsBuilder.UseProjectables();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderItem>().HasKey(x => new { x.OrderId, x.ProductId });
    }
}

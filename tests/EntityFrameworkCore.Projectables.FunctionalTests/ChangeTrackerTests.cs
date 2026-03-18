using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using EntityFrameworkCore.Projectables.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests;

public class ChangeTrackerTests
{
    public class SqliteSampleDbContext<TEntity> : DbContext
        where TEntity : class
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=test.sqlite");
            optionsBuilder.UseProjectables();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TEntity>();
        }
    }

    public record Entity
    {
        private static int _nextId = 1;
        public const int Computed1DefaultValue = -1;
        public int Id { get; set; } = _nextId++;
        public string? Name { get; set; }

        [Projectable(UseMemberBody = nameof(InternalComputed1))]
        public int Computed1 { get; set; } = Computed1DefaultValue;
        private int InternalComputed1 => Id;

        [Projectable]
        public int Computed2 => Id * 2;
    }

    [Fact]
    public async Task CanQueryAndChangeTrackedEntities()
    {
        using var dbContext = new SqliteSampleDbContext<Entity>();
        await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        dbContext.Add(new Entity());
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        dbContext.ChangeTracker.Clear();

        var entity = await dbContext.Set<Entity>().AsTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        var entityEntry = dbContext.ChangeTracker.Entries().Single();
        Assert.Same(entityEntry.Entity, entity);
        dbContext.Set<Entity>().Remove(entity);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CanSaveChanges()
    {
        using var dbContext = new SqliteSampleDbContext<Entity>();
        await dbContext.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        dbContext.Add(new Entity());
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        dbContext.ChangeTracker.Clear();

        var entity = await dbContext.Set<Entity>().AsTracking().FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        entity.Name = "test";
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        dbContext.ChangeTracker.Clear();
        var entity2 = await dbContext.Set<Entity>().FirstAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("test", entity2.Name);
    }
}
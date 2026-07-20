using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    // https://github.com/EFNext/EntityFrameworkCore.Projectables/issues/217
    //
    // A writable [Projectable] property triggers a whole-entity re-projection so the property can be
    // populated during materialization. When the entity also has a collection navigation that is
    // Include-d, that re-projection previously read every other member through its backing field,
    // which forced EF to re-materialize the whole entity (and its Include-d collection) once per
    // member -- producing one duplicate JOIN for every rewritten member.
    public class Issue217Tests
    {
        public class Entity1
        {
            public virtual int Id { get; set; }
            public virtual string? Name1 { get; set; }

            [NotMapped]
            [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore, UseMemberBody = nameof(Name1LenQuery))]
            public virtual int? Name1Len { get; set; }

            protected virtual int? Name1LenQuery => Name1 == null ? (int?)null : Name1.Length;

            public virtual List<Entity2> Entity2s { get; set; } = new();
        }

        public class Entity2
        {
            public virtual int Id { get; set; }
            public virtual string? Name2 { get; set; }

            public virtual int? Entity1Id { get; set; }
            public virtual Entity1? Entity1 { get; set; }
        }

        public class DemoDbContext : DbContext
        {
            private readonly string _dataSource;

            public DemoDbContext(string dataSource) => _dataSource = dataSource;

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlite($"Data Source={_dataSource}");
                optionsBuilder.UseProjectables();
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Entity1>();
                modelBuilder.Entity<Entity2>();
            }

            public DbSet<Entity1> Entity1s => Set<Entity1>();
            public DbSet<Entity2> Entity2s => Set<Entity2>();
        }

        [Fact]
        public void IncludingCollectionDoesNotProduceDuplicateJoins()
        {
            using var context = new DemoDbContext("issue217-sql.sqlite");

            var sql = context.Entity1s
                .AsNoTrackingWithIdentityResolution()
                .Include(e1 => e1.Entity2s)
                .ToQueryString();

            var joinCount = CountOccurrences(sql, "JOIN");

            Assert.True(joinCount <= 1, $"Expected at most one JOIN but found {joinCount}.{Environment.NewLine}{sql}");
        }

        [Fact]
        public void PopulatesWritablePropertyAndLoadsCollection()
        {
            using var context = new DemoDbContext("issue217-exec.sqlite");
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var entity = new Entity1 { Name1 = "hello" };
            entity.Entity2s.Add(new Entity2 { Name2 = "a" });
            entity.Entity2s.Add(new Entity2 { Name2 = "b" });
            context.Add(entity);
            context.SaveChanges();
            context.ChangeTracker.Clear();

            var loaded = context.Entity1s
                .AsNoTrackingWithIdentityResolution()
                .Include(e1 => e1.Entity2s)
                .Single();

            Assert.Equal(5, loaded.Name1Len);
            Assert.Equal(2, loaded.Entity2s.Count);

            context.Database.EnsureDeleted();
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            var count = 0;
            var index = 0;
            while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }
    }
}

using EntityFrameworkCore.Projectables.Extensions;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.FunctionalTests;

public class AsExpandedPropertiesTests
{
    /// <summary>
    /// Entity with both a read-only and a writable projectable property.
    /// <list type="bullet">
    ///   <item><c>ReadOnlyComputed</c> — expression-bodied (no setter); excluded from auto-inject.</item>
    ///   <item><c>WritableComputed</c> — has a setter; included in auto-inject.</item>
    /// </list>
    /// </summary>
    public class Entity
    {
        public int Id { get; set; }

        /// <summary>Read-only projectable: NOT included in the auto-generated SELECT (no setter).</summary>
        [Projectable]
        public int ReadOnlyComputed => Id * 2;

        /// <summary>Writable projectable: IS included in the auto-generated SELECT (has setter).</summary>
        [Projectable]
        public int WritableComputed
        {
            get => Id * 3;
            set { }
        }
    }

    [Fact]
    public Task TrackingContext_QueryRoot_InjectsWritableProjectableProperties()
    {
        // Tracking context: without AsExpandedProperties() the auto-inject is suppressed.
        // AsExpandedProperties() forces the Select injection regardless of tracking mode.
        // ReadOnlyComputed is absent from SELECT (no setter); WritableComputed is present.
        using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.TrackAll);

        var query = dbContext.Set<Entity>().AsExpandedProperties();

        return Verifier.Verify(query.ToQueryString());
    }

    [Fact]
    public Task TrackingContext_WithWhere_InjectsProjectablePropertiesAfterFilter()
    {
        // Verifies that AsExpandedProperties() wraps the Where clause (not the raw root),
        // so the generated SQL contains WHERE and SELECT in the correct order.
        using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.TrackAll);

        var query = dbContext.Set<Entity>()
            .Where(e => e.Id > 0)
            .AsExpandedProperties();

        return Verifier.Verify(query.ToQueryString());
    }

    [Fact]
    public Task NoTrackingContext_QueryRoot_InjectsProjectableProperties()
    {
        // NoTracking context: auto-inject already happens automatically via UseProjectables();
        // calling AsExpandedProperties() explicitly should produce the same result.
        using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.NoTracking);

        var query = dbContext.Set<Entity>().AsExpandedProperties();

        return Verifier.Verify(query.ToQueryString());
    }
}

using System.ComponentModel.DataAnnotations.Schema;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class QueryRootTests
    {
        public record Entity
        {
            public int Id { get; set; }

            [Projectable(UseMemberBody = nameof(Computed2))]
            public int Computed1 => Id;

            private int Computed2 => Id * 2;

            [Projectable(UseMemberBody = nameof(_ComputedWithBaking))]
            [NotMapped]
            public int ComputedWithBacking { get; set; }

            private int _ComputedWithBaking => Id * 5;
        }

        [Fact]
        public Task UseMemberPropertyQueryRootExpression()
        {
            using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.NoTracking);

            var query = dbContext.Set<Entity>();

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task DontUseMemberPropertyQueryRootExpression()
        {
            using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.TrackAll);

            var query = dbContext.Set<Entity>();

            return Verifier.Verify(query.ToQueryString());
        }


        [Fact]
        public Task EntityRootSubqueryExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var original = dbContext.Set<Entity>()
                .Where(e => e.ComputedWithBacking == 5);

            var query = original
                .Select(e => new { Item = e, TotalCount = original.Count() });

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task AsTrackingQueryRootExpression()
        {
            using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.NoTracking);

            var query = dbContext.Set<Entity>().AsTracking();

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task AsNoTrackingQueryRootExpression()
        {
            using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.TrackAll);

            var query = dbContext.Set<Entity>().AsNoTracking();

            return Verifier.Verify(query.ToQueryString());
        }

        // AsNoTracking() written *before* a projection to a non-entity type. The query root
        // rewrite must not be applied: there is no entity left in the result to populate.
        // Before the element-type guard this threw, because AsNoTracking is visited after
        // the Select (ExpressionVisitor walks outside-in) and so re-enabled the rewrite,
        // which then tried to append Select<Entity, Entity> to an IQueryable<anonymous>.
        [Fact]
        public Task AsNoTrackingThenProjectionToAnonymousTypeQueryRootExpression()
        {
            using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.TrackAll);

            var query = dbContext.Set<Entity>()
                .AsNoTracking()
                .Select(e => new { e.ComputedWithBacking });

            return Verifier.Verify(query.ToQueryString());
        }

        // The same projection, reached through the context's default tracking behaviour
        // rather than an explicit AsNoTracking() call.
        [Fact]
        public Task NoTrackingByDefaultThenProjectionToAnonymousTypeQueryRootExpression()
        {
            using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.NoTracking);

            var query = dbContext.Set<Entity>()
                .Select(e => new { e.ComputedWithBacking });

            return Verifier.Verify(query.ToQueryString());
        }

        // A projection that stays on the entity type is still rewritten, so a writable
        // projectable gets populated.
        [Fact]
        public Task AsNoTrackingThenProjectionToEntityTypeQueryRootExpression()
        {
            using var dbContext = new SampleDbContext<Entity>(queryTrackingBehavior: QueryTrackingBehavior.TrackAll);

            var query = dbContext.Set<Entity>()
                .AsNoTracking()
                .Select(e => e);

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

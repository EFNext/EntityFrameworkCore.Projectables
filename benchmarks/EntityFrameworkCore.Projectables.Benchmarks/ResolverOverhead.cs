using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityFrameworkCore.Projectables.Benchmarks.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.Benchmarks
{
    /// <summary>
    /// Measures the per-DbContext cold-start cost of resolver lookup by creating a new
    /// <see cref="TestDbContext"/> on every iteration.  The previous benchmarks reuse a single
    /// DbContext for 10 000 iterations, so the resolver cache is warm after the first query —
    /// these benchmarks expose the cost of the very first query per context.
    /// </summary>
    [MemoryDiagnoser]
    public class ResolverOverhead
    {
        const int Iterations = 1000;

        /// <summary>Baseline: no projectables, new DbContext per query.</summary>
        [Benchmark(Baseline = true)]
        public void WithoutProjectables_FreshDbContext()
        {
            for (int i = 0; i < Iterations; i++)
            {
                using var dbContext = new TestDbContext(false);
                dbContext.Entities.Select(x => x.Id + 1).ToQueryString();
            }
        }

        /// <summary>
        /// New DbContext per query with a projectable property.
        /// After the registry is in place this should approach baseline overhead.
        /// </summary>
        [Benchmark]
        public void WithProjectables_FreshDbContext_Property()
        {
            for (int i = 0; i < Iterations; i++)
            {
                using var dbContext = new TestDbContext(true, false);
                dbContext.Entities.Select(x => x.IdPlus1).ToQueryString();
            }
        }

        /// <summary>
        /// New DbContext per query with a projectable method.
        /// After the registry is in place this should approach baseline overhead.
        /// </summary>
        [Benchmark]
        public void WithProjectables_FreshDbContext_Method()
        {
            for (int i = 0; i < Iterations; i++)
            {
                using var dbContext = new TestDbContext(true, false);
                dbContext.Entities.Select(x => x.IdPlus1Method()).ToQueryString();
            }
        }

        /// <summary>
        /// New DbContext per query with a projectable method that takes a parameter,
        /// exercising parameter-type disambiguation in the registry key.
        /// </summary>
        [Benchmark]
        public void WithProjectables_FreshDbContext_MethodWithParam()
        {
            for (int i = 0; i < Iterations; i++)
            {
                using var dbContext = new TestDbContext(true, false);
                dbContext.Entities.Select(x => x.IdPlusDelta(5)).ToQueryString();
            }
        }
    }
}

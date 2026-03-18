using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityFrameworkCore.Projectables.Benchmarks.Helpers;
using EntityFrameworkCore.Projectables.Extensions;
using EntityFrameworkCore.Projectables.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.Benchmarks
{
    [MemoryDiagnoser]
    public class ProjectableMethods
    {
        const int innerLoop = 10000;

        [Benchmark(Baseline = true)]
        public void WithoutProjectables()
        {
            using var dbContext = new TestDbContext(false);

            for (int i = 0; i < innerLoop; i++)
            {
                dbContext.Entities.Select(x => x.Id + 1).ToQueryString();
            }
        }

        [Benchmark]
        public void WithProjectablesWithFullCompatibility()
        {
            using var dbContext = new TestDbContext(true);

            for (int i = 0; i < innerLoop; i++)
            {
                dbContext.Entities.Select(x => x.IdPlus1Method()).ToQueryString();
            }
        }

        [Benchmark]
        public void WithProjectablesWithLimitedCompatibility()
        {
            using var dbContext = new TestDbContext(true, false);

            for (int i = 0; i < innerLoop; i++)
            {
                dbContext.Entities.Select(x => x.IdPlus1Method()).ToQueryString();
            }
        }
    }
}

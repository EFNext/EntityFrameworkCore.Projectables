using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using EntityFrameworkCore.Projectables.Benchmarks.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.Benchmarks
{
    [MemoryDiagnoser]
    public class ProjectableExtensionMethods
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

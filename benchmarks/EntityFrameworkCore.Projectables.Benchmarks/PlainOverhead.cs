using System;
using System.IO.Compression;
using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityFrameworkCore.Projectables.Benchmarks.Helpers;
using EntityFrameworkCore.Projectables.Extensions;
using EntityFrameworkCore.Projectables.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.Benchmarks
{
    [MemoryDiagnoser]
    public class PlainOverhead
    {
        [Benchmark(Baseline = true)]
        public void WithoutProjectables()
        {
            using var dbContext = new TestDbContext(false);

            for (int i = 0; i < 10000; i++)
            {
                dbContext.Entities.Select(x => x.Id).ToQueryString();
            }
        }

        [Benchmark]
        public void WithProjectablesWithFullCompatibility()
        {
            using var dbContext = new TestDbContext(true);

            for (int i = 0; i < 10000; i++)
            {
                dbContext.Entities.Select(x => x.Id).ToQueryString();
            }
        }


        [Benchmark]
        public void WithProjectablesWithLimitedCompatibility()
        {
            using var dbContext = new TestDbContext(true, false);

            for (int i = 0; i < 10000; i++)
            {
                dbContext.Entities.Select(x => x.Id).ToQueryString();
            }
        }

    }
}

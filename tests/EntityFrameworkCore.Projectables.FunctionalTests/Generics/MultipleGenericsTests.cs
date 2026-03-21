using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests.Generics
{
    public class MultipleGenericsTests
    {
        [Projectable]
        public static object? Coalesce<T1, T2>(T1? t1, T2 t2)
            => t1 != null ? t1 : t2;

        [Fact]
        public Task TestMultipleArguments()
        {
            using var context = new SampleDbContext<Entity>();
            
            var query = context.Set<Entity>()
                .Select(x => Coalesce(x.Id, x.Name));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public void MultipleInvocations()
        {
            using var context = new SampleDbContext<Entity>(Infrastructure.CompatibilityMode.Full);

            var query1 = context.Set<Entity>()
                .Select(x => Coalesce(x.Id, x.Name))
                .ToQueryString();

            var query2 = context.Set<Entity>()
                .Select(x => Coalesce(x.Name, x.Id))
                .ToQueryString();

            Assert.NotEqual(query1, query2);
        }
    }
}

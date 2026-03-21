using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class MethodOverloadsTests
    {
        public record Entity
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";

            [Projectable]
            public int Calculate(int x) => Id + x;

            [Projectable]
            public int Calculate(string prefix) => (prefix + Name).Length;
        }

        [Fact]
        public Task MethodOverload_WithIntParameter()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(e => new { e.Id, Result = e.Calculate(10) });

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task MethodOverload_WithStringParameter()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(e => new { e.Id, Result = e.Calculate("Hello_") });

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

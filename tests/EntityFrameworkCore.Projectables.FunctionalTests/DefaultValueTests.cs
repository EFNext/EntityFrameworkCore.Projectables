using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class DefaultValueTests
    {
        public record Entity
        {
            public int Id { get; set; }

            [Projectable]
            public int NextId(int skip = 1) => Id + skip;
        }

        [Fact]
        public Task ExplicitDefaultValueIsSupported()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.NextId(2));

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests.NullConditionals
{
    public class IngoreNullConditionalRewriteTests
    {
        [Fact]
        public Task SimpleMemberExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetNameIgnoreNulls());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ComplexMemberExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetNameLengthIgnoreNulls());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task RelationalExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetFirstRelatedIgnoreNulls());

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

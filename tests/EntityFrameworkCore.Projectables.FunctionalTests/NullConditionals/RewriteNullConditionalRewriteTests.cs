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
    public class RewriteNullConditionalRewriteTests
    {
        [Fact]
        public Task SimpleMemberExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetNameRewriteNulls());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ComplexMemberExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetNameLengthRewriteNulls());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task RelationalExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetFirstRelatedRewriteNulls());

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

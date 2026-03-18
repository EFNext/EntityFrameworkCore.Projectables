using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests.ExtensionMethods
{

    [UsesVerify]
    public class ExtensionMethodTests
    {
        [Fact]
        public Task ExtensionOnPrimitive()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Id.Squared());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectProjectableExtensionMethod()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Foo());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectProjectableExtensionMethod2()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Foo2());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ExtensionMethodAcceptingDbContext()
        {
            using var dbContext = new SampleDbContext<Entity>(Infrastructure.CompatibilityMode.Full);

            var sampleQuery = dbContext.Set<Entity>()
                .Select(x => dbContext.Set<Entity>().Where(y => y.Id > x.Id).FirstOrDefault());

            var query = dbContext.Set<Entity>()
                .Select(x => x.LeadingEntity(dbContext));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ExtensionMethod_WithExpressionPropertyBody()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var nameToMatch = "Alice";
            var query = dbContext.Set<Entity>()
                .Where(x => x.NameEquals(new Entity { Name = nameToMatch }));

            return Verifier.Verify(query.ToQueryString());
        }
    }
}
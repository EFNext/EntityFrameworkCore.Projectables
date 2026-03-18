using System;
using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

#nullable disable

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class ExplicitInterfaceImplementationTests
    {
        public interface IStringId
        {
            string Id { get; }
        }

        public class Item : IStringId
        {
            public int Id { get; set; }
            
            // Explicit interface implementation without [Projectable]
            // This tests that GetImplementingProperty handles this scenario
            string IStringId.Id => Id.ToString();
            
            [Projectable]
            public string FormattedId => Id.ToString();
        }

        [Fact]
        public Task ProjectOverExplicitInterfaceImplementation()
        {
            using var dbContext = new SampleDbContext<Item>();

            var query = dbContext.Set<Item>()
                .Select(x => x.FormattedId);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task FilterOnExplicitInterfaceImplementation()
        {
            using var dbContext = new SampleDbContext<Item>();

            var query = dbContext.Set<Item>()
                .Where(x => x.FormattedId == "123");

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

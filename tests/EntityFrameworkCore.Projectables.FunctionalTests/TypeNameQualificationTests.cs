using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class TypeNameQualificationTests
    {
        public record Entity
        {
            public int Id { get; set; }

            public int? ParentId { get; set; }

            public ICollection<Entity> Children { get; } = new List<Entity>();

            [Projectable]
            public Entity? FirstChild =>
                Children.OrderBy(x => x.Id).FirstOrDefault();
        }

        [Fact]
        public Task SelectProjectableProperty()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.FirstChild);

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

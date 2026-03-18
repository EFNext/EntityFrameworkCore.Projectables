using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    [UsesVerify]
    public class StatelessSimpleFunctionTests
    {
        public record Entity
        {
            public int Id { get; set; }
            
            [Projectable]
            public int Computed() => 0; 
        }

        [Fact]
        public Task FilterOnProjectableProperty()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>().AsQueryable()
                .Where(x => x.Computed() == 1);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectProjectableProperty()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Computed());

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

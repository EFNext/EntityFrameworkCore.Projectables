using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using EntityFrameworkCore.Projectables.Services;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    [UsesVerify]
    public class StatelessComplexFunctionTests
    {
        public record Entity
        {
            public int Id { get; set; }
            
            [Projectable]
            public int Computed(int argument1) => argument1; 
        }


        [Fact]
        public Task FilterOnProjectableProperty()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>().AsQueryable()
                .Where(x => x.Computed(0) == 1);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectProjectableProperty()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Computed(0));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task PassInVariableArguments()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var argument = 1;
            var query = dbContext.Set<Entity>()
                .Select(x => x.Computed(argument));

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

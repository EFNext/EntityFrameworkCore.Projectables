using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    [UsesVerify]
    public class UseMemberBodyPropertyTests
    {
        public record Entity
        {
            public int Id { get; set; }
            
            [Projectable(UseMemberBody = nameof(Computed2))]
            public int Computed1 => Id;

            private int Computed2 => Id * 2;

            [Projectable(UseMemberBody = nameof(Computed4))]
            public int Computed3 => Id;

            private static Expression<Func<Entity, int>> Computed4 => x => x.Id * 3;
        }

        [Fact]
        public Task UseMemberPropertyGenerated()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Computed1);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task UseMemberPropertyExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Computed3);

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

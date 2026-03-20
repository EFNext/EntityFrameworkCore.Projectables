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
    public class PrivateProjectables
    {
        public record Entity
        {
            public int Id { get; set; }
        }

        bool IsAdmin => true;

        [Fact]
        public Task Issue63Repro()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                    .Where(product => IsAdmin || product.Id == 1);

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

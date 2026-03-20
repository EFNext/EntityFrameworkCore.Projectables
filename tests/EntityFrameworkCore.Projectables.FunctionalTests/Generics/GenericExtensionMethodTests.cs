using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests.Generics
{
    public class GenericFunctionTests
    {
        [Fact]
        public Task DefaultIfIdIsNegative()
        {
            using var context = new SampleDbContext<Entity>();
            var query = context.Set<Entity>()
                .Select(x => x.DefaultIfIdIsNegative());

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

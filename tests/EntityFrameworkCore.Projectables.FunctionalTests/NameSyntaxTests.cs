using System;
using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class Local
    {
        [Flags]
        public enum SampleEnum
        {
            One  = 0b001,
            Two  = 0b010,
            Four = 0b100,
        }
    }
    
    public class NameSyntaxTests
    {
        public class Entity
        {
            public int Id { get; set; }
            
            [Projectable]
            public Local.SampleEnum? Test => Local.SampleEnum.One | Local.SampleEnum.Two | Local.SampleEnum.Four;
        }

        [Fact]
        public Task QualifiedNameSyntaxTest()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Test);

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

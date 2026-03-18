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
    public class GenericEntityTests
    {
        public abstract class BaseEntity<TSelf, TKey>
            where TSelf : BaseEntity<TSelf, TKey>
        {
            public TKey Id { get; set; } = default!;

            [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
            public bool HasMatchingStringKeyConversion(string key)
                => Id?.ToString() == key;
        }

        public class ConcreteEntity : BaseEntity<ConcreteEntity, int>
        {

        }

        [Fact]
        public Task HasMatchingStringKeyConversion_GetsTranslated()
        {
            using var context = new SampleDbContext<ConcreteEntity>();
            var key = "x";
            var query = context.Set<ConcreteEntity>()
                .Where(x => x.HasMatchingStringKeyConversion(key))
                .Select(x => x.Id);

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

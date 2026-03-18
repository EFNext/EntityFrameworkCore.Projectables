using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests;

public class MethodGroupTests
{
    public record Entity
    {
        public int Id { get; set; }

        public List<Entity>? RelatedEntities { get; set; }
    }

    [Projectable]
    public static int NextId(Entity entity) => entity.Id + 1;

    [Fact]
    public Task ProjectOverMethodGroup()
    {
        using var dbContext = new SampleDbContext<Entity>();

        var query = dbContext.Set<Entity>()
            .Select(x => new { NextIds = x.RelatedEntities!.Select(NextId) });

        return Verifier.Verify(query.ToQueryString());
    }
}
#if NET10_0_OR_GREATER
using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests.ExtensionMembers
{
    /// <summary>
    /// Tests for C# 14 extension member support.
    /// These tests only run on .NET 10+ where extension members are supported.
    /// Note: Extension properties cannot currently be used directly in LINQ expression trees (CS9296),
    /// so only extension methods are tested here.
    /// </summary>
    public class ExtensionMemberTests
    {
        [Fact]
        public Task ExtensionMemberMethodOnEntity()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.TripleId());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ExtensionMemberMethodWithParameterOnEntity()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Multiply(5));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ExtensionMemberMethodOnGenericReceiverType()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => new GenericWrapper<Entity> { Id = x.Id })
                .Select(x => x.DoubleId());

            return Verifier.Verify(query.ToQueryString());
        }
    }
}
#endif

using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    /// <summary>
    /// An entity that uses the partial class pattern to expose private projectable members.
    /// Without the partial keyword, <see cref="PartialEntity.Total"/> could not call the private
    /// <see cref="PartialEntity.DoubleId"/> because the generated companion class would be in a
    /// separate namespace and lack access to private members.
    /// </summary>
    public partial record PartialEntity
    {
        public int Id { get; set; }

        /// <summary>
        /// Public projectable that delegates to a private projectable.
        /// Requires the companion to be nested inside this type so it can call DoubleId.
        /// </summary>
        [Projectable]
        public int Total => DoubleId + 1;

        /// <summary>
        /// Private projectable — only accessible from within this type.
        /// The companion is generated as a nested class inside <see cref="PartialEntity"/> (partial).
        /// </summary>
        [Projectable]
        private int DoubleId => Id * 2;
    }

    public class PartialClassWithPrivateMembersTests
    {
        [Fact]
        public Task FilterOnPrivateProjectableProperty()
        {
            using var dbContext = new SampleDbContext<PartialEntity>();

            var query = dbContext.Set<PartialEntity>()
                .Where(x => x.Total > 5);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectPrivateProjectableProperty()
        {
            using var dbContext = new SampleDbContext<PartialEntity>();

            var query = dbContext.Set<PartialEntity>()
                .Select(x => x.Total);

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

// ── Entity types for inline (partial class) projectable tests ─────────────────
// Defined at namespace level so they can be partial without requiring the
// test class to be partial.  EF Core only maps the source entity; DTOs are
// pure projection targets used in Select expressions.
namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    /// <summary>Entity whose public [Projectable] property depends on a private [Projectable].</summary>
    public partial class PrivatePropEntity
    {
        public int Id { get; set; }

        [Projectable]
        private int Doubled => Id * 2;

        [Projectable]
        public int Score => Doubled + 1;
    }

    /// <summary>Entity whose public [Projectable] property depends on a protected [Projectable].</summary>
    public partial class ProtectedPropEntity
    {
        public int Id { get; set; }

        [Projectable]
        protected int Doubled => Id * 2;

        [Projectable]
        public int Score => Doubled + 1;
    }

    /// <summary>Entity whose public [Projectable] method depends on a private [Projectable] method.</summary>
    public partial class PrivateMethodEntity
    {
        public int Id { get; set; }

        [Projectable]
        private int Double(int x) => x * 2;

        [Projectable]
        public int ComputedScore(int delta) => Double(Id) + delta;
    }

    // ── Constructor projectable with private static helper ──────────────────

    /// <summary>Simple source entity for constructor projection tests.</summary>
    public class PartialClassPersonSource
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    /// <summary>DTO built via a partial [Projectable] constructor that calls a private static helper.</summary>
    public partial class PartialPersonDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;

        public PartialPersonDto() { }  // required: EF Core uses the parameterless ctor for materialisation

        [Projectable]
        public PartialPersonDto(int id, string firstName, string lastName)
        {
            Id = id;
            FullName = FormatName(firstName, lastName);
        }

        [Projectable]
        private static string FormatName(string first, string last) => first + " " + last;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [UsesVerify]
    public class PartialClassProjectableTests
    {
        // ── Private property ──────────────────────────────────────────────────

        [Fact]
        public Task FilterOnPrivatePropertyProjectable()
        {
            using var dbContext = new SampleDbContext<PrivatePropEntity>();

            var query = dbContext.Set<PrivatePropEntity>()
                .Where(x => x.Score > 5);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectPrivatePropertyProjectable()
        {
            using var dbContext = new SampleDbContext<PrivatePropEntity>();

            var query = dbContext.Set<PrivatePropEntity>()
                .Select(x => x.Score);

            return Verifier.Verify(query.ToQueryString());
        }

        // ── Protected property ────────────────────────────────────────────────

        [Fact]
        public Task FilterOnProtectedPropertyProjectable()
        {
            using var dbContext = new SampleDbContext<ProtectedPropEntity>();

            var query = dbContext.Set<ProtectedPropEntity>()
                .Where(x => x.Score > 5);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectProtectedPropertyProjectable()
        {
            using var dbContext = new SampleDbContext<ProtectedPropEntity>();

            var query = dbContext.Set<ProtectedPropEntity>()
                .Select(x => x.Score);

            return Verifier.Verify(query.ToQueryString());
        }

        // ── Private method ────────────────────────────────────────────────────

        [Fact]
        public Task FilterOnPrivateMethodProjectable()
        {
            using var dbContext = new SampleDbContext<PrivateMethodEntity>();

            var query = dbContext.Set<PrivateMethodEntity>()
                .Where(x => x.ComputedScore(3) > 5);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectPrivateMethodProjectable()
        {
            using var dbContext = new SampleDbContext<PrivateMethodEntity>();

            var query = dbContext.Set<PrivateMethodEntity>()
                .Select(x => x.ComputedScore(3));

            return Verifier.Verify(query.ToQueryString());
        }

        // ── Constructor with private static helper ────────────────────────────

        [Fact]
        public Task SelectConstructorWithPrivateHelper()
        {
            using var dbContext = new SampleDbContext<PartialClassPersonSource>();

            var query = dbContext.Set<PartialClassPersonSource>()
                .Select(p => new PartialPersonDto(p.Id, p.FirstName, p.LastName));

            return Verifier.Verify(query.ToQueryString());
        }
    }
}


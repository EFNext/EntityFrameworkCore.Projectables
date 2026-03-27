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

        /// <summary>
        /// Regression test: extension member on a <em>closed</em> generic receiver type
        /// (e.g. <c>extension(GenericWrapper&lt;Entity&gt; w)</c>) previously threw
        /// "Unable to resolve generated expression" because <c>global::</c> inside generic
        /// type arguments caused a naming mismatch between the generator and the resolver.
        /// </summary>
        [Fact]
        public Task ExtensionMemberMethodOnClosedGenericReceiverType()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => new GenericWrapper<Entity> { Id = x.Id })
                .Select(x => x.DoubleId());

            return Verifier.Verify(query.ToQueryString());
        }

        /// <summary>
        /// Exercises support for extension members on an <em>open</em> generic receiver type
        /// (e.g. <c>extension&lt;T&gt;(GenericWrapper&lt;T&gt; w)</c>).
        /// The block-level type parameter <c>T</c> must be promoted to a method-level type
        /// parameter on the generated <c>Expression&lt;T&gt;()</c> factory so the runtime
        /// resolver can construct the correct closed-generic expression.
        /// </summary>
        [Fact]
        public Task ExtensionMemberMethodOnOpenGenericReceiverType()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => new GenericWrapper<Entity> { Id = x.Id })
                .Select(x => x.TripleId());

            return Verifier.Verify(query.ToQueryString());
        }
    }
}
#endif

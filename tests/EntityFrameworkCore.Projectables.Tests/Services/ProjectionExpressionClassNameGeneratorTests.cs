using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.Services;
using Xunit;

namespace EntityFrameworkCore.Projectables.Tests.Services
{
    public class ProjectionExpressionClassNameGeneratorTests
    {
        [Theory]
        [InlineData("ns", new string[] { "a" }, "m", "ns_a_m")]
        [InlineData("ns", new string[] { "a", "b" }, "m", "ns_a_b_m")]
        [InlineData(null, new string[] { "a" }, "m", "_a_m")]
        [InlineData("ns", new string[] { "a`1" }, "m", "ns_a_m`1" )]
        [InlineData("ns", new string[] { "a`1", "b`1" }, "m", "ns_a_b_m`2")]
        public void GenerateName(string? namespaceName, string[] nestedTypeNames, string memberName, string expected)
        {
            var result = ProjectionExpressionClassNameGenerator.GenerateName(namespaceName, nestedTypeNames, memberName);

            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifies that <c>global::</c> inside generic type argument lists is stripped, not
        /// preserved as <c>global__</c>.  This is critical for C# 14 extension members whose
        /// receiver type is a closed generic (e.g. <c>extension(Wrapper&lt;Entity&gt; w)</c>):
        /// the generator uses <c>SymbolDisplayFormat.FullyQualifiedFormat</c>
        /// which emits <c>global::</c> on every nested type, but the runtime resolver never
        /// includes <c>global::</c> — so both sides must agree on the sanitised name.
        /// </summary>
        [Theory]
        [InlineData(
            "global::Foo.Wrapper<global::Foo.Entity>",
            "ns_a_m_P0_Foo_Wrapper_Foo_Entity_")]
        [InlineData(
            "global::System.Collections.Generic.List<global::System.Int32>",
            "ns_a_m_P0_System_Collections_Generic_List_System_Int32_")]
        [InlineData(
            "global::Foo.Entity",
            "ns_a_m_P0_Foo_Entity")]
        public void GenerateName_WithGlobalPrefixInGenericArgs_StripsAllGlobalPrefixes(
            string paramTypeName, string expected)
        {
            var result = ProjectionExpressionClassNameGenerator.GenerateName(
                "ns", new[] { "a" }, "m", new[] { paramTypeName });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void GeneratedFullName()
        {
            var expected = $"{ProjectionExpressionClassNameGenerator.Namespace}.{ProjectionExpressionClassNameGenerator.GenerateName("a", new[] { "b" }, "m")}";
            var result = ProjectionExpressionClassNameGenerator.GenerateFullName("a", new [] { "b" }, "m");

            Assert.Equal(expected, result);
        }
    }
}

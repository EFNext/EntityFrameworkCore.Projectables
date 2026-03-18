using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public class InterfaceTests : ProjectionExpressionGeneratorTestsBase
{
    public InterfaceTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task ExplicitInterfaceMember()
    {
        var compilation = CreateCompilation(
            """
            using System;
            using EntityFrameworkCore.Projectables;

            public interface IBase
            {
                int ComputedProperty { get; }
            }

            public class Concrete : IBase
            {
                public int Id { get; }
                
                [Projectable]
                int IBase.ComputedProperty => Id + 1;
            }
            """);

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task DefaultInterfaceMember()
    {
        var compilation = CreateCompilation(
            """
            using System;
            using EntityFrameworkCore.Projectables;

            public interface IBase
            {
                int Id { get; }
                int ComputedProperty { get; }
                int ComputedMethod();
            }

            public interface IDefaultBase : IBase
            {
                [Projectable]
                int Default => ComputedProperty * 2;
            }
            """);

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task DefaultExplicitInterfaceMember()
    {
        var compilation = CreateCompilation(
            """
            using System;
            using EntityFrameworkCore.Projectables;

            public interface IBase
            {
                int Id { get; }
                int ComputedProperty { get; }
                int ComputedMethod();
            }

            public interface IDefaultBase
            {
                int Default { get; }
            }

            public interface IDefaultBaseImplementation : IDefaultBase, IBase
            {
                [Projectable]
                int IDefaultBase.Default => ComputedProperty * 2;
            }
            """);

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExplicitInterfaceImplementation()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public interface IStringId
    {
        string Id { get; }
    }

    public class Item : IStringId
    {
        public int Id { get; set; }
        
        // Explicit interface implementation without [Projectable]
        string IStringId.Id => Id.ToString();
        
        [Projectable]
        public string FormattedId => ((IStringId)this).Id;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
}
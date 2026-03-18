using Xunit.Abstractions;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

[UsesVerify]
public class RegistryTests : ProjectionExpressionGeneratorTestsBase
{
    public RegistryTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task NoProjectables_NoRegistry()
    {
        var compilation = CreateCompilation(@"class C { }");
        var result = RunGenerator(compilation);

        Assert.Null(result.RegistryTree);
        
        return Task.CompletedTask;
    }

    [Fact]
    public Task SingleProperty_RegistryContainsEntry()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }
        [Projectable]
        public int IdPlus1 => Id + 1;
    }
}");
        var result = RunGenerator(compilation);

        return Verifier.Verify(result.RegistryTree!.GetText().ToString());
    }

    [Fact]
    public Task SingleMethod_RegistryContainsEntry()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }
        [Projectable]
        public int AddDelta(int delta) => Id + delta;
    }
}");
        var result = RunGenerator(compilation);

        return Verifier.Verify(result.RegistryTree!.GetText().ToString());
    }

    [Fact]
    public Task MultipleProjectables_AllRegistered()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }
        [Projectable]
        public int IdPlus1 => Id + 1;
        [Projectable]
        public int AddDelta(int delta) => Id + delta;
    }
}");
        var result = RunGenerator(compilation);

        return Verifier.Verify(result.RegistryTree!.GetText().ToString());
    }

    [Fact]
    public Task GenericClass_NotIncludedInRegistry()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C<T> {
        public int Id { get; set; }
        [Projectable]
        public int IdPlus1 => Id + 1;
    }
}");
        var result = RunGenerator(compilation);
        
        Assert.Null(result.RegistryTree);
        
        return Task.CompletedTask;
    }

    [Fact]
    public Task Registry_ConstBindingFlagsUsedInBuild()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }
        [Projectable]
        public int IdPlus1 => Id + 1;
    }
}");
        var result = RunGenerator(compilation);

        return Verifier.Verify(result.RegistryTree!.GetText().ToString());
    }

    [Fact]
    public Task Registry_RegisterHelperUsesDeclaringTypeAssembly()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }
        [Projectable]
        public int IdPlus1 => Id + 1;
    }
}");
        var result = RunGenerator(compilation);

        return Verifier.Verify(result.RegistryTree!.GetText().ToString());
    }

    [Fact]
    public Task MethodOverloads_BothRegistered()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Id { get; set; }
        [Projectable]
        public int Add(int delta) => Id + delta;
        [Projectable]
        public long Add(long delta) => Id + delta;
    }
}");
        var result = RunGenerator(compilation);

        return Verifier.Verify(result.RegistryTree!.GetText().ToString());
    }
}

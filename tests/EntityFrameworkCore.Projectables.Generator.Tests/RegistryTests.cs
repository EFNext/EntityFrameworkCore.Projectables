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

    // ────────────────────────────────────────────────────────────────────────
    // Inline (partial class) registry entries — all three member kinds
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public Task PartialClass_Property_RegistryUsesRegisterInline()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class C {
        public int Id { get; set; }
        [Projectable]
        public int IdPlus1 => Id + 1;
    }
}");
        var result = RunGenerator(compilation);

        Assert.NotNull(result.RegistryTree);
        var registryText = result.RegistryTree!.GetText().ToString();
        Assert.Contains("RegisterInline", registryText);
        Assert.DoesNotContain("Register(map,", registryText); // only RegisterInline, no external Register

        return Verifier.Verify(registryText);
    }

    [Fact]
    public Task PartialClass_Method_RegistryUsesRegisterInline()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class C {
        public int Id { get; set; }
        [Projectable]
        public int AddDelta(int delta) => Id + delta;
    }
}");
        var result = RunGenerator(compilation);

        Assert.NotNull(result.RegistryTree);
        var registryText = result.RegistryTree!.GetText().ToString();
        Assert.Contains("RegisterInline", registryText);
        Assert.Contains("GetMethod", registryText);
        Assert.Contains("__Projectable__AddDelta", registryText);

        return Verifier.Verify(registryText);
    }

    [Fact]
    public Task PartialClass_Constructor_RegistryUsesRegisterInline()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class PointDto {
        public int X { get; set; }
        public int Y { get; set; }
        public PointDto() { }
        [Projectable]
        public PointDto(int x, int y) {
            X = x;
            Y = y;
        }
    }
}");
        var result = RunGenerator(compilation);

        Assert.NotNull(result.RegistryTree);
        var registryText = result.RegistryTree!.GetText().ToString();
        Assert.Contains("RegisterInline", registryText);
        Assert.Contains("GetConstructor", registryText);
        Assert.Contains("__Projectable___ctor", registryText);

        return Verifier.Verify(registryText);
    }

    [Fact]
    public Task MixedPartialAndNonPartial_BothInRegistryWithCorrectHelpers()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class PartialEntity {
        public int Id { get; set; }
        [Projectable]
        public int InlineScore => Id + 1;
    }
    public class NonPartialEntity {
        public int Id { get; set; }
        [Projectable]
        public int ExternalScore => Id + 2;
    }
}");
        var result = RunGenerator(compilation);

        Assert.NotNull(result.RegistryTree);
        var registryText = result.RegistryTree!.GetText().ToString();
        // Partial class uses RegisterInline; non-partial uses Register
        Assert.Contains("RegisterInline", registryText);
        Assert.Contains("Register(map,", registryText);

        return Verifier.Verify(registryText);
    }
}

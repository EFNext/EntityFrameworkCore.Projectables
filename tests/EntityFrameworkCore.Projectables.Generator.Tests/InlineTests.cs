using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

/// <summary>
/// Tests for the inline expression accessor feature: when the containing class is declared
/// <c>partial</c>, the generator emits the accessor as a <c>private static</c> hidden method
/// inside the class itself (instead of an external generated class), allowing the lambda to
/// capture <c>private</c> and <c>protected</c> members.
/// </summary>
[UsesVerify]
public class InlineTests : ProjectionExpressionGeneratorTestsBase
{
    public InlineTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    // ────────────────────────────────────────────────────────────────────────
    // Inline generation: partial classes
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public Task PartialClass_Property_GeneratesInlineAccessor()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class MyClass {
        public int Id { get; set; }
        [Projectable]
        public int IdPlus1 => Id + 1;
    }
}");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);
        // Inline: no external generated class, the accessor is inside the partial class
        Assert.DoesNotContain("EntityFrameworkCore.Projectables.Generated", result.GeneratedTrees[0].GetText().ToString());

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task PartialClass_Method_GeneratesInlineAccessor()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class MyClass {
        public int Id { get; set; }
        [Projectable]
        public int AddDelta(int delta) => Id + delta;
    }
}");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task PartialClass_InlineMethodName_EncodesParameters()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class MyClass {
        public int Id { get; set; }
        [Projectable]
        public int Add(int x) => Id + x;
        [Projectable]
        public long Add(long x) => Id + x;
    }
}");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedTrees.Length);

        return Verifier.Verify(result.GeneratedTrees.Select(t => t.ToString()));
    }

    [Fact]
    public Task PartialClass_GenericClass_GeneratesInlineAccessor()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class MyClass<T> {
        public T Value { get; set; }
        [Projectable]
        public T GetValue() => Value;
    }
}");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task PartialClass_Nested_AllPartial_GeneratesInlineAccessor()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class Outer {
        public partial class Inner {
            public int Id { get; set; }
            [Projectable]
            public int IdPlus1 => Id + 1;
        }
    }
}");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task PartialClass_InlineAccessor_HasHidingAttributes()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class MyClass {
        public int Id { get; set; }
        [Projectable]
        public int Score => Id * 2;
    }
}");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        var generatedText = result.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("EditorBrowsable", generatedText);
        Assert.Contains("Obsolete", generatedText);
        Assert.Contains("private static", generatedText);
        Assert.Contains("__Projectable__Score", generatedText);

        return Verifier.Verify(generatedText);
    }

    [Fact]
    public Task PartialClass_InlineAccessor_RegistryUsesInlinePath()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class MyClass {
        public int Id { get; set; }
        [Projectable]
        public int IdPlus1 => Id + 1;
    }
}");
        var result = RunGenerator(compilation);

        Assert.NotNull(result.RegistryTree);
        var registryText = result.RegistryTree!.GetText().ToString();
        // Registry must use RegisterInline, not Register
        Assert.Contains("RegisterInline", registryText);
        Assert.Contains("__Projectable__IdPlus1", registryText);

        return Verifier.Verify(registryText);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Non-partial classes: EFP0013 is reported, external class is generated
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NonPartialClass_ReportsEFP0013()
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

        // EFP0013 is Info — no Warning+ diagnostics
        Assert.Empty(result.Diagnostics);
        var diag = Assert.Single(result.AllDiagnostics.Where(d => d.Id == "EFP0013"));
        Assert.Equal(DiagnosticSeverity.Info, diag.Severity);
        Assert.Equal("C", diag.Location.SourceTree!
            .GetRoot().FindToken(diag.Location.SourceSpan.Start).ValueText);
    }

    [Fact]
    public void NonPartialClass_StillGeneratesExternalClass()
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

        Assert.Single(result.GeneratedTrees);
        // External path: class lives in the Generated namespace
        Assert.Contains("EntityFrameworkCore.Projectables.Generated", result.GeneratedTrees[0].GetText().ToString());
    }

    [Fact]
    public void NonPartialNestedClass_OuterNotPartial_ReportsEFP0013OnOuter()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public class Outer {           // not partial — EFP0013 on Outer
        public partial class Inner { // inner is partial but outer is not
            public int Id { get; set; }
            [Projectable]
            public int IdPlus1 => Id + 1;
        }
    }
}");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        var diag = Assert.Single(result.AllDiagnostics.Where(d => d.Id == "EFP0013"));
        Assert.Equal("Outer", diag.Location.SourceTree!
            .GetRoot().FindToken(diag.Location.SourceSpan.Start).ValueText);
    }

    [Fact]
    public void ExtensionMember_NoEFP0013()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public class MyClass {
        public int Id { get; set; }
    }
    public static class MyExtensions {
        extension(MyClass self) {
            [Projectable]
            public int IdPlus1 => self.Id + 1;
        }
    }
}");
        var result = RunGenerator(compilation);

        // Extension members never get EFP0013
        Assert.DoesNotContain(result.AllDiagnostics, d => d.Id == "EFP0013");
    }
}



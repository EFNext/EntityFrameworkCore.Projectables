using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using Xunit.Abstractions;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

/// <summary>
/// Tests that the generator reports <c>EFP0012</c> (Info) when a <c>[Projectable]</c> method
/// matches the factory-method pattern (expression body <c>=> new ContainingType { … }</c>),
/// and that it does NOT report the diagnostic for methods that do not match.
/// <para>
/// Unlike the previous standalone <c>DiagnosticAnalyzer</c>, the diagnostic is now emitted
/// directly in <see cref="ProjectionExpressionGenerator.Execute"/> so that it is part of the
/// same incremental pipeline that produces the expression tree — no separate analysis pass needed.
/// </para>
/// </summary>
public class FactoryMethodDiagnosticTests : ProjectionExpressionGeneratorTestsBase
{
    public FactoryMethodDiagnosticTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    // ────────────────────────────────────────────────────────────────────────
    // EFP0012 is reported
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ReportsEFP0012_OnStaticFactoryMethod()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class OtherObj { }
    class MyObj {
        [Projectable]
        public static MyObj Create(OtherObj o) => new MyObj { };
    }
}");
        var result = RunGenerator(compilation);

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0012", diag.Id);
        Assert.Equal(DiagnosticSeverity.Info, diag.Severity);
        Assert.Equal("Create", diag.Location.SourceTree!
            .GetRoot().FindToken(diag.Location.SourceSpan.Start).ValueText);
    }

    [Fact]
    public void ReportsEFP0012_OnInstanceFactoryMethod()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class OtherObj { }
    class MyObj {
        [Projectable]
        public MyObj Create(OtherObj o) => new MyObj { };
    }
}");
        var result = RunGenerator(compilation);

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0012", diag.Id);
    }

    [Fact]
    public void ReportsEFP0012_WithMultipleInitializerAssignments()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Src { public int A { get; set; } public int B { get; set; } }
    class Dest {
        public int A { get; set; }
        public int B { get; set; }
        [Projectable]
        public static Dest Map(Src s) => new Dest { A = s.A, B = s.B };
    }
}");
        var result = RunGenerator(compilation);

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0012", diag.Id);
    }

    [Fact]
    public void ReportsEFP0012_AndStillGeneratesExpressionTree()
    {
        // EFP0012 is Info — generation must not be blocked.
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Input { public int Value { get; set; } }
    class Output {
        public int Value { get; set; }
        [Projectable]
        public static Output From(Input i) => new Output { Value = i.Value };
    }
}");
        var result = RunGenerator(compilation);

        Assert.Single(result.Diagnostics.Where(d => d.Id == "EFP0012"));
        Assert.Single(result.GeneratedTrees); // expression tree is still generated
    }

    [Fact]
    public void ReportsEFP0012_WithProjectableOptions_Preserved()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class OtherObj { }
    class MyObj {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public static MyObj Create(OtherObj o) => new MyObj { };
    }
}");
        var result = RunGenerator(compilation);

        var diag = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0012", diag.Id);
    }

    // ────────────────────────────────────────────────────────────────────────
    // EFP0012 is NOT reported
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void NoEFP0012_WhenBodyHasConstructorArguments()
    {
        // new MyObj(x) { } — has constructor args, not a pure initializer
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class MyObj {
        public MyObj() { }
        public MyObj(int x) { }
        [Projectable]
        public static MyObj Create(int x) => new MyObj(x) { };
    }
}");
        var result = RunGenerator(compilation);

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "EFP0012");
    }

    [Fact]
    public void NoEFP0012_WhenBodyHasNoInitializer()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class MyObj {
        public MyObj() { }
        [Projectable]
        public static MyObj Create() => new MyObj();
    }
}");
        var result = RunGenerator(compilation);

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "EFP0012");
    }

    [Fact]
    public void NoEFP0012_WhenReturnTypeDoesNotMatchContainingClass()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Other { }
    class MyObj {
        [Projectable]
        public static Other Create() => new Other { };
    }
}");
        var result = RunGenerator(compilation);

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "EFP0012");
    }

    [Fact]
    public void NoEFP0012_WhenBodyIsNotObjectCreation()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class MyObj {
        public int Value { get; set; }
        [Projectable]
        public int Computed => Value * 2;
    }
}");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void NoEFP0012_ForProjectableConstructor()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class OtherObj { public int X { get; set; } }
    class MyObj {
        public int X { get; set; }
        public MyObj() { }
        [Projectable]
        public MyObj(OtherObj o) {
            X = o.X;
        }
    }
}");
        var result = RunGenerator(compilation);

        // A [Projectable] constructor is not a factory method — EFP0012 must not be reported.
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "EFP0012");
    }
}



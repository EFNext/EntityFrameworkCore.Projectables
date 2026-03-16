using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.CodeFixes.Tests;

/// <summary>
/// Tests for <see cref="FactoryMethodToCtorCodeFixProvider"/> (EFP0012).
/// Verifies the code fix output via Verify.Xunit snapshots.
/// </summary>
[UsesVerify]
public class FactoryMethodToCtorCodeFixProviderTests : CodeFixTestBase
{
    private static readonly FactoryMethodToCtorCodeFixProvider _provider = new();

    private static TextSpan FirstMethodIdentifierSpan(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First()
            .Identifier
            .Span;

    [Fact]
    public void FixableDiagnosticIds_ContainsEFP0012() =>
        Assert.Contains("EFP0012", _provider.FixableDiagnosticIds);

    [Fact]
    public Task CodeFix_SimpleStaticFactoryMethod() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class OtherObj { public string Prop1 { get; set; } }
    class MyObj {
        public string Prop1 { get; set; }
        [Projectable]
        public static MyObj Create(OtherObj obj) => new MyObj { Prop1 = obj.Prop1 };
    }
}",
                "EFP0012",
                FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task CodeFix_PreservesProjectableOptions() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class OtherObj { }
    class MyObj {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public static MyObj Create(OtherObj obj) => new MyObj { };
    }
}",
                "EFP0012",
                FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task CodeFix_AddsParameterlessConstructor_WhenNoneExists() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class Input { }
    class Output {
        [Projectable]
        public static Output Create(Input i) => new Output { };
    }
}",
                "EFP0012",
                FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task CodeFix_DoesNotAddParameterlessConstructor_WhenAlreadyPresent() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class Input { }
    class Output {
        public Output() { }
        [Projectable]
        public static Output Create(Input i) => new Output { };
    }
}",
                "EFP0012",
                FirstMethodIdentifierSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public async Task TwoCodeFixActionsAreOffered()
    {
        var actions = await GetCodeFixActionsAsync(
            @"
namespace Foo {
    class MyObj {
        [Projectable]
        public static MyObj Create() => new MyObj { };
    }
}",
            "EFP0012",
            FirstMethodIdentifierSpan,
            _provider);

        Assert.Equal(2, actions.Count);
        Assert.Contains("constructor", actions[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("callers", actions[1].Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoCodeFix_WhenPatternDoesNotMatch()
    {
        // Without [Projectable] the pattern check returns false → no fix registered.
        var actions = await GetCodeFixActionsAsync(
            @"
namespace Foo {
    class MyObj {
        public static MyObj Create() => new MyObj { };
    }
}",
            "EFP0012",
            FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }
}

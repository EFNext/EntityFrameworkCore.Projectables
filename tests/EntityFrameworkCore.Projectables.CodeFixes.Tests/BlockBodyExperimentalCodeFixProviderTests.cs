using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.CodeFixes.Tests;

/// <summary>
/// Tests for <see cref="BlockBodyExperimentalCodeFixProvider"/> (EFP0001).
/// The fix adds <c>AllowBlockBody = true</c> to the <c>[Projectable]</c> attribute.
/// </summary>
[UsesVerify]
public class BlockBodyExperimentalCodeFixProviderTests : CodeFixTestBase
{
    private readonly static BlockBodyExperimentalCodeFixProvider _provider = new();

    // Locates the first method identifier span — the code fix walks up to MemberDeclarationSyntax.
    private static TextSpan FirstMethodIdentifierSpan(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First()
            .Identifier
            .Span;

    [Fact]
    public void FixableDiagnosticIds_ContainsEFP0001() =>
        Assert.Contains("EFP0001", _provider.FixableDiagnosticIds);

    [Fact]
    public Task AddAllowBlockBody_WhenProjectableHasNoArguments() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class C {
        [Projectable]
        public int Bar() { return 42; }
    }
}",
                "EFP0001",
                FirstMethodIdentifierSpan,
                _provider));

    [Fact]
    public Task AddAllowBlockBody_WhenProjectableAlreadyHasOtherArguments() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public int Bar() { return 42; }
    }
}",
                "EFP0001",
                FirstMethodIdentifierSpan,
                _provider));

    [Fact]
    public Task ReplaceAllowBlockBody_WhenAlreadySetToFalse() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class C {
        [Projectable(AllowBlockBody = false)]
        public int Bar() { return 42; }
    }
}",
                "EFP0001",
                FirstMethodIdentifierSpan,
                _provider));

    [Fact]
    public Task AddAllowBlockBody_OnBlockBodiedProperty() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class C {
        [Projectable]
        public int Double
        {
            get { return 2; }
        }
    }
}",
                "EFP0001",
                root => root.DescendantNodes()
                    .OfType<PropertyDeclarationSyntax>()
                    .First()
                    .Identifier
                    .Span,
                _provider));

    [Fact]
    public async Task NoCodeFix_WhenMemberHasNoProjectableAttribute()
    {
        var actions = await GetCodeFixActionsAsync(
            @"
namespace Foo {
    class C {
        [OtherAttribute]
        public int Bar() { return 42; }
    }
}",
            "EFP0001",
            FirstMethodIdentifierSpan,
            _provider);

        Assert.Empty(actions);
    }
}




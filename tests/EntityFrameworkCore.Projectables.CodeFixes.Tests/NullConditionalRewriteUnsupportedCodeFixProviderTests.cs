using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.CodeFixes.Tests;

/// <summary>
/// Tests for <see cref="NullConditionalRewriteUnsupportedCodeFixProvider"/> (EFP0002).
/// The fix sets <c>NullConditionalRewriteSupport</c> on the <c>[Projectable]</c> attribute to
/// either <c>Ignore</c> (action index 0) or <c>Rewrite</c> (action index 1).
/// </summary>
[UsesVerify]
public class NullConditionalRewriteUnsupportedCodeFixProviderTests : CodeFixTestBase
{
    private readonly static NullConditionalRewriteUnsupportedCodeFixProvider _provider = new();

    private const string SourceWithNullConditional = @"
namespace Foo {
    class C {
        public string Name { get; set; }

        [Projectable]
        public int NameLength() => Name?.Length ?? 0;
    }
}";

    // Locates the first null-conditional expression — the code fix walks up to a [Projectable] member.
    private static TextSpan FirstConditionalAccessSpan(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<ConditionalAccessExpressionSyntax>()
            .First()
            .Span;

    [Fact]
    public void FixableDiagnosticIds_ContainsEFP0002() =>
        Assert.Contains("EFP0002", _provider.FixableDiagnosticIds);

    [Fact]
    public async Task RegistersBothCodeFixes()
    {
        var actions = await GetCodeFixActionsAsync(
            SourceWithNullConditional,
            "EFP0002",
            FirstConditionalAccessSpan,
            _provider);

        Assert.Equal(2, actions.Count);
        Assert.Contains(actions, a => a.Title.Contains("Ignore", StringComparison.Ordinal));
        Assert.Contains(actions, a => a.Title.Contains("Rewrite", StringComparison.Ordinal));
    }

    [Fact]
    public Task SetNullConditionalSupport_Ignore() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                SourceWithNullConditional,
                "EFP0002",
                FirstConditionalAccessSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public Task SetNullConditionalSupport_Rewrite() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                SourceWithNullConditional,
                "EFP0002",
                FirstConditionalAccessSpan,
                _provider,
                actionIndex: 1));

    [Fact]
    public Task ReplacesExistingNullConditionalRewriteSupport_WithRewrite() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class C {
        public string Name { get; set; }

        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public int NameLength() => Name?.Length ?? 0;
    }
}",
                "EFP0002",
                FirstConditionalAccessSpan,
                _provider,
                actionIndex: 1));

    [Fact]
    public Task ReplacesExistingNullConditionalRewriteSupport_WithIgnore() =>
        Verifier.Verify(
            ApplyCodeFixAsync(
                @"
namespace Foo {
    class C {
        public string Name { get; set; }

        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public int NameLength() => Name?.Length ?? 0;
    }
}",
                "EFP0002",
                FirstConditionalAccessSpan,
                _provider,
                actionIndex: 0));

    [Fact]
    public async Task NoCodeFix_WhenContainingMemberHasNoProjectableAttribute()
    {
        var actions = await GetCodeFixActionsAsync(
            @"
namespace Foo {
    class C {
        public string Name { get; set; }

        public int NameLength() => Name?.Length ?? 0;
    }
}",
            "EFP0002",
            FirstConditionalAccessSpan,
            _provider);

        Assert.Empty(actions);
    }
}



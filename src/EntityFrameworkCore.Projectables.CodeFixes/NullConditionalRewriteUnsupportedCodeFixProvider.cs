using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.CodeFixes;

/// <summary>
/// Code fix provider for EFP0002 (Null-conditional expression unsupported).
/// Offers two options to configure <c>NullConditionalRewriteSupport</c> on the <c>[Projectable]</c> attribute:
/// <list type="bullet">
///   <item><description><c>Ignore</c> — strips the null-conditional operator from the generated expression tree.</description></item>
///   <item><description><c>Rewrite</c> — translates the null-conditional operator into an explicit null check.</description></item>
/// </list>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullConditionalRewriteUnsupportedCodeFixProvider))]
[Shared]
public sealed class NullConditionalRewriteUnsupportedCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["EFP0002"];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // The diagnostic is on the null-conditional expression inside the body.
        // Walk up to find the containing member that carries [Projectable].
        var member = node.AncestorsAndSelf()
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(m => ProjectableCodeFixHelper.TryFindProjectableAttribute(m, out _));

        if (member is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Set NullConditionalRewriteSupport = Ignore on [Projectable]",
                createChangedDocument: ct => SetNullConditionalSupportAsync(context.Document, member, "Ignore", ct),
                equivalenceKey: "EFP0002_Ignore"),
            diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Set NullConditionalRewriteSupport = Rewrite on [Projectable]",
                createChangedDocument: ct => SetNullConditionalSupportAsync(context.Document, member, "Rewrite", ct),
                equivalenceKey: "EFP0002_Rewrite"),
            diagnostic);
    }

    private static Task<Document> SetNullConditionalSupportAsync(
        Document document,
        MemberDeclarationSyntax member,
        string enumValueName,
        CancellationToken cancellationToken)
    {
        // Produces: NullConditionalRewriteSupport.Ignore  (or .Rewrite)
        var enumValue = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName("NullConditionalRewriteSupport"),
            SyntaxFactory.IdentifierName(enumValueName));

        return ProjectableCodeFixHelper.AddOrReplaceNamedArgumentInProjectableAttributeAsync(
            document,
            member,
            "NullConditionalRewriteSupport",
            enumValue,
            cancellationToken);
    }
}


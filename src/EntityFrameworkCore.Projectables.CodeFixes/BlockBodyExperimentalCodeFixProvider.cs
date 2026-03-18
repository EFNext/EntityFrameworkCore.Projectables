using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.CodeFixes;

/// <summary>
/// Code fix provider for EFP0001 (Block-bodied member support is experimental).
/// Adds <c>AllowBlockBody = true</c> to the <c>[Projectable]</c> attribute.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BlockBodyExperimentalCodeFixProvider))]
[Shared]
public sealed class BlockBodyExperimentalCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["EFP0001"];

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
        var member = node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        if (member is null)
        {
            return;
        }

        if (!ProjectableCodeFixHelper.TryFindProjectableAttribute(member, out _))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add AllowBlockBody = true to [Projectable]",
                createChangedDocument: ct =>
                    ProjectableCodeFixHelper.AddOrReplaceNamedArgumentInProjectableAttributeAsync(
                        context.Document,
                        member,
                        "AllowBlockBody",
                        SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression),
                        ct),
                equivalenceKey: "EFP0001_AddAllowBlockBody"),
            diagnostic);
    }
}


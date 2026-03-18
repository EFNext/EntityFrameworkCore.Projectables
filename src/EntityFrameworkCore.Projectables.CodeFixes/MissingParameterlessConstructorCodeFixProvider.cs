using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace EntityFrameworkCore.Projectables.CodeFixes;

/// <summary>
/// Code fix provider for EFP0008 (Target class is missing a parameterless constructor).
/// Inserts a <c>public ClassName() { }</c> constructor into the class that carries the
/// <c>[Projectable]</c> constructor, satisfying the object-initializer requirement of the
/// generated expression tree.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingParameterlessConstructorCodeFixProvider))]
[Shared]
public sealed class MissingParameterlessConstructorCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ["EFP0008"];

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

        // The diagnostic is reported on the [Projectable] constructor declaration.
        // Walk up to find the containing type.
        var typeDecl = node.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (typeDecl is null)
        {
            return;
        }

        var typeName = typeDecl.Identifier.Text;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add parameterless constructor to '{typeName}'",
                createChangedDocument: ct => AddParameterlessConstructorAsync(context.Document, typeDecl, ct),
                equivalenceKey: "EFP0008_AddParameterlessConstructor"),
            diagnostic);
    }

    private async static Task<Document> AddParameterlessConstructorAsync(
        Document document,
        TypeDeclarationSyntax typeDecl,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var parameterlessCtor = SyntaxFactory
            .ConstructorDeclaration(typeDecl.Identifier.WithoutTrivia())
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword)
                    .WithTrailingTrivia(SyntaxFactory.Space)))
            .WithParameterList(SyntaxFactory.ParameterList())
            .WithBody(SyntaxFactory.Block())
            .WithAdditionalAnnotations(Formatter.Annotation)
            .WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        // Insert before the first existing member so it appears at the top of the class body.
        var newTypeDecl = typeDecl.WithMembers(
            typeDecl.Members.Insert(0, parameterlessCtor));

        var newRoot = root.ReplaceNode(typeDecl, newTypeDecl);
        return document.WithSyntaxRoot(newRoot);
    }
}


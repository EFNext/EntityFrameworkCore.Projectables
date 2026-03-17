using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace EntityFrameworkCore.Projectables.CodeFixes;

/// <summary>
/// Code fix provider for <c>EFP0012</c>.
/// Offers two fixes on a <c>[Projectable]</c> factory method that can be a constructor:
/// <list type="number">
///   <item><description>Convert the factory method to a constructor (current document).</description></item>
///   <item><description>Convert the factory method to a constructor <em>and</em> update all
///       callers throughout the solution.</description></item>
/// </list>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FactoryMethodToCtorCodeFixProvider))]
[Shared]
public sealed class FactoryMethodToCtorCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ["EFP0012"];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var node = root.FindNode(context.Span);

        if (!ProjectableCodeFixHelper.TryGetFixableFactoryMethodPattern(node, out var containingType, out var method))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert [Projectable] factory method to constructor",
                createChangedDocument: ct =>
                    FactoryMethodTransformationHelper.ConvertToConstructorAsync(
                        context.Document, method!, containingType!, ct),
                equivalenceKey: "EFP0012_FactoryToConstructor"),
            context.Diagnostics[0]);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert [Projectable] factory method to constructor (and update callers)",
                createChangedSolution: ct =>
                    FactoryMethodTransformationHelper.ConvertToConstructorAndUpdateCallersAsync(
                        context.Document, method!, containingType!, ct),
                equivalenceKey: "EFP0012_FactoryToConstructorWithCallers"),
            context.Diagnostics[0]);
    }
}


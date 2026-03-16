using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace EntityFrameworkCore.Projectables.CodeFixes.Tests;

/// <summary>
/// Base class providing helpers for <see cref="CodeRefactoringProvider"/> tests.
/// Builds an in-memory document, invokes the provider at the supplied span, and
/// optionally applies one of the offered refactoring actions.
/// </summary>
public abstract class RefactoringTestBase : CodeFixTestBase
{
    private async static Task<(Document Document, IReadOnlyList<CodeAction> Actions)> CollectRefactoringActionsAsync(
        [StringSyntax("csharp")]
        string source,
        Func<SyntaxNode, TextSpan> locateSpan,
        CodeRefactoringProvider provider)
    {
        var document = CreateDocument(source);
        var root = await document.GetSyntaxRootAsync();
        var span = locateSpan(root!);

        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(
            document,
            span,
            action => actions.Add(action),
            CancellationToken.None);

        await provider.ComputeRefactoringsAsync(context);
        return (document, actions);
    }

    /// <summary>
    /// Returns all <see cref="CodeAction"/> instances offered by <paramref name="provider"/>
    /// for the span returned by <paramref name="locateSpan"/>.
    /// </summary>
    protected async static Task<IReadOnlyList<CodeAction>> GetRefactoringActionsAsync(
        [StringSyntax("csharp")]
        string source,
        Func<SyntaxNode, TextSpan> locateSpan,
        CodeRefactoringProvider provider)
    {
        var (_, actions) = await CollectRefactoringActionsAsync(source, locateSpan, provider);
        return actions;
    }

    /// <summary>
    /// Applies the refactoring action at <paramref name="actionIndex"/> and returns the full
    /// source text of the primary (originating) document after the change.
    /// </summary>
    protected async static Task<string> ApplyRefactoringAsync(
        [StringSyntax("csharp")]
        string source,
        Func<SyntaxNode, TextSpan> locateSpan,
        CodeRefactoringProvider provider,
        int actionIndex = 0)
    {
        var (document, actions) = await CollectRefactoringActionsAsync(source, locateSpan, provider);

        Assert.True(
            actions.Count > actionIndex,
            $"Expected at least {actionIndex + 1} refactoring action(s) but only {actions.Count} were registered.");

        var action = actions[actionIndex];
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyOp = operations.OfType<ApplyChangesOperation>().Single();

        var newDocument = applyOp.ChangedSolution.GetDocument(document.Id)!;
        var newRoot = await newDocument.GetSyntaxRootAsync();
        return newRoot!.ToFullString();
    }
}


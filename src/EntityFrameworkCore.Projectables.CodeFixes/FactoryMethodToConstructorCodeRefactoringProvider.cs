using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace EntityFrameworkCore.Projectables.CodeFixes;

/// <summary>
/// Code refactoring provider that converts a <c>[Projectable]</c> factory method whose body is
/// an object-initializer expression (<c>=> new T { … }</c>) into a <c>[Projectable]</c>
/// constructor of the same class.
/// <para>
/// Two refactoring actions are offered:
/// <list type="number">
///   <item><description>Convert the factory method to a constructor (current document only).</description></item>
///   <item><description>Convert the factory method to a constructor <em>and</em> replace all
///       callers throughout the solution with <c>new T(…)</c> invocations.</description></item>
/// </list>
/// </para>
/// <para>
/// This provider is complementary to <see cref="FactoryMethodToCtorCodeFixProvider"/>,
/// which fixes the <c>EFP0012</c> diagnostic. The refactoring provider remains useful when
/// the diagnostic is suppressed.
/// </para>
/// <para>
/// A public parameterless constructor is automatically inserted when the class does not already
/// have one, preserving the implicit default constructor that would otherwise be lost.
/// </para>
/// </summary>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(FactoryMethodToConstructorCodeRefactoringProvider))]
[Shared]
public sealed class FactoryMethodToConstructorCodeRefactoringProvider : CodeRefactoringProvider
{
    /// <inheritdoc />
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
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

        context.RegisterRefactoring(
            CodeAction.Create(
                title: "Convert [Projectable] factory method to constructor",
                createChangedDocument: ct =>
                    FactoryMethodTransformationHelper.ConvertToConstructorAsync(
                        context.Document, method!, containingType!, ct),
                equivalenceKey: "EFP_FactoryToConstructor"));

        context.RegisterRefactoring(
            CodeAction.Create(
                title: "Convert [Projectable] factory method to constructor (and update callers)",
                createChangedSolution: ct =>
                    FactoryMethodTransformationHelper.ConvertToConstructorAndUpdateCallersAsync(
                        context.Document, method!, containingType!, ct),
                equivalenceKey: "EFP_FactoryToConstructorWithCallers"));
    }
}

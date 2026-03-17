using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace EntityFrameworkCore.Projectables.CodeFixes.Tests;

/// <summary>
/// Base class providing helpers for code fix provider tests.
/// Creates an in-memory workspace document, builds a synthetic diagnostic at the
/// supplied span, invokes the provider, and optionally applies a code action.
/// </summary>
public abstract class CodeFixTestBase
{
    protected static Document CreateDocument([StringSyntax("csharp")] string source)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        return solution.GetDocument(documentId)!;
    }

    /// <summary>
    /// Like <see cref="CreateDocument"/>, but also adds all trusted platform assemblies
    /// so that the workspace's semantic model can resolve symbols fully.
    /// Required by refactoring actions that call
    /// <c>SymbolFinder.FindReferencesAsync</c> (e.g. "update callers").
    /// </summary>
    protected static Document CreateDocumentWithReferences([StringSyntax("csharp")] string source)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var trustedAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => MetadataReference.CreateFromFile(p))
            .Cast<MetadataReference>()
            .ToList();

        var solution = workspace.CurrentSolution
            .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp)
            .WithProjectCompilationOptions(
                projectId,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithProjectMetadataReferences(projectId, trustedAssemblies)
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        return solution.GetDocument(documentId)!;
    }

    private async static Task<(Document Document, IReadOnlyList<CodeAction> Actions)> CollectActionsAsync(
        [StringSyntax("csharp")] string source,
        string diagnosticId,
        Func<SyntaxNode, TextSpan> locateDiagnosticSpan,
        CodeFixProvider provider)
    {
        var document = CreateDocument(source);
        var root = await document.GetSyntaxRootAsync();
        var span = locateDiagnosticSpan(root!);

        var tree = await document.GetSyntaxTreeAsync();
        var descriptor = new DiagnosticDescriptor(
            diagnosticId,
            "Test diagnostic",
            "Test message",
            "Test",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        var location = Location.Create(tree!, span);
        var diagnostic = Diagnostic.Create(descriptor, location);

        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await provider.RegisterCodeFixesAsync(context);
        return (document, actions);
    }

    /// <summary>
    /// Collects the <see cref="CodeAction"/> titles offered by <paramref name="provider"/>
    /// for a synthetic diagnostic with <paramref name="diagnosticId"/> located at the span
    /// returned by <paramref name="locateDiagnosticSpan"/>.
    /// </summary>
    protected async static Task<IReadOnlyList<CodeAction>> GetCodeFixActionsAsync(
        [StringSyntax("csharp")] string source,
        string diagnosticId,
        Func<SyntaxNode, TextSpan> locateDiagnosticSpan,
        CodeFixProvider provider)
    {
        var (_, actions) = await CollectActionsAsync(source, diagnosticId, locateDiagnosticSpan, provider);
        return actions;
    }

    /// <summary>
    /// Applies the code fix action at <paramref name="actionIndex"/> and returns the full
    /// source text of the resulting document.
    /// </summary>
    protected async static Task<string> ApplyCodeFixAsync(
        [StringSyntax("csharp")] string source,
        string diagnosticId,
        Func<SyntaxNode, TextSpan> locateDiagnosticSpan,
        CodeFixProvider provider,
        int actionIndex = 0)
    {
        var (document, actions) = await CollectActionsAsync(source, diagnosticId, locateDiagnosticSpan, provider);

        Assert.True(
            actions.Count > actionIndex,
            $"Expected at least {actionIndex + 1} code fix action(s) but only {actions.Count} were registered.");

        var action = actions[actionIndex];
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyOp = operations.OfType<ApplyChangesOperation>().Single();

        var newDocument = applyOp.ChangedSolution.GetDocument(document.Id)!;
        var newRoot = await newDocument.GetSyntaxRootAsync();
        return newRoot!.ToFullString();
    }
}


using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace EntityFrameworkCore.Projectables.CodeFixes;

/// <summary>
/// Shared helpers for the factory-method → projectable-constructor transformation.
/// Used by both <see cref="FactoryMethodToConstructorCodeRefactoringProvider"/> and
/// <see cref="FactoryMethodToCtorCodeFixProvider"/>.
/// </summary>
static internal class FactoryMethodTransformationHelper
{
    /// <summary>
    /// Fetches a fresh root, applies the factory → constructor transformation, and
    /// returns an updated document.
    /// </summary>
    async static internal Task<Document> ConvertToConstructorAsync(
        Document document,
        MethodDeclarationSyntax method,
        TypeDeclarationSyntax containingType,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        return document.WithSyntaxRoot(BuildRootWithConstructor(root, method, containingType));
    }

    /// <summary>
    /// Applies the factory → constructor transformation on the declaring document and
    /// replaces all <c>instance.FactoryMethod(args)</c> call sites in the solution with
    /// <c>new ReturnType(args)</c>.
    /// </summary>
    async static internal Task<Solution> ConvertToConstructorAndUpdateCallersAsync(
        Document document,
        MethodDeclarationSyntax method,
        TypeDeclarationSyntax containingType,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document.Project.Solution;
        }

        var methodSymbol = semanticModel.GetDeclaredSymbol(method, cancellationToken);
        if (methodSymbol is null)
        {
            return document.Project.Solution;
        }

        var solution = document.Project.Solution;
        var returnType = methodSymbol.ReturnType;
        var returnTypeSyntax = SyntaxFactory
            .ParseTypeName(returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .WithAdditionalAnnotations(Simplifier.Annotation);

        // Find all callers BEFORE modifying the solution so that spans are still valid.
        var references = await SymbolFinder
            .FindReferencesAsync(methodSymbol, solution, cancellationToken)
            .ConfigureAwait(false);

        // Group locations by document (including the declaring document).
        var locationsByDoc = new Dictionary<DocumentId, List<ReferenceLocation>>();
        foreach (var referencedSymbol in references)
        {
            foreach (var refLocation in referencedSymbol.Locations)
            {
                if (!locationsByDoc.TryGetValue(refLocation.Document.Id, out var list))
                {
                    list = [];
                    locationsByDoc[refLocation.Document.Id] = list;
                }

                list.Add(refLocation);
            }
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return solution;
        }

        // Map annotation → data needed to build the replacement node.
        var invocationAnnotations = new Dictionary<SyntaxAnnotation,
            (ArgumentListSyntax ArgList, SyntaxTriviaList Leading, SyntaxTriviaList Trailing)>();

        var workingRoot = root;

        if (locationsByDoc.TryGetValue(document.Id, out var declaringDocLocations))
        {
            // Use a Dictionary keyed by invocation node to deduplicate: multiple
            // reference spans can resolve to the same InvocationExpressionSyntax.
            var annotationByInvocation = new Dictionary<InvocationExpressionSyntax, SyntaxAnnotation>();

            foreach (var refLocation in declaringDocLocations)
            {
                var refNode = root.FindNode(refLocation.Location.SourceSpan);
                var invocation = refNode.AncestorsAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault();

                if (invocation is null || invocation.Parent is ConditionalAccessExpressionSyntax)
                {
                    continue;
                }

                if (annotationByInvocation.ContainsKey(invocation))
                {
                    // Same invocation reached via a different reference span — skip.
                    continue;
                }

                var ann = new SyntaxAnnotation();
                invocationAnnotations[ann] = (
                    invocation.ArgumentList,
                    invocation.GetLeadingTrivia(),
                    invocation.GetTrailingTrivia());
                annotationByInvocation[invocation] = ann;
            }

            if (annotationByInvocation.Count > 0)
            {
                // Annotate all unique invocations in one ReplaceNodes pass.
                // This does NOT shift spans — only metadata is added.
                workingRoot = root.ReplaceNodes(
                    annotationByInvocation.Keys,
                    (original, _) => original.WithAdditionalAnnotations(annotationByInvocation[original]));
            }
        }

        // Re-find method and containingType in workingRoot by their original spans
        // (safe because adding annotations does not shift spans).
        var currentMethod = workingRoot.FindNode(method.Span) as MethodDeclarationSyntax ?? method;
        var currentContainingType = workingRoot.FindNode(containingType.Span) as TypeDeclarationSyntax ?? containingType;

        // Apply the factory → constructor transformation.
        // Annotated call-site nodes that live OUTSIDE the transformed type survive
        // untouched (annotations are preserved by ReplaceNode).
        var transformedRoot = BuildRootWithConstructor(workingRoot, currentMethod, currentContainingType);

        // Replace annotated invocations — found by annotation, not by span.
        var finalDeclaringRoot = transformedRoot;
        foreach (var annEntry in invocationAnnotations)
        {
            var ann = annEntry.Key;
            var argList = annEntry.Value.ArgList;
            var leading = annEntry.Value.Leading;
            var trailing = annEntry.Value.Trailing;

            var annotatedInvocation = finalDeclaringRoot
                .GetAnnotatedNodes(ann)
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault();

            if (annotatedInvocation is null)
            {
                continue;
            }

            // Rewrite:  instance.FactoryMethod(args)  →  new ReturnType(args)
            var newCreation = SyntaxFactory
                .ObjectCreationExpression(
                    SyntaxFactory.Token(SyntaxKind.NewKeyword)
                        .WithTrailingTrivia(SyntaxFactory.Space),
                    returnTypeSyntax,
                    argList,
                    initializer: null)
                .WithLeadingTrivia(leading)
                .WithTrailingTrivia(trailing);

            finalDeclaringRoot = finalDeclaringRoot.ReplaceNode(annotatedInvocation, newCreation);
        }

        solution = solution.WithDocumentSyntaxRoot(document.Id, finalDeclaringRoot);

        // -----------------------------------------------------------------------
        // Other caller documents — spans in these roots are still the original
        // unmodified spans, so the existing end-to-start approach is correct.
        // -----------------------------------------------------------------------
        foreach (var kvp in locationsByDoc)
        {
            var docId = kvp.Key;
            if (docId == document.Id)
            {
                // Already handled above via annotations.
                continue;
            }

            var locations = kvp.Value;

            var callerDoc = solution.GetDocument(docId);
            if (callerDoc is null)
            {
                continue;
            }

            var callerRoot = await callerDoc.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (callerRoot is null)
            {
                continue;
            }

            // Process end-to-start so that earlier spans remain valid.
            var newCallerRoot = callerRoot;
            foreach (var refLocation in locations.OrderByDescending(l => l.Location.SourceSpan.Start))
            {
                var refNode = newCallerRoot.FindNode(refLocation.Location.SourceSpan);
                var invocation = refNode.AncestorsAndSelf()
                    .OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault();

                if (invocation is null)
                {
                    continue;
                }

                // Skip conditional-access invocations like x?.FactoryMethod(...)
                // to avoid producing invalid syntax such as x?.new ReturnType(...).
                if (invocation.Parent is ConditionalAccessExpressionSyntax)
                {
                    continue;
                }

                // Rewrite:  instance.FactoryMethod(args)  →  new ReturnType(args)
                var newCreation = SyntaxFactory
                    .ObjectCreationExpression(
                        SyntaxFactory.Token(SyntaxKind.NewKeyword)
                            .WithTrailingTrivia(SyntaxFactory.Space),
                        returnTypeSyntax,
                        invocation.ArgumentList,
                        initializer: null)
                    .WithLeadingTrivia(invocation.GetLeadingTrivia())
                    .WithTrailingTrivia(invocation.GetTrailingTrivia());

                newCallerRoot = newCallerRoot.ReplaceNode(invocation, newCreation);
            }

            solution = solution.WithDocumentSyntaxRoot(docId, newCallerRoot);
        }

        return solution;
    }

    /// <summary>
    /// Core transformation: removes the factory method, inserts an equivalent
    /// <c>[Projectable]</c> constructor at the same position, and prepends a public
    /// parameterless constructor when the class does not already have one.
    /// </summary>
    private static SyntaxNode BuildRootWithConstructor(
        SyntaxNode root,
        MethodDeclarationSyntax method,
        TypeDeclarationSyntax containingType)
    {
        var creation = (ObjectCreationExpressionSyntax)method.ExpressionBody!.Expression;
        var initializer = creation.Initializer!;

        // Only support simple object-initializer assignments (Prop = value). If there are
        // other initializer forms (e.g., collection initializers), bail out to avoid
        // producing a constructor that does not preserve behavior.
        if (initializer.Expressions.Any(e => e is not AssignmentExpressionSyntax))
        {
            return root;
        }

        // Convert each object-initializer assignment (Prop = value) to a statement (Prop = value;).
        var statements = initializer.Expressions
            .OfType<AssignmentExpressionSyntax>()
            .Select(a => (StatementSyntax)SyntaxFactory.ExpressionStatement(a))
            .ToArray();

        var ctorModifiers = GetConstructorModifiers(method);

        var ctor = SyntaxFactory
            .ConstructorDeclaration(containingType.Identifier.WithoutTrivia())
            .WithAttributeLists(method.AttributeLists)
            .WithModifiers(ctorModifiers)
            .WithParameterList(method.ParameterList)
            .WithBody(SyntaxFactory.Block(statements))
            .WithAdditionalAnnotations(Formatter.Annotation)
            .WithLeadingTrivia(method.GetLeadingTrivia());

        var methodIndex = containingType.Members.IndexOf(method);
        var newMembers = containingType.Members.RemoveAt(methodIndex);
        newMembers = newMembers.Insert(Math.Min(methodIndex, newMembers.Count), ctor);

        // Add an explicit parameterless constructor if the class has none, to avoid breaking
        // code that relied on the implicit default constructor.
        var hasParamlessCtor = containingType.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Any(c => c.ParameterList.Parameters.Count == 0);

        if (!hasParamlessCtor)
        {
            var paramlessCtor = SyntaxFactory
                .ConstructorDeclaration(containingType.Identifier.WithoutTrivia())
                .WithModifiers(ctorModifiers)
                .WithParameterList(SyntaxFactory.ParameterList())
                .WithBody(SyntaxFactory.Block())
                .WithAdditionalAnnotations(Formatter.Annotation)
                .WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            newMembers = newMembers.Insert(0, paramlessCtor);
        }

        return root.ReplaceNode(containingType, containingType.WithMembers(newMembers));
    }

    private static SyntaxTokenList GetConstructorModifiers(MethodDeclarationSyntax method)
    {
        // Derive constructor modifiers from the factory method, dropping modifiers that are
        // invalid or meaningless for instance constructors (e.g., static, async, extern, unsafe).
        var filteredModifiers = method.Modifiers
            .Where(m =>
                !m.IsKind(SyntaxKind.StaticKeyword) &&
                !m.IsKind(SyntaxKind.AsyncKeyword) &&
                !m.IsKind(SyntaxKind.ExternKeyword) &&
                !m.IsKind(SyntaxKind.UnsafeKeyword));

        return SyntaxFactory.TokenList(filteredModifiers);
    }
}


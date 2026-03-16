using EntityFrameworkCore.Projectables.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using EntityFrameworkCore.Projectables.Generator.Comparers;
using EntityFrameworkCore.Projectables.Generator.Interpretation;
using EntityFrameworkCore.Projectables.Generator.Models;
using EntityFrameworkCore.Projectables.Generator.Registry;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace EntityFrameworkCore.Projectables.Generator;

[Generator]
public class ProjectionExpressionGenerator : IIncrementalGenerator
{
    private const string ProjectablesAttributeName = "EntityFrameworkCore.Projectables.ProjectableAttribute";

    private readonly static AttributeSyntax _editorBrowsableAttribute =
        Attribute(
            ParseName("global::System.ComponentModel.EditorBrowsable"),
            AttributeArgumentList(
                SingletonSeparatedList(
                    AttributeArgument(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("global::System.ComponentModel.EditorBrowsableState"),
                            IdentifierName("Never")
                        )
                    )
                )
            )
        );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Extract only pure stable data from the attribute in the transform.
        // No live Roslyn objects (no AttributeData, SemanticModel, Compilation, ISymbol) —
        // those are always new instances and defeat incremental caching entirely.
        var memberDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ProjectablesAttributeName,
                predicate: static (s, _) => s is MemberDeclarationSyntax,
                transform: static (c, _) => (
                    Member: (MemberDeclarationSyntax)c.TargetNode,
                    Attribute: new ProjectableAttributeData(c.Attributes[0])
                ));

        var compilationAndMemberPairs = memberDeclarations
            .Combine(context.CompilationProvider)
            .WithComparer(new MemberDeclarationSyntaxAndCompilationEqualityComparer());

        context.RegisterSourceOutput(compilationAndMemberPairs,
            static (spc, source) =>
            {
                var ((member, attribute), compilation) = source;
                var semanticModel = compilation.GetSemanticModel(member.SyntaxTree);
                var memberSymbol = semanticModel.GetDeclaredSymbol(member);

                if (memberSymbol is null)
                {
                    return;
                }

                Execute(member, semanticModel, memberSymbol, attribute, compilation, spc);
            });

        // Build the projection registry: collect all entries and emit a single registry file
        var registryEntries = compilationAndMemberPairs.Select(
            static (source, cancellationToken) => {
                var ((member, _), compilation) = source;

                var semanticModel = compilation.GetSemanticModel(member.SyntaxTree);
                var memberSymbol = semanticModel.GetDeclaredSymbol(member, cancellationToken);

                if (memberSymbol is null)
                {
                    return null;
                }

                return ExtractRegistryEntry(memberSymbol);
            });

        // Delegate registry file emission to the dedicated ProjectionRegistryEmitter,
        // which uses a string-based CodeWriter instead of SyntaxFactory.
        context.RegisterImplementationSourceOutput(
            registryEntries.Collect(),
            static (spc, entries) => ProjectionRegistryEmitter.Emit(entries, spc));
    }

    private static SyntaxTriviaList BuildSourceDocComment(ConstructorDeclarationSyntax ctor, Compilation compilation)
    {
        var chain = CollectConstructorChain(ctor, compilation);

        var lines = new List<SyntaxTrivia>();

        void AddLine(string text)
        {
            lines.Add(Comment(text));
            lines.Add(CarriageReturnLineFeed);
        }

        AddLine("/// <summary>");
        AddLine("/// <para>Generated from:</para>");

        foreach (var ctorSyntax in chain)
        {
            AddLine("/// <code>");
            var originalSource = ctorSyntax.NormalizeWhitespace().ToFullString();
            foreach (var rawLine in originalSource.Split('\n'))
            {
                var lineText = rawLine.TrimEnd('\r')
                    .Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;");
                AddLine($"/// {lineText}");
            }
            AddLine("/// </code>");
        }

        AddLine("/// </summary>");

        return TriviaList(lines);
    }

    /// <summary>
    /// Collects the constructor and every constructor it delegates to via <c>this(...)</c> or
    /// <c>base(...)</c>, in declaration order (annotated constructor first, then its delegate,
    /// then its delegate's delegate, …). Stops when a delegated constructor has no source
    /// available in the compilation (e.g. a compiler-synthesised parameterless constructor).
    /// </summary>
    private static List<ConstructorDeclarationSyntax> CollectConstructorChain(
        ConstructorDeclarationSyntax ctor, Compilation compilation)
    {
        var result = new List<ConstructorDeclarationSyntax> { ctor };
        var visited = new HashSet<SyntaxNode>() { ctor };

        var current = ctor;
        while (current.Initializer is { } initializer)
        {
            var semanticModel = compilation.GetSemanticModel(current.SyntaxTree);
            if (semanticModel.GetSymbolInfo(initializer).Symbol is not IMethodSymbol delegated)
            {
                break;
            }

            var delegatedSyntax = delegated.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax())
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault();

            if (delegatedSyntax is null || !visited.Add(delegatedSyntax))
            {
                break;
            }

            result.Add(delegatedSyntax);
            current = delegatedSyntax;
        }

        return result;
    }

    private static void Execute(
        MemberDeclarationSyntax member,
        SemanticModel semanticModel,
        ISymbol memberSymbol,
        ProjectableAttributeData projectableAttribute,
        Compilation? compilation,
        SourceProductionContext context)
    {
        var projectable = ProjectableInterpreter.GetDescriptor(
            semanticModel, member, memberSymbol, projectableAttribute, context, compilation);

        if (projectable is null)
        {
            return;
        }

        if (projectable.MemberName is null)
        {
            throw new InvalidOperationException("Expected a memberName here");
        }
        
        // Report EFP0012 when a [Projectable] method is a factory that could be a constructor.
        if (member is MethodDeclarationSyntax factoryCandidate)
        {
            ReportFactoryMethodDiagnosticIfApplicable(factoryCandidate, context);
        }

        var generatedClassName = ProjectionExpressionClassNameGenerator.GenerateName(projectable.ClassNamespace, projectable.NestedInClassNames, projectable.MemberName, projectable.ParameterTypeNames);
        var generatedFileName = projectable.ClassTypeParameterList is not null ? $"{generatedClassName}-{projectable.ClassTypeParameterList.ChildNodes().Count()}.g.cs" : $"{generatedClassName}.g.cs";

        var classSyntax = ClassDeclaration(generatedClassName)
            .WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword)))
            .WithTypeParameterList(projectable.ClassTypeParameterList)
            .WithConstraintClauses(projectable.ClassConstraintClauses ?? List<TypeParameterConstraintClauseSyntax>())
            .AddAttributeLists(
                AttributeList()
                    .AddAttributes(_editorBrowsableAttribute)
            )
            .WithLeadingTrivia(member is ConstructorDeclarationSyntax ctor && compilation is not null ? BuildSourceDocComment(ctor, compilation) : TriviaList())
            .AddMembers(
                MethodDeclaration(
                        GenericName(
                            Identifier("global::System.Linq.Expressions.Expression"),
                            TypeArgumentList(
                                SingletonSeparatedList(
                                    (TypeSyntax)GenericName(
                                        Identifier("global::System.Func"),
                                        GetLambdaTypeArgumentListSyntax(projectable)
                                    )
                                )
                            )
                        ),
                        "Expression"
                    )
                    .WithModifiers(TokenList(Token(SyntaxKind.StaticKeyword)))
                    .WithTypeParameterList(projectable.TypeParameterList)
                    .WithConstraintClauses(projectable.ConstraintClauses ?? List<TypeParameterConstraintClauseSyntax>())
                    .WithBody(
                        Block(
                            ReturnStatement(
                                ParenthesizedLambdaExpression(
                                    projectable.ParametersList ?? ParameterList(),
                                    null,
                                    projectable.ExpressionBody
                                )
                            )
                        )
                    )
            );

        var compilationUnit = CompilationUnit();

        foreach (var usingDirective in projectable.UsingDirectives!)
        {
            compilationUnit = compilationUnit.AddUsings(usingDirective);
        }

        if (projectable.ClassNamespace is not null)
        {
            compilationUnit = compilationUnit.AddUsings(
                UsingDirective(
                    ParseName(projectable.ClassNamespace)
                )
            );
        }

        compilationUnit = compilationUnit
            .AddMembers(
                NamespaceDeclaration(
                    ParseName("EntityFrameworkCore.Projectables.Generated")
                ).AddMembers(classSyntax)
            )
            .WithLeadingTrivia(
                TriviaList(
                    Comment("// <auto-generated/>"),
                    // Uncomment line below, for debugging purposes, to see when the generator is run on source generated files
                    // CarriageReturnLineFeed, Comment($"// Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} UTC for '{memberSymbol.Name}' in '{memberSymbol.ContainingType?.Name}'"),
                    Trivia(NullableDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true))
                )
            );

        context.AddSource(generatedFileName, SourceText.From(compilationUnit.NormalizeWhitespace().ToFullString(), Encoding.UTF8));

        static TypeArgumentListSyntax GetLambdaTypeArgumentListSyntax(ProjectableDescriptor projectable)
        {
            var lambdaTypeArguments = TypeArgumentList(
                SeparatedList(
                    // In Roslyn's syntax model, ParameterSyntax.Type is nullable: it is null for
                    // implicitly-typed lambda parameters (e.g. `(x, y) => x + y`).
                    // We filter those out to avoid passing null nodes into TypeArgumentList,
                    // which would cause a NullReferenceException at generation time.
                    // In practice all [Projectable] members have explicitly-typed parameters,
                    // so this filter acts as a defensive guard rather than a functional branch.
                    projectable.ParametersList?.Parameters.Where(p => p.Type is not null).Select(p => p.Type!)
                )
            );

            if (projectable.ReturnTypeName is not null)
            {
                lambdaTypeArguments = lambdaTypeArguments.AddArguments(ParseTypeName(projectable.ReturnTypeName));
            }

            return lambdaTypeArguments;
        }
    }

    /// <summary>
    /// Reports <c>EFP0012</c> when <paramref name="method"/> is a <c>[Projectable]</c> factory
    /// method whose expression body is a pure object-initializer expression targeting the
    /// containing class (e.g. <c>public static MyObj Create(…) => new MyObj { … }</c>).
    /// </summary>
    private static void ReportFactoryMethodDiagnosticIfApplicable(
        MethodDeclarationSyntax method,
        SourceProductionContext context)
    {
        if (method.Parent is not TypeDeclarationSyntax containingType)
        {
            return;
        }

        if (method.ExpressionBody is null)
        {
            return;
        }

        if (method.ExpressionBody.Expression is not ObjectCreationExpressionSyntax creation)
        {
            return;
        }

        // Only pure object-initializer bodies — no constructor arguments on the new expression.
        if (creation.ArgumentList?.Arguments.Count > 0)
        {
            return;
        }

        if (creation.Initializer is null)
        {
            return;
        }

        // The return type's simple name must equal the containing class name.
        var containingTypeName = containingType.Identifier.Text;
        if (GetFactorySimpleTypeName(method.ReturnType) != containingTypeName
            || GetFactorySimpleTypeName(creation.Type) != containingTypeName)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Infrastructure.Diagnostics.FactoryMethodShouldBeConstructor,
            method.Identifier.GetLocation(),
            method.Identifier.Text));
    }

    private static string? GetFactorySimpleTypeName(TypeSyntax type) =>
        type switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            QualifiedNameSyntax qn => qn.Right.Identifier.Text,
            _ => null
        };

    /// <summary>
    /// Extracts a <see cref="ProjectionRegistryEntry"/> from a member declaration.
    /// Returns null when the member does not have [Projectable], is an extension member,
    /// or cannot be represented in the registry (e.g. a generic class member or generic method).
    /// </summary>
    private static ProjectionRegistryEntry? ExtractRegistryEntry(ISymbol memberSymbol)
    {
        var containingType = memberSymbol.ContainingType;

        // Skip C# 14 extension type members — they require special handling (fall back to reflection)
        if (containingType is { IsExtension: true })
        {
            return null;
        }

        // Skip generic classes: the registry only supports closed constructed types.
        if (containingType.TypeParameters.Length > 0)
        {
            return null;
        }

        // Determine member kind and lookup name
        ProjectionRegistryMemberType memberKind;
        string memberLookupName;
        var parameterTypeNames = ImmutableArray<string>.Empty;

        if (memberSymbol is IMethodSymbol methodSymbol)
        {
            // Skip generic methods for the same reason as generic classes
            if (methodSymbol.TypeParameters.Length > 0)
            {
                return null;
            }

            if (methodSymbol.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor)
            {
                memberKind = ProjectionRegistryMemberType.Constructor;
                memberLookupName = "_ctor";
            }
            else
            {
                memberKind = ProjectionRegistryMemberType.Method;
                memberLookupName = memberSymbol.Name;
            }

            parameterTypeNames = [
                ..methodSymbol.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            ];
        }
        else
        {
            memberKind = ProjectionRegistryMemberType.Property;
            memberLookupName = memberSymbol.Name;
        }

        // Build the generated class name using the same logic as Execute
        var classNamespace = containingType.ContainingNamespace.IsGlobalNamespace
            ? null
            : containingType.ContainingNamespace.ToDisplayString();

        var nestedTypePath = GetRegistryNestedTypePath(containingType);

        var generatedClassName = ProjectionExpressionClassNameGenerator.GenerateName(
            classNamespace,
            nestedTypePath,
            memberLookupName,
            parameterTypeNames.IsEmpty ? null : parameterTypeNames);

        var generatedClassFullName = "EntityFrameworkCore.Projectables.Generated." + generatedClassName;

        var declaringTypeFullName = containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new ProjectionRegistryEntry(
            DeclaringTypeFullName: declaringTypeFullName,
            MemberKind: memberKind,
            MemberLookupName: memberLookupName,
            GeneratedClassFullName: generatedClassFullName,
            ParameterTypeNames: parameterTypeNames);
    }

    private static IEnumerable<string> GetRegistryNestedTypePath(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.ContainingType is not null)
        {
            foreach (var name in GetRegistryNestedTypePath(typeSymbol.ContainingType))
            {
                yield return name;
            }
        }
        yield return typeSymbol.Name;
    }
}
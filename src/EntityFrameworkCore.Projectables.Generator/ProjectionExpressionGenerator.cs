using EntityFrameworkCore.Projectables.Services;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using EntityFrameworkCore.Projectables.CodeFixes;
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

    private readonly static AttributeSyntax _obsoleteAttribute =
        Attribute(
            ParseName("global::System.Obsolete"),
            AttributeArgumentList(
                SingletonSeparatedList(
                    AttributeArgument(
                        LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            Literal("Generated member. Do not use."))))));

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
        if (member is MethodDeclarationSyntax factoryCandidate && SyntaxHelpers.TryGetFactoryMethodPattern(factoryCandidate, out _))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Infrastructure.Diagnostics.FactoryMethodShouldBeConstructor,
                factoryCandidate.Identifier.GetLocation(),
                factoryCandidate.Identifier.Text));
        }

        var generatedClassName = ProjectionExpressionClassNameGenerator.GenerateName(projectable.ClassNamespace, projectable.NestedInClassNames, projectable.MemberName, projectable.ParameterTypeNames);
        var generatedFileName = projectable.ClassTypeParameterList is not null ? $"{generatedClassName}-{projectable.ClassTypeParameterList.ChildNodes().Count()}.g.cs" : $"{generatedClassName}.g.cs";

        // Determine whether inline generation is possible:
        // all containing type declarations in the syntax hierarchy must carry the `partial` modifier,
        // and the member must not be a C# 14 extension member (those live in a synthetic type).
        var isExtensionMember = memberSymbol.ContainingType is { IsExtension: true };
        var containingTypeDecls = member.Ancestors().OfType<TypeDeclarationSyntax>().ToList();
        var generateInline = !isExtensionMember
            && containingTypeDecls.Count > 0
            && containingTypeDecls.All(t => t.Modifiers.Any(SyntaxKind.PartialKeyword));

        // EFP0013: suggest making the class partial to enable inline generation.
        if (!isExtensionMember && containingTypeDecls.Count > 0 && !generateInline)
        {
            var firstNonPartial = containingTypeDecls.First(t => !t.Modifiers.Any(SyntaxKind.PartialKeyword));
            context.ReportDiagnostic(Diagnostic.Create(
                Infrastructure.Diagnostics.ContainingClassShouldBePartial,
                firstNonPartial.Identifier.GetLocation(),
                firstNonPartial.Identifier.Text));
        }

        if (generateInline)
        {
            EmitInlinePartialClass(member, projectable, generatedFileName, containingTypeDecls, compilation, context);
        }
        else
        {
            EmitExternalClass(member, projectable, generatedClassName, generatedFileName, compilation, context);
        }
    }

    /// <summary>
    /// Generates the expression accessor as a <c>private static</c> method inside the declaring
    /// partial class. The method is hidden from the IDE via <c>[EditorBrowsable(Never)]</c> and
    /// <c>[Obsolete]</c>, and its name starts with <c>__Projectable__</c> to signal it is generated.
    /// Generating inside the class allows the lambda to capture <c>private</c> / <c>protected</c>
    /// members that would be inaccessible from an external generated class.
    /// </summary>
    private static void EmitInlinePartialClass(
        MemberDeclarationSyntax member,
        ProjectableDescriptor projectable,
        string generatedFileName,
        List<TypeDeclarationSyntax> containingTypeDecls,
        Compilation? compilation,
        SourceProductionContext context)
    {
        var inlineMethodName = ProjectionExpressionClassNameGenerator.GenerateInlineMethodName(
            projectable.MemberName!, projectable.ParameterTypeNames);

        var methodDecl = MethodDeclaration(
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
                inlineMethodName
            )
            .WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
            .WithTypeParameterList(projectable.TypeParameterList)
            .WithConstraintClauses(projectable.ConstraintClauses ?? List<TypeParameterConstraintClauseSyntax>())
            .AddAttributeLists(
                AttributeList().AddAttributes(_editorBrowsableAttribute),
                AttributeList().AddAttributes(_obsoleteAttribute)
            )
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
            );

        // Wrap the method in the partial class hierarchy (innermost containing type first).
        MemberDeclarationSyntax current = methodDecl;
        foreach (var typeDecl in containingTypeDecls)
        {
            current = CreatePartialTypeStub(typeDecl).AddMembers(current);
        }

        var compilationUnit = CompilationUnit();

        foreach (var usingDirective in projectable.UsingDirectives!)
        {
            compilationUnit = compilationUnit.AddUsings(usingDirective);
        }

        if (projectable.ClassNamespace is not null)
        {
            compilationUnit = compilationUnit.AddMembers(
                NamespaceDeclaration(ParseName(projectable.ClassNamespace))
                    .AddMembers((TypeDeclarationSyntax)current));
        }
        else
        {
            compilationUnit = compilationUnit.AddMembers((TypeDeclarationSyntax)current);
        }

        compilationUnit = compilationUnit
            .WithLeadingTrivia(
                TriviaList(
                    Comment("// <auto-generated/>"),
                    Trivia(NullableDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true))
                )
            );

        context.AddSource(generatedFileName, SourceText.From(compilationUnit.NormalizeWhitespace().ToFullString(), Encoding.UTF8));
    }

    /// <summary>
    /// Generates the expression accessor as an external <c>static</c> class in the
    /// <c>EntityFrameworkCore.Projectables.Generated</c> namespace. This is the classic
    /// (non-inline) code path used when the containing class is not fully <c>partial</c>.
    /// </summary>
    private static void EmitExternalClass(
        MemberDeclarationSyntax member,
        ProjectableDescriptor projectable,
        string generatedClassName,
        string generatedFileName,
        Compilation? compilation,
        SourceProductionContext context)
    {
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
                    Trivia(NullableDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true))
                )
            );

        context.AddSource(generatedFileName, SourceText.From(compilationUnit.NormalizeWhitespace().ToFullString(), Encoding.UTF8));
    }

    /// <summary>
    /// Creates a minimal <c>partial</c> stub of <paramref name="originalDecl"/> containing only
    /// the <c>partial</c> modifier, the type name, and the type-parameter list. All attribute
    /// lists, base types, constraints, and members are stripped so the stub can be used as a
    /// container wrapper in generated partial-class source files.
    /// </summary>
    private static TypeDeclarationSyntax CreatePartialTypeStub(TypeDeclarationSyntax originalDecl)
    {
        var stub = originalDecl
            .WithAttributeLists(List<AttributeListSyntax>())
            .WithModifiers(TokenList(Token(SyntaxKind.PartialKeyword)))
            .WithBaseList(null)
            .WithConstraintClauses(List<TypeParameterConstraintClauseSyntax>())
            .WithMembers(List<MemberDeclarationSyntax>());

        // Remove the primary constructor parameter list for record declarations.
        if (stub is RecordDeclarationSyntax record)
        {
            return record.WithParameterList(null);
        }

        return stub;
    }

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

        var declaringTypeFullName = containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Determine whether inline generation was used (all containing types are partial).
        var isInline = IsContainingTypeHierarchyPartial(containingType);

        if (isInline)
        {
            var inlineMethodName = ProjectionExpressionClassNameGenerator.GenerateInlineMethodName(
                memberLookupName,
                parameterTypeNames.IsEmpty ? null : (IEnumerable<string>)parameterTypeNames);

            return new ProjectionRegistryEntry(
                DeclaringTypeFullName: declaringTypeFullName,
                MemberKind: memberKind,
                MemberLookupName: memberLookupName,
                GeneratedClassFullName: string.Empty,
                ParameterTypeNames: parameterTypeNames,
                InlineMethodName: inlineMethodName);
        }

        // Build the generated class name using the same logic as EmitExternalClass.
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

        return new ProjectionRegistryEntry(
            DeclaringTypeFullName: declaringTypeFullName,
            MemberKind: memberKind,
            MemberLookupName: memberLookupName,
            GeneratedClassFullName: generatedClassFullName,
            ParameterTypeNames: parameterTypeNames);
    }

    /// <summary>
    /// Returns <c>true</c> when every type in the containing-type hierarchy (from
    /// <paramref name="typeSymbol"/> up to the outermost type) has at least one partial
    /// declaration. Used to decide whether to generate an inline accessor or an external class.
    /// </summary>
    private static bool IsContainingTypeHierarchyPartial(INamedTypeSymbol typeSymbol)
    {
        var isPartial = typeSymbol.DeclaringSyntaxReferences
            .Any(r => r.GetSyntax() is TypeDeclarationSyntax tds
                      && tds.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

        if (!isPartial)
        {
            return false;
        }

        if (typeSymbol.ContainingType is { } outer)
        {
            return IsContainingTypeHierarchyPartial(outer);
        }

        return true;
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

    private static TypeArgumentListSyntax GetLambdaTypeArgumentListSyntax(ProjectableDescriptor projectable)
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
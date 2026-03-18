using EntityFrameworkCore.Projectables.Generator.Models;
using EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.Interpretation;

static internal partial class ProjectableInterpreter
{
    public static ProjectableDescriptor? GetDescriptor(
        SemanticModel semanticModel,
        MemberDeclarationSyntax member,
        ISymbol memberSymbol,
        ProjectableAttributeData projectableAttribute,
        SourceProductionContext context,
        Compilation? compilation = null)
    {
        // Read directly from the struct fields
        var nullConditionalRewriteSupport = projectableAttribute.NullConditionalRewriteSupport;
        var useMemberBody = projectableAttribute.UseMemberBody;
        var expandEnumMethods = projectableAttribute.ExpandEnumMethods;
        var allowBlockBody = projectableAttribute.AllowBlockBody;

        // 1. Resolve the member body (handles UseMemberBody redirection)
        var memberBody = TryResolveMemberBody(member, memberSymbol, useMemberBody, context);
        if (memberBody is null)
        {
            return null;
        }

        // 2. Detect C# 14 extension member context
        var isExtensionMember = memberSymbol.ContainingType is { IsExtension: true };
        IParameterSymbol? extensionParameter = null;
        ITypeSymbol? extensionReceiverType = null;

        if (isExtensionMember && memberSymbol.ContainingType is { } extensionType)
        {
            extensionParameter = extensionType.ExtensionParameter;
            extensionReceiverType = extensionParameter?.Type;
        }

        // 3. Create syntax rewriters
        var targetTypeForRewriting = isExtensionMember && extensionReceiverType is INamedTypeSymbol receiverNamedType
            ? receiverNamedType
            : memberSymbol.ContainingType;

        var expressionSyntaxRewriter = new ExpressionSyntaxRewriter(
            targetTypeForRewriting,
            nullConditionalRewriteSupport,
            expandEnumMethods,
            semanticModel,
            context,
            extensionParameter?.Name);
        var declarationSyntaxRewriter = new DeclarationSyntaxRewriter(semanticModel);

        // 4. Build base descriptor (class names, namespaces, @this parameter, target class)
        var methodSymbol = memberSymbol as IMethodSymbol;
        var descriptor = BuildBaseDescriptor(
            member, memberSymbol, methodSymbol,
            isExtensionMember, extensionParameter, extensionReceiverType);

        // 5. Fill descriptor from the body — dispatch on the body syntax kind
        var success = (member, memberBody) switch
        {
            // Projectable method
            (_, MethodDeclarationSyntax methodDecl) =>
                TryApplyMethodBody(methodDecl, allowBlockBody, memberSymbol,
                    expressionSyntaxRewriter, declarationSyntaxRewriter, context, descriptor),

            // Projectable method whose body is an Expression<TDelegate> property
            (MethodDeclarationSyntax originalMethodDecl, PropertyDeclarationSyntax exprPropDecl) =>
                TryApplyExpressionPropertyBody(originalMethodDecl, exprPropDecl,
                    semanticModel, member, memberSymbol,
                    expressionSyntaxRewriter, declarationSyntaxRewriter, context, descriptor),

            // Projectable property whose body is an Expression<TDelegate> property
            (PropertyDeclarationSyntax originalPropertyDecl, PropertyDeclarationSyntax exprPropDecl)
                when IsExpressionDelegatePropertyDecl(exprPropDecl, semanticModel) =>
                TryApplyExpressionPropertyBodyForProperty(originalPropertyDecl, exprPropDecl,
                    semanticModel, member, memberSymbol,
                    expressionSyntaxRewriter, declarationSyntaxRewriter, context, descriptor),

            // Projectable property
            (_, PropertyDeclarationSyntax propDecl) =>
                TryApplyPropertyBody(propDecl, allowBlockBody, memberSymbol,
                    expressionSyntaxRewriter, declarationSyntaxRewriter, context, descriptor),

            // Projectable constructor
            (_, ConstructorDeclarationSyntax ctorDecl) =>
                TryApplyConstructorBody(ctorDecl, semanticModel, memberSymbol,
                    expressionSyntaxRewriter, declarationSyntaxRewriter, context, compilation, descriptor),

            _ => false
        };

        return success ? descriptor : null;
    }

    /// <summary>
    /// Builds a <see cref="ProjectableDescriptor"/> with all fields populated except
    /// <see cref="ProjectableDescriptor.ReturnTypeName"/>, <see cref="ProjectableDescriptor.ExpressionBody"/>,
    /// type-parameter lists, and constraint clauses (those are filled by the body processors).
    /// </summary>
    private static ProjectableDescriptor BuildBaseDescriptor(
        MemberDeclarationSyntax member,
        ISymbol memberSymbol,
        IMethodSymbol? methodSymbol,
        bool isExtensionMember,
        IParameterSymbol? extensionParameter,
        ITypeSymbol? extensionReceiverType)
    {
        // For extension members, use the outer class for naming
        var classForNaming = isExtensionMember && memberSymbol.ContainingType.ContainingType is not null
            ? memberSymbol.ContainingType.ContainingType
            : memberSymbol.ContainingType;

        // Sanitize constructor name (.ctor / .cctor are not valid C# identifiers, use _ctor)
        var memberName = methodSymbol?.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor
            ? "_ctor"
            : memberSymbol.Name;

        var descriptor = new ProjectableDescriptor
        {
            UsingDirectives = member.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>(),
            ClassName = classForNaming.Name,
            ClassNamespace = classForNaming.ContainingNamespace.IsGlobalNamespace
                ? null
                : classForNaming.ContainingNamespace.ToDisplayString(),
            MemberName = memberName,
            NestedInClassNames = isExtensionMember
                ? GetNestedInClassPathForExtensionMember(memberSymbol.ContainingType)
                : GetNestedInClassPath(memberSymbol.ContainingType),
            ParametersList = SyntaxFactory.ParameterList()
        };

        // Collect parameter type names for method overload disambiguation
        if (methodSymbol is not null)
        {
            var parameterTypeNames = methodSymbol.Parameters
                .Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToList();

            // For extension members, prepend the extension receiver type to match how the
            // runtime sees the method (receiver is the first implicit parameter).
            if (isExtensionMember && extensionReceiverType is not null)
            {
                parameterTypeNames.Insert(0,
                    extensionReceiverType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }

            descriptor.ParameterTypeNames = parameterTypeNames;
        }

        // Set up generic type parameters and constraints for the containing class
        if (classForNaming is { IsGenericType: true })
        {
            SetupGenericTypeParameters(descriptor, classForNaming);
        }

        // Add the implicit @this parameter
        if (isExtensionMember && extensionReceiverType is not null)
        {
            descriptor.ParametersList = descriptor.ParametersList.AddParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("@this"))
                    .WithType(SyntaxFactory.ParseTypeName(
                        extensionReceiverType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));
        }
        else if (!member.Modifiers.Any(SyntaxKind.StaticKeyword) && member is not ConstructorDeclarationSyntax)
        {
            descriptor.ParametersList = descriptor.ParametersList.AddParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("@this"))
                    .WithType(SyntaxFactory.ParseTypeName(
                        memberSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));
        }

        // Resolve target class info (used by the registry to associate the projection)
        if (isExtensionMember && extensionReceiverType is not null)
        {
            descriptor.TargetClassNamespace = extensionReceiverType.ContainingNamespace.IsGlobalNamespace
                ? null
                : extensionReceiverType.ContainingNamespace.ToDisplayString();
            descriptor.TargetNestedInClassNames = GetNestedInClassPath(extensionReceiverType);
        }
        else if (methodSymbol is { IsExtensionMethod: true })
        {
            var targetTypeSymbol = methodSymbol.Parameters.First().Type;
            descriptor.TargetClassNamespace = targetTypeSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : targetTypeSymbol.ContainingNamespace.ToDisplayString();
            descriptor.TargetNestedInClassNames = GetNestedInClassPath(targetTypeSymbol);
        }
        else
        {
            descriptor.TargetClassNamespace = descriptor.ClassNamespace;
            descriptor.TargetNestedInClassNames = descriptor.NestedInClassNames;
        }

        return descriptor;
    }
    
    /// <summary>
    /// Gets the nested class path for a given type symbol, recursively including
    /// all containing types.
    /// </summary>
    private static IEnumerable<string> GetNestedInClassPath(ITypeSymbol namedTypeSymbol)
    {
        if (namedTypeSymbol.ContainingType is not null)
        {
            foreach (var nestedInClassName in GetNestedInClassPath(namedTypeSymbol.ContainingType))
            {
                yield return nestedInClassName;
            }
        }

        yield return namedTypeSymbol.Name;
    }

    /// <summary>
    /// Gets the nested class path for extension members, skipping the extension block itself
    /// and using the outer class as the containing type.
    /// </summary>
    private static IEnumerable<string> GetNestedInClassPathForExtensionMember(ITypeSymbol extensionType)
    {
        // For extension members, the ContainingType is the extension block,
        // and its ContainingType is the outer class (e.g., EntityExtensions)
        var outerType = extensionType.ContainingType;

        if (outerType is not null)
        {
            return GetNestedInClassPath(outerType);
        }

        return [];
    }

    private static TypeConstraintSyntax MakeTypeConstraint(string constraint) =>
        SyntaxFactory.TypeConstraint(SyntaxFactory.IdentifierName(constraint));

    /// <summary>
    /// Populates the <see cref="ProjectableDescriptor.ClassTypeParameterList"/> and
    /// <see cref="ProjectableDescriptor.ClassConstraintClauses"/> from a generic containing class.
    /// </summary>
    private static void SetupGenericTypeParameters(ProjectableDescriptor descriptor, INamedTypeSymbol classForNaming)
    {
        descriptor.ClassTypeParameterList = SyntaxFactory.TypeParameterList();

        foreach (var tp in classForNaming.TypeParameters)
        {
            descriptor.ClassTypeParameterList = descriptor.ClassTypeParameterList.AddParameters(
                SyntaxFactory.TypeParameter(tp.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));

            // See https://github.com/dotnet/roslyn/blob/d7e010bbe5b1d37837417fc5e79ecb2fd9b7b487/src/VisualStudio/CSharp/Impl/ObjectBrowser/DescriptionBuilder.cs#L340
            if (tp.ConstraintTypes.IsDefaultOrEmpty)
            {
                continue;
            }

            descriptor.ClassConstraintClauses ??= SyntaxFactory.List<TypeParameterConstraintClauseSyntax>();

            var constraints = new List<TypeConstraintSyntax>();

            if (tp.HasReferenceTypeConstraint)
            {
                constraints.Add(MakeTypeConstraint("class"));
            }

            if (tp.HasValueTypeConstraint)
            {
                constraints.Add(MakeTypeConstraint("struct"));
            }

            if (tp.HasNotNullConstraint)
            {
                constraints.Add(MakeTypeConstraint("notnull"));
            }

            constraints.AddRange(tp.ConstraintTypes
                .Select(c => MakeTypeConstraint(c.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));

            if (tp.HasConstructorConstraint)
            {
                constraints.Add(MakeTypeConstraint("new()"));
            }

            descriptor.ClassConstraintClauses = descriptor.ClassConstraintClauses.Value.Add(
                SyntaxFactory.TypeParameterConstraintClause(
                    SyntaxFactory.IdentifierName(tp.Name),
                    SyntaxFactory.SeparatedList<TypeParameterConstraintSyntax>(constraints)));
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="prop"/> is a property that returns
    /// <c>Expression&lt;TDelegate&gt;</c>.
    /// <para>
    /// For nodes in the same <see cref="SyntaxTree"/> as the <paramref name="semanticModel"/>,
    /// a full semantic check is performed.  For cross-tree nodes (e.g., a backing property
    /// declared in a different file of a split partial class), a syntactic name check is used
    /// as a safe fallback — <see cref="TryResolveMemberBody"/> has already validated
    /// compatibility semantically before handing us the node.
    /// </para>
    /// </summary>
    private static bool IsExpressionDelegatePropertyDecl(PropertyDeclarationSyntax prop, SemanticModel semanticModel)
    {
        if (prop.SyntaxTree == semanticModel.SyntaxTree)
        {
            return semanticModel.GetDeclaredSymbol(prop) is IPropertySymbol s && IsExpressionDelegateProperty(s);
        }

        // Cross-tree: syntactic name check (type is Expression<...> or qualified variant).
        return prop.Type is GenericNameSyntax { Identifier.ValueText: "Expression" }
            || prop.Type is QualifiedNameSyntax { Right: GenericNameSyntax { Identifier.ValueText: "Expression" } };
    }
}
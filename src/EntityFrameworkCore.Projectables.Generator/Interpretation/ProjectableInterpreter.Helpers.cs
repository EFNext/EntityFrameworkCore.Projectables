using EntityFrameworkCore.Projectables.Generator.Infrastructure;
using EntityFrameworkCore.Projectables.Generator.Models;
using EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.Interpretation;

static internal partial class ProjectableInterpreter
{
    /// <summary>
    /// Visits <paramref name="parameterList"/> through <paramref name="rewriter"/> and appends
    /// all resulting parameters to <see cref="ProjectableDescriptor.ParametersList"/>.
    /// </summary>
    private static void ApplyParameterList(
        ParameterListSyntax parameterList,
        DeclarationSyntaxRewriter rewriter,
        ProjectableDescriptor descriptor)
    {
        foreach (var p in ((ParameterListSyntax)rewriter.Visit(parameterList)).Parameters)
        {
            descriptor.ParametersList = descriptor.ParametersList!.AddParameters(p);
        }
    }

    /// <summary>
    /// Visits the type-parameter list and constraint clauses of <paramref name="methodDecl"/>
    /// through <paramref name="rewriter"/> and stores them on <paramref name="descriptor"/>.
    /// </summary>
    private static void ApplyTypeParameters(
        MethodDeclarationSyntax methodDecl,
        DeclarationSyntaxRewriter rewriter,
        ProjectableDescriptor descriptor)
    {
        if (methodDecl.TypeParameterList is not null)
        {
            descriptor.TypeParameterList = SyntaxFactory.TypeParameterList();
            foreach (var tp in ((TypeParameterListSyntax)rewriter.Visit(methodDecl.TypeParameterList)).Parameters)
            {
                descriptor.TypeParameterList = descriptor.TypeParameterList.AddParameters(tp);
            }
        }

        if (methodDecl.ConstraintClauses.Any())
        {
            descriptor.ConstraintClauses = SyntaxFactory.List(
                methodDecl.ConstraintClauses
                    .Select(x => (TypeParameterConstraintClauseSyntax)rewriter.Visit(x)));
        }
    }

    /// <summary>
    /// For C# 14 generic extension blocks (e.g. <c>extension&lt;T&gt;(Wrapper&lt;T&gt; w)</c>),
    /// the block-level type parameter <c>T</c> is owned by the extension type, not by the
    /// method declaration syntax. <see cref="ApplyTypeParameters"/> therefore finds no
    /// <c>TypeParameterList</c> on the method and produces nothing.
    /// <para>
    /// This helper promotes those extension-block type parameters to method-level type
    /// parameters on <paramref name="descriptor"/> so the generated
    /// <c>Expression&lt;T&gt;()</c> factory method is correctly generic.
    /// It is a no-op when the containing type is not a generic extension block.
    /// </para>
    /// </summary>
    private static void ApplyExtensionBlockTypeParameters(
        ISymbol memberSymbol,
        ProjectableDescriptor descriptor)
    {
        if (memberSymbol.ContainingType is not { IsExtension: true } extensionType
            || extensionType.TypeParameters.IsDefaultOrEmpty)
        {
            return;
        }

        descriptor.TypeParameterList = SyntaxFactory.TypeParameterList();

        foreach (var tp in extensionType.TypeParameters)
        {
            descriptor.TypeParameterList = descriptor.TypeParameterList.AddParameters(
                SyntaxFactory.TypeParameter(tp.Name));

            // Build the constraint clause when any constraint is present.
            var hasAnyConstraint =
                tp.HasReferenceTypeConstraint
                || tp.HasValueTypeConstraint
                || tp.HasNotNullConstraint
                || !tp.ConstraintTypes.IsDefaultOrEmpty
                || tp.HasConstructorConstraint;

            if (!hasAnyConstraint)
            {
                continue;
            }

            descriptor.ConstraintClauses ??= SyntaxFactory.List<TypeParameterConstraintClauseSyntax>();
            descriptor.ConstraintClauses = descriptor.ConstraintClauses.Value.Add(BuildConstraintClause(tp));
        }
    }

    /// <summary>
    /// Builds a <see cref="TypeParameterConstraintClauseSyntax"/> for <paramref name="tp"/>
    /// by collecting all of its constraints in canonical order:
    /// <c>class</c> / <c>struct</c> / <c>notnull</c>, explicit type constraints, then <c>new()</c>.
    /// </summary>
    private static TypeParameterConstraintClauseSyntax BuildConstraintClause(ITypeParameterSymbol tp)
    {
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

        return SyntaxFactory.TypeParameterConstraintClause(
            SyntaxFactory.IdentifierName(tp.Name),
            SyntaxFactory.SeparatedList<TypeParameterConstraintSyntax>(constraints));
    }

    /// <summary>
    /// Returns the readable getter expression from a property declaration, trying in order:
    /// the property-level expression-body, the getter's expression-body, then the first
    /// <see langword="return"/> expression in a block-bodied getter.
    /// Returns <c>null</c> when none of these are present.
    /// </summary>
    private static ExpressionSyntax? TryGetPropertyGetterExpression(PropertyDeclarationSyntax prop)
    {
        if (prop.ExpressionBody?.Expression is { } exprBody)
        {
            return exprBody;
        }

        if (prop.AccessorList is not null)
        {
            var getter = prop.AccessorList.Accessors
                .FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));

            if (getter?.ExpressionBody?.Expression is { } getterExpr)
            {
                return getterExpr;
            }

            if (getter?.Body?.Statements.OfType<ReturnStatementSyntax>().FirstOrDefault()?.Expression is { } returnExpr)
            {
                return returnExpr;
            }
        }

        return null;
    }

    /// <summary>
    /// Reports <see cref="Diagnostics.RequiresBodyDefinition"/> for <paramref name="node"/>
    /// and returns <c>false</c> so callers can write <c>return ReportRequiresBodyAndFail(…)</c>.
    /// </summary>
    private static bool ReportRequiresBodyAndFail(
        SourceProductionContext context,
        SyntaxNode node,
        string memberName)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.RequiresBodyDefinition,
            node.GetLocation(),
            memberName));
        return false;
    }
}


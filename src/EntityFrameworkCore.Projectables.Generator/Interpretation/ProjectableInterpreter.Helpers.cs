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


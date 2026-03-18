using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;

internal class VariableReplacementRewriter : CSharpSyntaxRewriter
{
    private readonly string _variableName;
    private readonly ExpressionSyntax _replacement;

    public VariableReplacementRewriter(string variableName, ExpressionSyntax replacement)
    {
        _variableName = variableName;
        _replacement = replacement;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (node.Identifier.ValueText == _variableName)
        {
            return _replacement;
        }

        return base.VisitIdentifierName(node);
    }
    
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        if (node.Expression is IdentifierNameSyntax identifier && 
            identifier.Identifier.ValueText == _variableName)
        {
            return SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                _replacement,
                node.Name
            );
        }

        return base.VisitMemberAccessExpression(node);
    }

    // ── Scope tracking ────────────────────────────────────────────────────────
    // When a nested lambda re-declares the same parameter name, it shadows the
    // outer variable we are renaming.  Stop descending so that identifiers in
    // that nested lambda body refer to the inner parameter, not to @this.

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        if (node.Parameter.Identifier.ValueText == _variableName)
        {
            return node; // inner parameter shadows – leave entire sub-tree untouched
        }

        return base.VisitSimpleLambdaExpression(node);
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        if (node.ParameterList.Parameters.Any(p => p.Identifier.ValueText == _variableName))
        {
            return node; // inner parameter shadows – leave entire sub-tree untouched
        }

        return base.VisitParenthesizedLambdaExpression(node);
    }
}

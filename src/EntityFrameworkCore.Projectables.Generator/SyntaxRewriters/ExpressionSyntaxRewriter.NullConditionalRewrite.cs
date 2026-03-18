using EntityFrameworkCore.Projectables.Generator.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;

internal partial class ExpressionSyntaxRewriter
{
    public override SyntaxNode? VisitConditionalAccessExpression(ConditionalAccessExpressionSyntax node)
    {
        var targetExpression = (ExpressionSyntax)Visit(node.Expression);
        var targetType = _semanticModel.GetTypeInfo(node.Expression).Type;

        _conditionalAccessExpressionsStack.Push((targetExpression, targetType));

        if (_nullConditionalRewriteSupport == NullConditionalRewriteSupport.None)
        {
            var diagnostic = Diagnostic.Create(Diagnostics.NullConditionalRewriteUnsupported, node.GetLocation(), node);
            _context.ReportDiagnostic(diagnostic);

            // Return the original node, do not attempt further rewrites
            return node;
        }

        else if (_nullConditionalRewriteSupport is NullConditionalRewriteSupport.Ignore)
        {
            // Ignore the conditional access and simply visit the WhenNotNull expression
            return Visit(node.WhenNotNull);
        }

        else if (_nullConditionalRewriteSupport is NullConditionalRewriteSupport.Rewrite)
        {
            var typeInfo = _semanticModel.GetTypeInfo(node);

            // Do not translate until we can resolve the target type
            if (typeInfo.ConvertedType is not null)
            {
                // Translate null-conditional into a conditional expression, wrapped inside parenthesis
                return SyntaxFactory.ParenthesizedExpression(
                    SyntaxFactory.ConditionalExpression(
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.NotEqualsExpression,
                        targetExpression.WithTrailingTrivia(SyntaxFactory.Whitespace(" ")),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression).WithLeadingTrivia(SyntaxFactory.Whitespace(" "))
                   ).WithTrailingTrivia(SyntaxFactory.Whitespace(" ")),
                   SyntaxFactory.ParenthesizedExpression(
                       (ExpressionSyntax)Visit(node.WhenNotNull)
                   ).WithLeadingTrivia(SyntaxFactory.Whitespace(" ")).WithTrailingTrivia(SyntaxFactory.Whitespace(" ")),
                    SyntaxFactory.CastExpression(
                        SyntaxFactory.ParseName(typeInfo.ConvertedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                    ).WithLeadingTrivia(SyntaxFactory.Whitespace(" "))
                ).WithLeadingTrivia(node.GetLeadingTrivia()).WithTrailingTrivia(node.GetTrailingTrivia()));
            }
        }

        return base.VisitConditionalAccessExpression(node);
    }

    public override SyntaxNode? VisitMemberBindingExpression(MemberBindingExpressionSyntax node)
    {
        if (_conditionalAccessExpressionsStack.Count > 0)
        {
            var (targetExpression, targetType) = _conditionalAccessExpressionsStack.Pop();

            // When the target is a Nullable<T> value type, we need .Value to access members on the underlying type
            var accessExpression = IsNullableValueType(targetType)
                ? SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetExpression, SyntaxFactory.IdentifierName("Value"))
                : targetExpression;

            return _nullConditionalRewriteSupport switch {
                NullConditionalRewriteSupport.Ignore => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, accessExpression, node.Name),
                NullConditionalRewriteSupport.Rewrite => SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, accessExpression, node.Name),
                _ => node
            };
        }

        return base.VisitMemberBindingExpression(node);
    }

    public override SyntaxNode? VisitElementBindingExpression(ElementBindingExpressionSyntax node)
    {
        if (_conditionalAccessExpressionsStack.Count > 0)
        {
            var (targetExpression, targetType) = _conditionalAccessExpressionsStack.Pop();

            // When the target is a Nullable<T> value type, we need .Value to access indexer on the underlying type
            var accessExpression = IsNullableValueType(targetType)
                ? SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, targetExpression, SyntaxFactory.IdentifierName("Value"))
                : targetExpression;

            return _nullConditionalRewriteSupport switch {
                NullConditionalRewriteSupport.Ignore => SyntaxFactory.ElementAccessExpression(accessExpression, node.ArgumentList),
                NullConditionalRewriteSupport.Rewrite => SyntaxFactory.ElementAccessExpression(accessExpression, node.ArgumentList),
                _ => Visit(node)
            };
        }

        return base.VisitElementBindingExpression(node);
    }

    private static bool IsNullableValueType(ITypeSymbol? type)
    {
        return type is { IsValueType: true } &&
               type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }
}

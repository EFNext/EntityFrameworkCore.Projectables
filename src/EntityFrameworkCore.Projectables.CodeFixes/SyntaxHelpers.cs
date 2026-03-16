using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.CodeFixes;

static internal class SyntaxHelpers
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="method"/> matches the
    /// factory-method pattern:
    /// <list type="bullet">
    ///   <item><description>Decorated with <c>[Projectable]</c>.</description></item>
    ///   <item><description>Expression body of the form <c>=> new ContainingType { … }</c>
    ///       (object initializer only, no constructor arguments in the <c>new</c>
    ///       expression).</description></item>
    ///   <item><description>Return type simple name equals the containing class name.</description></item>
    /// </list>
    /// </summary>
    static internal bool TryGetFactoryMethodPattern(
        MethodDeclarationSyntax method,
        out TypeDeclarationSyntax? containingType)
    {
        containingType = null;

        if (method.Parent is not TypeDeclarationSyntax parentType)
        {
            return false;
        }

        if (method.ExpressionBody is null)
        {
            return false;
        }

        if (method.ExpressionBody.Expression is not BaseObjectCreationExpressionSyntax creation)
        {
            return false;
        }

        // Only pure object-initializer bodies — no constructor arguments on the new expression.
        if (creation.ArgumentList?.Arguments.Count > 0)
        {
            return false;
        }

        if (creation.Initializer is null)
        {
            return false;
        }

        // The method's return type must match the containing type (syntax-level name comparison).
        var returnTypeName = method.ReturnType switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            QualifiedNameSyntax { Right: IdentifierNameSyntax right } => right.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            _ => null
        };

        if (returnTypeName is null || returnTypeName != parentType.Identifier.Text)
        {
            return false;
        }
        
        containingType = parentType;
        
        return true;
    }
}
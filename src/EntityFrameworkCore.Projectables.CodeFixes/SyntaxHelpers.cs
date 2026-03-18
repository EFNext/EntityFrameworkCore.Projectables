using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.CodeFixes;

static internal class SyntaxHelpers
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="method"/> matches the
    /// factory-method pattern:
    /// <list type="bullet">
    ///   <item><description>Expression body of the form <c>=> new ContainingType { … }</c>
    ///       (object initializer only, no constructor arguments in the <c>new</c>
    ///       expression).</description></item>
    ///   <item><description>Static method.</description></item>
    ///   <item><description>Return type simple name equals the containing class name.</description></item>
    ///   <item><description>For explicit <c>new T { … }</c>, <c>T</c> must be an unqualified
    ///       identifier that matches the containing class name.  Qualified names such as
    ///       <c>new Other.MyObj { … }</c> or <c>new global::Other.MyObj { … }</c> are
    ///       rejected because they cannot be confirmed as the same type without a semantic
    ///       model.</description></item>
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

        // Only pure simple-assignment initializers (Prop = value) — no bare collection
        // elements (which are not AssignmentExpressionSyntax) and no nested initializer
        // assignments (Items = { 1, 2 }) whose RHS is an InitializerExpressionSyntax.
        // Converting such entries to statements would produce invalid C#, so we must not
        // offer the refactoring at all for these patterns.
        if (creation.Initializer.Expressions.Any(
            e => e is not AssignmentExpressionSyntax { Right: not InitializerExpressionSyntax }))
        {
            return false;
        }

        // Only allow static factory methods, to keep the code fix simpler
        if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
        {
            return false;
        }

        // For explicit new T { … }, verify the type is an unqualified name matching the
        // containing class.  Qualified names (new Other.MyObj { }, new global::Other.MyObj { })
        // are rejected: without a semantic model we cannot confirm they resolve to the same
        // type, so this is the conservative safe choice.
        // For implicit new() { }, the compiler infers the type from the method's return type,
        // which is already validated below, so no additional check is needed here.
        if (creation is ObjectCreationExpressionSyntax { Type: var createdType })
        {
            var createdTypeName = createdType switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                GenericNameSyntax generic => generic.Identifier.Text,
                _ => null  // QualifiedNameSyntax, AliasQualifiedNameSyntax, etc. — reject
            };

            if (createdTypeName is null || createdTypeName != parentType.Identifier.Text)
            {
                return false;
            }
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
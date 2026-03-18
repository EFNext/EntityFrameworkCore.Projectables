using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;

internal partial class ExpressionSyntaxRewriter
{
    private bool TryExpandEnumMethodCall(InvocationExpressionSyntax node, MemberAccessExpressionSyntax memberAccess, IMethodSymbol methodSymbol, out ExpressionSyntax? expandedExpression)
    {
        expandedExpression = null;

        // Get the receiver expression (the enum instance or variable)
        var receiverExpression = memberAccess.Expression;
        var receiverTypeInfo = _semanticModel.GetTypeInfo(receiverExpression);
        var receiverType = receiverTypeInfo.Type;

        // Handle nullable enum types
        ITypeSymbol enumType;
        var isNullable = false;
        if (receiverType is INamedTypeSymbol { IsGenericType: true, Name: "Nullable" } nullableType && 
            nullableType.TypeArguments.Length == 1 && 
            nullableType.TypeArguments[0].TypeKind == TypeKind.Enum)
        {
            enumType = nullableType.TypeArguments[0];
            isNullable = true;
        }
        else if (receiverType?.TypeKind == TypeKind.Enum)
        {
            enumType = receiverType;
        }
        else
        {
            // Not an enum type
            return false;
        }

        // Get all enum members
        var enumMembers = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.HasConstantValue)
            .ToList();

        if (enumMembers.Count == 0)
        {
            return false;
        }

        // Visit the receiver expression to transform it (e.g., @this.MyProperty)
        var visitedReceiver = (ExpressionSyntax)Visit(receiverExpression);

        // Get the original method (in case of reduced extension method)
        var originalMethod = methodSymbol.ReducedFrom ?? methodSymbol;
        
        // Get the return type of the method to determine the default value
        var returnType = methodSymbol.ReturnType;
        
        // Build a chain of ternary expressions for each enum value
        // Start with default(T) as the fallback for non-nullable types, or null for nullable/reference types
        ExpressionSyntax defaultExpression;
        if (returnType.IsReferenceType || returnType.NullableAnnotation == NullableAnnotation.Annotated || 
            returnType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            defaultExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
        }
        else
        {
            // Use default(T) for value types
            defaultExpression = SyntaxFactory.DefaultExpression(
                SyntaxFactory.ParseTypeName(returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        var currentExpression = defaultExpression;
        
        // Create the enum value access: EnumType.Value
        var enumAccessValues = enumMembers
            .AsEnumerable()
            .Reverse()
            .Select(m =>
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseTypeName(enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                    SyntaxFactory.IdentifierName(m.Name)
                )
            );

        // Build the ternary chain, calling the method on each enum value
        foreach (var enumValueAccess in enumAccessValues)
        {
            // Create the method call on the enum value: ExtensionClass.Method(EnumType.Value)
            var methodCall = CreateMethodCallOnEnumValue(originalMethod, enumValueAccess, node.ArgumentList);

            // Create condition: receiver == EnumType.Value
            var condition = SyntaxFactory.BinaryExpression(
                SyntaxKind.EqualsExpression,
                visitedReceiver,
                enumValueAccess
            );

            // Create conditional expression: condition ? methodCall : previousExpression
            currentExpression = SyntaxFactory.ConditionalExpression(
                condition,
                methodCall,
                currentExpression
            );
        }

        // If nullable, wrap in null check
        if (isNullable)
        {
            var nullCheck = SyntaxFactory.BinaryExpression(
                SyntaxKind.EqualsExpression,
                visitedReceiver,
                SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
            );

            currentExpression = SyntaxFactory.ConditionalExpression(
                nullCheck,
                defaultExpression,
                currentExpression
            );
        }

        expandedExpression = SyntaxFactory.ParenthesizedExpression(currentExpression);
        return true;
    }

    private ExpressionSyntax CreateMethodCallOnEnumValue(IMethodSymbol methodSymbol, ExpressionSyntax enumValueExpression, ArgumentListSyntax originalArguments)
    {
        // Get the fully qualified containing type name
        var containingTypeName = methodSymbol.ContainingType.ToDisplayString(NullableFlowState.None, SymbolDisplayFormat.FullyQualifiedFormat);
        
        // Create the method access expression: ContainingType.MethodName
        var methodAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.ParseName(containingTypeName),
            SyntaxFactory.IdentifierName(methodSymbol.Name)
        );

        // Build arguments: the enum value as the first argument (for extension methods), followed by any additional arguments
        var arguments = SyntaxFactory.SeparatedList<ArgumentSyntax>();
        arguments = arguments.Add(SyntaxFactory.Argument(enumValueExpression));
        
        // Add any additional arguments from the original call
        foreach (var arg in originalArguments.Arguments)
        {
            arguments = arguments.Add((ArgumentSyntax)Visit(arg));
        }

        return SyntaxFactory.InvocationExpression(
            methodAccess,
            SyntaxFactory.ArgumentList(arguments)
        );
    }
}

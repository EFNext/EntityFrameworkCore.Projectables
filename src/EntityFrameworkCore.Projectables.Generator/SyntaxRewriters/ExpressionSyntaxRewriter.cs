using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;

internal partial class ExpressionSyntaxRewriter : CSharpSyntaxRewriter
{
    readonly INamedTypeSymbol _targetTypeSymbol;
    readonly SemanticModel _semanticModel;
    readonly NullConditionalRewriteSupport _nullConditionalRewriteSupport;
    readonly bool _expandEnumMethods;
    readonly SourceProductionContext _context;
    readonly Stack<(ExpressionSyntax Expression, ITypeSymbol? Type)> _conditionalAccessExpressionsStack = new();
    readonly string? _extensionParameterName;

    public ExpressionSyntaxRewriter(INamedTypeSymbol targetTypeSymbol, NullConditionalRewriteSupport nullConditionalRewriteSupport, bool expandEnumMethods, SemanticModel semanticModel, SourceProductionContext context, string? extensionParameterName = null)
    {
        _targetTypeSymbol = targetTypeSymbol;
        _nullConditionalRewriteSupport = nullConditionalRewriteSupport;
        _expandEnumMethods = expandEnumMethods;
        _semanticModel = semanticModel;
        _context = context;
        _extensionParameterName = extensionParameterName;
    }

    public SemanticModel GetSemanticModel() => _semanticModel;

    private SyntaxNode? VisitThisBaseExpression(CSharpSyntaxNode node)
    {
        // Swap out the use of this and base to @this and keep leading and trailing trivias
        return SyntaxFactory.IdentifierName("@this")
            .WithLeadingTrivia(node.GetLeadingTrivia())
            .WithTrailingTrivia(node.GetTrailingTrivia());
    }
    
    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var expressionSyntax = (ExpressionSyntax?)Visit(node.Expression) ?? throw new ArgumentNullException("expression");
    
        var syntaxNode = Visit(node.Name);
    
        // Prevents invalid cast when visiting a QualifiedNameSyntax
        if (syntaxNode is QualifiedNameSyntax qst)
        {
            syntaxNode = qst.Right;
        }
        
        return node.Update(expressionSyntax, VisitToken(node.OperatorToken), (SimpleNameSyntax)syntaxNode);
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Fully qualify extension method calls
        if (node.Expression is not MemberAccessExpressionSyntax memberAccessExpressionSyntax)
        {
            return base.VisitInvocationExpression(node);
        }

        var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return base.VisitInvocationExpression(node);
        }

        // Check if we should expand enum methods
        if (_expandEnumMethods && TryExpandEnumMethodCall(node, memberAccessExpressionSyntax, methodSymbol, out var expandedExpression))
        {
            return expandedExpression;
        }

        // Fully qualify extension method calls
        if (methodSymbol.IsExtensionMethod)
        {
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseName(methodSymbol.ContainingType.ToDisplayString(NullableFlowState.None, SymbolDisplayFormat.FullyQualifiedFormat)),
                    memberAccessExpressionSyntax.Name
                ),
                node.ArgumentList.WithArguments(
                    ((ArgumentListSyntax)VisitArgumentList(node.ArgumentList)!).Arguments.Insert(0, SyntaxFactory.Argument(
                            (ExpressionSyntax)Visit(memberAccessExpressionSyntax.Expression)
                        )
                    )
                )
            );
        }

        return base.VisitInvocationExpression(node);
    }

    public override SyntaxNode? VisitInterpolation(InterpolationSyntax node)
    {
        // Visit the expression first
        var targetExpression = (ExpressionSyntax)Visit(node.Expression);
        
        // Check if the expression already has parentheses
        if (targetExpression is ParenthesizedExpressionSyntax)
        {
            return node.WithExpression(targetExpression);
        }
        
        // Create a new expression wrapped in parentheses
        var newExpression = SyntaxFactory.ParenthesizedExpression(targetExpression);
        
        return node.WithExpression(newExpression);
    }

    public override SyntaxNode? VisitThisExpression(ThisExpressionSyntax node)
    {
        // Swap out the use of this to @this
        return VisitThisBaseExpression(node);
    }

    public override SyntaxNode? VisitBaseExpression(BaseExpressionSyntax node)
    {
        // Swap out the use of this to @this
        return VisitThisBaseExpression(node);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        // Handle C# 14 extension parameter replacement (e.g., `e` in `extension(Entity e)` becomes `@this`)
        if (_extensionParameterName is not null && node.Identifier.Text == _extensionParameterName)
        {
            var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
            
            // Check if this identifier refers to the extension parameter
            if (symbol is IParameterSymbol { ContainingSymbol: INamedTypeSymbol { IsExtension: true } })
            {
                return SyntaxFactory.IdentifierName("@this")
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }
        }

        var identifierSymbol = _semanticModel.GetSymbolInfo(node).Symbol;
        if (identifierSymbol is not null)
        {
            var operation = node switch { { Parent: { } parent } when parent.IsKind(SyntaxKind.InvocationExpression) => _semanticModel.GetOperation(node.Parent),
                _ => _semanticModel.GetOperation(node!)
            };

            if (operation is IMemberReferenceOperation memberReferenceOperation)
            {
                var memberAccessCanBeQualified = node switch { { Parent: { Parent: { } parent } } when parent.IsKind(SyntaxKind.ObjectInitializerExpression) => false,
                    _ => true
                };

                if (memberAccessCanBeQualified)
                {
                    // if this operation is targeting an instance member on our targetType implicitly
                    if (memberReferenceOperation.Instance is { IsImplicit: true } && SymbolEqualityComparer.Default.Equals(memberReferenceOperation.Instance.Type, _targetTypeSymbol))
                    {
                        return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("@this"),
                            node.WithoutLeadingTrivia()
                        ).WithLeadingTrivia(node.GetLeadingTrivia());
                    }

                    // if this operation is targeting a static member on our targetType implicitly
                    if (memberReferenceOperation.Instance is null && SymbolEqualityComparer.Default.Equals(memberReferenceOperation.Member.ContainingType, _targetTypeSymbol))
                    {
                        return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.ParseTypeName(_targetTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                            node.WithoutLeadingTrivia()
                        ).WithLeadingTrivia(node.GetLeadingTrivia());
                    }
                }
            }
            else if (operation is IInvocationOperation invocationOperation)
            {
                // if this operation is targeting an instance method on our targetType implicitly
                if (invocationOperation.Instance is { IsImplicit: true } && SymbolEqualityComparer.Default.Equals(invocationOperation.Instance.Type, _targetTypeSymbol))
                {
                    return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("@this"),
                        node.WithoutLeadingTrivia()
                    ).WithLeadingTrivia(node.GetLeadingTrivia());
                }

                // if this operation is targeting a static method on our targetType implicitly
                if (invocationOperation.Instance is null && SymbolEqualityComparer.Default.Equals(invocationOperation.TargetMethod.ContainingType, _targetTypeSymbol))
                {
                    return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.ParseTypeName(_targetTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)),
                        node.WithoutLeadingTrivia()
                    ).WithLeadingTrivia(node.GetLeadingTrivia());
                }
            }

            // if this node refers to a named type which is not yet fully qualified, we want to fully qualify it
            if (identifierSymbol.Kind is SymbolKind.NamedType && node.Parent?.Kind() is not SyntaxKind.QualifiedName)
            {
                return SyntaxFactory.ParseTypeName(
                    identifierSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                ).WithLeadingTrivia(node.GetLeadingTrivia());
            }
        }

        return base.VisitIdentifierName(node);
    }

    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        var symbolInfo = _semanticModel.GetSymbolInfo(node);

        if (symbolInfo.Symbol is not null)
        {
            if (symbolInfo.Symbol.Kind is SymbolKind.NamedType)
            {
                var typeInfo = _semanticModel.GetTypeInfo(node);

                if (typeInfo.Type is not null)
                {
                    return SyntaxFactory.ParseTypeName(
                        typeInfo.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    );
                }
            }
        }

        return base.VisitQualifiedName(node);
    }

    public override SyntaxNode? VisitNullableType(NullableTypeSyntax node)
    {
        var typeInfo = _semanticModel.GetTypeInfo(node);
        if (typeInfo.Type is not null)
        {
            if (typeInfo.Type.TypeKind is not TypeKind.Struct)
            {
                return Visit(node.ElementType)
                    .WithLeadingTrivia(node.GetLeadingTrivia())
                    .WithTrailingTrivia(node.GetTrailingTrivia());
            }
        }

        return base.VisitNullableType(node);
    }

    public override SyntaxNode? VisitInitializerExpression(InitializerExpressionSyntax node)
    {
        // Only handle object initializers that might contain indexer assignments
        if (!node.IsKind(SyntaxKind.ObjectInitializerExpression))
        {
            return base.VisitInitializerExpression(node);
        }

        // Check if any expression is an indexer assignment (e.g., ["key"] = value)
        var hasIndexerAssignment = node.Expressions.Any(e => 
            e is AssignmentExpressionSyntax { Left: ImplicitElementAccessSyntax });

        if (!hasIndexerAssignment)
        {
            return base.VisitInitializerExpression(node);
        }

        var newExpressions = new SeparatedSyntaxList<ExpressionSyntax>();

        foreach (var expression in node.Expressions)
        {
            if (expression is AssignmentExpressionSyntax assignment &&
                assignment.Left is ImplicitElementAccessSyntax implicitElementAccess)
            {
                // Transform ["key"] = value into { "key", value }
                var arguments = new SeparatedSyntaxList<ExpressionSyntax>();

                foreach (var argument in implicitElementAccess.ArgumentList.Arguments)
                {
                    var visitedArgument = (ExpressionSyntax?)Visit(argument.Expression) ?? argument.Expression;
                    arguments = arguments.Add(visitedArgument);
                }

                var visitedValue = (ExpressionSyntax?)Visit(assignment.Right) ?? assignment.Right;
                arguments = arguments.Add(visitedValue);

                var complexElementInitializer = SyntaxFactory.InitializerExpression(
                    SyntaxKind.ComplexElementInitializerExpression,
                    arguments
                );

                newExpressions = newExpressions.Add(complexElementInitializer);
            }
            else
            {
                var visitedExpression = (ExpressionSyntax?)Visit(expression) ?? expression;
                newExpressions = newExpressions.Add(visitedExpression);
            }
        }

        return SyntaxFactory.InitializerExpression(
            SyntaxKind.CollectionInitializerExpression,
            newExpressions
        ).WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitIsPatternExpression(IsPatternExpressionSyntax node)
    {
        // Pattern matching is not supported in expression trees (CS8122).
        // We need to convert patterns into equivalent expressions.
        var expression = (ExpressionSyntax)Visit(node.Expression);

        // ConvertPatternToExpression returns null when the pattern cannot be rewritten and has
        // already reported a diagnostic (EFP0007).  Return a 'false' literal placeholder so
        // the generated lambda stays syntactically valid and no additional CS8122 errors are
        // triggered by leaving raw pattern-matching syntax inside an expression tree.
        return ConvertPatternToExpression(node.Pattern, expression)
            ?? SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression);
    }
}

using EntityFrameworkCore.Projectables.Generator.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;

internal partial class ExpressionSyntaxRewriter
{
    public override SyntaxNode? VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        var arms = node.Arms.Reverse();
        var visitedGoverning = (ExpressionSyntax)Visit(node.GoverningExpression);
        ExpressionSyntax? currentExpression = null;

        foreach (var arm in arms)
        {
            var armExpression = (ExpressionSyntax)Visit(arm.Expression);

            if (currentExpression == null)
            {
                currentExpression = arm.Pattern is DiscardPatternSyntax
                    ? armExpression
                    : SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression);
                continue;
            }

            ExpressionSyntax? condition;

            // DeclarationPattern with a named variable requires replacing the variable with a cast in the arm body
            if (arm.Pattern is DeclarationPatternSyntax declaration && declaration.Designation is SingleVariableDesignationSyntax)
            {
                condition = SyntaxFactory.BinaryExpression(SyntaxKind.IsExpression, visitedGoverning, declaration.Type);
                armExpression = ReplaceVariableWithCast(armExpression, declaration, visitedGoverning);
            }
            else
            {
                condition = ConvertPatternToExpression(arm.Pattern, visitedGoverning);
                if (condition is null)
                {
                    // A diagnostic (EFP0007) has already been reported for this arm.
                    // Skip it instead of falling back to base.VisitSwitchExpression which
                    // would leave an unsupported switch expression in the generated lambda and
                    // produce unrelated compiler errors.  The best-effort ternary chain built
                    // so far is still emitted so the output remains valid C#.
                    continue;
                }
            }

            if (arm.WhenClause != null)
            {
                condition = SyntaxFactory.BinaryExpression(
                    SyntaxKind.LogicalAndExpression,
                    condition,
                    (ExpressionSyntax)Visit(arm.WhenClause.Condition)
                );
            }

            currentExpression = SyntaxFactory.ConditionalExpression(condition, armExpression, currentExpression);
        }

        return currentExpression;
    }

    private ExpressionSyntax ReplaceVariableWithCast(ExpressionSyntax expression, DeclarationPatternSyntax declaration, ExpressionSyntax governingExpression)
    {
        if (declaration.Designation is SingleVariableDesignationSyntax variableDesignation)
        {
            var variableName = variableDesignation.Identifier.ValueText;
        
            var castExpression = SyntaxFactory.ParenthesizedExpression(
                SyntaxFactory.CastExpression(
                    declaration.Type,
                    (ExpressionSyntax)Visit(governingExpression)
                )
            );

            var rewriter = new VariableReplacementRewriter(variableName, castExpression);
            return (ExpressionSyntax)rewriter.Visit(expression);
        }

        return expression;
    }

    /// <summary>
    /// Returns true when <paramref name="type"/> can be compared to null.
    /// Accepts a pre-resolved symbol so synthesized (unbound) expression nodes can bypass
    /// semantic-model lookup, which would return <c>null</c> for synthesized nodes and cause
    /// the method to conservatively (and incorrectly) emit a null-check for value-type properties.
    /// </summary>
    private static bool TypeRequiresNullCheck(ITypeSymbol? type)
    {
        if (type is null)
        {
            return true; // conservative: unknown type → assume nullable
        }

        // Nullable<T> is a value type whose OriginalDefinition is System.Nullable<T>
        if (type.IsValueType &&
            type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
        {
            return false; // plain struct / record struct — null check would not compile
        }

        return true;
    }

    /// <summary>
    /// Attempts to convert <paramref name="pattern"/> into an ordinary expression that is valid
    /// inside an expression tree.  Returns <c>null</c> and reports a diagnostic when the pattern
    /// cannot be rewritten.
    /// </summary>
    /// <param name="pattern">The pattern syntax to convert.</param>
    /// <param name="expression">The expression being tested against the pattern.</param>
    /// <param name="expressionType">
    /// Pre-resolved type of <paramref name="expression"/>. When the expression is a synthesized
    /// node (not present in the original source) Roslyn cannot bind it, so callers that know the
    /// type should pass it here to avoid falling back to the conservative "assume nullable" path.
    /// </param>
    private ExpressionSyntax? ConvertPatternToExpression(PatternSyntax pattern, ExpressionSyntax expression, ITypeSymbol? expressionType = null)
    {
        switch (pattern)
        {
            case RecursivePatternSyntax recursivePattern:
                return ConvertRecursivePattern(recursivePattern, expression, expressionType);

            case ConstantPatternSyntax constantPattern:
                // e is null  /  e is 5
                return SyntaxFactory.BinaryExpression(
                    SyntaxKind.EqualsExpression,
                    expression,
                    (ExpressionSyntax)Visit(constantPattern.Expression)
                );

            case DeclarationPatternSyntax declarationPattern:
                // e is string _  → type-check only (discard is fine)
                // e is string s  → we cannot safely rewrite because references to 's' in
                //                  the surrounding expression are outside this node's scope.
                if (declarationPattern.Designation is SingleVariableDesignationSyntax)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedPatternInExpression,
                        pattern.GetLocation(),
                        pattern.ToString()));
                    return null;
                }

                return SyntaxFactory.BinaryExpression(
                    SyntaxKind.IsExpression,
                    expression,
                    declarationPattern.Type
                );

            case RelationalPatternSyntax relationalPattern:
            {
                // e is > 100
                SyntaxKind? binaryKind = relationalPattern.OperatorToken.Kind() switch
                {
                    SyntaxKind.LessThanToken        => SyntaxKind.LessThanExpression,
                    SyntaxKind.LessThanEqualsToken  => SyntaxKind.LessThanOrEqualExpression,
                    SyntaxKind.GreaterThanToken     => SyntaxKind.GreaterThanExpression,
                    SyntaxKind.GreaterThanEqualsToken => SyntaxKind.GreaterThanOrEqualExpression,
                    _ => null
                };

                if (binaryKind is null)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedPatternInExpression,
                        pattern.GetLocation(),
                        pattern.ToString()));
                    return null;
                }

                return SyntaxFactory.BinaryExpression(
                    binaryKind.Value,
                    expression,
                    (ExpressionSyntax)Visit(relationalPattern.Expression)
                );
            }

            case BinaryPatternSyntax binaryPattern:
            {
                // e is > 10 and < 100
                var left  = ConvertPatternToExpression(binaryPattern.Left,  expression);
                var right = ConvertPatternToExpression(binaryPattern.Right, expression);

                // Propagate failures from either side
                if (left is null || right is null)
                {
                    return null;
                }

                SyntaxKind? logicalKind = binaryPattern.OperatorToken.Kind() switch
                {
                    SyntaxKind.AndKeyword => SyntaxKind.LogicalAndExpression,
                    SyntaxKind.OrKeyword  => SyntaxKind.LogicalOrExpression,
                    _ => null
                };

                if (logicalKind is null)
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnsupportedPatternInExpression,
                        pattern.GetLocation(),
                        pattern.ToString()));
                    return null;
                }

                return SyntaxFactory.BinaryExpression(logicalKind.Value, left, right);
            }

            case UnaryPatternSyntax unaryPattern when unaryPattern.OperatorToken.IsKind(SyntaxKind.NotKeyword):
            {
                // e is not null
                var inner = ConvertPatternToExpression(unaryPattern.Pattern, expression);
                if (inner is null)
                {
                    return null;
                }

                return SyntaxFactory.PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    SyntaxFactory.ParenthesizedExpression(inner)
                );
            }

            default:
                _context.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnsupportedPatternInExpression,
                    pattern.GetLocation(),
                    pattern.ToString()));
                return null;
        }
    }

    private ExpressionSyntax? ConvertRecursivePattern(RecursivePatternSyntax recursivePattern, ExpressionSyntax expression, ITypeSymbol? expressionType = null)
    {
        // Positional / deconstruct patterns (e.g. obj is Point(1, 2)) cannot be rewritten
        // into a plain expression tree.  Report a diagnostic and bail out.
        if (recursivePattern.PositionalPatternClause != null)
        {
            _context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.UnsupportedPatternInExpression,
                recursivePattern.GetLocation(),
                recursivePattern.ToString()));
            return null;
        }

        var conditions = new List<ExpressionSyntax>();

        // Null check: only legal (and only necessary) for reference types and nullable value types.
        // Emitting "x != null" for a plain struct / record struct would not compile.
        // Use the pre-resolved expressionType when available so synthesized nodes (which Roslyn
        // cannot bind) are handled correctly instead of falling back to the conservative path.
        var typeForNullCheck = expressionType ?? _semanticModel.GetTypeInfo(expression).Type;
        if (TypeRequiresNullCheck(typeForNullCheck))
        {
            conditions.Add(SyntaxFactory.BinaryExpression(
                SyntaxKind.NotEqualsExpression,
                expression,
                SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
            ));
        }

        // Type check: "obj is SomeType { ... }" — add "expression is SomeType" guard.
        TypeSyntax? visitedType = null;
        if (recursivePattern.Type != null)
        {
            visitedType = (TypeSyntax)Visit(recursivePattern.Type);
            conditions.Add(SyntaxFactory.BinaryExpression(
                SyntaxKind.IsExpression,
                expression,
                visitedType
            ));
        }

        // When a concrete type is known, member accesses on sub-patterns must go through a
        // cast so the generated code compiles correctly (e.g. ((SomeType)expression).Prop).
        var memberBase = visitedType != null
            ? SyntaxFactory.ParenthesizedExpression(
                SyntaxFactory.CastExpression(visitedType, expression))
            : expression;

        // Handle property sub-patterns: { Prop: value, ... }
        if (recursivePattern.PropertyPatternClause != null)
        {
            foreach (var subpattern in recursivePattern.PropertyPatternClause.Subpatterns)
            {
                ExpressionSyntax propExpression;
                ITypeSymbol? propType = null;

                if (subpattern.NameColon != null)
                {
                    // Look up the property/field type from the original source binding so that
                    // when the recursive ConvertPatternToExpression call checks TypeRequiresNullCheck
                    // on the synthesized propExpression it receives the real symbol instead of null.
                    var memberSymbol = _semanticModel.GetSymbolInfo(subpattern.NameColon.Name).Symbol;
                    propType = memberSymbol switch
                    {
                        IPropertySymbol prop  => prop.Type,
                        IFieldSymbol    field => field.Type,
                        _                     => null
                    };

                    propExpression = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        memberBase,
                        SyntaxFactory.IdentifierName(subpattern.NameColon.Name.Identifier));
                }
                else
                {
                    propExpression = memberBase;
                }

                // Pass propType so nested recursive patterns don't misidentify value-type
                // properties as nullable when Roslyn can't bind the synthesized node.
                var condition = ConvertPatternToExpression(subpattern.Pattern, propExpression, propType);
                if (condition is null)
                {
                    return null; // diagnostic already emitted
                }

                conditions.Add(condition);
            }
        }

        if (conditions.Count == 0)
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression);
        }

        // Combine all conditions with &&
        var result = conditions[0];
        for (var i = 1; i < conditions.Count; i++)
        {
            result = SyntaxFactory.BinaryExpression(
                SyntaxKind.LogicalAndExpression,
                result,
                conditions[i]
            );
        }

        return result;
    }
}

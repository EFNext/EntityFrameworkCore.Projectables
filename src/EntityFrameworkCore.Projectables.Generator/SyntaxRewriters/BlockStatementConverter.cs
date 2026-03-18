using EntityFrameworkCore.Projectables.Generator.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;

/// <summary>
/// Converts block-bodied methods to expression syntax that can be used in expression trees.
/// Only supports a subset of C# statements.
/// </summary>
internal class BlockStatementConverter
{
    private readonly SourceProductionContext _context;
    private readonly ExpressionSyntaxRewriter _expressionRewriter;
    private readonly Dictionary<string, ExpressionSyntax> _localVariables = new();

    public BlockStatementConverter(SourceProductionContext context, ExpressionSyntaxRewriter expressionRewriter)
    {
        _context = context;
        _expressionRewriter = expressionRewriter;
    }

    /// <summary>
    /// Attempts to convert a block statement into a single expression.
    /// Returns null if the block contains unsupported statements.
    /// </summary>
    public ExpressionSyntax? TryConvertBlock(BlockSyntax block, string memberName)
    {
        if (block.Statements.Count == 0)
        {
            var diagnostic = Diagnostic.Create(
                Diagnostics.UnsupportedStatementInBlockBody,
                block.GetLocation(), memberName,
                "Block body must contain at least one statement"
            );
            _context.ReportDiagnostic(diagnostic);
            return null;
        }

        // Try to convert the block statements into an expression
        return TryConvertStatements(block.Statements.ToList(), memberName);
    }

    /// <summary>
    /// Converts a list of statements into a single expression by processing them right-to-left.
    /// Local declarations must appear before any non-local statement; they are inlined at their
    /// use sites. Each remaining statement receives the accumulated result as its implicit
    /// "fallthrough" (i.e. the else/default branch when no explicit one exists).
    /// </summary>
    private ExpressionSyntax? TryConvertStatements(List<StatementSyntax> statements, string memberName)
    {
        if (statements.Count == 0)
        {
            return null;
        }

        // Process local declarations in order, collecting the remaining code statements.
        // Enforce that all local declarations appear before any non-local statement so that
        // hoisting is well-defined and the diagnostic in TryConvertStatementWithFallthrough
        // is reachable only as a defensive fallback.
        var codeStatements = new List<StatementSyntax>();
        var seenNonLocal = false;
        foreach (var stmt in statements)
        {
            if (stmt is LocalDeclarationStatementSyntax localDecl)
            {
                if (seenNonLocal)
                {
                    // Local declaration appears after executable code — ordering violation.
                    ReportUnsupportedStatement(localDecl, memberName,
                        "Local variable declarations must appear before any other statements (return, if, switch, …)");
                    return null;
                }

                if (!TryProcessLocalDeclaration(localDecl, memberName))
                {
                    return null;
                }
            }
            else
            {
                seenNonLocal = true;
                codeStatements.Add(stmt);
            }
        }

        if (codeStatements.Count == 0)
        {
            return null;
        }

        // Right-to-left fold: build nested expressions so that each statement wraps the
        // next as its "fallthrough" branch.  This naturally handles chains like:
        //   if (a) return 1;  if (b) return 2;  return 3;
        //   => a ? 1 : (b ? 2 : 3)
        ExpressionSyntax? result = null;
        for (var i = codeStatements.Count - 1; i >= 0; i--)
        {
            result = TryConvertStatementWithFallthrough(codeStatements[i], result, memberName);
            if (result == null)
            {
                return null;
            }
        }

        return result;
    }

    /// <summary>
    /// Converts a single statement into an expression, using <paramref name="fallthrough"/> as the
    /// implicit else/default branch for statements that have no explicit one (if-without-else,
    /// switch-without-default).  Pass <c>null</c> to use a <c>default</c> literal instead.
    /// </summary>
    private ExpressionSyntax? TryConvertStatementWithFallthrough(
        StatementSyntax statement, ExpressionSyntax? fallthrough, string memberName)
    {
        switch (statement)
        {
            case ReturnStatementSyntax returnStmt:
                return TryConvertReturnStatement(returnStmt, memberName);

            case IfStatementSyntax ifStmt:
            {
                var condition = ReplaceLocalVariables(
                    (ExpressionSyntax)_expressionRewriter.Visit(ifStmt.Condition));

                var whenTrue = TryConvertStatement(ifStmt.Statement, memberName);
                if (whenTrue == null)
                {
                    return null;
                }

                ExpressionSyntax whenFalse;
                if (ifStmt.Else != null)
                {
                    var converted = TryConvertStatement(ifStmt.Else.Statement, memberName);
                    if (converted == null)
                    {
                        return null;
                    }

                    whenFalse = converted;
                }
                else
                {
                    whenFalse = fallthrough ?? DefaultLiteral();
                }

                return SyntaxFactory.ConditionalExpression(condition, whenTrue, whenFalse);
            }

            case SwitchStatementSyntax switchStmt:
                return TryConvertSwitchStatement(switchStmt, fallthrough, memberName);

            case BlockSyntax blockStmt:
            {
                var nestedLocal = blockStmt.DescendantNodes()
                    .OfType<LocalDeclarationStatementSyntax>()
                    .FirstOrDefault();
                if (nestedLocal != null)
                {
                    ReportUnsupportedStatement(nestedLocal, memberName,
                        "Local declarations in nested blocks are not supported");
                    return null;
                }
                return TryConvertStatements(blockStmt.Statements.ToList(), memberName);
            }

            case ExpressionStatementSyntax exprStmt:
                return AnalyzeExpressionStatement(exprStmt, memberName);

            case LocalDeclarationStatementSyntax:
                // Defensive guard: TryConvertStatements already enforces that locals appear
                // before any non-local statement and reports EFP0003 for ordering violations.
                // This branch is reached only if a local declaration somehow reaches
                // TryConvertStatementWithFallthrough directly (e.g. future call-sites).
                ReportUnsupportedStatement(statement, memberName,
                    "Local variable declarations must appear before any other statements (return, if, switch, …)");
                return null;

            default:
                ReportUnsupportedStatement(statement, memberName,
                    $"Statement type '{statement.GetType().Name}' is not supported");
                return null;
        }
    }

    /// <summary>
    /// Thin wrapper: converts a statement with no implicit fallthrough (uses <c>default</c> literal).
    /// Used for sub-expressions such as the then/else branches of an if statement.
    /// </summary>
    private ExpressionSyntax? TryConvertStatement(StatementSyntax statement, string memberName)
        => TryConvertStatementWithFallthrough(statement, null, memberName);

    /// <summary>
    /// Processes a local variable declaration statement, rewriting the initializer and storing it in the local variables dictionary.
    /// </summary>
    private bool TryProcessLocalDeclaration(LocalDeclarationStatementSyntax localDecl, string memberName)
    {
        foreach (var variable in localDecl.Declaration.Variables)
        {
            if (variable.Initializer == null)
            {
                ReportUnsupportedStatement(localDecl, memberName, "Local variables must have an initializer");
                return false;
            }

            var variableName = variable.Identifier.Text;

            // Rewrite and eagerly inline any already-known local variables so that
            // transitive substitution works (e.g. var a = 1; var b = a + 2; return b; => 1 + 2).
            var rewrittenInitializer = (ExpressionSyntax)_expressionRewriter.Visit(variable.Initializer.Value);
            rewrittenInitializer = ReplaceLocalVariables(rewrittenInitializer);

            _localVariables[variableName] = rewrittenInitializer;
        }

        return true;
    }

    /// <summary>
    /// Converts a return statement to its expression, after rewriting it and replacing any local variable references.
    /// </summary>
    private ExpressionSyntax? TryConvertReturnStatement(ReturnStatementSyntax returnStmt, string memberName)
    {
        if (returnStmt.Expression == null)
        {
            ReportUnsupportedStatement(returnStmt, memberName, "Return statement must have an expression");
            return null;
        }

        var expression = (ExpressionSyntax)_expressionRewriter.Visit(returnStmt.Expression);
        return ReplaceLocalVariables(expression);
    }

    /// <summary>
    /// Converts a switch statement to nested conditional expressions.
    /// <paramref name="fallthrough"/> is used as the base when there is no default section;
    /// pass <c>null</c> to fall back to a <c>default</c> literal.
    /// </summary>
    private ExpressionSyntax? TryConvertSwitchStatement(
        SwitchStatementSyntax switchStmt, ExpressionSyntax? fallthrough, string memberName)
    {
        var switchExpression = ReplaceLocalVariables(
            (ExpressionSyntax)_expressionRewriter.Visit(switchStmt.Expression));

        SwitchSectionSyntax? defaultSection = null;
        var nonDefaultSections = new List<SwitchSectionSyntax>();

        foreach (var section in switchStmt.Sections)
        {
            if (section.Labels.Any(l => l is DefaultSwitchLabelSyntax))
            {
                defaultSection = section;
            }
            else
            {
                nonDefaultSections.Add(section);
            }
        }

        // Base expression: explicit default section, caller-supplied fallthrough, or default literal.
        ExpressionSyntax? current;
        if (defaultSection != null)
        {
            current = ConvertSwitchSection(defaultSection, memberName);
            if (current == null)
            {
                return null;
            }
        }
        else
        {
            current = fallthrough ?? DefaultLiteral();
        }

        // Build nested conditionals from the last non-default section inward.
        for (var i = nonDefaultSections.Count - 1; i >= 0; i--)
        {
            var section = nonDefaultSections[i];
            var sectionExpr = ConvertSwitchSection(section, memberName);
            if (sectionExpr == null)
            {
                return null;
            }

            ExpressionSyntax? condition = null;
            foreach (var label in section.Labels)
            {
                if (label is CaseSwitchLabelSyntax caseLabel)
                {
                    var caseValue = ReplaceLocalVariables(
                        (ExpressionSyntax)_expressionRewriter.Visit(caseLabel.Value));

                    var labelCondition = SyntaxFactory.BinaryExpression(
                        SyntaxKind.EqualsExpression, switchExpression, caseValue);

                    condition = condition == null
                        ? labelCondition
                        : SyntaxFactory.BinaryExpression(
                            SyntaxKind.LogicalOrExpression, condition, labelCondition);
                }
                else if (label is not DefaultSwitchLabelSyntax)
                {
                    ReportUnsupportedStatement(switchStmt, memberName,
                        $"Switch label type '{label.GetType().Name}' is not supported. Use case labels or switch expressions instead.");
                    return null;
                }
            }

            if (condition != null)
            {
                current = SyntaxFactory.ConditionalExpression(condition, sectionExpr, current);
            }
        }

        return current;
    }

    /// <summary>
    /// Converts a switch section to an expression (strips trailing break).
    /// </summary>
    private ExpressionSyntax? ConvertSwitchSection(SwitchSectionSyntax section, string memberName)
    {
        var statements = section.Statements.ToList();

        if (statements.Count > 0 && statements.Last() is BreakStatementSyntax)
        {
            statements = statements.Take(statements.Count - 1).ToList();
        }

        if (statements.Count > 0)
        {
            return TryConvertStatements(statements, memberName);
        }

        var location = section.Labels.FirstOrDefault()?.GetLocation() ?? section.GetLocation();
        _context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.UnsupportedStatementInBlockBody,
            location, memberName,
            "Switch section must have at least one statement"));
            
        return null;
    }

    /// <summary>
    /// Replaces references to local variables in the given expression with their initializer expressions.
    /// </summary>
    private ExpressionSyntax ReplaceLocalVariables(ExpressionSyntax expression)
        => (ExpressionSyntax)new LocalVariableReplacer(_localVariables).Visit(expression);

    private static LiteralExpressionSyntax DefaultLiteral()
        => SyntaxFactory.LiteralExpression(
            SyntaxKind.DefaultLiteralExpression,
            SyntaxFactory.Token(SyntaxKind.DefaultKeyword));

    /// <summary>
    /// Analyzes an expression statement for side effects. If it has side effects, reports a diagnostic and returns null.
    /// </summary>
    private ExpressionSyntax? AnalyzeExpressionStatement(ExpressionStatementSyntax exprStmt, string memberName)
    {
        var expression = exprStmt.Expression;

        if (HasSideEffects(expression, out var errorMessage))
        {
            ReportSideEffect(expression, errorMessage);
            return null;
        }

        if (expression is InvocationExpressionSyntax invocation)
        {
            if (!IsProjectableMethodCall(invocation, out var warningMessage))
            {
                ReportPotentialSideEffect(invocation, warningMessage);
                return null;
            }
        }

        ReportUnsupportedStatement(exprStmt, memberName,
            "Expression statements are not supported in projectable methods. Consider removing this statement or converting it to a return statement.");
        return null;
    }

    private bool HasSideEffects(ExpressionSyntax expression, out string errorMessage) => expression switch
    {
        AssignmentExpressionSyntax assignment =>
            (errorMessage = GetAssignmentErrorMessage(assignment)) != null,

        PostfixUnaryExpressionSyntax postfix when
            postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
            postfix.IsKind(SyntaxKind.PostDecrementExpression)
            => (errorMessage = $"Increment/decrement operator '{postfix.OperatorToken.Text}' has side effects and cannot be used in projectable methods") != null,

        PrefixUnaryExpressionSyntax prefix when
            prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
            prefix.IsKind(SyntaxKind.PreDecrementExpression)
            => (errorMessage = $"Increment/decrement operator '{prefix.OperatorToken.Text}' has side effects and cannot be used in projectable methods") != null,

        _ => (errorMessage = string.Empty) == null
    };

    private bool IsProjectableMethodCall(InvocationExpressionSyntax invocation, out string warningMessage)
    {
        var symbolInfo = _expressionRewriter.GetSemanticModel().GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is IMethodSymbol methodSymbol)
        {
            var hasProjectableAttr = methodSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "ProjectableAttribute");

            if (!hasProjectableAttr)
            {
                warningMessage = $"Method call '{methodSymbol.Name}' may have side effects. Only calls to methods marked with [Projectable] are guaranteed to be safe in projectable methods";
                return false;
            }
        }

        warningMessage = string.Empty;
        return true;
    }

    private string GetAssignmentErrorMessage(AssignmentExpressionSyntax assignment)
    {
        if (assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
            {
                return $"Property assignment '{memberAccess.Name}' has side effects and cannot be used in projectable methods";
            }

            return "Assignment operation has side effects and cannot be used in projectable methods";
        }

        return $"Compound assignment operator '{assignment.OperatorToken.Text}' has side effects and cannot be used in projectable methods";
    }

    private void ReportSideEffect(SyntaxNode node, string message)
        => _context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.SideEffectInBlockBody, node.GetLocation(), message));

    private void ReportPotentialSideEffect(SyntaxNode node, string message)
        => _context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.PotentialSideEffectInBlockBody, node.GetLocation(), message));

    private void ReportUnsupportedStatement(StatementSyntax statement, string memberName, string reason)
        => _context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.UnsupportedStatementInBlockBody,
            statement.GetLocation(), memberName, reason));

    private class LocalVariableReplacer : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, ExpressionSyntax> _localVariables;

        public LocalVariableReplacer(Dictionary<string, ExpressionSyntax> localVariables)
        {
            _localVariables = localVariables;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (_localVariables.TryGetValue(node.Identifier.Text, out var replacement))
            {
                return SyntaxFactory.ParenthesizedExpression(replacement.WithoutTrivia())
                    .WithTriviaFrom(node);
            }

            return base.VisitIdentifierName(node);
        }
    }
}
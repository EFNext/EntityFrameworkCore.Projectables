using EntityFrameworkCore.Projectables.Generator.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;

/// <summary>
/// Converts constructor body statements into a dictionary of property-name → expression
/// pairs that are used to build a member-init expression for EF Core projections.
/// Supports simple assignments, local variable declarations, and if/else statements.
/// Previously-assigned properties (including those from a delegated base/this ctor) are
/// inlined when referenced in subsequent assignments.
/// </summary>
internal class ConstructorBodyConverter
{
    private readonly SourceProductionContext _context;
    private readonly Func<ExpressionSyntax, ExpressionSyntax> _rewrite;
    private readonly Dictionary<string, ExpressionSyntax> _paramSubstitutions;

    /// <summary>Creates a converter for the <em>main</em> constructor body.</summary>
    public ConstructorBodyConverter(
        SourceProductionContext context,
        ExpressionSyntaxRewriter expressionRewriter)
    {
        _context = context;
        _rewrite = expr => (ExpressionSyntax)expressionRewriter.Visit(expr);
        _paramSubstitutions = new Dictionary<string, ExpressionSyntax>();
    }

    /// <summary>Creates a converter for a <em>delegated</em> (base/this) constructor body.</summary>
    public ConstructorBodyConverter(
        SourceProductionContext context,
        Dictionary<string, ExpressionSyntax> paramSubstitutions)
    {
        _context = context;
        _rewrite = expr => expr;
        _paramSubstitutions = paramSubstitutions;
    }
        
    public Dictionary<string, ExpressionSyntax>? TryConvertBody(
        IEnumerable<StatementSyntax> statements,
        string memberName,
        IReadOnlyDictionary<string, ExpressionSyntax>? initialContext = null)
    {
        var assignments = new Dictionary<string, ExpressionSyntax>();
        if (!TryProcessBlock(statements, assignments, memberName, outerContext: initialContext, inheritedLocals: null))
        {
            return null;
        }

        return assignments;
    }

    private bool TryProcessBlock(
        IEnumerable<StatementSyntax> statements,
        Dictionary<string, ExpressionSyntax> assignments,
        string memberName,
        IReadOnlyDictionary<string, ExpressionSyntax>? outerContext,
        IReadOnlyDictionary<string, ExpressionSyntax>? inheritedLocals = null)
    {
        // Create a fresh local-variable scope for this block.
        // Inherited locals (from parent scopes) are copied in so they remain visible here,
        // but any new locals added inside this block won't escape to the caller's scope.
        var blockLocals = inheritedLocals is { Count: > 0 }
            ? inheritedLocals.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            : new Dictionary<string, ExpressionSyntax>();

        foreach (var statement in statements)
        {
            // Everything accumulated so far (from outer scope + this block) is visible.
            var visible = BuildVisible(outerContext, assignments);
            if (!TryProcessStatement(statement, assignments, memberName, visible, blockLocals))
            {
                return false;
            }
        }
        return true;
    }

    private bool TryProcessStatement(
        StatementSyntax statement,
        Dictionary<string, ExpressionSyntax> assignments,
        string memberName,
        IReadOnlyDictionary<string, ExpressionSyntax>? visibleContext,
        Dictionary<string, ExpressionSyntax> currentLocals)
    {
        switch (statement)
        {
            case LocalDeclarationStatementSyntax localDecl:
                return TryProcessLocalDeclaration(localDecl, memberName, visibleContext, currentLocals);

            case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment }
                when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                return TryProcessAssignment(assignment, assignments, memberName, visibleContext, currentLocals);

            case IfStatementSyntax ifStmt:
                return TryProcessIfStatement(ifStmt, assignments, memberName, visibleContext, currentLocals);

            case BlockSyntax block:
                // Nested block: inherit current locals but new locals declared inside won't escape.
                return TryProcessBlock(block.Statements, assignments, memberName, visibleContext, currentLocals);

            default:
                ReportUnsupported(statement, memberName,
                    $"Statement type '{statement.GetType().Name}' is not supported in a [Projectable] constructor body. " +
                    "Only assignments, local variable declarations, and if/else statements are supported.");
                return false;
        }
    }

    private bool TryProcessLocalDeclaration(
        LocalDeclarationStatementSyntax localDecl,
        string memberName,
        IReadOnlyDictionary<string, ExpressionSyntax>? visibleContext,
        Dictionary<string, ExpressionSyntax> currentLocals)
    {
        foreach (var variable in localDecl.Declaration.Variables)
        {
            if (variable.Initializer == null)
            {
                ReportUnsupported(localDecl, memberName, "Local variables must have an initializer");
                return false;
            }

            var rewritten = _rewrite(variable.Initializer.Value);
            rewritten = ApplySubstitutions(rewritten, visibleContext, currentLocals);
            // Store in the current block scope; won't be visible outside this block.
            currentLocals[variable.Identifier.Text] = rewritten;
        }
        return true;
    }

    private bool TryProcessAssignment(
        AssignmentExpressionSyntax assignment,
        Dictionary<string, ExpressionSyntax> assignments,
        string memberName,
        IReadOnlyDictionary<string, ExpressionSyntax>? visibleContext,
        IReadOnlyDictionary<string, ExpressionSyntax> currentLocals)
    {
        var targetMember = GetTargetMember(assignment.Left);
        if (targetMember is null)
        {
            ReportUnsupported(assignment, memberName,
                $"Unsupported assignment target '{assignment.Left}'. " +
                "Only 'PropertyName = ...' or 'this.PropertyName = ...' are supported.");
            return false;
        }

        var rewritten = _rewrite(assignment.Right);
        rewritten = ApplySubstitutions(rewritten, visibleContext, currentLocals);
        assignments[targetMember.Identifier.Text] = rewritten;
        return true;
    }

    private bool TryProcessIfStatement(
        IfStatementSyntax ifStmt,
        Dictionary<string, ExpressionSyntax> assignments,
        string memberName,
        IReadOnlyDictionary<string, ExpressionSyntax>? visibleContext,
        IReadOnlyDictionary<string, ExpressionSyntax> currentLocals)
    {
        var condition = _rewrite(ifStmt.Condition);
        condition = ApplySubstitutions(condition, visibleContext, currentLocals);

        // Each branch gets currentLocals as inherited context. TryProcessBlock will create
        // its own block-scoped copy, so locals declared inside a branch won't leak into the
        // sibling branch or into code that follows the if-statement.
        var thenAssignments = new Dictionary<string, ExpressionSyntax>();
        if (!TryProcessBlock(GetStatements(ifStmt.Statement), thenAssignments, memberName, visibleContext, currentLocals))
        {
            return false;
        }

        var elseAssignments = new Dictionary<string, ExpressionSyntax>();
        if (ifStmt.Else != null)
        {
            if (!TryProcessBlock(GetStatements(ifStmt.Else.Statement), elseAssignments, memberName, visibleContext, currentLocals))
            {
                return false;
            }
        }

        foreach (var thenKvp in thenAssignments)
        {
            var prop = thenKvp.Key;
            var thenExpr = thenKvp.Value;

            ExpressionSyntax elseExpr;
            if (elseAssignments.TryGetValue(prop, out var elseVal))
            {
                elseExpr = elseVal;
            }
            else if (assignments.TryGetValue(prop, out var existing))
            {
                elseExpr = existing;
            }
            else
            {
                elseExpr = DefaultLiteral();
            }

            assignments[prop] = SyntaxFactory.ConditionalExpression(condition, thenExpr, elseExpr);
        }

        foreach (var elseKvp in elseAssignments)
        {
            var prop = elseKvp.Key;
            var elseExpr = elseKvp.Value;

            if (thenAssignments.ContainsKey(prop))
            {
                continue;
            }

            ExpressionSyntax thenExpr;
            if (assignments.TryGetValue(prop, out var existing))
            {
                thenExpr = existing;
            }
            else
            {
                thenExpr = DefaultLiteral();
            }

            assignments[prop] = SyntaxFactory.ConditionalExpression(condition, thenExpr, elseExpr);
        }

        return true;
    }

    private static IEnumerable<StatementSyntax> GetStatements(StatementSyntax statement) =>
        statement is BlockSyntax block
            ? block.Statements
            : new StatementSyntax[] { statement };

    private static IdentifierNameSyntax? GetTargetMember(ExpressionSyntax left) =>
        left switch
        {
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => name,
            IdentifierNameSyntax ident => ident,
            _ => null
        };

    /// <summary>
    /// Merges outer (parent scope) and local (current block) accumulated dictionaries
    /// into a single read-only view for use as a substitution context.
    /// Local entries take priority over outer entries.
    /// Returns <c>null</c> when both are empty (avoids unnecessary allocations).
    /// </summary>
    private static IReadOnlyDictionary<string, ExpressionSyntax>? BuildVisible(
        IReadOnlyDictionary<string, ExpressionSyntax>? outer,
        Dictionary<string, ExpressionSyntax> local)
    {
        var outerEmpty = outer == null || outer.Count == 0;
        var localEmpty = local.Count == 0;

        if (outerEmpty && localEmpty)
        {
            return null;
        }

        if (outerEmpty)
        {
            return local;
        }

        if (localEmpty)
        {
            return outer;
        }

        var merged = new Dictionary<string, ExpressionSyntax>();
        foreach (var kvp in outer!)
        {
            merged[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in local)
        {
            merged[kvp.Key] = kvp.Value;
        }

        return merged;
    }

    private ExpressionSyntax ApplySubstitutions(
        ExpressionSyntax expr,
        IReadOnlyDictionary<string, ExpressionSyntax>? visibleContext,
        IReadOnlyDictionary<string, ExpressionSyntax>? currentLocals = null)
    {
        if (_paramSubstitutions.Count > 0)
        {
            expr = ParameterSubstitutor.Substitute(expr, _paramSubstitutions);
        }

        if (currentLocals != null && currentLocals.Count > 0)
        {
            expr = LocalVariableSubstitutor.Substitute(expr, currentLocals);
        }

        if (visibleContext != null && visibleContext.Count > 0)
        {
            expr = AssignedPropertySubstitutor.Substitute(expr, visibleContext);
        }

        return expr;
    }

    private static ExpressionSyntax DefaultLiteral() =>
        SyntaxFactory.LiteralExpression(
            SyntaxKind.DefaultLiteralExpression,
            SyntaxFactory.Token(SyntaxKind.DefaultKeyword));

    private void ReportUnsupported(SyntaxNode node, string memberName, string reason)
    {
        _context.ReportDiagnostic(Diagnostic.Create(
            Diagnostics.UnsupportedStatementInBlockBody,
            node.GetLocation(),
            memberName,
            reason));
    }

    /// <summary>
    /// Replaces parameter-name identifier references with call-site argument expressions
    /// (used when inlining a delegated base/this constructor body).
    /// </summary>
    sealed internal class ParameterSubstitutor : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, ExpressionSyntax> _map;
        private ParameterSubstitutor(Dictionary<string, ExpressionSyntax> map) => _map = map;

        public static ExpressionSyntax Substitute(ExpressionSyntax expr, Dictionary<string, ExpressionSyntax> map)
            => (ExpressionSyntax)new ParameterSubstitutor(map).Visit(expr);

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
            => _map.TryGetValue(node.Identifier.Text, out var replacement)
                ? replacement.WithTriviaFrom(node)
                : base.VisitIdentifierName(node);
    }

    /// <summary>
    /// Replaces local-variable identifier references with their inlined (parenthesised)
    /// initializer expressions.
    /// </summary>
    private sealed class LocalVariableSubstitutor : CSharpSyntaxRewriter
    {
        private readonly IReadOnlyDictionary<string, ExpressionSyntax> _locals;
        private LocalVariableSubstitutor(IReadOnlyDictionary<string, ExpressionSyntax> locals) => _locals = locals;

        public static ExpressionSyntax Substitute(ExpressionSyntax expr, IReadOnlyDictionary<string, ExpressionSyntax> locals)
            => (ExpressionSyntax)new LocalVariableSubstitutor(locals).Visit(expr);

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
            => _locals.TryGetValue(node.Identifier.Text, out var replacement)
                ? SyntaxFactory.ParenthesizedExpression(replacement.WithoutTrivia()).WithTriviaFrom(node)
                : base.VisitIdentifierName(node);
    }

    /// <summary>
    /// Replaces references to previously-assigned properties with the expression that was
    /// assigned to them, so that EF Core sees a fully-inlined projection.
    /// <para>
    /// Handles two syntactic forms:
    /// <list type="bullet">
    ///   <item><c>@this.PropName</c> — produced by <see cref="ExpressionSyntaxRewriter"/> for
    ///   instance-member references in the main constructor body.</item>
    ///   <item>Bare <c>PropName</c> identifier — appears in delegated (base/this) constructor
    ///   bodies where the identity rewriter is used.</item>
    /// </list>
    /// </para>
    /// </summary>
    private sealed class AssignedPropertySubstitutor : CSharpSyntaxRewriter
    {
        private readonly IReadOnlyDictionary<string, ExpressionSyntax> _accumulated;

        private AssignedPropertySubstitutor(IReadOnlyDictionary<string, ExpressionSyntax> accumulated)
            => _accumulated = accumulated;

        public static ExpressionSyntax Substitute(
            ExpressionSyntax expr,
            IReadOnlyDictionary<string, ExpressionSyntax> accumulated)
            => (ExpressionSyntax)new AssignedPropertySubstitutor(accumulated).Visit(expr);

        // Catches @this.PropName → inline the accumulated expression for PropName.
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax thisRef &&
                (thisRef.Identifier.Text == "@this" || thisRef.Identifier.ValueText == "this") &&
                node.Name is IdentifierNameSyntax propName &&
                _accumulated.TryGetValue(propName.Identifier.Text, out var replacement))
            {
                return SyntaxFactory.ParenthesizedExpression(replacement.WithoutTrivia())
                    .WithTriviaFrom(node);
            }

            return base.VisitMemberAccessExpression(node);
        }

        // Catches bare PropName → inline accumulated expression (delegated ctor case).
        // Params and locals have already been substituted before this runs, so any remaining
        // bare identifier that matches an accumulated property key is a property reference.
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Do not substitute special identifiers (@this, type keywords, etc.)
            var text = node.Identifier.Text;
            if (text.StartsWith("@") || text == "default" || text == "null" || text == "true" || text == "false")
            {
                return base.VisitIdentifierName(node);
            }

            if (_accumulated.TryGetValue(text, out var replacement))
            {
                return SyntaxFactory.ParenthesizedExpression(replacement.WithoutTrivia())
                    .WithTriviaFrom(node);
            }

            return base.VisitIdentifierName(node);
        }
    }
}
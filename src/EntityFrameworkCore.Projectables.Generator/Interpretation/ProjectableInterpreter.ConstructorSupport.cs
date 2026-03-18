using EntityFrameworkCore.Projectables.Generator.Infrastructure;
using EntityFrameworkCore.Projectables.Generator.SyntaxRewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.Interpretation;

static internal partial class ProjectableInterpreter
{
    /// <summary>
    /// Collects the property-assignment expressions that the delegated constructor (base/this)
    /// would perform, substituting its parameters with the actual call-site argument expressions.
    /// Supports if/else logic inside the delegated constructor body, and follows the chain of
    /// base/this initializers recursively.
    /// Returns <c>null</c> when an unsupported statement is encountered (diagnostics reported).
    /// </summary>
    private static Dictionary<string, ExpressionSyntax>? CollectDelegatedConstructorAssignments(
        IMethodSymbol delegatedCtor,
        SeparatedSyntaxList<ArgumentSyntax> callerArgs,
        ExpressionSyntaxRewriter expressionSyntaxRewriter,
        SourceProductionContext context,
        string memberName,
        Compilation compilation,
        bool argsAlreadyRewritten = false)
    {
        // Only process constructors whose source is available in this compilation
        var syntax = delegatedCtor.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        if (syntax is null)
        {
            // The delegated constructor is not available in source, so we cannot analyze
            // its body or any assignments it performs. Report a diagnostic and return null.
            var location = delegatedCtor.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Diagnostics.NoSourceAvailableForDelegatedConstructor,
                    location,
                    delegatedCtor.ToDisplayString(),
                    delegatedCtor.ContainingType?.ToDisplayString() ?? "<unknown>",
                    memberName));
            return null;
        }

        // Build a mapping: delegated-param-name → caller argument expression.
        // First-level args come from the original syntax tree and must be visited by the
        // ExpressionSyntaxRewriter. Recursive-level args are already-substituted detached
        // nodes and must NOT be visited (doing so throws "node not in syntax tree").
        var paramToArg = new Dictionary<string, ExpressionSyntax>();
        for (var i = 0; i < callerArgs.Count && i < delegatedCtor.Parameters.Length; i++)
        {
            var paramName = delegatedCtor.Parameters[i].Name;
            var argExpr = argsAlreadyRewritten
                ? callerArgs[i].Expression
                : (ExpressionSyntax)expressionSyntaxRewriter.Visit(callerArgs[i].Expression);
            paramToArg[paramName] = argExpr;
        }

        // The accumulated assignments start from the delegated ctor's own initializer (if any),
        // so that base/this chains are followed recursively.
        var accumulated = new Dictionary<string, ExpressionSyntax>();

        if (syntax.Initializer is { } delegatedInitializer)
        {
            // Use the semantic model for the delegated constructor's own SyntaxTree.
            // The original member's semantic model is bound to a different tree and
            // would throw if the delegated/base ctor is declared in another file.
            var delegatedCtorSemanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
            var delegatedInitializerSymbol =
                delegatedCtorSemanticModel.GetSymbolInfo(delegatedInitializer).Symbol as IMethodSymbol;

            if (delegatedInitializerSymbol is not null)
            {
                // Substitute the delegated ctor's initializer arguments using our paramToArg map,
                // so that e.g. `: base(id)` becomes `: base(<caller's expression for id>)`.
                var substitutedInitArgs = SubstituteArguments(
                    delegatedInitializer.ArgumentList.Arguments, paramToArg);

                var chainedAssignments = CollectDelegatedConstructorAssignments(
                    delegatedInitializerSymbol,
                    substitutedInitArgs,
                    expressionSyntaxRewriter,
                    context,
                    memberName,
                    compilation,
                    argsAlreadyRewritten: true); // args are now detached substituted nodes

                if (chainedAssignments is null)
                    return null;

                foreach (var kvp in chainedAssignments)
                    accumulated[kvp.Key] = kvp.Value;
            }
        }

        if (syntax.Body is null)
            return accumulated;

        // Use ConstructorBodyConverter (identity rewriter + param substitutions) so that
        // if/else, local variables and simple assignments in the delegated ctor are all handled.
        // Pass the already-accumulated chained assignments as the initial visible context.
        IReadOnlyDictionary<string, ExpressionSyntax>? initialCtx =
            accumulated.Count > 0 ? accumulated : null;
        var converter = new ConstructorBodyConverter(context, paramToArg);
        var bodyAssignments = converter.TryConvertBody(syntax.Body.Statements, memberName, initialCtx);

        if (bodyAssignments is null)
            return null;

        foreach (var kvp in bodyAssignments)
            accumulated[kvp.Key] = kvp.Value;

        return accumulated;
    }

    /// <summary>
    /// Substitutes identifiers in <paramref name="args"/> using the <paramref name="paramToArg"/>
    /// mapping. Used to forward the outer caller's arguments through a chain of
    /// base/this initializer calls.
    /// </summary>
    private static SeparatedSyntaxList<ArgumentSyntax> SubstituteArguments(
        SeparatedSyntaxList<ArgumentSyntax> args,
        Dictionary<string, ExpressionSyntax> paramToArg)
    {
        if (paramToArg.Count == 0)
            return args;

        var result = new List<ArgumentSyntax>();
        foreach (var arg in args)
        {
            var substituted = ConstructorBodyConverter.ParameterSubstitutor.Substitute(
                arg.Expression, paramToArg);
            result.Add(arg.WithExpression(substituted));
        }
        return SyntaxFactory.SeparatedList(result);
    }
}
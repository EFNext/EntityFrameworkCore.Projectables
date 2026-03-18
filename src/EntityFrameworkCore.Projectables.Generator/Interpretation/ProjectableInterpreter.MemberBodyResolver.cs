using EntityFrameworkCore.Projectables.Generator.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.Interpretation;

static internal partial class ProjectableInterpreter
{
    /// <summary>
    /// Resolves the member body syntax to use.
    /// <para>
    /// When <paramref name="useMemberBody"/> is <c>null</c>, returns <paramref name="member"/> unchanged.
    /// When resolution succeeds, returns the resolved body.
    /// When resolution fails (silent skip or diagnostic reported), returns <c>null</c>.
    /// </para>
    /// </summary>
    private static MemberDeclarationSyntax? TryResolveMemberBody(
        MemberDeclarationSyntax member,
        ISymbol memberSymbol,
        string? useMemberBody,
        SourceProductionContext context)
    {
        if (useMemberBody is null)
        {
            return member;
        }

        var comparer = SymbolEqualityComparer.Default;

        // Step 1: find all members with the requested name
        var allCandidates = memberSymbol.ContainingType.GetMembers(useMemberBody);

        if (allCandidates.IsEmpty)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.UseMemberBodyNotFound,
                member.GetLocation(),
                memberSymbol.Name,
                useMemberBody,
                memberSymbol.ContainingType.Name));
            return null;
        }

        // Step 2: partition into same-type candidates and Expression<TDelegate> property candidates
        var regularCompatible = allCandidates.Where(x =>
        {
            if (memberSymbol is IMethodSymbol symbolMethod &&
                x is IMethodSymbol xMethod &&
                comparer.Equals(symbolMethod.ReturnType, xMethod.ReturnType) &&
                symbolMethod.TypeArguments.Length == xMethod.TypeArguments.Length &&
                !symbolMethod.TypeArguments.Zip(xMethod.TypeArguments, (a, b) => !comparer.Equals(a, b)).Any(v => v) &&
                symbolMethod.Parameters.Length == xMethod.Parameters.Length &&
                !symbolMethod.Parameters.Zip(xMethod.Parameters,
                    (a, b) => !comparer.Equals(a.Type, b.Type) || a.RefKind != b.RefKind).Any(v => v))
            {
                return true;
            }

            if (memberSymbol is IPropertySymbol symbolProperty &&
                x is IPropertySymbol xProperty &&
                comparer.Equals(symbolProperty.Type, xProperty.Type))
            {
                return true;
            }

            return false;
        }).ToList();

        // Expression-property candidates: a property returning Expression<TDelegate>.
        // Supported in the generator for both projectable methods and projectable properties.
        var exprPropertyCandidates = allCandidates.Where(IsExpressionDelegateProperty).ToList();

        // Filter Expression<TDelegate> candidates whose Func generic-argument count, return type,
        // and parameter types are all compatible with the projectable member's signature.
        List<ISymbol> compatibleExprPropertyCandidates = [];
        if (exprPropertyCandidates.Count > 0)
        {
            if (memberSymbol is IMethodSymbol exprCheckMethod)
            {
                var isExtensionBlock = memberSymbol.ContainingType is { IsExtension: true };
                var hasImplicitThis = !exprCheckMethod.IsStatic || isExtensionBlock;
                var expectedFuncArgCount = exprCheckMethod.Parameters.Length + (hasImplicitThis ? 2 : 1);

                // Determine the expected receiver type when hasImplicitThis is true.
                // For extension-block members the receiver is the extension parameter's type;
                // for ordinary instance methods it is the containing type.
                ITypeSymbol? expectedReceiverType = null;
                if (hasImplicitThis)
                {
                    expectedReceiverType = isExtensionBlock
                        ? exprCheckMethod.ContainingType.ExtensionParameter?.Type
                        : exprCheckMethod.ContainingType;
                }

                compatibleExprPropertyCandidates = exprPropertyCandidates.Where(x =>
                {
                    if (x is not IPropertySymbol propSym)
                    {
                        return false;
                    }

                    if (propSym.Type is not INamedTypeSymbol exprType || exprType.TypeArguments.Length != 1)
                    {
                        return false;
                    }

                    if (exprType.TypeArguments[0] is not INamedTypeSymbol delegateType)
                    {
                        return false;
                    }

                    if (delegateType.TypeArguments.Length != expectedFuncArgCount)
                    {
                        return false;
                    }

                    // Receiver-type check: when hasImplicitThis is true, TypeArguments[0] must match
                    // the implicit receiver — the containing type for instance methods, or the extension
                    // receiver type for extension-block members.
                    if (hasImplicitThis && expectedReceiverType is not null)
                    {
                        if (!comparer.Equals(delegateType.TypeArguments[0], expectedReceiverType))
                        {
                            return false;
                        }
                    }

                    // Return-type check: the last type argument of the delegate must match the method's return type.
                    var delegateReturnType = delegateType.TypeArguments[delegateType.TypeArguments.Length - 1];
                    if (!comparer.Equals(delegateReturnType, exprCheckMethod.ReturnType))
                    {
                        return false;
                    }

                    // Parameter-type checks: each explicit parameter type must match.
                    // When hasImplicitThis is true, TypeArguments[0] is the implicit receiver — skip it.
                    var paramOffset = hasImplicitThis ? 1 : 0;
                    for (var i = 0; i < exprCheckMethod.Parameters.Length; i++)
                    {
                        if (!comparer.Equals(delegateType.TypeArguments[paramOffset + i], exprCheckMethod.Parameters[i].Type))
                        {
                            return false;
                        }
                    }

                    return true;
                }).ToList();
            }
            else if (memberSymbol is IPropertySymbol exprCheckProperty)
            {
                // Instance property: Func<ContainingType, PropType> — 2 type arguments.
                // Static property:   Func<PropType>                  — 1 type argument.
                var expectedFuncArgCount = exprCheckProperty.IsStatic ? 1 : 2;

                compatibleExprPropertyCandidates = exprPropertyCandidates.Where(x =>
                {
                    if (x is not IPropertySymbol propSym)
                    {
                        return false;
                    }

                    if (propSym.Type is not INamedTypeSymbol exprType || exprType.TypeArguments.Length != 1)
                    {
                        return false;
                    }

                    if (exprType.TypeArguments[0] is not INamedTypeSymbol delegateType)
                    {
                        return false;
                    }

                    if (delegateType.TypeArguments.Length != expectedFuncArgCount)
                    {
                        return false;
                    }

                    // For instance properties, the first delegate type argument must be the containing type
                    // (the implicit receiver).
                    if (!exprCheckProperty.IsStatic)
                    {
                        var receiverType = delegateType.TypeArguments[0];
                        if (!comparer.Equals(receiverType, exprCheckProperty.ContainingType))
                        {
                            return false;
                        }
                    }
                    // Return-type check: the last type argument of the delegate must match the property type.
                    var delegateReturnType = delegateType.TypeArguments[delegateType.TypeArguments.Length - 1];
                    return comparer.Equals(delegateReturnType, exprCheckProperty.Type);
                }).ToList();
            }
        }

        // Step 3: if no generator-handled candidates exist, diagnose
        if (regularCompatible.Count == 0 && compatibleExprPropertyCandidates.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.UseMemberBodyIncompatible,
                member.GetLocation(),
                memberSymbol.Name,
                useMemberBody));
            return null;
        }

        // Step 4a: locate valid syntax for regular (same-type) candidates
        var resolvedBody = regularCompatible
            .SelectMany(x => x.DeclaringSyntaxReferences)
            .Select(x => x.GetSyntax())
            .OfType<MemberDeclarationSyntax>()
            .FirstOrDefault(x =>
            {
                if (x == null ||
                    x.SyntaxTree != member.SyntaxTree ||
                    x.Modifiers.Any(SyntaxKind.StaticKeyword) != member.Modifiers.Any(SyntaxKind.StaticKeyword))
                {
                    return false;
                }

                if (x is MethodDeclarationSyntax xMethod &&
                    (xMethod.ExpressionBody is not null || xMethod.Body is not null))
                {
                    return true;
                }

                if (x is PropertyDeclarationSyntax xProp)
                {
                    return HasReadablePropertyBody(xProp);
                }

                return false;
            });

        // Step 4b: if not found, try compatible Expression<TDelegate> property candidates.
        // These don't need to share the member's static modifier because a
        // static Expression<Func<...>> property can legitimately back either
        // a static or an instance projectable method.
        // They are also allowed to live in a different file (e.g. a split partial class):
        // a direct lambda body can be extracted purely syntactically without a shared
        // SemanticModel; the runtime fallback handles any cases the generator can't inline.
        if (resolvedBody is null && compatibleExprPropertyCandidates.Count > 0)
        {
            resolvedBody = compatibleExprPropertyCandidates
                .SelectMany(x => x.DeclaringSyntaxReferences)
                .Select(x => x.GetSyntax())
                .OfType<MemberDeclarationSyntax>()
                .FirstOrDefault(x =>
                {
                    if (x == null)
                    {
                        return false;
                    }

                    return x is PropertyDeclarationSyntax xProp && HasReadablePropertyBody(xProp);
                });
        }

        // Step 5: if still null, the candidates exist but are syntactically
        //         inaccessible (different file, no body, etc.)
        if (resolvedBody is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.UseMemberBodyIncompatible,
                member.GetLocation(),
                memberSymbol.Name,
                useMemberBody));
            return null;
        }

        return resolvedBody;
    }

    /// <summary>Returns true when a <see cref="PropertyDeclarationSyntax"/> has a readable body.</summary>
    private static bool HasReadablePropertyBody(PropertyDeclarationSyntax prop)
    {
        if (prop.ExpressionBody is not null)
        {
            return true;
        }

        var getter = prop.AccessorList?.Accessors
            .FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
        
        return getter?.ExpressionBody is not null || getter?.Body is not null;
    }

    /// <summary>Returns true when a symbol is a property returning <c>Expression&lt;TDelegate&gt;</c>.</summary>
    private static bool IsExpressionDelegateProperty(ISymbol sym) =>
        sym is IPropertySymbol p &&
        p.Type is INamedTypeSymbol { Name: "Expression", IsGenericType: true } et &&
        et.TypeArguments.Length == 1 &&
        et.ContainingNamespace?.ToDisplayString() == "System.Linq.Expressions";
}
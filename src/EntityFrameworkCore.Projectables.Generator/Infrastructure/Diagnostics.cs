using Microsoft.CodeAnalysis;

namespace EntityFrameworkCore.Projectables.Generator.Infrastructure;

static internal class Diagnostics
{
    public readonly static DiagnosticDescriptor BlockBodyExperimental = new DiagnosticDescriptor(
        id: "EFP0001",
        title: "Block-bodied member support is experimental",
        messageFormat: "Block-bodied member '{0}' is using an experimental feature. Set AllowBlockBody = true on the Projectable attribute to suppress this warning.",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor NullConditionalRewriteUnsupported = new DiagnosticDescriptor(
        id: "EFP0002",
        title: "Method or property is not configured to support null-conditional expressions",
        messageFormat: "'{0}' has a null-conditional expression exposed but is not configured to rewrite this (Consider configuring a strategy using the NullConditionalRewriteSupport property on the Projectable attribute)",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor UnsupportedStatementInBlockBody = new DiagnosticDescriptor(
        id: "EFP0003",
        title: "Unsupported statement in block-bodied method",
        messageFormat: "Method '{0}' contains an unsupported statement: {1}",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor SideEffectInBlockBody = new DiagnosticDescriptor(
        id: "EFP0004",
        title: "Statement with side effects in block-bodied method",
        messageFormat: "{0}",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor PotentialSideEffectInBlockBody = new DiagnosticDescriptor(
        id: "EFP0005",
        title: "Potential side effect in block-bodied method",
        messageFormat: "{0}",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor RequiresBodyDefinition = new DiagnosticDescriptor(
        id: "EFP0006",
        title: "Method or property should expose a body definition",
        messageFormat: "Method or property '{0}' should expose a body definition (e.g. an expression-bodied member or a block-bodied method) to be used as the source for the generated expression tree.",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor UnsupportedPatternInExpression = new DiagnosticDescriptor(
        id: "EFP0007",
        title: "Unsupported pattern in projectable expression",
        messageFormat: "The pattern '{0}' cannot be rewritten into an expression tree. Simplify the pattern or restructure the projectable member body.",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor MissingParameterlessConstructor = new DiagnosticDescriptor(
        id: "EFP0008",
        title: "Target class is missing a parameterless constructor",
        messageFormat: "Class '{0}' must have a parameterless constructor to be used with a [Projectable] constructor. The generated projection uses 'new {0}() {{ ... }}' (object-initializer syntax), which requires an accessible parameterless constructor.",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor NoSourceAvailableForDelegatedConstructor = new DiagnosticDescriptor(
        id: "EFP0009",
        title: "Delegated constructor cannot be analyzed for projection",
        messageFormat: "The delegated constructor '{0}' in type '{1}' has no source available and cannot be analyzed. Base/this initializer in member '{2}' will not be projected.",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor UseMemberBodyNotFound = new DiagnosticDescriptor(
        id: "EFP0010",
        title: "UseMemberBody target member not found",
        messageFormat: "Member '{1}' referenced by UseMemberBody on '{0}' was not found on type '{2}'",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor UseMemberBodyIncompatible = new DiagnosticDescriptor(
        id: "EFP0011",
        title: "UseMemberBody target member is incompatible",
        messageFormat: "Member '{1}' referenced by UseMemberBody on '{0}' has an incompatible type or signature",
        category: "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor FactoryMethodShouldBeConstructor = new DiagnosticDescriptor(
        id: "EFP0012",
        title: "[Projectable] factory method can be converted to a constructor",
        messageFormat: "Factory method '{0}' creates and returns an instance of the containing class via object initializer. Consider converting it to a [Projectable] constructor.",
        category: "Design",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public readonly static DiagnosticDescriptor ContainingClassShouldBePartial = new DiagnosticDescriptor(
        id: "EFP0013",
        title: "Containing class should be partial",
        messageFormat: "Class '{0}' should be declared as 'partial' to allow [Projectable] to generate the expression accessor inside the class, enabling access to private and protected members",
        category: "Design",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}

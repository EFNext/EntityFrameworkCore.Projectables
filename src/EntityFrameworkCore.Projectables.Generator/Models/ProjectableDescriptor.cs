using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.Models;

internal class ProjectableDescriptor
{
    public IEnumerable<UsingDirectiveSyntax>? UsingDirectives { get; set; }

    public string? ClassNamespace { get; set; }

    public IEnumerable<string>? NestedInClassNames { get; set; }

    public string? TargetClassNamespace { get; set; }

    public IEnumerable<string>? TargetNestedInClassNames { get; set; }

    public string? ClassName { get; set; }

    public TypeParameterListSyntax? ClassTypeParameterList { get; set; }

    public SyntaxList<TypeParameterConstraintClauseSyntax>? ClassConstraintClauses { get; set; }

    public string? MemberName { get; set; }

    public string? ReturnTypeName { get; set; }

    public ParameterListSyntax? ParametersList { get; set; }

    public IEnumerable<string>? ParameterTypeNames { get; set; }

    public TypeParameterListSyntax? TypeParameterList { get; set; }
        
    public SyntaxList<TypeParameterConstraintClauseSyntax>? ConstraintClauses { get; set; }

    public ExpressionSyntax? ExpressionBody { get; set; }
}
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.CodeFixes;

static internal class ProjectableCodeFixHelper
{
    private const string ProjectableAttributeName = "Projectable";
    private const string ProjectableAttributeFullName = "ProjectableAttribute";

    static internal bool TryFindProjectableAttribute(MemberDeclarationSyntax member, out AttributeSyntax? attribute)
    {
        attribute = member.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a =>
            {
                var name = a.Name.ToString();
                return name == ProjectableAttributeName || name == ProjectableAttributeFullName;
            });

        return attribute is not null;
    }
    
    static internal bool TryGetFixableFactoryMethodPattern(
        SyntaxNode methodNode,
        out TypeDeclarationSyntax? containingType,
        out MethodDeclarationSyntax? method)
    {
        containingType = null;
        method = null;
        
        var localMethod = methodNode.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (localMethod is null)
        {
            return false;
        }

        if (!SyntaxHelpers.TryGetFactoryMethodPattern(localMethod, out containingType))
        {
            return false;
        }

        method = localMethod;

        return TryFindProjectableAttribute(localMethod, out _);
    }

    /// <summary>
    /// Adds or replaces a named argument on the [Projectable] attribute of <paramref name="member"/>.
    /// If the attribute already has the argument, it is replaced; otherwise it is appended.
    /// </summary>
    async static internal Task<Document> AddOrReplaceNamedArgumentInProjectableAttributeAsync(
        Document document,
        MemberDeclarationSyntax member,
        string argumentName,
        ExpressionSyntax argumentValue,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (!TryFindProjectableAttribute(member, out var attribute) || attribute is null)
        {
            return document;
        }

        var newArgument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.NameEquals(SyntaxFactory.IdentifierName(argumentName)),
            null,
            argumentValue);

        AttributeSyntax newAttribute;

        if (attribute.ArgumentList is null)
        {
            newAttribute = attribute.WithArgumentList(
                SyntaxFactory.AttributeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(newArgument)));
        }
        else
        {
            var existingArg = attribute.ArgumentList.Arguments
                .FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == argumentName);

            newAttribute = existingArg is not null
                ? attribute.WithArgumentList(
                    attribute.ArgumentList.ReplaceNode(existingArg, newArgument))
                : attribute.WithArgumentList(
                    attribute.ArgumentList.AddArguments(newArgument));
        }

        var newRoot = root.ReplaceNode(attribute, newAttribute);
        return document.WithSyntaxRoot(newRoot);
    }
}


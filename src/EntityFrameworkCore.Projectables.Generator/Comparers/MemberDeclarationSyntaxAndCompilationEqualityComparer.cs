using System.Runtime.CompilerServices;
using EntityFrameworkCore.Projectables.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.Comparers;

/// <summary>
/// Equality comparer for tuples of (MemberDeclarationSyntax, ProjectableAttributeData) and Compilation,
/// used as keys in the registry to determine if a member's projectable status has changed across incremental generation steps.
/// </summary>
internal class MemberDeclarationSyntaxAndCompilationEqualityComparer
    : IEqualityComparer<((MemberDeclarationSyntax Member, ProjectableAttributeData Attribute), Compilation)>
{
    private readonly static MemberDeclarationSyntaxEqualityComparer _memberComparer = new();

    public bool Equals(
        ((MemberDeclarationSyntax Member, ProjectableAttributeData Attribute), Compilation) x,
        ((MemberDeclarationSyntax Member, ProjectableAttributeData Attribute), Compilation) y)
    {
        var (xLeft, xCompilation) = x;
        var (yLeft, yCompilation) = y;

        // 1. Fast reference equality short-circuit
        if (ReferenceEquals(xLeft.Member, yLeft.Member) &&
            ReferenceEquals(xCompilation, yCompilation))
        {
            return true;
        }

        // 2. The syntax tree of the member's own file must be the same object
        //    (Roslyn reuses SyntaxTree instances for unchanged files, even when
        //    the Compilation object itself is new due to edits elsewhere)
        //    Single pointer comparison — very cheap.
        if (!ReferenceEquals(xLeft.Member.SyntaxTree, yLeft.Member.SyntaxTree))
        {
            return false;
        }

        // 3. Attribute arguments (primitive record struct) — cheap value comparison
        if (xLeft.Attribute != yLeft.Attribute)
        {
            return false;
        }

        // 4. Member text — string allocation, only reached when the SyntaxTree is shared
        if (!_memberComparer.Equals(xLeft.Member, yLeft.Member))
        {
            return false;
        }

        // 5. Assembly-level references — most expensive (ImmutableArray enumeration)
        return xCompilation.ExternalReferences.SequenceEqual(yCompilation.ExternalReferences);
    }

    public int GetHashCode(((MemberDeclarationSyntax Member, ProjectableAttributeData Attribute), Compilation) obj)
    {
        var (left, compilation) = obj;
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + _memberComparer.GetHashCode(left.Member);
            hash = hash * 31 + RuntimeHelpers.GetHashCode(left.Member.SyntaxTree);
            hash = hash * 31 + left.Attribute.GetHashCode();
            
            // Incorporate compilation external references to align with Equals
            var references = compilation.ExternalReferences;
            var referencesHash = 17;
            referencesHash = referencesHash * 31 + references.Length;
            foreach (var reference in references)
            {
                referencesHash = referencesHash * 31 + RuntimeHelpers.GetHashCode(reference);
            }
            hash = hash * 31 + referencesHash;
            
            return hash;
        }
    }
}

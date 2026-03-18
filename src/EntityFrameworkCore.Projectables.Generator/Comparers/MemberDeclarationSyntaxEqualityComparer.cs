using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EntityFrameworkCore.Projectables.Generator.Comparers;

/// <summary>
/// Equality comparer for MemberDeclarationSyntax nodes that treats nodes as equal if they are from the same syntax
/// tree and have the same structure, even if they are different instances.
/// </summary>
internal class MemberDeclarationSyntaxEqualityComparer : IEqualityComparer<MemberDeclarationSyntax>
{
    public bool Equals(MemberDeclarationSyntax x, MemberDeclarationSyntax y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        // Must be in the same file — if the syntax tree changed, treat as different
        // (Roslyn reuses SyntaxTree objects for unchanged files, so a new SyntaxTree
        // means the file was edited, even if this specific node text looks the same)
        if (!ReferenceEquals(x.SyntaxTree, y.SyntaxTree))
        {
            return false;
        }

        // Pré-filtres O(1) avant IsEquivalentTo
        if (x.RawKind != y.RawKind)
        {
            return false;
        }

        if (x.FullSpan.Length != y.FullSpan.Length)
        {
            return false;
        }

        // Comparaison structurelle Roslyn — pas d'allocation de string
        return x.IsEquivalentTo(y);
    }

    public int GetHashCode(MemberDeclarationSyntax obj)
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + RuntimeHelpers.GetHashCode(obj.SyntaxTree);
            hash = hash * 31 + obj.RawKind;
            hash = hash * 31 + obj.FullSpan.Length;
            return hash;
        }
    }
}
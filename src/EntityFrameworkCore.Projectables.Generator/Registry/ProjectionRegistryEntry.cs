using System.Collections.Immutable;

namespace EntityFrameworkCore.Projectables.Generator.Registry;

/// <summary>
/// Incremental-pipeline-safe representation of a single projectable member.
/// Contains only primitive types and an equatable wrapper around <see cref="ImmutableArray{T}"/>
/// so that structural value equality works correctly across incremental generation steps.
/// </summary>
sealed internal record ProjectionRegistryEntry(
    string DeclaringTypeFullName,
    ProjectionRegistryMemberType MemberKind,
    string MemberLookupName,
    string GeneratedClassFullName,
    EquatableImmutableArray ParameterTypeNames
);

/// <summary>
/// A structural-equality wrapper around <see cref="ImmutableArray{T}"/> of strings.
/// <see cref="ImmutableArray{T}"/> uses reference equality by default, which breaks
/// Roslyn's incremental-source-generator caching when the same logical array is
/// produced by two different steps. This wrapper provides element-wise equality so
/// that incremental steps are correctly cached and skipped.
/// </summary>
readonly internal struct EquatableImmutableArray(ImmutableArray<string> array) : IEquatable<EquatableImmutableArray>
{
    private readonly ImmutableArray<string> _array = array;

    public bool Equals(EquatableImmutableArray other) => _array.SequenceEqual(other._array);

    public override bool Equals(object? obj) => obj is EquatableImmutableArray other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var s in _array)
            {
                hash = hash * 31 + (s?.GetHashCode() ?? 0);
            }

            return hash;
        }
    }

    public static implicit operator ImmutableArray<string>(EquatableImmutableArray e) => e._array;
    public static implicit operator EquatableImmutableArray(ImmutableArray<string> a) => new(a);
}
namespace EntityFrameworkCore.Projectables.Generator.Infrastructure;

/// <summary>
/// Builds the hint name (the file name shown in the IDE) for a generated per-member source file.
/// <para>
/// The generated class name embeds the fully-qualified type name of every parameter, so for methods
/// with many or deeply generic parameters it grows to hundreds of characters. Using that string
/// directly as the file name produces paths that overflow limits such as Windows' <c>MAX_PATH</c>
/// (~260) — the generator sub-folder prefix alone consumes ~200 characters — which crashes Visual
/// Studio when browsing the files under <c>Dependencies &gt; Analyzers</c>.
/// </para>
/// <para>
/// The hint name is a pure IDE/filesystem artifact and is never read at runtime, so it can be shortened
/// freely. This is distinct from the generated <b>class name</b>, which is a runtime contract (resolved
/// by <c>Assembly.GetType</c> in the reflection fallback and embedded in the registry) and must never
/// change here. Short names are returned unchanged; over-long names are rewritten to a readable prefix
/// plus a deterministic hash of the full name so the result stays unique per member/overload.
/// </para>
/// </summary>
internal static class GeneratedHintName
{
    /// <summary>
    /// Names whose base is at most this long are emitted unchanged, keeping the common case fully
    /// readable and byte-identical to previous versions.
    /// </summary>
    private const int MaxBaseNameLength = 64;

    /// <summary>
    /// Upper bound on the readable head kept in the shortened form before the hash is appended.
    /// </summary>
    private const int MaxReadablePrefixLength = 40;

    /// <summary>
    /// Builds the <c>.g.cs</c> hint name for a generated member.
    /// </summary>
    /// <param name="baseName">
    /// The name emitted before the <c>.g.cs</c> suffix today (the generated class name, optionally with
    /// the open-generic <c>-{typeParamCount}</c> disambiguator). This is the uniqueness carrier: the hash
    /// is computed over its entirety so distinct members can never collide.
    /// </param>
    /// <param name="readablePrefix">
    /// The parameter-less identity head (namespace + nested classes + member name). It is a literal prefix
    /// of <paramref name="baseName"/> and is kept, truncated if necessary, for human readability.
    /// </param>
    public static string Build(string baseName, string readablePrefix)
    {
        if (baseName.Length <= MaxBaseNameLength)
        {
            return baseName + ".g.cs";
        }

        var prefix = readablePrefix.Length <= MaxReadablePrefixLength
            ? readablePrefix
            : readablePrefix.Substring(0, MaxReadablePrefixLength);

        return prefix + "_" + Fnv1a64Hex(baseName) + ".g.cs";
    }

    /// <summary>
    /// Computes a stable, deterministic 64-bit FNV-1a hash of <paramref name="value"/> and formats it as
    /// 16 lowercase hex characters.
    /// <para>
    /// A hand-rolled hash is required because the name must be identical across builds, machines and
    /// target frameworks (<c>string.GetHashCode()</c> is randomized per process on modern .NET, and
    /// <c>System.HashCode</c> is unavailable on netstandard2.0).
    /// </para>
    /// </summary>
    private static string Fnv1a64Hex(string value)
    {
        unchecked
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            var hash = offsetBasis;
            foreach (var ch in value)
            {
                // Fold both bytes of each char so the hash is well-defined for any input, not just ASCII.
                hash = (hash ^ (byte)ch) * prime;
                hash = (hash ^ (byte)(ch >> 8)) * prime;
            }

            return hash.ToString("x16");
        }
    }
}

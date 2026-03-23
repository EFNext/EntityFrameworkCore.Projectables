using Microsoft.CodeAnalysis.Diagnostics;

namespace EntityFrameworkCore.Projectables.Generator.Models;

/// <summary>
/// Plain-data snapshot of the MSBuild global defaults for [Projectable] options.
/// Read from <c>build_property.*</c> entries in <c>AnalyzerConfigOptions.GlobalOptions</c>.
/// <c>null</c> means the property was not set (no global override).
/// </summary>
readonly internal record struct ProjectableGlobalOptions
{
    public NullConditionalRewriteSupport? NullConditionalRewriteSupport { get; }
    public bool? ExpandEnumMethods { get; }
    public bool? AllowBlockBody { get; }

    public ProjectableGlobalOptions(AnalyzerConfigOptions globalOptions)
    {
        if (globalOptions.TryGetValue("build_property.Projectables_NullConditionalRewriteSupport", out var nullConditionalStr)
            && !string.IsNullOrEmpty(nullConditionalStr)
            && Enum.TryParse<NullConditionalRewriteSupport>(nullConditionalStr, ignoreCase: true, out var nullConditional))
        {
            NullConditionalRewriteSupport = nullConditional;
        }

        if (globalOptions.TryGetValue("build_property.Projectables_ExpandEnumMethods", out var expandStr)
            && bool.TryParse(expandStr, out var expand))
        {
            ExpandEnumMethods = expand;
        }

        if (globalOptions.TryGetValue("build_property.Projectables_AllowBlockBody", out var allowStr)
            && bool.TryParse(allowStr, out var allow))
        {
            AllowBlockBody = allow;
        }
    }
}

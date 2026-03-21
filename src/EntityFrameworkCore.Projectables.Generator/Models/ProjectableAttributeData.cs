using Microsoft.CodeAnalysis;

namespace EntityFrameworkCore.Projectables.Generator.Models;

/// <summary>
/// Plain-data snapshot of the [Projectable] attribute arguments.
/// Nullable option fields are <c>null</c> when the named argument was absent from the attribute,
/// meaning the global MSBuild default (or hard-coded fallback) should be used instead.
/// </summary>
readonly internal record struct ProjectableAttributeData
{
    public NullConditionalRewriteSupport? NullConditionalRewriteSupport { get; }
    public string? UseMemberBody { get; }
    public bool? ExpandEnumMethods { get; }
    public bool? AllowBlockBody { get; }

    public ProjectableAttributeData(AttributeData attribute)
    {
        NullConditionalRewriteSupport? nullConditionalRewriteSupport = null;
        string? useMemberBody = null;
        bool? expandEnumMethods = null;
        bool? allowBlockBody = null;

        foreach (var namedArgument in attribute.NamedArguments)
        {
            var key = namedArgument.Key;
            var value = namedArgument.Value;
            switch (key)
            {
                case nameof(NullConditionalRewriteSupport):
                    if (value.Kind == TypedConstantKind.Enum &&
                        value.Value is not null &&
                        Enum.IsDefined(typeof(NullConditionalRewriteSupport), value.Value))
                    {
                        nullConditionalRewriteSupport = (NullConditionalRewriteSupport)value.Value;
                    }
                    break;
                case nameof(UseMemberBody):
                    if (value.Value is string s)
                    {
                        useMemberBody = s;
                    }
                    break;
                case nameof(ExpandEnumMethods):
                    if (value.Value is bool expand)
                    {
                        expandEnumMethods = expand;
                    }
                    break;
                case nameof(AllowBlockBody):
                    if (value.Value is bool allow)
                    {
                        allowBlockBody = allow;
                    }
                    break;
            }
        }

        NullConditionalRewriteSupport = nullConditionalRewriteSupport;
        UseMemberBody = useMemberBody;
        ExpandEnumMethods = expandEnumMethods;
        AllowBlockBody = allowBlockBody;
    }
}

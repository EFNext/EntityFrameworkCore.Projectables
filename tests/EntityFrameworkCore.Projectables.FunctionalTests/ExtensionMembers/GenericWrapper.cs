#if NET10_0_OR_GREATER
namespace EntityFrameworkCore.Projectables.FunctionalTests.ExtensionMembers
{
    public sealed class GenericWrapper<T>
    {
        public required T Wrapped { get; set; }
    }
}
#endif

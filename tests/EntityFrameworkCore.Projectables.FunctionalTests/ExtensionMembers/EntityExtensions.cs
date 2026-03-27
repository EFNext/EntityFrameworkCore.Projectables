#if NET10_0_OR_GREATER
namespace EntityFrameworkCore.Projectables.FunctionalTests.ExtensionMembers
{
    public static class EntityExtensions
    {
        extension(Entity e)
        {
            /// <summary>
            /// Extension member property that doubles the entity's ID.
            /// </summary>
            [Projectable]
            public int DoubleId => e.Id * 2;

            /// <summary>
            /// Extension member method that triples the entity's ID.
            /// </summary>
            [Projectable]
            public int TripleId() => e.Id * 3;

            /// <summary>
            /// Extension member method that multiplies the entity's ID by a factor.
            /// </summary>
            [Projectable]
            public int Multiply(int factor) => e.Id * factor;
        }

        extension (GenericWrapper<Entity> w)
        {
            /// <summary>
            /// Extension member method that multiplies the wrapped entity's ID by a factor.
            /// </summary>
            [Projectable]
            public int MultiplyWrapped(int factor) => w.Wrapped.Id * factor;
        }
    }

    public static class IntExtensions
    {
        extension(int i)
        {
            /// <summary>
            /// Extension member property that squares an integer.
            /// </summary>
            [Projectable]
            public int SquaredMember => i * i;
        }
    }
}
#endif

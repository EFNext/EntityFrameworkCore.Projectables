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
    }

    public static class GenericWrapperExtensions
    {
        extension(GenericWrapper<Entity> w)
        {
            /// <summary>
            /// Extension member on a closed generic receiver type — exercises the
            /// code path where <c>global::</c> appears inside generic type arguments
            /// in the fully-qualified receiver type name.
            /// </summary>
            [Projectable]
            public int DoubleId() => w.Id * 2;
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

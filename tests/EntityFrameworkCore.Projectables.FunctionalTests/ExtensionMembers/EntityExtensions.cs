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

    /// <summary>
    /// Extension on a closed generic receiver type: <c>extension(GenericWrapper&lt;Entity&gt; w)</c>.
    /// Tests the fix for the bug where <c>global::</c> inside generic type arguments caused a
    /// name mismatch between the generated class and the runtime resolver.
    /// </summary>
    public static class ClosedGenericWrapperExtensions
    {
        extension(GenericWrapper<Entity> w)
        {
            [Projectable]
            public int DoubleId() => w.Id * 2;
        }
    }

    /// <summary>
    /// Extension on an open generic receiver type: <c>extension&lt;T&gt;(GenericWrapper&lt;T&gt; w)</c>.
    /// The block-level type parameter <c>T</c> becomes a method-level type parameter on the
    /// generated <c>Expression&lt;T&gt;()</c> factory, resolved at runtime via generic method
    /// reflection.
    /// </summary>
    public static class OpenGenericWrapperExtensions
    {
        extension<T>(GenericWrapper<T> w) where T : class
        {
            [Projectable]
            public int TripleId() => w.Id * 3;
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

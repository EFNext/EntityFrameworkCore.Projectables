using System.Linq.Expressions;
using System.Reflection;
using EntityFrameworkCore.Projectables.Services;

// Hand-crafted generated classes that mimic what the source generator would emit for
// Contact<T>.GetDisplayNameByType and GetDisplayNameWithComplementByType.
// They live in this assembly so Assembly.GetType() can locate them during testing.
//
// Class names are computed by ProjectionExpressionClassNameGenerator.GenerateFullName:
//   namespace  = "EntityFrameworkCore.Projectables.Tests.Services.GenericResolution"
//   class path = ["Contact`1"]  →  "Contact" + arity suffix `1
//   member     = <method name>
//   param[0]   = "T"  (generic type parameter — correct after the fix)
// Resulting CLR names end in _Contact_GetDisplayNameByType_P0_T`1 / ...WithComplement...
namespace EntityFrameworkCore.Projectables.Generated
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EntityFrameworkCore.Projectables.Tests.Services.GenericResolution;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes (accessed via reflection)

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    static class EntityFrameworkCore_Projectables_Tests_Services_GenericResolution_Contact_GetDisplayNameByType_P0_T<T>
        where T : struct, Enum
    {
#pragma warning disable IDE0051 // Private member unused (invoked via reflection)
        static Expression<Func<Contact<T>, T, string?>> Expression()
            => (@this, type) => @this.Addresses
                .Where(a => a.Type.Equals(type))
                .Select(a => a.Address.DisplayName)
                .FirstOrDefault();
#pragma warning restore IDE0051
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    static class EntityFrameworkCore_Projectables_Tests_Services_GenericResolution_Contact_GetDisplayNameWithComplementByType_P0_T<T>
        where T : struct, Enum
    {
#pragma warning disable IDE0051 // Private member unused (invoked via reflection)
        static Expression<Func<Contact<T>, T, string?>> Expression()
            => (@this, type) => @this.Addresses
                .Where(a => a.Type.Equals(type))
                .Select(a => a.Address.DisplayNameWithComplement)
                .FirstOrDefault();
#pragma warning restore IDE0051
    }

#pragma warning restore CA1812
}

namespace EntityFrameworkCore.Projectables.Tests.Services.GenericResolution
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EntityFrameworkCore.Projectables;

    // ── Test entities ─────────────────────────────────────────────────────────

    public enum AddressKind { Home, Work }

    public class AddressInfo
    {
        public string? DisplayName { get; init; }
        public string? DisplayNameWithComplement { get; init; }
    }

    public class ContactAddress<T> where T : struct, Enum
    {
        public T Type { get; init; }
        public AddressInfo Address { get; init; } = new();
    }

    /// <summary>
    /// A generic class whose <c>[Projectable]</c> methods reproduce the reported runtime
    /// resolution failure: when the resolver was called with a <see cref="MethodInfo"/> from a
    /// <em>closed</em> generic type (e.g. <c>Contact&lt;AddressKind&gt;</c>), it read the
    /// concrete parameter type (<c>AddressKind</c>) instead of the generic type parameter
    /// (<c>T</c>), producing a wrong generated-class name and failing to find the expression.
    /// </summary>
    public class Contact<T> where T : struct, Enum
    {
        public IList<ContactAddress<T>> Addresses { get; init; } = new List<ContactAddress<T>>();

        [Projectable]
        public string? GetDisplayNameByType(T type) =>
            Addresses.Where(a => a.Type.Equals(type)).Select(a => a.Address.DisplayName).FirstOrDefault();

        [Projectable]
        public string? GetDisplayNameWithComplementByType(T type) =>
            Addresses.Where(a => a.Type.Equals(type)).Select(a => a.Address.DisplayNameWithComplement).FirstOrDefault();
    }
}

namespace EntityFrameworkCore.Projectables.Tests.Services
{
    using System;
    using System.Reflection;
    using EntityFrameworkCore.Projectables.Tests.Services.GenericResolution;
    using Xunit;

    /// <summary>
    /// Regression tests for the runtime resolution of <c>[Projectable]</c> methods
    /// declared on a generic class when the <see cref="MethodInfo"/> is obtained from a
    /// <em>closed</em> generic type instantiation.
    /// </summary>
    public class ProjectionExpressionResolverGenericTypeTests
    {
        // ── Helper ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the <see cref="MethodInfo"/> for <paramref name="methodName"/> as it
        /// appears on the <em>closed</em> generic type <c>Contact&lt;AddressKind&gt;</c>.
        /// This is the same object that EF Core's expression tree captures when a lambda
        /// such as <c>c => c.GetDisplayNameByType(kind)</c> is compiled.
        /// </summary>
        private static MethodInfo GetClosedMethod(string methodName)
        {
            var closedType = typeof(Contact<AddressKind>);
            return closedType.GetMethod(methodName)
                ?? throw new InvalidOperationException($"Method '{methodName}' not found on {closedType}.");
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Verifies that <c>GetDisplayNameByType</c> on the closed generic type
        /// <c>Contact&lt;AddressKind&gt;</c> is resolved by the reflection-based path.
        /// </summary>
        [Fact]
        public void GetDisplayNameByType_OnClosedGenericType_IsResolved()
        {
            var method = GetClosedMethod(nameof(Contact<AddressKind>.GetDisplayNameByType));

            var result = ProjectionExpressionResolver.FindGeneratedExpressionViaReflection(method);

            Assert.NotNull(result);
        }

        /// <summary>
        /// Verifies that <c>GetDisplayNameWithComplementByType</c> on the closed generic type
        /// <c>Contact&lt;AddressKind&gt;</c> is resolved by the reflection-based path.
        /// This was the method specifically reported as "not resolved" in the original bug.
        /// </summary>
        [Fact]
        public void GetDisplayNameWithComplementByType_OnClosedGenericType_IsResolved()
        {
            var method = GetClosedMethod(nameof(Contact<AddressKind>.GetDisplayNameWithComplementByType));

            var result = ProjectionExpressionResolver.FindGeneratedExpressionViaReflection(method);

            Assert.NotNull(result);
        }

        /// <summary>
        /// Verifies that the resolved expression for <c>GetDisplayNameByType</c> has the
        /// correct parameter types after closing the generic type with <c>AddressKind</c>.
        /// </summary>
        [Fact]
        public void GetDisplayNameByType_ResolvedExpression_HasCorrectSignature()
        {
            var method = GetClosedMethod(nameof(Contact<AddressKind>.GetDisplayNameByType));

            var expression = ProjectionExpressionResolver.FindGeneratedExpressionViaReflection(method);

            Assert.NotNull(expression);
            // Lambda must accept (Contact<AddressKind> @this, AddressKind type)
            Assert.Equal(2, expression.Parameters.Count);
            Assert.Equal(typeof(Contact<AddressKind>), expression.Parameters[0].Type);
            Assert.Equal(typeof(AddressKind), expression.Parameters[1].Type);
        }

        /// <summary>
        /// Verifies that the resolved expression for <c>GetDisplayNameWithComplementByType</c>
        /// has the correct parameter types after closing the generic type with <c>AddressKind</c>.
        /// </summary>
        [Fact]
        public void GetDisplayNameWithComplementByType_ResolvedExpression_HasCorrectSignature()
        {
            var method = GetClosedMethod(nameof(Contact<AddressKind>.GetDisplayNameWithComplementByType));

            var expression = ProjectionExpressionResolver.FindGeneratedExpressionViaReflection(method);

            Assert.NotNull(expression);
            Assert.Equal(2, expression.Parameters.Count);
            Assert.Equal(typeof(Contact<AddressKind>), expression.Parameters[0].Type);
            Assert.Equal(typeof(AddressKind), expression.Parameters[1].Type);
        }
    }
}

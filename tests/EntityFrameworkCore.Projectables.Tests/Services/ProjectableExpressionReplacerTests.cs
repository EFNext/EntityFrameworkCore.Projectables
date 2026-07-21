using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.Services;
using Xunit;

namespace EntityFrameworkCore.Projectables.Tests.Services
{
    public class ProjectableExpressionReplacerTests
    {
        public class ProjectableExpressionResolverStub : IProjectionExpressionResolver
        {
            readonly Func<MemberInfo, ProjectableAttribute?, LambdaExpression> _implementation;

            public ProjectableExpressionResolverStub(Func<MemberInfo, ProjectableAttribute?, LambdaExpression> implementation)
            {
                _implementation = implementation;
            }

            public LambdaExpression FindGeneratedExpression(MemberInfo projectableMemberInfo,
                ProjectableAttribute? projectableAttribute = null) => _implementation(projectableMemberInfo, projectableAttribute);
        }

        class Entity
        {
            public int Id { get; set; }

            [Projectable]
            public int SimpleProperty => 0;

            [Projectable]
            public int SimpleMethod() => 0;

            [Projectable]
            public int SimpleMethodWithArguments(int a, object b) => 0;

            [Projectable]
            public int SimpleStatefullProperty => Id;

            [Projectable]
            public int SimpleStatefullMethod() => Id;

            [Projectable]
            public static int SimpleStaticMethod() => 0;

            [Projectable]
            public static int SimpleStaticMethodWithArguments(int a, Entity b) => 0;
        }

        [Fact]
        public void VisitMember_SimpleProperty()
        {
            Expression<Func<Entity, int>> input = x => x.SimpleProperty;
            Expression<Func<Entity, int>> expected = x => 0;

            var resolver = new ProjectableExpressionResolverStub(
                (x, a) => expected
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_SimpleMethod()
        {
            Expression<Func<Entity, int>> input = x => x.SimpleMethod();
            Expression<Func<Entity, int>> expected = x => 0;

            var resolver = new ProjectableExpressionResolverStub(
                (x, a) => expected
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_SimpleMethodWithArguments()
        {
            Expression<Func<Entity, int>> input = x => x.SimpleMethodWithArguments(1, true);
            Expression<Func<Entity, int>> expected = x => 0;

            var resolver = new ProjectableExpressionResolverStub(
                (x, a) => expected
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_SimpleStatefullProperty()
        {
            Expression<Func<Entity, int>> input = x => x.SimpleStatefullProperty;
            Expression<Func<Entity, int>> expected = x => x.Id;

            var resolver = new ProjectableExpressionResolverStub(
                (x, a) => expected
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_SimpleStatefullMethod()
        {
            Expression<Func<Entity, int>> input = x => x.SimpleStatefullMethod();
            Expression<Func<Entity, int>> expected = x => x.Id;

            var resolver = new ProjectableExpressionResolverStub(
                (x, a) => expected
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_SimpleStaticMethod()
        {
            Expression<Func<Entity, int>> input = x => Entity.SimpleStaticMethod();
            Expression<Func<Entity, int>> expected = x => 0;

            var resolver = new ProjectableExpressionResolverStub(
                (x, a) => expected
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_SimpleStaticMethodWithArguments()
        {
            Expression<Func<Entity, int>> input = x => Entity.SimpleStaticMethodWithArguments(0, x);
            Expression<Func<Entity, int>> expected = x => 0;

            var resolver = new ProjectableExpressionResolverStub(
                (x, a) => expected
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        /// <summary>
        /// Exercises the <c>PropertyInfo prop =&gt; prop.GetValue(...)</c> branch inside the
        /// closure-inlining guard of <c>VisitMember</c>.
        ///
        /// Standard C# compiler-generated closures always use <em>fields</em>, making the
        /// <c>PropertyInfo</c> arm unreachable from ordinary lambdas.  This test constructs
        /// the expression tree manually — using a nested private <c>[CompilerGenerated]</c>
        /// class whose member is a property — to ensure the branch is executed without
        /// throwing and falls through correctly when no active <see cref="IQueryProvider"/>
        /// is set (i.e., no inlining occurs, the original expression is returned unchanged).
        /// </summary>
        [Fact]
        public void VisitMember_CompilerGeneratedClosure_PropertyInfoBranch_FallsThroughWithoutInlining()
        {
            var closure = new FakeClosureWithIQueryableProperty
            {
                Items = new[] { new Entity { Id = 1 } }.AsQueryable()
            };

            var closureConst = Expression.Constant(closure);
            var propertyInfo = typeof(FakeClosureWithIQueryableProperty)
                .GetProperty(nameof(FakeClosureWithIQueryableProperty.Items))!;
            var memberAccess = Expression.MakeMemberAccess(closureConst, propertyInfo);

            var resolver = new ProjectableExpressionResolverStub(
                (x, a) => throw new InvalidOperationException("Resolver should not be called for non-projectable members.")
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            // The replacer must not throw. Since there is no active IQueryProvider (no EF
            // query root has been visited), the provider check fails and the expression is
            // returned unchanged.
            var actual = subject.Replace(memberAccess);

            Assert.Same(memberAccess, actual);
        }

        // Simulates a compiler-generated closure whose member is a *property* (not a field).
        // Real C# closures always generate fields; this class is only used to exercise the
        // defensive PropertyInfo branch in ProjectableExpressionReplacer.VisitMember.
        [CompilerGenerated]
        private sealed class FakeClosureWithIQueryableProperty
        {
            public IQueryable<Entity>? Items { get; set; }
        }

        public class ProjectableExpressionResolverStubBase : IProjectionExpressionResolver
        {
            readonly Func<MemberInfo, ProjectableAttribute?, LambdaExpression> _implementation;

            public ProjectableExpressionResolverStubBase(Func<MemberInfo, ProjectableAttribute?, LambdaExpression> implementation)
            {
                _implementation = implementation;
            }

            public LambdaExpression FindGeneratedExpression(MemberInfo projectableMemberInfo,
                ProjectableAttribute? projectableAttribute = null) => _implementation(projectableMemberInfo, projectableAttribute);
        }

        class Foo
        {
            [Projectable(PolymorphicDispatch = true)]
            public virtual int VirtualProperty => 1;

            [Projectable(PolymorphicDispatch = true)]
            public virtual int VirtualMethod() => 1;
        }

        class Bar : Foo
        {
            [Projectable]
            override public int VirtualProperty => 2;

            [Projectable]
            override public int VirtualMethod() => 2;
        }

        [Fact]
        public void VisitMember_PolymorphicProperty()
        {
            Expression<Func<Foo, int>> input = x => x.VirtualProperty;
            Expression<Func<Foo, int>> expectedFoo = x => 1;
            Expression<Func<Bar, int>> expectedBar = x => 2;
            Expression<Func<Foo, int>> expected = x => x is Bar ? 2 : 1;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo) ? expectedFoo : expectedBar
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_PolymorphicMethod()
        {
            Expression<Func<Foo, int>> input = x => x.VirtualMethod();
            Expression<Func<Foo, int>> expectedFoo = x => 1;
            Expression<Func<Bar, int>> expectedBar = x => 2;
            Expression<Func<Foo, int>> expected = x => x is Bar ? 2 : 1;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo) ? expectedFoo : expectedBar
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        class Foo1
        {
            [Projectable(PolymorphicDispatch = true)]
            public virtual int VirtualProperty => 1;

            [Projectable(PolymorphicDispatch = true)]
            public virtual int VirtualMethod() => 1;
        }

        class Bar1 : Foo1
        {
            [Projectable]
            override public int VirtualProperty => 2;

            [Projectable]
            override public int VirtualMethod() => 2;
        }

        class Baz1 : Foo1
        {
            [Projectable]
            override public int VirtualProperty => 3;

            [Projectable]
            override public int VirtualMethod() => 3;
        }

        [Fact]
        public void VisitMember_PolymorphicPropertyMultiple()
        {
            Expression<Func<Foo1, int>> input = x => x.VirtualProperty;
            Expression<Func<Foo1, int>> expectedFoo = x => 1;
            Expression<Func<Bar1, int>> expectedBar = x => 2;
            Expression<Func<Baz1, int>> expectedBaz = x => 3;
            Expression<Func<Foo1, int>> expected = x => x is Bar1 ? 2 : x is Baz1 ? 3 : 1;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo1) ? expectedFoo : (x.DeclaringType == typeof(Bar1) ? expectedBar : expectedBaz)
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_PolymorphicMethodMultiple()
        {
            Expression<Func<Foo1, int>> input = x => x.VirtualMethod();
            Expression<Func<Foo1, int>> expectedFoo = x => 1;
            Expression<Func<Bar1, int>> expectedBar = x => 2;
            Expression<Func<Baz1, int>> expectedBaz = x => 3;
            Expression<Func<Foo1, int>> expected = x => x is Bar1 ? 2 : x is Baz1 ? 3 : 1;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo1) ? expectedFoo : (x.DeclaringType == typeof(Bar1) ? expectedBar : expectedBaz)
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        class Foo2
        {
            [Projectable(PolymorphicDispatch = true)]
            public virtual int VirtualProperty => 1;

            [Projectable(PolymorphicDispatch = true)]
            public virtual int VirtualMethod() => 1;
        }

        class Bar2 : Foo2
        {
            [Projectable(PolymorphicDispatch = true)]
            override public int VirtualProperty => true ? 2 : base.VirtualProperty;

            [Projectable(PolymorphicDispatch = true)]
            override public int VirtualMethod() => true ? 2 : base.VirtualProperty;
        }

        [Fact]
        public void VisitMember_PolymorphicBaseProperty()
        {
            Expression<Func<Foo2, int>> input = x => x.VirtualProperty;
            Expression<Func<Foo2, int>> expectedFoo = x => 1;
            Expression<Func<Bar2, int>> expectedBar = x => true ? 2 : ((Foo2)x).VirtualProperty;
            Expression<Func<Foo2, int>> expected = x => x is Bar2 ? true ? 2 : 1 : 1;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo2) ? expectedFoo : expectedBar
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_PolymorphicBaseMethod()
        {
            Expression<Func<Foo2, int>> input = x => x.VirtualMethod();
            Expression<Func<Foo2, int>> expectedFoo = x => 1;
            Expression<Func<Bar2, int>> expectedBar = x => true ? 2 : ((Foo2)x).VirtualMethod();
            Expression<Func<Foo2, int>> expected = x => x is Bar2 ? true ? 2 : 1 : 1;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo2) ? expectedFoo : expectedBar
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        abstract class Foo3
        {
            [Projectable(PolymorphicDispatch = true)]
            public abstract int VirtualProperty { get; }

            [Projectable(PolymorphicDispatch = true)]
            public abstract int VirtualMethod();
        }

        class Bar3 : Foo3
        {
            [Projectable(PolymorphicDispatch = true)]
            override public int VirtualProperty => 2;

            [Projectable(PolymorphicDispatch = true)]
            override public int VirtualMethod() => 2;
        }

        [Fact]
        public void VisitMember_PolymorphicPropertyAbstract()
        {
            Expression<Func<Foo3, int>> input = x => x.VirtualProperty;
            Expression<Func<Bar3, int>> expectedBar = x => 2;
            Expression<Func<Foo3, int>> expected = x => 2;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo3) ? null! : expectedBar
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_PolymorphicMethodAbstract()
        {
            Expression<Func<Foo3, int>> input = x => x.VirtualMethod();
            Expression<Func<Bar3, int>> expectedBar = x => 2;
            Expression<Func<Foo3, int>> expected = x => 2;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo3) ? null! : expectedBar
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        abstract class Foo4
        {
            [Projectable(PolymorphicDispatch = true)]
            public abstract int VirtualProperty { get; }

            [Projectable(PolymorphicDispatch = true)]
            public abstract int VirtualMethod();
        }

        class Bar4 : Foo4
        {
            [Projectable]
            override public int VirtualProperty => 2;

            [Projectable]
            override public int VirtualMethod() => 2;
        }

        class Baz4 : Foo4
        {
            [Projectable]
            override public int VirtualProperty => 3;

            [Projectable]
            override public int VirtualMethod() => 3;
        }

        [Fact]
        public void VisitMember_PolymorphicPropertyAbstractMultiple()
        {
            Expression<Func<Foo4, int>> input = x => x.VirtualProperty;
            Expression<Func<Bar4, int>> expectedBar = x => 2;
            Expression<Func<Baz4, int>> expectedBaz = x => 3;
            Expression<Func<Foo4, int>> expected = x => x is Bar4 ? 2 : 3;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo4) ? null! : (x.DeclaringType == typeof(Bar4) ? expectedBar : expectedBaz)
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }

        [Fact]
        public void VisitMember_PolymorphicMethodAbstractMultiple()
        {
            Expression<Func<Foo4, int>> input = x => x.VirtualMethod();
            Expression<Func<Bar4, int>> expectedBar = x => 2;
            Expression<Func<Baz4, int>> expectedBaz = x => 3;
            Expression<Func<Foo4, int>> expected = x => x is Bar4 ? 2 : 3;

            var resolver = new ProjectableExpressionResolverStubBase(
                (x, a) => x.DeclaringType == typeof(Foo4) ? null! : (x.DeclaringType == typeof(Bar4) ? expectedBar : expectedBaz)
            );
            var subject = new ProjectableExpressionReplacer(resolver);

            var actual = subject.Replace(input);

            Assert.Equal(expected.ToString(), actual.ToString());
        }
    }
}

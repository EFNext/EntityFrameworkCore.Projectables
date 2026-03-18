using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Xunit;

namespace EntityFrameworkCore.Projectables.Tests.Extensions
{
    public class TypeExtensionTests
    {
        class InnerType
        {
            public class SubsequentlyInnerType
            {

            }
        }

        class BaseType
        {
            public virtual void VirtualMethod(int arg1) { }
            public virtual void GenericVirtualMethod<TArg>(TArg arg1) { } 
        }

        class DerivedType : BaseType
        {
            public override void VirtualMethod(int arg1) { }
            public override void GenericVirtualMethod<TArg>(TArg arg1) { }
        }

        interface IStringId
        {
            string Id { get; }
        }

        class ItemWithExplicitInterfaceImplementation : IStringId
        {
            public int Id { get; set; }
            string IStringId.Id => Id.ToString();
        }

        [Fact]
        public void GetNestedTypePath_OuterType_Returns1Entry()
        {
            var subject = typeof(TypeExtensionTests);

            var result = subject.GetNestedTypePath();

            Assert.Single(result);
        }

        [Fact]
        public void GetNestedTypePath_InnerType_Returns2Entries()
        {
            var subject = typeof(InnerType);

            var result = subject.GetNestedTypePath();

            Assert.Equal(2, result.Length);
        }

        [Fact]
        public void GetNestedTypePath_SubsequentlyInnerType_Returns3Entries()
        {
            var subject = typeof(InnerType.SubsequentlyInnerType);

            var result = subject.GetNestedTypePath();

            Assert.Equal(3, result.Length);
        }

        [Fact]
        public void GetNestedTypePath_SubsequentlyInnerType_ReturnsTypesInOrder()
        {
            var subject = typeof(InnerType.SubsequentlyInnerType);

            var result = subject.GetNestedTypePath();

            Assert.Equal(typeof(TypeExtensionTests), result.First());
            Assert.Equal(typeof(InnerType.SubsequentlyInnerType), result.Last());
        }

        [Fact]
        public void GetOverridingMethod_BaseTypeVirtualMethod_FindsSameMethod()
        {
            var type = typeof(BaseType);
            var method = typeof(BaseType).GetMethod("VirtualMethod")!;

            var result =  type.GetOverridingMethod(method);

            Assert.Equal(method, result);
        }

        [Fact]
        public void GetOverridingMethod_DerivedTypeVirtualMethod_FindsOverridingMethod()
        {
            var baseType = typeof(BaseType);
            var baseMethod = baseType.GetMethod("VirtualMethod")!;
            var derivedType = typeof(DerivedType);
            var derivedMethod = typeof(DerivedType).GetMethod("VirtualMethod")!;

            var resolvedMethod = derivedType.GetOverridingMethod(baseMethod);

            Assert.Equal(derivedMethod, resolvedMethod);
        }

        [Fact]
        public void GetOverridingMethod_BaseTypeGenericVirtualMethod_FindsSameMethod()
        {
            var type = typeof(BaseType);
            var method = typeof(BaseType).GetMethod("GenericVirtualMethod")!;

            var result = type.GetOverridingMethod(method);

            Assert.Equal(method, result);
        }

        [Fact]
        public void GetOverridingMethod_DerivedTypeGenericVirtualMethod_FindsOverridingMethod()
        {
            var baseType = typeof(BaseType);
            var baseMethod = baseType.GetMethod("GenericVirtualMethod")!;
            var derivedType = typeof(DerivedType);
            var derivedMethod = typeof(DerivedType).GetMethod("GenericVirtualMethod")!;

            var resolvedMethod = derivedType.GetOverridingMethod(baseMethod);

            Assert.Equal(derivedMethod, resolvedMethod);
        }

        [Fact]
        public void GetImplementingProperty_ExplicitInterfaceImplementation_ReturnsConcreteImplementation()
        {
            // This test verifies that when a class explicitly implements an interface property
            // (e.g., string IStringId.Id => Id.ToString();), GetImplementingProperty doesn't throw
            // an InvalidOperationException
            var interfaceType = typeof(IStringId);
            var interfaceProperty = interfaceType.GetProperty("Id")!;
            var concreteType = typeof(ItemWithExplicitInterfaceImplementation);

            // This should not throw InvalidOperationException
            var result = concreteType.GetImplementingProperty(interfaceProperty);

            // The result should be the explicit interface implementation property
            Assert.NotNull(result);
            Assert.NotEqual(interfaceProperty, result);
            Assert.Contains("IStringId.Id", result.Name);
        }

        [Fact]
        public void GetConcreteProperty_ExplicitInterfaceImplementation_ReturnsConcreteImplementation()
        {
            // This test verifies the same scenario but through GetConcreteProperty which is the entry point
            var interfaceType = typeof(IStringId);
            var interfaceProperty = interfaceType.GetProperty("Id")!;
            var concreteType = typeof(ItemWithExplicitInterfaceImplementation);

            // This should not throw InvalidOperationException
            var result = concreteType.GetConcreteProperty(interfaceProperty);

            // The result should be the explicit interface implementation property
            Assert.NotNull(result);
            Assert.NotEqual(interfaceProperty, result);
            Assert.Contains("IStringId.Id", result.Name);
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using EntityFrameworkCore.Projectables.Services;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

#nullable disable

namespace EntityFrameworkCore.Projectables.FunctionalTests
{

    [UsesVerify]
    public class InheritedModelTests
    {
        public interface IBaseProvider<TBase>
        {
            ICollection<TBase> Bases { get; set; }
        }

        public class BaseProvider : IBaseProvider<Concrete>
        {
            public int Id { get; set; }
            public ICollection<Concrete> Bases { get; set; }
        }

        public interface IBase
        {
            int Id { get; }
            int ComputedProperty { get; }
            int ComputedMethod();
        }

        public abstract class Base : IBase
        {
            public int Id { get; set; }

            [Projectable]
            public int ComputedProperty => SampleProperty + 1;

            public virtual int SampleProperty => 0;

            [Projectable]
            public int ComputedMethod() => SampleMethod() + 1;

            public virtual int SampleMethod() => 0;
        }

        public interface IDefaultBase
        {
            int Explicit { get; }

            [Projectable]
            int Default => 49;
        }

        public interface IDefaultBaseImplementation : IDefaultBase, IBase
        {
            [Projectable]
            int IDefaultBase.Explicit => Id * 2;
        }

        public class Concrete : Base, IDefaultBaseImplementation
        {
            [Projectable]
            public override int SampleProperty => 1;

            [Projectable]
            public override int SampleMethod() => 1;
        }

        public class OtherConcrete : Base, IDefaultBase
        {
            [Projectable]
            int IDefaultBase.Explicit => Id / 2;
        }

        public class MoreConcrete : Concrete
        {
        }

        [Fact]
        public Task ProjectOverOverriddenPropertyImplementation()
        {
            using var dbContext = new SampleDbContext<Concrete>();

            var query = dbContext.Set<Concrete>()
                .Select(x => x.ComputedProperty);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverInheritedPropertyImplementation()
        {
            using var dbContext = new SampleDbContext<MoreConcrete>();

            var query = dbContext.Set<MoreConcrete>()
                .Select(x => x.ComputedProperty);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverOverriddenMethodImplementation()
        {
            using var dbContext = new SampleDbContext<Concrete>();

            var query = dbContext.Set<Concrete>()
                .Select(x => x.ComputedMethod());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverInheritedMethodImplementation()
        {
            using var dbContext = new SampleDbContext<MoreConcrete>();

            var query = dbContext.Set<MoreConcrete>()
                .Select(x => x.ComputedMethod());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverImplementedProperty()
        {
            using var dbContext = new SampleDbContext<Concrete>();

            var query = dbContext.Set<Concrete>().SelectComputedProperty();

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverImplementedMethod()
        {
            using var dbContext = new SampleDbContext<Concrete>();

            var query = dbContext.Set<Concrete>().SelectComputedMethod();

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverDefaultImplementedProperty()
        {
            using var dbContext = new SampleDbContext<Concrete>();

            var query = dbContext.Set<Concrete>().SelectDefaultProperty();

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverExplicitlyImplementedProperty()
        {
            using var dbContext = new SampleDbContext<OtherConcrete>();

            var query = dbContext.Set<OtherConcrete>().SelectExplicitProperty();

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverDefaultExplicitlyImplementedProperty()
        {
            using var dbContext = new SampleDbContext<Concrete>();

            var query = dbContext.Set<Concrete>().SelectExplicitProperty();

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverProvider()
        {
            using var dbContext = new SampleDbContext<BaseProvider>();

            var query = dbContext.Set<BaseProvider>().AllBases<BaseProvider, Concrete>();

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ProjectOverExtensionMethod()
        {
            using var dbContext = new SampleDbContext<Concrete>();

            var query = dbContext.Set<Concrete>().Select(c => c.ComputedPropertyPlusMethod());

            return Verifier.Verify(query.ToQueryString());
        }
    }

    public static class ModelExtensions
    {
        public static IQueryable<int> SelectComputedProperty<TConcrete>(this IQueryable<TConcrete> concretes)
            where TConcrete : InheritedModelTests.IBase
            => concretes.Select(x => x.ComputedProperty);
        
        public static IQueryable<int> SelectDefaultProperty<TConcrete>(this IQueryable<TConcrete> concretes)
            where TConcrete : InheritedModelTests.IDefaultBase
            => concretes.Select(x => x.Default);
        
        public static IQueryable<int> SelectExplicitProperty<TConcrete>(this IQueryable<TConcrete> concretes)
            where TConcrete : InheritedModelTests.IDefaultBase
            => concretes.Select(x => x.Explicit);

        public static IQueryable<int> SelectComputedMethod<TConcrete>(this IQueryable<TConcrete> concretes)
            where TConcrete : InheritedModelTests.IBase
            => concretes.Select(x => x.ComputedMethod());

        public static IQueryable<int> AllBases<TProvider, TBase>(this IQueryable<TProvider> concretes)
            where TProvider : InheritedModelTests.IBaseProvider<TBase>
            where TBase : InheritedModelTests.IBase
            => concretes.SelectMany(x => x.Bases).Select(x => x.Id);

        [Projectable]
        public static int ComputedPropertyPlusMethod<TConcrete>(this TConcrete concrete)
            where TConcrete : InheritedModelTests.IBase
            => concrete.ComputedProperty + concrete.ComputedMethod();
    }
}

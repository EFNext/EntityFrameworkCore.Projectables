using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class ComplexArgumentsTests
    {
        public class TestEntity
        {
            public int Id { get; set; }

            [Projectable]
            public bool IsValid1(List<int> validIds) => validIds.Contains(Id);

            [Projectable]
            public bool IsValid2(int[] validIds) => validIds.Contains(Id);

            [Projectable]
            public bool IsValid3(params int[] validIds) => validIds.Contains(Id);

            [Projectable]
            public bool IsValid4(int? validId, Guid? guid) => validId > Id && guid != Guid.Parse("00000000-0000-0000-0000-000000000000");
        }

        [Fact]
        public Task ListOfPrimitivesArguments()
        {
            using var dbContext = new SampleDbContext<TestEntity>();

            var validList = new List<int>() { 1, 2, 3 };

            var query = dbContext.Set<TestEntity>()
                .Where(x => x.IsValid1(validList));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ArrayOfPrimitivesArguments()
        {
            using var dbContext = new SampleDbContext<TestEntity>();

            var validArray = new[]  { 1, 2, 3 };

            var query = dbContext.Set<TestEntity>()
                .Where(x => x.IsValid2(validArray));

            return Verifier.Verify(query.ToQueryString());
        }
        
        [Fact]
        public Task ParamsOfPrimitivesArguments()
        {
            using var dbContext = new SampleDbContext<TestEntity>();

            var query = dbContext.Set<TestEntity>()
                .Where(x => x.IsValid3(1, 2, 3));

            return Verifier.Verify(query.ToQueryString());
        }
        
        [Fact]
        public Task NullableValueTypes()
        {
            using var dbContext = new SampleDbContext<TestEntity>();

            var query = dbContext.Set<TestEntity>()
                .Where(x => x.IsValid4(1, Guid.NewGuid()));

            return Verifier.Verify(query.ToQueryString());
        }
    }
}

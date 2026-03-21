using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests.BlockBodiedMethods
{
    /// <summary>
    /// Tests for calling projectable methods from within block-bodied methods
    /// </summary>
    public class BlockBodyProjectableCallTests
    {
        public record Entity
        {
            public int Id { get; set; }
            public int Value { get; set; }
            public bool IsActive { get; set; }
            public string? Name { get; set; }
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_Simple()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.GetAdjustedWithConstant());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_InReturn()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.GetDoubledValue());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_InCondition()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.GetCategoryBasedOnAdjusted());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_Multiple()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.CombineProjectableMethods());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_InSwitch()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.GetLabelBasedOnCategory());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_InSwitchExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.GetDescriptionByLevel());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_WithLocalVariable()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.CalculateUsingProjectable());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_Nested()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.GetNestedProjectableCall());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_InEarlyReturn()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.GetStatusWithProjectableCheck());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_InTernary()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.GetConditionalProjectable());
            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockBodyCallingProjectableMethod_InLogicalExpression()
        {
            using var dbContext = new SampleDbContext<Entity>();
            var query = dbContext.Set<Entity>()
                .Select(x => x.IsComplexCondition());
            return Verifier.Verify(query.ToQueryString());
        }
    }

    public static class ProjectableCallExtensions
    {
        // Base projectable methods (helper methods)
        [Projectable(AllowBlockBody = true)]
        public static int GetConstant(this BlockBodyProjectableCallTests.Entity entity)
        {
            return 42;
        }

        [Projectable(AllowBlockBody = true)]
        public static int GetDoubled(this BlockBodyProjectableCallTests.Entity entity)
        {
            return entity.Value * 2;
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetCategory(this BlockBodyProjectableCallTests.Entity entity)
        {
            if (entity.Value > 100)
                return "High";
            else
                return "Low";
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetLevel(this BlockBodyProjectableCallTests.Entity entity)
        {
            if (entity.Value > 100) return "Level3";
            if (entity.Value > 50) return "Level2";
            return "Level1";
        }

        [Projectable(AllowBlockBody = true)]
        public static bool IsHighValue(this BlockBodyProjectableCallTests.Entity entity)
        {
            return entity.Value > 100;
        }

        // Block-bodied methods calling projectable methods

        [Projectable(AllowBlockBody = true)]
        public static int GetAdjustedWithConstant(this BlockBodyProjectableCallTests.Entity entity)
        {
            return entity.Value + entity.GetConstant();
        }

        [Projectable(AllowBlockBody = true)]
        public static int GetDoubledValue(this BlockBodyProjectableCallTests.Entity entity)
        {
            var doubled = entity.GetDoubled();
            return doubled;
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetCategoryBasedOnAdjusted(this BlockBodyProjectableCallTests.Entity entity)
        {
            if (entity.GetDoubled() > 200)
            {
                return "Very High";
            }
            else
            {
                return "Normal";
            }
        }

        [Projectable(AllowBlockBody = true)]
        public static int CombineProjectableMethods(this BlockBodyProjectableCallTests.Entity entity)
        {
            return entity.GetDoubled() + entity.GetConstant();
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetLabelBasedOnCategory(this BlockBodyProjectableCallTests.Entity entity)
        {
            switch (entity.GetCategory())
            {
                case "High":
                    return "Premium";
                case "Low":
                    return "Standard";
                default:
                    return "Unknown";
            }
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetDescriptionByLevel(this BlockBodyProjectableCallTests.Entity entity)
        {
            return entity.GetLevel() switch
            {
                "Level3" => "Expert",
                "Level2" => "Intermediate",
                "Level1" => "Beginner",
                _ => "Unknown"
            };
        }

        [Projectable(AllowBlockBody = true)]
        public static int CalculateUsingProjectable(this BlockBodyProjectableCallTests.Entity entity)
        {
            var doubled = entity.GetDoubled();
            var withConstant = doubled + entity.GetConstant();
            return withConstant * 2;
        }

        [Projectable(AllowBlockBody = true)]
        public static int GetNestedProjectableCall(this BlockBodyProjectableCallTests.Entity entity)
        {
            return entity.GetAdjustedWithConstant() + 10;
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetStatusWithProjectableCheck(this BlockBodyProjectableCallTests.Entity entity)
        {
            if (entity.IsHighValue())
                return "Premium";

            if (entity.GetCategory() == "High")
                return "Standard High";

            return "Normal";
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetConditionalProjectable(this BlockBodyProjectableCallTests.Entity entity)
        {
            return entity.IsActive ? entity.GetCategory() : "Inactive";
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetChainedResult(this BlockBodyProjectableCallTests.Entity entity)
        {
            var doubled = entity.GetDoubled();
            
            if (doubled > 200)
            {
                return entity.GetCategory() + " Priority";
            }
            
            return entity.GetLevel();
        }

        [Projectable(AllowBlockBody = true)]
        public static bool IsComplexCondition(this BlockBodyProjectableCallTests.Entity entity)
        {
            return entity.IsActive && entity.IsHighValue() || entity.GetDoubled() > 150;
        }
    }
}

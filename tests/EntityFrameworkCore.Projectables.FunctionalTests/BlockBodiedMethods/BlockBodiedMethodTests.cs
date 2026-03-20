using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests.BlockBodiedMethods
{
    public class BlockBodiedMethodTests
    {
        public record Entity
        {
            public int Id { get; set; }
            public int Value { get; set; }
            public bool IsActive { get; set; }
            public string? Name { get; set; }
        }

        [Fact]
        public Task SimpleReturn_IsTranslatedToSql()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetConstant());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ReturnWithPropertyAccess_IsTranslatedToSql()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetValuePlusTen());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task IfElseStatement_IsTranslatedToTernary()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetCategory());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task NestedIfElse_IsTranslatedToNestedTernary()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetLevel());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task LocalVariable_IsInlined()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.CalculateDouble());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ComplexConditional_IsTranslatedCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetAdjustedValue());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BlockMethodWithParameters_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.Add(5, 10));

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task IfWithoutElse_UsesDefault()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetPremiumIfActive());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task IfWithoutElse_WithFallbackReturn()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetStatus());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SwitchStatement_Simple()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetValueLabel());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SwitchStatement_WithMultipleCases()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetPriority());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task MultipleEarlyReturns_ConvertedToNestedTernaries()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetValueCategory());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task NullCoalescing_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetNameOrDefault());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ConditionalAccess_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetNameLength());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SwitchExpression_Simple()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetValueLabelModern());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SwitchExpression_WithDiscard()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetPriorityModern());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task MultipleLocalVariables_AreInlinedCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.CalculateComplex());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task NestedConditionals_WithLogicalOperators()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetComplexCategory());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task GuardClause_WithEarlyReturn()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetGuardedValue());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task NestedSwitchInIf_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetCombinedLogic());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task TernaryExpression_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetValueUsingTernary());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task NestedTernary_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetNestedTernary());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task MixedIfAndSwitch_WithMultiplePatterns()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetComplexMix());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SwitchWithWhenClause_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetValueWithCondition());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task LocalVariableReuse_IsInlinedMultipleTimes()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.CalculateWithReuse());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task BooleanReturn_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.IsHighValue());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ConditionalWithNegation_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetInactiveStatus());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task StringInterpolation_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.GetFormattedValue());

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task ArithmeticInReturn_WorksCorrectly()
        {
            using var dbContext = new SampleDbContext<Entity>();

            var query = dbContext.Set<Entity>()
                .Select(x => x.CalculatePercentage());

            return Verifier.Verify(query.ToQueryString());
        }
    }

    public static class EntityExtensions
    {
        [Projectable(AllowBlockBody = true)]
        public static int GetConstant(this BlockBodiedMethodTests.Entity entity)
        {
            return 42;
        }

        [Projectable(AllowBlockBody = true)]
        public static int GetValuePlusTen(this BlockBodiedMethodTests.Entity entity)
        {
            return entity.Value + 10;
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetCategory(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.Value > 100)
            {
                return "High";
            }
            else
            {
                return "Low";
            }
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetLevel(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.Value > 100)
            {
                return "High";
            }
            else if (entity.Value > 50)
            {
                return "Medium";
            }
            else
            {
                return "Low";
            }
        }

        [Projectable(AllowBlockBody = true)]
        public static int CalculateDouble(this BlockBodiedMethodTests.Entity entity)
        {
            var doubled = entity.Value * 2;
            return doubled + 5;
        }

        [Projectable(AllowBlockBody = true)]
        public static int GetAdjustedValue(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.IsActive && entity.Value > 0)
            {
                return entity.Value * 2;
            }
            else
            {
                return 0;
            }
        }

        [Projectable(AllowBlockBody = true)]
        public static int Add(this BlockBodiedMethodTests.Entity entity, int a, int b)
        {
            return a + b;
        }

        [Projectable(AllowBlockBody = true)]
        public static int? GetPremiumIfActive(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.IsActive)
            {
                return entity.Value * 2;
            }
            return null;
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetStatus(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.IsActive)
            {
                return "Active";
            }
            return "Inactive";
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetValueLabel(this BlockBodiedMethodTests.Entity entity)
        {
            switch (entity.Value)
            {
                case 1:
                    return "One";
                case 2:
                    return "Two";
                case 3:
                    return "Three";
                default:
                    return "Many";
            }
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetPriority(this BlockBodiedMethodTests.Entity entity)
        {
            switch (entity.Value)
            {
                case 1:
                case 2:
                    return "Low";
                case 3:
                case 4:
                case 5:
                    return "Medium";
                case 6:
                case 7:
                case 8:
                    return "High";
                default:
                    return "Critical";
            }
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetValueCategory(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.Value > 100)
            {
                return "Very High";
            }

            if (entity.Value > 50)
            {
                return "High";
            }

            if (entity.Value > 10)
            {
                return "Medium";
            }

            return "Low";
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetNameOrDefault(this BlockBodiedMethodTests.Entity entity)
        {
            return entity.Name ?? "Unknown";
        }

        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite, AllowBlockBody = true)]
        public static int? GetNameLength(this BlockBodiedMethodTests.Entity entity)
        {
            return entity.Name?.Length;
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetValueLabelModern(this BlockBodiedMethodTests.Entity entity)
        {
            return entity.Value switch
            {
                1 => "One",
                2 => "Two",
                3 => "Three",
                _ => "Many"
            };
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetPriorityModern(this BlockBodiedMethodTests.Entity entity)
        {
            return entity.Value switch
            {
                <= 2 => "Low",
                <= 5 => "Medium",
                <= 8 => "High",
                _ => "Critical"
            };
        }

        [Projectable(AllowBlockBody = true)]
        public static int CalculateComplex(this BlockBodiedMethodTests.Entity entity)
        {
            var doubled = entity.Value * 2;
            var tripled = entity.Value * 3;
            var sum = doubled + tripled;
            return sum + 10;
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetComplexCategory(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.IsActive && entity.Value > 100)
            {
                return "Active High";
            }

            if (entity.IsActive || entity.Value > 50)
            {
                return "Active or Medium";
            }

            if (!entity.IsActive && entity.Value <= 10)
            {
                return "Inactive Low";
            }

            return "Other";
        }

        [Projectable(AllowBlockBody = true)]
        public static int GetGuardedValue(this BlockBodiedMethodTests.Entity entity)
        {
            if (!entity.IsActive)
            {
                return 0;
            }

            if (entity.Value < 0)
            {
                return 0;
            }

            return entity.Value * 2;
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetCombinedLogic(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.IsActive)
            {
                switch (entity.Value)
                {
                    case 1:
                        return "Active One";
                    case 2:
                        return "Active Two";
                    default:
                        return "Active Other";
                }
            }

            return "Inactive";
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetValueUsingTernary(this BlockBodiedMethodTests.Entity entity)
        {
            return entity.IsActive ? "Active" : "Inactive";
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetNestedTernary(this BlockBodiedMethodTests.Entity entity)
        {
            return entity.Value > 100 ? "High" : entity.Value > 50 ? "Medium" : "Low";
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetComplexMix(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.IsActive)
            {
                return entity.Value switch
                {
                    > 100 => "Active High",
                    > 50 => "Active Medium",
                    _ => "Active Low"
                };
            }

            return "Inactive";
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetValueWithCondition(this BlockBodiedMethodTests.Entity entity)
        {
            return entity.Value switch
            {
                1 when entity.IsActive => "Active One",
                1 => "Inactive One",
                > 10 when entity.IsActive => "Active High",
                _ => "Other"
            };
        }

        [Projectable(AllowBlockBody = true)]
        public static int CalculateWithReuse(this BlockBodiedMethodTests.Entity entity)
        {
            var doubled = entity.Value * 2;
            return doubled + doubled;
        }

        [Projectable(AllowBlockBody = true)]
        public static bool IsHighValue(this BlockBodiedMethodTests.Entity entity)
        {
            if (entity.Value > 100)
            {
                return true;
            }
            return false;
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetInactiveStatus(this BlockBodiedMethodTests.Entity entity)
        {
            if (!entity.IsActive)
            {
                return "Not Active";
            }
            else
            {
                return "Active";
            }
        }

        [Projectable(AllowBlockBody = true)]
        public static string GetFormattedValue(this BlockBodiedMethodTests.Entity entity)
        {
            return $"Value: {entity.Value}";
        }

        [Projectable(AllowBlockBody = true)]
        public static double CalculatePercentage(this BlockBodiedMethodTests.Entity entity)
        {
            return (double)entity.Value / 100.0 * 50.0;
        }
    }
}

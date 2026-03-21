using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.FunctionalTests
{
    public class ExpandEnumMethodsTests
    {
        public enum OrderStatus
        {
            [Display(Name = "Pending Review")]
            Pending,
            
            [Display(Name = "Approved")]
            Approved,
            
            [Display(Name = "Rejected")]
            Rejected
        }

        public enum Priority
        {
            [Description("Low Priority")]
            Low,
            
            [Description("Medium Priority")]
            Medium,
            
            [Description("High Priority")]
            High
        }

        public record Order
        {
            public int Id { get; set; }
            public OrderStatus Status { get; set; }
            public Priority? Priority { get; set; }
            public Customer? Customer { get; set; }

            [Projectable(ExpandEnumMethods = true)]
            public string StatusName => Status.GetDisplayName();

            [Projectable(ExpandEnumMethods = true, NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
            public string? PriorityDescription => Priority.HasValue ? Priority.Value.GetDescription() : null;

            [Projectable(ExpandEnumMethods = true)]
            public bool IsApproved => Status.IsApproved();

            [Projectable(ExpandEnumMethods = true)]
            public int PrioritySortOrder => (Priority ?? ExpandEnumMethodsTests.Priority.Low).GetSortOrder();

            [Projectable(ExpandEnumMethods = true)]
            public string StatusWithPrefix => Status.GetDisplayNameWithPrefix("Order Status: ");
        }

        public record Customer
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public Priority PreferredPriority { get; set; }
        }

        public record OrderWithNavigation
        {
            public int Id { get; set; }
            public Customer? Customer { get; set; }

            [Projectable(ExpandEnumMethods = true)]
            public string CustomerPriorityDescription => Customer!.PreferredPriority.GetDescription();
        }

        [Fact]
        public Task FilterOnExpandedEnumProperty()
        {
            using var dbContext = new SampleDbContext<Order>();

            var query = dbContext.Set<Order>()
                .Where(x => x.StatusName == "Pending Review");

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectExpandedEnumProperty()
        {
            using var dbContext = new SampleDbContext<Order>();

            var query = dbContext.Set<Order>()
                .Select(x => x.StatusName);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task OrderByExpandedEnumProperty()
        {
            using var dbContext = new SampleDbContext<Order>();

            var query = dbContext.Set<Order>()
                .OrderBy(x => x.StatusName);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectNullableEnumExpandedProperty()
        {
            using var dbContext = new SampleDbContext<Order>();

            var query = dbContext.Set<Order>()
                .Select(x => x.PriorityDescription);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectEnumOnNavigationProperty()
        {
            using var dbContext = new SampleDbContext<OrderWithNavigation>();

            var query = dbContext.Set<OrderWithNavigation>()
                .Select(x => x.CustomerPriorityDescription);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task FilterOnBooleanEnumExpansion()
        {
            using var dbContext = new SampleDbContext<Order>();

            var query = dbContext.Set<Order>()
                .Where(x => x.IsApproved);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectBooleanEnumExpansion()
        {
            using var dbContext = new SampleDbContext<Order>();

            var query = dbContext.Set<Order>()
                .Select(x => x.IsApproved);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task OrderByIntegerEnumExpansion()
        {
            using var dbContext = new SampleDbContext<Order>();

            var query = dbContext.Set<Order>()
                .OrderBy(x => x.PrioritySortOrder);

            return Verifier.Verify(query.ToQueryString());
        }

        [Fact]
        public Task SelectEnumMethodWithParameter()
        {
            using var dbContext = new SampleDbContext<Order>();

            var query = dbContext.Set<Order>()
                .Select(x => x.StatusWithPrefix);

            return Verifier.Verify(query.ToQueryString());
        }
    }

    public static class EnumExtensions
    {
        public static string GetDisplayName<TEnum>(this TEnum value) where TEnum : struct, System.Enum
        {
            var type = value.GetType();
            var memberInfo = type.GetMember(value.ToString())[0];
            var displayAttribute = memberInfo.GetCustomAttributes(typeof(DisplayAttribute), false)
                .OfType<DisplayAttribute>()
                .FirstOrDefault();
            return displayAttribute?.Name ?? value.ToString();
        }

        public static string GetDescription<TEnum>(this TEnum value) where TEnum : struct, System.Enum
        {
            var type = value.GetType();
            var memberInfo = type.GetMember(value.ToString())[0];
            var descriptionAttribute = memberInfo.GetCustomAttributes(typeof(DescriptionAttribute), false)
                .OfType<DescriptionAttribute>()
                .FirstOrDefault();
            return descriptionAttribute?.Description ?? value.ToString();
        }

        public static bool IsApproved(this ExpandEnumMethodsTests.OrderStatus value)
        {
            return value == ExpandEnumMethodsTests.OrderStatus.Approved;
        }

        public static int GetSortOrder(this ExpandEnumMethodsTests.Priority value)
        {
            return value switch
            {
                ExpandEnumMethodsTests.Priority.Low => 1,
                ExpandEnumMethodsTests.Priority.Medium => 2,
                ExpandEnumMethodsTests.Priority.High => 3,
                _ => 0
            };
        }

        public static string GetDisplayNameWithPrefix(this ExpandEnumMethodsTests.OrderStatus value, string prefix)
        {
            return prefix + value.ToString();
        }
    }
}

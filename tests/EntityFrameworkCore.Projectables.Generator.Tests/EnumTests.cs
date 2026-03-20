using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public class EnumTests : ProjectionExpressionGeneratorTestsBase
{
    public EnumTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task ExpandEnumMethodsWithDisplayAttribute()
    {
        var compilation = CreateCompilation(@"
using System;
using System.ComponentModel.DataAnnotations;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public enum CustomEnum
    {
        [Display(Name = ""Value 1"")]
        Value1,
        
        [Display(Name = ""Value 2"")]
        Value2,
    }
    
    public static class EnumExtensions
    {
        public static string GetDisplayName(this CustomEnum value)
        {
            return value.ToString();
        }
    }
    
    public record Entity
    {
        public int Id { get; set; }
        public CustomEnum MyValue { get; set; }
        
        [Projectable(ExpandEnumMethods = true)]
        public string MyEnumName => MyValue.GetDisplayName();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpandEnumMethodsWithNullableEnum()
    {
        var compilation = CreateCompilation(@"
using System;
using System.ComponentModel.DataAnnotations;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public enum CustomEnum
    {
        [Display(Name = ""First Value"")]
        First,
        
        [Display(Name = ""Second Value"")]
        Second,
    }
    
    public static class EnumExtensions
    {
        public static string GetDisplayName(this CustomEnum value)
        {
            return value.ToString();
        }
    }
    
    public record Entity
    {
        public int Id { get; set; }
        public CustomEnum? MyValue { get; set; }
        
        [Projectable(ExpandEnumMethods = true)]
        public string MyEnumName => MyValue.HasValue ? MyValue.Value.GetDisplayName() : null;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpandEnumMethodsWithDescriptionAttribute()
    {
        var compilation = CreateCompilation(@"
using System;
using System.ComponentModel;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public enum Status
    {
        [Description(""The item is pending"")]
        Pending,
        
        [Description(""The item is approved"")]
        Approved,
        
        [Description(""The item is rejected"")]
        Rejected,
    }
    
    public static class EnumExtensions
    {
        public static string GetDescription(this Status value)
        {
            return value.ToString();
        }
    }
    
    public record Entity
    {
        public int Id { get; set; }
        public Status Status { get; set; }
        
        [Projectable(ExpandEnumMethods = true)]
        public string StatusDescription => Status.GetDescription();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpandEnumMethodsOnNavigationProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using System.ComponentModel.DataAnnotations;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public enum OrderStatus
    {
        [Display(Name = ""Pending Review"")]
        Pending,
        
        [Display(Name = ""Approved"")]
        Approved,
    }
    
    public static class EnumExtensions
    {
        public static string GetDisplayName(this OrderStatus value)
        {
            return value.ToString();
        }
    }

    public record Order
    {
        public int Id { get; set; }
        public OrderStatus Status { get; set; }
    }
    
    public record OrderItem
    {
        public int Id { get; set; }
        public Order Order { get; set; }
        
        [Projectable(ExpandEnumMethods = true)]
        public string OrderStatusName => Order.Status.GetDisplayName();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpandEnumMethodsReturningBoolean()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public enum Status
    {
        Pending,
        Approved,
        Rejected,
    }
    
    public static class EnumExtensions
    {
        public static bool IsApproved(this Status value)
        {
            return value == Status.Approved;
        }
    }
    
    public record Entity
    {
        public int Id { get; set; }
        public Status Status { get; set; }
        
        [Projectable(ExpandEnumMethods = true)]
        public bool IsStatusApproved => Status.IsApproved();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpandEnumMethodsReturningInteger()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public enum Priority
    {
        Low,
        Medium,
        High,
    }
    
    public static class EnumExtensions
    {
        public static int GetSortOrder(this Priority value)
        {
            return (int)value;
        }
    }
    
    public record Entity
    {
        public int Id { get; set; }
        public Priority Priority { get; set; }
        
        [Projectable(ExpandEnumMethods = true)]
        public int PrioritySortOrder => Priority.GetSortOrder();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpandEnumMethodsWithParameter()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public enum Status
    {
        Pending,
        Approved,
        Rejected,
    }
    
    public static class EnumExtensions
    {
        public static string GetDisplayNameWithPrefix(this Status value, string prefix)
        {
            return prefix + value.ToString();
        }
    }
    
    public record Entity
    {
        public int Id { get; set; }
        public Status Status { get; set; }
        
        [Projectable(ExpandEnumMethods = true)]
        public string StatusWithPrefix => Status.GetDisplayNameWithPrefix(""Status: "");
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpandEnumMethodsWithMultipleParameters()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public enum Status
    {
        Pending,
        Approved,
        Rejected,
    }
    
    public static class EnumExtensions
    {
        public static string Format(this Status value, string prefix, string suffix)
        {
            return prefix + value.ToString() + suffix;
        }
    }
    
    public record Entity
    {
        public int Id { get; set; }
        public Status Status { get; set; }
        
        [Projectable(ExpandEnumMethods = true)]
        public string FormattedStatus => Status.Format(""["", ""]"");
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
}
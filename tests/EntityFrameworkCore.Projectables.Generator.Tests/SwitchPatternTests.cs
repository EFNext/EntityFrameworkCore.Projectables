using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public class SwitchPatternTests : ProjectionExpressionGeneratorTestsBase
{
    public SwitchPatternTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task SwitchExpressionWithConstantPattern()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

class Foo {
    public int? FancyNumber { get; set; }

    [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
    public int SomeNumber(int input) => input switch {
            1 => 2,
            3 => 4,
            4 when FancyNumber == 12 => 48,
            _ => 1000,
        };
    }
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task SwitchExpressionWithTypePattern()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

public abstract class Item
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class GroupItem : Item
{
    public string Description { get; set; }
}

public class DocumentItem : Item
{
    public int Priority { get; set; }
}

public abstract record ItemData(int Id, string Name);
public record GroupData(int Id, string Name, string Description) : ItemData(Id, Name);
public record DocumentData(int Id, string Name, int Priority) : ItemData(Id, Name);

public static class ItemMapper
{
    [Projectable]
    public static ItemData ToData(this Item item) =>
        item switch
        {
            GroupItem groupItem => new GroupData(groupItem.Id, groupItem.Name, groupItem.Description),
            DocumentItem documentItem => new DocumentData(documentItem.Id, documentItem.Name, documentItem.Priority),
            _ => null!
        };
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
            
        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task SwitchExpression_WithRelationalPattern()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Score { get; set; }

        [Projectable]
        public string GetGrade() => Score switch
        {
            >= 90 => ""A"",
            >= 80 => ""B"",
            >= 70 => ""C"",
            _ => ""F"",
        };
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task SwitchExpression_WithRelationalPattern_OnExtensionMethod()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Order {
        public decimal Amount { get; set; }
    }

    static class OrderExtensions {
        [Projectable]
        public static string GetTier(this Order order) => order.Amount switch
        {
            >= 1000 => ""Platinum"",
            >= 500  => ""Gold"",
            >= 100  => ""Silver"",
            _       => ""Bronze"",
        };
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpressionBodied_IsPattern_WithAndPattern()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Value { get; set; }

        [Projectable]
        public bool IsInRange => Value is >= 1 and <= 100;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpressionBodied_IsPattern_WithOrPattern()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Value { get; set; }

        [Projectable]
        public bool IsOutOfRange => Value is 0 or > 100;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpressionBodied_IsPattern_WithPropertyPattern()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public bool IsActive { get; set; }
        public int Value { get; set; }
    }

    static class Extensions {
        [Projectable]
        public static bool IsActiveAndPositive(this Entity entity) =>
            entity is { IsActive: true, Value: > 0 };
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExpressionBodied_IsPattern_WithNotNullPattern()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public string? Name { get; set; }

        [Projectable]
        public bool HasName => Name is not null;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task DictionaryIndexInitializer_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public static class EntityExtensions
    {
        public record Entity
        {
            public int Id { get; set; }
            public string? FullName { get; set; }
        }

        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public static Dictionary<string, object> ToDictionary(this Entity entity)
            => new Dictionary<string, object> 
            {
                [""FullName""] = entity.FullName ?? ""N/A"",
                [""Id""] = entity.Id.ToString(),
            };
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task DictionaryObjectInitializer_PreservesCollectionInitializerSyntax()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public static class EntityExtensions
    {
        public record Entity
        {
            public int Id { get; set; }
            public string? FullName { get; set; }
        }

        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public static Dictionary<string, string> ToDictionary(this Entity entity)
            => new Dictionary<string, string> 
            {
                { ""FullName"", entity.FullName ?? ""N/A"" }
            };
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
}
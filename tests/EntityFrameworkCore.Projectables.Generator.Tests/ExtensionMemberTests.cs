using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public class ExtensionMemberTests : ProjectionExpressionGeneratorTestsBase
{
    public ExtensionMemberTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

#if NET10_0_OR_GREATER
    [Fact]
    public Task ExtensionMemberProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Id { get; set; }
    }

    static class EntityExtensions {
        extension(Entity e) {
            [Projectable]
            public int DoubleId => e.Id * 2;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExtensionMemberMethod()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Id { get; set; }
    }

    static class EntityExtensions {
        extension(Entity e) {
            [Projectable]
            public int TripleId() => e.Id * 3;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExtensionMemberMethodWithParameters()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Id { get; set; }
    }

    static class EntityExtensions {
        extension(Entity e) {
            [Projectable]
            public int Multiply(int factor) => e.Id * factor;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExtensionMemberOnPrimitive()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

static class IntExtensions {
    extension(int i) {
        [Projectable]
        public int Squared => i * i;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExtensionMemberWithMemberAccess()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    static class EntityExtensions {
        extension(Entity e) {
            [Projectable]
            public string IdAndName => e.Id + "": "" + e.Name;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExtensionMemberWithBlockBody()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Value { get; set; }
        public bool IsActive { get; set; }
    }

    static class EntityExtensions {
        extension(Entity e) {
            [Projectable(AllowBlockBody = true)]
            public string GetStatus()
            {
                if (e.IsActive && e.Value > 0)
                {
                    return ""Active"";
                }
                return ""Inactive"";
            }
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExtensionMemberWithSwitchExpression()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Score { get; set; }
    }

    static class EntityExtensions {
        extension(Entity e) {
            [Projectable]
            public string GetGrade() => e.Score switch
            {
                >= 90 => ""A"",
                >= 80 => ""B"",
                >= 70 => ""C"",
                _ => ""F"",
            };
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExtensionMemberOnInterface()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    interface IEntity {
        int Id { get; }
        string Name { get; }
    }

    static class IEntityExtensions {
        extension(IEntity e) {
            [Projectable]
            public string Label => e.Id + "": "" + e.Name;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ExtensionMemberWithIsPatternExpression()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Value { get; set; }
    }

    static class EntityExtensions {
        extension(Entity e) {
            [Projectable]
            public bool IsHighValue => e.Value is > 100;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
    [Fact]
    public Task ExtensionMemberOnGenericType()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    class Entity {
        public int Value { get; set; }
    }

    class Wrapper<T> {
        public T Wrapped { get; set; }
    }

    static class EntityExtensions {
        extension(Wrapper<Entity> e) {
            [Projectable]
            public int TimesHundred => e.Wrapped.Value * 100;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
#endif
}

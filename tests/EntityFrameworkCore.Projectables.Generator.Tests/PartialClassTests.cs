using System.Linq;
using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public class PartialClassTests : ProjectionExpressionGeneratorTestsBase
{
    public PartialClassTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task PartialClass_SimpleMethod_GeneratesNestedCompanion()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class C {
        [Projectable]
        public int Foo() => 1;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task PartialClass_PrivateFieldAccess_CompanionCanAccessPrivateField()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class C {
        private int _secret = 42;

        [Projectable]
        public int GetSecret() => _secret;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task PartialClass_PrivateMethodCall_CompanionCanCallPrivateMethod()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class C {
        private int Helper() => 10;

        [Projectable]
        public int Compute() => Helper() + 1;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task PartialClass_NestedPartialType_TwoLevelShellWrap()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class Outer {
        public partial class Inner {
            [Projectable]
            public int Value() => 99;
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
    public Task PartialClass_Registry_UsesClrNestedTypeName()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class C {
        public int Id { get; set; }
        [Projectable]
        public int IdPlus1 => Id + 1;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.NotNull(result.RegistryTree);
        return Verifier.Verify(result.RegistryTree!.GetText(TestContext.Current.CancellationToken).ToString());
    }

    [Fact]
    public Task NonPartialClass_Unchanged()
    {
        // Non-partial class should still produce companion in Generated namespace
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Foo() => 1;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task PartialClass_MethodOverloads_EachGetsUniqueCompanionInsidePartialType()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class C {
        [Projectable]
        public int Compute(int x) => x + 1;

        [Projectable]
        public int Compute(string s) => s.Length;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedTrees.Length);

        // Both companions should be nested inside partial class C
        return Verifier.Verify(result.GeneratedTrees.Select(t => t.ToString()));
    }

    [Fact]
    public Task PartialClass_MethodOverloads_Registry_BothEntriesUseClrNestedTypeName()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public partial class C {
        [Projectable]
        public int Compute(int x) => x + 1;

        [Projectable]
        public int Compute(string s) => s.Length;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.NotNull(result.RegistryTree);
        return Verifier.Verify(result.RegistryTree!.GetText(TestContext.Current.CancellationToken).ToString());
    }
}

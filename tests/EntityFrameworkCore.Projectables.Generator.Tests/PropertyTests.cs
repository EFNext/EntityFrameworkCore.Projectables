using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

[UsesVerify]
public class PropertyTests : ProjectionExpressionGeneratorTestsBase
{
    public PropertyTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public void EmtpyCode_Noop()
    {
        var compilation = CreateCompilation(@"
class C { }
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.GeneratedTrees);
    }

    [Fact]
    public Task SimpleProjectableProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Foo => 1;
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task MinimalProjectableComputedProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable]
        public int Foo => Bar;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task SimpleProjectableComputedProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable]
        public int Foo => Bar + 1;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task SimpleProjectableComputedInNestedClassProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    public class C {
        public class D {
            public int Bar { get; set; }

            [Projectable]
            public int Foo => Bar + 1;
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
    public Task ProjectableComputedPropertyUsingThis()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable]
        public int Foo => this.Bar;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectableComputedPropertyMethod()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar() => 1;

        [Projectable]
        public int Foo => Bar();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectablePropertyWithExplicitExpressionGetter()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Foo { get => 1; }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectablePropertyWithExplicitBlockGetter()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable(AllowBlockBody = true)]
        public int Foo { get { return 1; } }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectableComputedPropertyWithExplicitExpressionGetter()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable]
        public int Foo { get => Bar + 1; }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectableComputedPropertyWithExplicitBlockGetter()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo { get { return Bar + 1; } }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectablePropertyWithExplicitBlockGetterUsingThis()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable(AllowBlockBody = true)]
        public int Foo { get { return this.Bar; } }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectablePropertyWithExplicitBlockGetterAndMethodCall()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar() => 1;

        [Projectable(AllowBlockBody = true)]
        public int Foo { get { return Bar(); } }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public void ProjectablePropertyWithExplicitBlockGetter_WithoutAllowBlockBody_EmitsWarning()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Foo { get { return 1; } }
    }
}
");

        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public Task MoreComplexProjectableComputedProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable]
        public int Foo => Bar + this.Bar + Bar;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectablePropertyToNavigationalProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class D { }
    
    class C {
        public System.Collections.Generic.List<D> Dees { get; set; }

        [Projectable]
        public D Foo => Dees.First();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NavigationProperties()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
using System.Collections.Generic;

namespace Projectables.Repro;

public class SomeEntity
{
    public int Id { get; set; }

    public SomeEntity Parent { get; set; }

    public ICollection<SomeEntity> Children { get; set; }

    [Projectable]
    public ICollection<SomeEntity> RootChildren =>
        Parent != null ? Parent.RootChildren : Children;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task FooOrBar()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
using System.Collections.Generic;

namespace Projectables.Repro;

public class SomeEntity
{
    public int Id { get; set; }

    public string Foo { get; set; }

    public string Bar { get; set; }

    [Projectable]
    public string FooOrBar =>
        Foo != null ? Foo : Bar;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task RelationalProperty()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foos {
    public class Foo {
        public int Id { get; set; }
    }

    public class Bar {
        public Foo Foo { get; set; }

        [Projectable]
        public int FooId => Foo.Id;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
    
    [Fact]
    public Task SimpleProjectableComputedPropertyWithSetter()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Bar { get; set; }

        [Projectable]
        public int Foo 
        { 
            get => Bar;
            set => Bar = value;
        }
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
}
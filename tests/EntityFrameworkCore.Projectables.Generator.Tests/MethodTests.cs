using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public class MethodTests : ProjectionExpressionGeneratorTestsBase
{
    public MethodTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task SimpleProjectableMethod()
    {
        var compilation = CreateCompilation(@"
using System;
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
    public Task ArgumentlessProjectableComputedMethod()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Foo() => 0;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectableComputedMethodWithSingleArgument()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Foo(int i) => i;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ProjectableComputedMethodWithMultipleArguments()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Foo(int a, string b, object d) => a;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task StaticMethodWithNoParameters()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

public static class Foo {
    [Projectable]
    public static int Zero() => 0;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task StaticMethodWithParameters()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

public static class Foo {
    [Projectable]
    public static int Zero(int x) => 0;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task StaticMembers()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public class Foo {
        public static int Bar { get; set; }

        public int Id { get; set; }
  
        [Projectable]
        public int IdWithBar() => Id + Bar;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task StaticMembers2()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public static class Constants {
        public static readonly int Bar  = 1;
    }

    public class Foo {
        public int Id { get; set; }
  
        [Projectable]
        public int IdWithBar() => Id + Constants.Bar;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ConstMember()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public class Foo {
        public const int Bar = 1;

        public int Id { get; set; }
  
        [Projectable]
        public int IdWithBar() => Id + Bar;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ConstMember2()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public static class Constants {
        public const int Bar  = 1;
    }

    public class Foo {
        public int Id { get; set; }
  
        [Projectable]
        public int IdWithBar() => Id + Constants.Bar;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ConstMember3()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public class Foo {
        public const int Bar = 1;

        public int Id { get; set; }
  
        [Projectable]
        public int IdWithBar() => Id + Foo.Bar;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task DefaultValuesGetRemoved()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

class Foo {
    [Projectable]
    public int Calculate(int i = 0) => i;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task ParamsModifiedGetsRemoved()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

class Foo {
    [Projectable]
    public int First(params int[] all) => all[0];
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task MethodOverloads_WithDifferentParameterTypes()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Method(int x) => x;
        
        [Projectable]
        public int Method(string s) => s.Length;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedTrees.Length);
            
        var generatedFiles = result.GeneratedTrees.Select(t => t.FilePath).ToList();
        Assert.Contains(generatedFiles, f => f.Contains("Method_P0_int.g.cs"));
        Assert.Contains(generatedFiles, f => f.Contains("Method_P0_string.g.cs"));

        return Verifier.Verify(result.GeneratedTrees.Select(t => t.ToString()));
    }

    [Fact]
    public Task MethodOverloads_WithDifferentParameterCounts()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Method(int x) => x;
        
        [Projectable]
        public int Method(int x, int y) => x + y;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedTrees.Length);
            
        var generatedFiles = result.GeneratedTrees.Select(t => t.FilePath).ToList();
        Assert.Contains(generatedFiles, f => f.Contains("Method_P0_int.g.cs"));
        Assert.Contains(generatedFiles, f => f.Contains("Method_P0_int_P1_int.g.cs"));

        return Verifier.Verify(result.GeneratedTrees.Select(t => t.ToString()));
    }

    [Fact]
    public Task InheritedMembers()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public class Foo {
        public int Id { get; set; }
    }

    public class Bar : Foo {
        [Projectable]
        public int ProjectedId => Id;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task BaseMemberExplicitReference()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Projectables.Repro;

class Base 
{
    public string Foo { get; set; }
}

class Derived : Base
{
    [Projectable]
    public string Bar => base.Foo;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task BaseMemberImplicitReference()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Projectables.Repro;

class Base 
{
    public string Foo { get; set; }
}

class Derived : Base
{
    [Projectable]
    public string Bar => Foo;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task BaseMethodExplicitReference()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Projectables.Repro;

class Base 
{
    public string Foo() => """";
}

class Derived : Base
{
    [Projectable]
    public string Bar => base.Foo();
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task BaseMethorImplicitReference()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Projectables.Repro;

class Base 
{
    public string Foo() => """";
}

class Derived : Base
{
    [Projectable]
    public string Bar => Foo();
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task IsOperator()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class A {
        [Projectable] 
        public bool IsB => this is B;
    }
    
    class B : A {
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task Cast()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace Projectables.Repro;

public class SuperEntity : SomeEntity
{
    public string Superpower { get; set; }
}

public class SomeEntity
{
    public int Id { get; set; }
}

public static class SomeExtensions
{
    [Projectable]
    public static string AsSomeResult(this SomeEntity e) => ((SuperEntity)e).Superpower;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task EnumAccessor()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

public enum SomeFlag
{
    Foo
}

public static class SomeExtensions
{
    [Projectable]
    public static bool Test(this SomeFlag f) => f == SomeFlag.Foo;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task StringInterpolationWithStaticCall_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class MyExtensions {
        public static string ToDateString(this DateTime date) => date.ToString(""dd/MM/yyyy"");
    }

    class C {
        public DateTime? ValidationDate { get; set; }

        [Projectable]
        public string Status => ValidationDate != null ? $""Validation date : ({ValidationDate.Value.ToDateString()})"" : """";
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task StringInterpolationWithParenthesis_NoParenthesisAdded()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class MyExtensions {
        public static string ToDateString(this DateTime date) => date.ToString(""dd/MM/yyyy"");
    }

    class C {
        public DateTime? ValidationDate { get; set; }

        [Projectable]
        public string Status => ValidationDate != null ? $""Validation date : ({(ValidationDate.Value.ToDateString())})"" : """";
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task TypesInBodyGetsFullyQualified()
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
        public int Foo => Dees.OfType<D>().Count();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task DeclarationTypeNamesAreGettingFullyQualified()
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

            [Projectable]
            public static Entity Something(Entity entity)
                => entity;
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
    public Task MixPrimaryConstructorAndProperties()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public static class EntityExtensions
    {
        public record Entity(int Id)
        {
            public int Id { get; set; }
            public string? FullName { get; set; }

            [Projectable]
            public static Entity Something(Entity entity)
                => new Entity(entity.Id) {
                    FullName = entity.FullName
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
    public Task RequiredNamespace()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

namespace One {
    static class IntExtensions {
        public static int AddOne(this int i) => i + 1;    
    }
}

namespace One.Two {
    class Bar {
        [Projectable]
        public int Method() => 1.AddOne();
    }   
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
}
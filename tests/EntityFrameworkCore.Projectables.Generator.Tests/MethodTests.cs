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
    public void LongParameterMethod_HintNameIsBounded()
    {
        // A deeply-qualified nested generic parameter makes the generated class name — and, historically,
        // the file name — exceed ~150 characters, which overflows path limits and crashes Visual Studio
        // when browsing generated files. The hint name must be shortened well below that.
        var compilation = CreateCompilation(@"
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;
namespace Some.Deep.Namespace.For.The.Test {
    class Alpha {}
    class Beta {}
    class Container {
        [Projectable]
        public int Combine(Dictionary<Alpha, List<Beta>> values) => values.Count;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        var hintName = result.GeneratedTrees[0].FilePath.Split('/', '\\')[^1];

        // Bounded length (readable head + deterministic hash + ".g.cs") while staying recognizable via its
        // namespace/member head. The un-shortened base name here is well over 150 characters.
        Assert.EndsWith(".g.cs", hintName);
        Assert.True(hintName.Length <= 69, $"Hint name too long ({hintName.Length}): {hintName}");
        Assert.StartsWith("Some_Deep_", hintName);
    }

    [Fact]
    public void LongOverloads_HintNamesAreUnique()
    {
        // Two overloads share the same class/member (identical readable prefix) but differ only in their
        // long parameter types. The appended hash must keep the two hint names distinct, otherwise Roslyn
        // rejects the duplicate hint name.
        var compilation = CreateCompilation(@"
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Combine(Dictionary<string, List<int>> values) => values.Count;

        [Projectable]
        public int Combine(Dictionary<string, List<long>> values) => values.Count;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        // Two trees (rather than a thrown duplicate-hint-name error) confirms the names disambiguated.
        Assert.Equal(2, result.GeneratedTrees.Length);

        var hintNames = result.GeneratedTrees.Select(t => t.FilePath.Split('/', '\\')[^1]).ToList();

        Assert.Equal(2, hintNames.Distinct().Count());
        Assert.All(hintNames, h => Assert.True(h.Length <= 69, $"Hint name too long ({h.Length}): {h}"));
        Assert.All(hintNames, h => Assert.StartsWith("Foo_C_Combine_", h));
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
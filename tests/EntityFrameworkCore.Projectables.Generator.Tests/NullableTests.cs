using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

[UsesVerify]
public class NullableTests : ProjectionExpressionGeneratorTestsBase
{
    public NullableTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task NullableReferenceTypesAreBeingEliminated()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

#nullable enable

namespace Foo {
    static class C {
        [Projectable]
        public static object? NextFoo(this object? unusedArgument, int? nullablePrimitiveArgument) => null;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task GenericNullableReferenceTypesAreBeingEliminated()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Collections.Generic;
using System.Linq;
using EntityFrameworkCore.Projectables;

#nullable enable

namespace Foo {
    static class C {
        [Projectable]
        public static List<object?> NextFoo(this List<object?> input, List<int?> nullablePrimitiveArgument) => input;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableReferenceTypeCastOperatorGetsEliminated()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Collections.Generic;
using System.Linq;
using EntityFrameworkCore.Projectables;

#nullable enable

namespace Foo {
    static class C {
        [Projectable]
        public static string? NullableReferenceType(object? input) => (string?)input;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableValueCastOperatorsPersist()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Collections.Generic;
using System.Linq;
using EntityFrameworkCore.Projectables;

#nullable enable

namespace Foo {
    static class C {
        [Projectable]        
        public static int? NullableValueType(object? input) => (int?)input;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public void NullableMemberBinding_WithoutSupport_IsBeingReported()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.None)]
        public static int? GetLength(this string input) => input?.Length;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0002", diagnostic.Id);
    }

    [Fact]
    public void NullableMemberBinding_UndefinedSupport_IsBeingReported()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class C {
        [Projectable]
        public static int? GetLength(this string input) => input?.Length;
    }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0002", diagnostic.Id);
    }

    [Fact]
    public void MultiLevelNullableMemberBinding_UndefinedSupport_IsBeingReported()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
        public record Address
        {
            public int Id { get; set; }
            public string? Country { get; set; }
        }

        public record Party
        {
            public int Id { get; set; }

            public Address? Address { get; set; }
        }

        public record Entity
        {
            public int Id { get; set; }

            public Party? Left { get; set; }
            public Party? Right { get; set; }

            [Projectable]
            public bool IsSameCountry => Left?.Address?.Country == Right?.Address?.Country;
        }
}
");
        var result = RunGenerator(compilation);

        Assert.All(result.Diagnostics, diagnostic => {
            Assert.Equal("EFP0002", diagnostic.Id);
        });
    }

    [Fact]
    public Task NullableMemberBinding_WithIgnoreSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public static int? GetLength(this string input) => input?.Length;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableMemberBinding_WithRewriteSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public static int? GetLength(this string input) => input?.Length;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableSimpleElementBinding_WithIgnoreSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public static char? GetFirst(this string input) => input?[0];
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableSimpleElementBinding_WithRewriteSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public static char? GetFirst(this string input) => input?[0];
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task BooleanSimpleTernary_WithRewriteSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public static bool Test(this object? x) => x?.Equals(4) == false;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableElementBinding_WithIgnoreSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public static string? GetFirst(this string input) => input?[0].ToString();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableElementBinding_WithRewriteSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using EntityFrameworkCore.Projectables;

namespace Foo {
    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public static string? GetFirst(this string input) => input?[0].ToString();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableElementAndMemberBinding_WithIgnoreSupport_IsBeingRewritten()
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
            public List<Entity>? RelatedEntities { get; set; }
        }

        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public static Entity GetFirstRelatedIgnoreNulls(this Entity entity)
            => entity?.RelatedEntities?[0];
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableElementAndMemberBinding_WithRewriteSupport_IsBeingRewritten()
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
            public List<Entity>? RelatedEntities { get; set; }
        }

        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public static Entity GetFirstRelatedIgnoreNulls(this Entity entity)
            => entity?.RelatedEntities?[0];
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableParameters_WithRewriteSupport_IsBeingRewritten()
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
        public static string GetFirstName(this Entity entity)
            => entity.FullName?.Substring(entity.FullName?.IndexOf(' ') ?? 0);
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullConditionalNullCoalesceTypeConversion()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

class Foo {
    public int? FancyNumber { get; set; }

    [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
    public static int SomeNumber(Foo fancyClass) => fancyClass?.FancyNumber ?? 3;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableValueType_MemberAccess_WithRewriteSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public struct Point {
        public double X { get; set; }
        public double Y { get; set; }
    }

    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public static double? GetX(this Point? point) => point?.X;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableValueType_MemberAccess_WithIgnoreSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public struct Point {
        public double X { get; set; }
        public double Y { get; set; }
    }

    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
        public static double? GetX(this Point? point) => point?.X;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task NullableValueType_MemberAccessWithCoalesce_WithRewriteSupport_IsBeingRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public struct Point {
        public double X { get; set; }
        public double Y { get; set; }
    }

    static class C {
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
        public static double GetX(this Point? point) => point?.X ?? 0.0;
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
}
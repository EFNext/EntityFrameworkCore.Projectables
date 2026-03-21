using System.Threading.Tasks;
using VerifyXunit;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public class GenericTests : ProjectionExpressionGeneratorTestsBase
{
    public GenericTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task GenericMethods_AreRewritten()
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

        [Projectable]
        public static string EnforceString<T>(T value) where T : unmanaged
            => value.ToString();
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task GenericClassesWithContraints_AreRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public class TypedObject<TEnum> where TEnum : struct, System.Enum
    {
        public TEnum SomeProp { get; set; }
    }

    public abstract class Entity<T, TEnum>where T : TypedObject<TEnum> where TEnum : struct, System.Enum 
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public T SomeSubobject { get; set; }

        [Projectable]
        public string FullName => $""{FirstName} {LastName} {SomeSubobject.SomeProp}"";
    }
}
");
        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task GenericClassesWithTypeContraints_AreRewritten()
    {
        var compilation = CreateCompilation(@"
using System;
using System.Linq;
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

namespace Foo {
    public abstract class Entity<T> where T : notnull
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public T SomeSubobject { get; set; }

        [Projectable]
        public string FullName => $""{FirstName} {LastName} {SomeSubobject}"";
    }
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task GenericTypes()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

class EntiyBase<TId> {
    [Projectable]
    public static TId GetId() => default;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }

    [Fact]
    public Task GenericTypesWithConstraints()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

class EntityBase<TId> where TId : ICloneable, new() {
    [Projectable]
    public static TId GetId() => default;
}
");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
}
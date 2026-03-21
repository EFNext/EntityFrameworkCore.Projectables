using Microsoft.CodeAnalysis;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

/// <summary>
/// Tests that C# 12–14 syntax nodes that cannot be represented in expression trees cause the
/// generator to emit a clear EFP0013 diagnostic instead of silently producing broken generated code.
/// </summary>
public class UnsupportedExpressionTests : ProjectionExpressionGeneratorTestsBase
{
    public UnsupportedExpressionTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public void CollectionExpression_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using System.Collections.Generic;
using EntityFrameworkCore.Projectables;

class Entity
{
    [Projectable]
    public List<int> Ids => [1, 2, 3];
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void IndexFromEndOperator_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

class Entity
{
    public int[] Items { get; set; }

    [Projectable]
    public int Last => Items[^1];
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void RangeOperator_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

class Entity
{
    public int[] Items { get; set; }

    [Projectable]
    public int[] Slice => Items[1..3];
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void PrimaryConstructorParameter_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

class Entity(int id)
{
    [Projectable]
    public int DoubledId => id * 2;
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

#if NET9_0_OR_GREATER
    [Fact]
    public void RefStructParameter_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;

static class Extensions
{
    [Projectable]
    public static int Sum(params ReadOnlySpan<int> values) => 0;
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }
#endif

#if NET10_0_OR_GREATER
    [Fact]
    public void FieldKeyword_EmitsDiagnostic()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;

class Entity
{
    [Projectable]
    public string Name { get => field ?? ""default""; set; }
}
");
        var result = RunGenerator(compilation);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0013", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }
#endif
}

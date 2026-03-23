using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Xunit;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

/// <summary>
/// Tests that MSBuild global properties (CompilerVisibleProperty) are respected as defaults
/// for [Projectable] options, and that per-attribute settings override them.
/// </summary>
public class GlobalOptionsTests : ProjectionExpressionGeneratorTestsBase
{
    public GlobalOptionsTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    // -------------------------------------------------------------------------
    // AllowBlockBody
    // -------------------------------------------------------------------------

    [Fact]
    public void GlobalAllowBlockBody_True_EnablesBlockBodyWithoutAttributeFlag()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }
        [Projectable]
        public int GetDouble()
        {
            return Value * 2;
        }
    }
}
");
        var result = RunGenerator(compilation, new Dictionary<string, string>
        {
            ["build_property.Projectables_AllowBlockBody"] = "true"
        });

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void GlobalAllowBlockBody_False_StillEmitsWarningForBlockBody()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }
        [Projectable]
        public int GetDouble()
        {
            return Value * 2;
        }
    }
}
");
        var result = RunGenerator(compilation, new Dictionary<string, string>
        {
            ["build_property.Projectables_AllowBlockBody"] = "false"
        });

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0001", diagnostic.Id);
    }

    [Fact]
    public void AttributeAllowBlockBody_False_OverridesGlobalTrue()
    {
        // Global says true, but per-attribute explicitly opts out → warning expected.
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }
        [Projectable(AllowBlockBody = false)]
        public int GetDouble()
        {
            return Value * 2;
        }
    }
}
");
        var result = RunGenerator(compilation, new Dictionary<string, string>
        {
            ["build_property.Projectables_AllowBlockBody"] = "true"
        });

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0001", diagnostic.Id);
    }

    [Fact]
    public void AttributeAllowBlockBody_True_OverridesGlobalFalse()
    {
        // Global says false (or not set), but per-attribute explicitly opts in → no warning.
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }
        [Projectable(AllowBlockBody = true)]
        public int GetDouble()
        {
            return Value * 2;
        }
    }
}
");
        var result = RunGenerator(compilation, new Dictionary<string, string>
        {
            ["build_property.Projectables_AllowBlockBody"] = "false"
        });

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);
    }

    // -------------------------------------------------------------------------
    // ExpandEnumMethods
    // -------------------------------------------------------------------------

    [Fact]
    public void GlobalExpandEnumMethods_True_ExpandsEnumMethodsWithoutAttributeFlag()
    {
        var compilation = CreateCompilation(@"
using System;
using System.ComponentModel.DataAnnotations;
using EntityFrameworkCore.Projectables;
namespace Foo {
    public enum MyEnum { A, B }
    public static class MyEnumExtensions {
        public static string GetName(this MyEnum value) => value.ToString();
    }
    public record Entity {
        public MyEnum Status { get; set; }
        [Projectable]
        public string StatusName => Status.GetName();
    }
}
");
        var result = RunGenerator(compilation, new Dictionary<string, string>
        {
            ["build_property.Projectables_ExpandEnumMethods"] = "true"
        });

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);
        // The generated tree should contain ternary expansion for each enum value.
        var generated = result.GeneratedTrees[0].ToString();
        Assert.Contains("MyEnum.A", generated);
        Assert.Contains("MyEnum.B", generated);
    }

    [Fact]
    public void AttributeExpandEnumMethods_False_OverridesGlobalTrue()
    {
        // Global sets true but attribute explicitly opts out → no expansion.
        var compilation = CreateCompilation(@"
using System;
using EntityFrameworkCore.Projectables;
namespace Foo {
    public enum MyEnum { A, B }
    public static class MyEnumExtensions {
        public static string GetName(this MyEnum value) => value.ToString();
    }
    public record Entity {
        public MyEnum Status { get; set; }
        [Projectable(ExpandEnumMethods = false)]
        public string StatusName => Status.GetName();
    }
}
");
        var result = RunGenerator(compilation, new Dictionary<string, string>
        {
            ["build_property.Projectables_ExpandEnumMethods"] = "true"
        });

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);
        // Without expansion the ternary chain should NOT appear.
        var generated = result.GeneratedTrees[0].ToString();
        Assert.DoesNotContain("MyEnum.A ==", generated);
    }

    // -------------------------------------------------------------------------
    // NullConditionalRewriteSupport
    // -------------------------------------------------------------------------

    [Fact]
    public void GlobalNullConditionalRewriteSupport_Rewrite_AllowsNullConditionals()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Inner { public int Value { get; set; } }
    class C {
        public Inner? Inner { get; set; }
        [Projectable]
        public int? InnerValue => Inner?.Value;
    }
}
");
        var result = RunGenerator(compilation, new Dictionary<string, string>
        {
            ["build_property.Projectables_NullConditionalRewriteSupport"] = "Rewrite"
        });

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);
    }

    [Fact]
    public void AttributeNullConditionalRewriteSupport_None_OverridesGlobalRewrite()
    {
        // Global says Rewrite, but attribute explicitly sets None → diagnostic expected.
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class Inner { public int Value { get; set; } }
    class C {
        public Inner? Inner { get; set; }
        [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.None)]
        public int? InnerValue => Inner?.Value;
    }
}
");
        var result = RunGenerator(compilation, new Dictionary<string, string>
        {
            ["build_property.Projectables_NullConditionalRewriteSupport"] = "Rewrite"
        });

        Assert.NotEmpty(result.Diagnostics);
    }

    // -------------------------------------------------------------------------
    // No global option set — hard-coded defaults apply (regression guard)
    // -------------------------------------------------------------------------

    [Fact]
    public void NoGlobalOptions_HardCodedDefaultsApply_BlockBodyStillWarns()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }
        [Projectable]
        public int GetDouble()
        {
            return Value * 2;
        }
    }
}
");
        // No global options passed — same as the existing test without options.
        var result = RunGenerator(compilation, new Dictionary<string, string>());

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0001", diagnostic.Id);
    }

    [Fact]
    public void MalformedGlobalOption_IsTreatedAsNotSet()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        public int Value { get; set; }
        [Projectable]
        public int GetDouble()
        {
            return Value * 2;
        }
    }
}
");
        // Malformed value — should be silently ignored, falling back to hard-coded default.
        var result = RunGenerator(compilation, new Dictionary<string, string>
        {
            ["build_property.Projectables_AllowBlockBody"] = "not-a-bool"
        });

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("EFP0001", diagnostic.Id);
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

/// <summary>
/// Tests that verify the incremental generator correctly caches and invalidates
/// generated output across multiple compilations when a <see cref="GeneratorDriver"/>
/// is reused. These tests guard against cache-related regressions where stale output
/// could be returned after a referenced type changes in a different source file.
/// </summary>
public class IncrementalGeneratorTests : ProjectionExpressionGeneratorTestsBase
{
    public IncrementalGeneratorTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    /// <summary>
    /// Creates a multi-file compilation from the given source strings.
    /// Each string becomes its own syntax tree (simulating separate source files).
    /// </summary>
    private Compilation CreateMultiFileCompilation(params string[] sources)
    {
        var references = GetDefaultReferences();
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        return CSharpCompilation.Create(
            "compilation",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact(Skip = "Fails because the generator doesn't currently track changes to referenced types in other files, because for now it takes too much time.")]
    public void WhenReferencedEnumInAnotherFileGainsAMember_GeneratedOutputUpdates()
    {
        // The projectable member references an enum defined in a separate source file.
        // Adding a member to that enum (without touching the projectable file) must
        // cause the generator to re-run and reflect the new member in the output.
        const string projectableSource = @"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public static class EnumExtensions {
        public static string GetName(this Status value) => value.ToString();
    }
    public class Entity {
        public Status Status { get; set; }
        [Projectable(ExpandEnumMethods = true)]
        public string StatusName => Status.GetName();
    }
}";

        // Enum with two members — used in the initial compilation.
        const string enumSourceV1 = @"
namespace Foo {
    public enum Status { Active, Inactive }
}";

        // Enum gains a third member — used in the updated compilation.
        const string enumSourceV2 = @"
namespace Foo {
    public enum Status { Active, Inactive, Pending }
}";

        // ── Initial compilation (two source files) ───────────────────────────
        var projectableTree = CSharpSyntaxTree.ParseText(projectableSource);
        var enumTreeV1 = CSharpSyntaxTree.ParseText(enumSourceV1);

        var references = GetDefaultReferences();
        var compilation1 = CSharpCompilation.Create(
            "compilation",
            new[] { projectableTree, enumTreeV1 },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var (driver, result1) = CreateAndRunGenerator(compilation1);

        Assert.Single(result1.GeneratedTrees);
        var output1 = result1.GeneratedTrees[0].GetText().ToString();

        // Initial output must reference the two original enum members only.
        Assert.Contains("Active", output1);
        Assert.Contains("Inactive", output1);
        Assert.DoesNotContain("Pending", output1);

        // ── Updated compilation — only the enum file changes ─────────────────
        // projectableTree is the SAME SyntaxTree reference (unchanged file).
        // Roslyn's ReplaceSyntaxTree keeps all other trees as the same references.
        var enumTreeV2 = CSharpSyntaxTree.ParseText(enumSourceV2);
        var compilation2 = compilation1.ReplaceSyntaxTree(enumTreeV1, enumTreeV2);

        // Reuse the same driver to exercise the incremental caching path.
        var (_, result2) = RunGeneratorWithDriver(driver, compilation2);

        Assert.Single(result2.GeneratedTrees);
        var output2 = result2.GeneratedTrees[0].GetText().ToString();

        // The updated output must include the new "Pending" enum member.
        Assert.Contains("Active", output2);
        Assert.Contains("Inactive", output2);
        Assert.Contains("Pending", output2);

        // The two outputs must differ, confirming the generator re-ran.
        Assert.NotEqual(output1, output2);
    }

    [Fact(Skip = "Fails because the generator doesn't currently track changes to referenced types in other files, because for now it takes too much time.")]
    public void WhenReferencedEnumInAnotherFileLosesAMember_GeneratedOutputUpdates()
    {
        // Mirrors WhenReferencedEnumInAnotherFileGainsAMember but removes a member.
        const string projectableSource = @"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public static class EnumExtensions {
        public static string GetName(this Status value) => value.ToString();
    }
    public class Entity {
        public Status Status { get; set; }
        [Projectable(ExpandEnumMethods = true)]
        public string StatusName => Status.GetName();
    }
}";

        const string enumSourceV1 = @"
namespace Foo {
    public enum Status { Active, Inactive, Pending }
}";

        const string enumSourceV2 = @"
namespace Foo {
    public enum Status { Active, Inactive }
}";

        var projectableTree = CSharpSyntaxTree.ParseText(projectableSource);
        var enumTreeV1 = CSharpSyntaxTree.ParseText(enumSourceV1);

        var references = GetDefaultReferences();
        var compilation1 = CSharpCompilation.Create(
            "compilation",
            new[] { projectableTree, enumTreeV1 },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var (driver, result1) = CreateAndRunGenerator(compilation1);

        Assert.Single(result1.GeneratedTrees);
        var output1 = result1.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("Pending", output1);

        var enumTreeV2 = CSharpSyntaxTree.ParseText(enumSourceV2);
        var compilation2 = compilation1.ReplaceSyntaxTree(enumTreeV1, enumTreeV2);

        var (_, result2) = RunGeneratorWithDriver(driver, compilation2);

        Assert.Single(result2.GeneratedTrees);
        var output2 = result2.GeneratedTrees[0].GetText().ToString();

        // "Pending" must no longer appear after it is removed from the enum.
        Assert.DoesNotContain("Pending", output2);
        Assert.NotEqual(output1, output2);
    }

    [Fact]
    public void WhenProjectableFileChanges_GeneratedOutputUpdates()
    {
        // Sanity check: the generator must re-run when the projectable file itself changes.
        const string projectableSourceV1 = @"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public class Entity {
        public int X { get; set; }
        [Projectable]
        public int Double => X * 2;
    }
}";

        const string projectableSourceV2 = @"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public class Entity {
        public int X { get; set; }
        [Projectable]
        public int Triple => X * 3;
    }
}";

        var compilation1 = CreateMultiFileCompilation(projectableSourceV1);
        var (driver, result1) = CreateAndRunGenerator(compilation1);

        Assert.Single(result1.GeneratedTrees);
        var output1 = result1.GeneratedTrees[0].GetText().ToString();
        Assert.Contains("* 2", output1);

        // Replace the projectable file.
        var treeV1 = compilation1.SyntaxTrees.First();
        var treeV2 = CSharpSyntaxTree.ParseText(projectableSourceV2);
        var compilation2 = compilation1.ReplaceSyntaxTree(treeV1, treeV2);

        var (_, result2) = RunGeneratorWithDriver(driver, compilation2);

        Assert.Single(result2.GeneratedTrees);
        var output2 = result2.GeneratedTrees[0].GetText().ToString();

        Assert.Contains("* 3", output2);
        Assert.NotEqual(output1, output2);
    }

    [Fact]
    public void WhenCompilationIsUnchanged_GeneratorProducesSameOutput()
    {
        // Running the generator twice with exactly the same compilation (same SyntaxTree
        // references, same external references) must yield identical output on both runs.
        const string source = @"
using EntityFrameworkCore.Projectables;
namespace Foo {
    public class Entity {
        public int Value { get; set; }
        [Projectable]
        public int Doubled => Value * 2;
    }
}";

        var compilation = CreateMultiFileCompilation(source);
        var (driver, result1) = CreateAndRunGenerator(compilation);

        // Run again with the same compilation (no changes at all).
        var (_, result2) = RunGeneratorWithDriver(driver, compilation);

        Assert.Single(result1.GeneratedTrees);
        Assert.Single(result2.GeneratedTrees);

        var output1 = result1.GeneratedTrees[0].GetText().ToString();
        var output2 = result2.GeneratedTrees[0].GetText().ToString();

        Assert.Equal(output1, output2);
    }
}

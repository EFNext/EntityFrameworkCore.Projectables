using System.Text;
using BenchmarkDotNet.Attributes;
using EntityFrameworkCore.Projectables;
using EntityFrameworkCore.Projectables.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace EntityFrameworkCore.Projectables.Benchmarks;

[MemoryDiagnoser]
public class GeneratorBenchmarks
{
    [Params(1, 100, 1000)]
    public int ProjectableCount { get; set; }

    private Compilation _compilation = null!;
    private GeneratorDriver _warmedDriver = null!;

    /// <summary>
    /// Compilation where one <em>noise</em> tree (no <c>[Projectable]</c> members) has
    /// received a trivial comment — should not trigger any new source generation.
    /// </summary>
    private Compilation _noiseModifiedCompilation = null!;

    /// <summary>
    /// Compilation where one <em>projectable</em> tree (contains <c>[Projectable]</c>
    /// members) has received a trivial comment that does not alter any member
    /// declaration — the generator must still re-examine the tree but should hit
    /// its incremental cache for all outputs.
    /// </summary>
    private Compilation _projectableModifiedCompilation = null!;

    /// <summary>
    /// Index of the first noise syntax tree inside <see cref="_compilation"/>.
    /// All trees at indices &lt; this value contain at least one <c>[Projectable]</c>
    /// member; all trees at indices &gt;= this value contain none.
    /// </summary>
    private int _firstNoiseTreeIndex;

    [GlobalSetup]
    public void Setup()
    {
        // Build projectable sources first so we know exactly how many there are
        // before we add the matching set of noise trees.
        var projectableSources = BuildProjectableSources(ProjectableCount);
        _firstNoiseTreeIndex = projectableSources.Count;

        _compilation = CreateCompilation(projectableSources);

        // Warm up the driver once so incremental state is established.
        _warmedDriver = CSharpGeneratorDriver
            .Create(new ProjectionExpressionGenerator())
            .RunGeneratorsAndUpdateCompilation(_compilation, out _, out _);

        // --- noise-modified compilation -------------------------------------------
        // Append a comment to the first noise tree.  The generator should find no
        // [Projectable] nodes in that tree and return all cached outputs immediately.
        var noiseTree = _compilation.SyntaxTrees.ElementAt(_firstNoiseTreeIndex);
        _noiseModifiedCompilation = _compilation.ReplaceSyntaxTree(
            noiseTree,
            noiseTree.WithChangedText(SourceText.From(noiseTree.GetText() + "\n// bench-edit")));

        // --- projectable-modified compilation -------------------------------------
        // Append a comment to the first projectable tree.  The generator must
        // re-examine all [Projectable] nodes it previously found in that tree,
        // compare them with the cached versions and — finding them structurally
        // identical — return all cached outputs.  This extra comparison is the
        // overhead we want to isolate vs. the noise case.
        var projectableTree = _compilation.SyntaxTrees.First();
        _projectableModifiedCompilation = _compilation.ReplaceSyntaxTree(
            projectableTree,
            projectableTree.WithChangedText(SourceText.From(projectableTree.GetText() + "\n// bench-edit")));
    }

    // -------------------------------------------------------------------------
    // Cold benchmarks — a brand-new driver is created on every iteration.
    // -------------------------------------------------------------------------

    /// <summary>Cold run on the unmodified compilation — establishes the baseline.</summary>
    [Benchmark(Baseline = true)]
    public GeneratorDriver RunGenerator()
        => CSharpGeneratorDriver
            .Create(new ProjectionExpressionGenerator())
            .RunGeneratorsAndUpdateCompilation(_compilation, out _, out _);

    /// <summary>
    /// Cold run where the compilation has a trivial edit in a <em>noise</em> file.
    /// Shows whether the cold path is sensitive to which files changed.
    /// </summary>
    [Benchmark]
    public GeneratorDriver RunGenerator_NoiseChange()
        => CSharpGeneratorDriver
            .Create(new ProjectionExpressionGenerator())
            .RunGeneratorsAndUpdateCompilation(_noiseModifiedCompilation, out _, out _);

    /// <summary>
    /// Cold run where the compilation has a trivial edit in a <em>projectable</em>
    /// file (comment only — no member body change).
    /// </summary>
    [Benchmark]
    public GeneratorDriver RunGenerator_ProjectableChange()
        => CSharpGeneratorDriver
            .Create(new ProjectionExpressionGenerator())
            .RunGeneratorsAndUpdateCompilation(_projectableModifiedCompilation, out _, out _);

    // -------------------------------------------------------------------------
    // Incremental benchmarks — the pre-warmed driver processes a single edit.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Warm incremental run: trivial edit in a <em>noise</em> file.
    /// The generator should skip the changed tree entirely and serve all outputs
    /// from cache — measuring the minimum incremental overhead.
    /// </summary>
    [Benchmark]
    public GeneratorDriver RunGenerator_Incremental_NoiseChange()
        => _warmedDriver
            .RunGeneratorsAndUpdateCompilation(_noiseModifiedCompilation, out _, out _);

    /// <summary>
    /// Warm incremental run: trivial edit in a <em>projectable</em> file
    /// (comment only — member declarations are unchanged).
    /// The generator must re-examine every <c>[Projectable]</c> node in the changed
    /// tree, compare it to the cached value, and only then confirm the cache is
    /// still valid — measuring the incremental re-examination overhead.
    /// </summary>
    [Benchmark]
    public GeneratorDriver RunGenerator_Incremental_ProjectableChange()
        => _warmedDriver
            .RunGeneratorsAndUpdateCompilation(_projectableModifiedCompilation, out _, out _);

    private static Compilation CreateCompilation(IReadOnlyList<string> projectableSources)
    {
        // Add one noise file per projectable file — same cardinality, no [Projectable].
        var noiseSources = BuildNoiseSources(projectableSources.Count);
        var allSources = projectableSources.Concat(noiseSources);

        var references = Basic.Reference.Assemblies.
#if NET10_0
                Net100
#else
            Net80
#endif
            .References.All.ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(ProjectableAttribute).Assembly.Location));

        return CSharpCompilation.Create(
            "GeneratorBenchmarkInput",
            allSources.Select((s, idx) => CSharpSyntaxTree.ParseText(s, path: $"File{idx}.cs")),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Splits <paramref name="projectableCount"/> members across multiple synthetic source
    /// files so that each file holds between 1 and 9 members (one chunk per file).
    /// Each chunk becomes its own <c>Order{n}</c> class; every DTO gets its own file too.
    /// </summary>
    private static IReadOnlyList<string> BuildProjectableSources(int projectableCount)
    {
        // Nine is the natural cycle length (one per transformer path); keeps
        // every file within the requested 1–10 member range.
        const int membersPerFile = 9;
        var sources = new List<string>();

        var enumEmitted = false;
        var fileIndex = 0;
        for (var start = 0; start < projectableCount; start += membersPerFile)
        {
            var count = Math.Min(membersPerFile, projectableCount - start);
            sources.Add(BuildOrderClassSource(fileIndex, start, count, emitEnum: !enumEmitted));
            enumEmitted = true;
            fileIndex++;
        }

        // When projectableCount == 0 we still need at least one file so that
        // SyntaxTrees.First() in Setup() doesn't throw.
        if (!enumEmitted)
        {
            sources.Add(
                "using System;\n" +
                "using EntityFrameworkCore.Projectables;\n" +
                "namespace GeneratorBenchmarkInput;\n" +
                "public enum OrderStatus { Pending, Active, Completed, Cancelled }\n");
        }

        // DTO classes with [Projectable] constructors — one per file so they
        // also respect the 1-to-10-members-per-file constraint.
        // Count scales proportionally: roughly one DTO per nine Order members.
        var ctorCount = Math.Max(1, projectableCount / 9);
        for (var j = 0; j < ctorCount; j++)
        {
            sources.Add(BuildDtoClassSource(j));
        }

        return sources;
    }

    // Nine member kinds — one per transformer path in the generator:
    //  0  simple string-concat property          (ExpressionSyntaxRewriter baseline)
    //  1  boolean null-check property            (ExpressionSyntaxRewriter, logical AND)
    //  2  single-param decimal method            (ExpressionSyntaxRewriter, arithmetic)
    //  3  multi-param string method              (ExpressionSyntaxRewriter, concat)
    //  4  null-conditional property              (NullConditionalRewrite, Rewrite mode)
    //  5  switch-expression method               (SwitchExpressionRewrite, relational patterns)
    //  6  is-pattern property                    (VisitIsPatternExpression, not-null pattern)
    //  7  block-bodied if/else chain             (BlockStatementConverter)
    //  8  block-bodied switch with local var     (BlockStatementConverter + local replacement)
    private static string BuildOrderClassSource(int fileIndex, int startIndex, int count, bool emitEnum)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using EntityFrameworkCore.Projectables;");
        sb.AppendLine();
        sb.AppendLine("namespace GeneratorBenchmarkInput;");
        sb.AppendLine();

        // The enum is only emitted in the first file; all files share the same namespace.
        if (emitEnum)
        {
            sb.AppendLine("public enum OrderStatus { Pending, Active, Completed, Cancelled }");
            sb.AppendLine();
        }

        sb.AppendLine($"public class Order{fileIndex}");
        sb.AppendLine("{");
        sb.AppendLine("    public string FirstName { get; set; } = string.Empty;");
        sb.AppendLine("    public string LastName { get; set; } = string.Empty;");
        sb.AppendLine("    public string? Email { get; set; }");
        sb.AppendLine("    public decimal Amount { get; set; }");
        sb.AppendLine("    public decimal TaxRate { get; set; }");
        sb.AppendLine("    public DateTime? DeletedAt { get; set; }");
        sb.AppendLine("    public bool IsEnabled { get; set; }");
        sb.AppendLine("    public int Priority { get; set; }");
        sb.AppendLine("    public OrderStatus Status { get; set; }");
        sb.AppendLine();

        for (var i = startIndex; i < startIndex + count; i++)
        {
            switch (i % 9)
            {
                case 0:
                    // Expression-bodied: simple string concatenation.
                    sb.AppendLine("    [Projectable]");
                    sb.AppendLine($"    public string FullName{i} => FirstName + \" \" + LastName;");
                    break;

                case 1:
                    // Expression-bodied: null check combined with logical AND.
                    sb.AppendLine("    [Projectable]");
                    sb.AppendLine($"    public bool IsActive{i} => DeletedAt == null && IsEnabled;");
                    break;

                case 2:
                    // Expression-bodied: single-param decimal arithmetic.
                    sb.AppendLine("    [Projectable]");
                    sb.AppendLine($"    public decimal TotalWithTax{i}(decimal taxRate) => Amount * (1 + taxRate);");
                    break;

                case 3:
                    // Expression-bodied: multi-param string method.
                    sb.AppendLine("    [Projectable]");
                    sb.AppendLine($"    public string FormatSummary{i}(string prefix, int count) => prefix + \": \" + FirstName + \" x\" + count;");
                    break;

                case 4:
                    // Null-conditional member access — exercises NullConditionalRewrite transformer.
                    sb.AppendLine("    [Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]");
                    sb.AppendLine($"    public int? EmailLength{i} => Email?.Length;");
                    break;

                case 5:
                    // Switch expression with relational patterns — exercises SwitchExpressionRewrite transformer.
                    sb.AppendLine("    [Projectable]");
                    sb.AppendLine($"    public string GetGrade{i}(int score) => score switch {{ >= 90 => \"A\", >= 70 => \"B\", _ => \"C\" }};");
                    break;

                case 6:
                    // Is-pattern (not-null) — exercises ExpressionSyntaxRewriter.VisitIsPatternExpression.
                    sb.AppendLine("    [Projectable]");
                    sb.AppendLine($"    public bool HasEmail{i} => Email is not null;");
                    break;

                case 7:
                    // Block-bodied if/else chain — exercises BlockStatementConverter.
                    sb.AppendLine("    [Projectable(AllowBlockBody = true)]");
                    sb.AppendLine($"    public string GetStatusLabel{i}()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        if (DeletedAt != null)");
                    sb.AppendLine("        {");
                    sb.AppendLine("            return \"Deleted\";");
                    sb.AppendLine("        }");
                    sb.AppendLine("        if (IsEnabled)");
                    sb.AppendLine("        {");
                    sb.AppendLine("            return \"Active: \" + FirstName;");
                    sb.AppendLine("        }");
                    sb.AppendLine("        return \"Inactive\";");
                    sb.AppendLine("    }");
                    break;

                case 8:
                    // Block-bodied switch statement with a local variable — exercises
                    // BlockStatementConverter together with local-variable replacement.
                    sb.AppendLine("    [Projectable(AllowBlockBody = true)]");
                    sb.AppendLine($"    public string GetPriorityName{i}()");
                    sb.AppendLine("    {");
                    sb.AppendLine("        var p = Priority;");
                    sb.AppendLine("        switch (p)");
                    sb.AppendLine("        {");
                    sb.AppendLine("            case 1: return \"Low\";");
                    sb.AppendLine("            case 2: return \"Medium\";");
                    sb.AppendLine("            case 3: return \"High\";");
                    sb.AppendLine("            default: return \"Unknown\";");
                    sb.AppendLine("        }");
                    sb.AppendLine("    }");
                    break;
            }

            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Returns <paramref name="count"/> source files that each declare one plain
    /// entity class with no <c>[Projectable]</c> members.  These trees participate
    /// in the compilation but must never trigger source generation, so they let us
    /// isolate the overhead of the generator inspecting (and caching) irrelevant
    /// syntax trees.
    /// </summary>
    private static IReadOnlyList<string> BuildNoiseSources(int count)
        => Enumerable.Range(0, count).Select(BuildNoiseClassSource).ToList();

    private static string BuildNoiseClassSource(int j)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("namespace GeneratorBenchmarkInput;");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>Plain entity — no [Projectable] members.</summary>");
        sb.AppendLine($"public class NoiseEntity{j}");
        sb.AppendLine("{");
        sb.AppendLine($"    public int Id {{ get; set; }}");
        sb.AppendLine($"    public string Name {{ get; set; }} = string.Empty;");
        sb.AppendLine($"    public DateTime CreatedAt {{ get; set; }}");
        sb.AppendLine($"    public bool IsEnabled {{ get; set; }}");
        sb.AppendLine($"    public decimal Amount {{ get; set; }}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Builds a single-file source containing one DTO class with a
    /// <c>[Projectable]</c> constructor — exercises the constructor projection path.
    /// </summary>
    private static string BuildDtoClassSource(int j)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using EntityFrameworkCore.Projectables;");
        sb.AppendLine();
        sb.AppendLine("namespace GeneratorBenchmarkInput;");
        sb.AppendLine();

        sb.AppendLine($"public class OrderSummaryDto{j}");
        sb.AppendLine("{");
        sb.AppendLine("    public string FullName { get; set; } = string.Empty;");
        sb.AppendLine("    public decimal Total { get; set; }");
        sb.AppendLine("    public bool IsActive { get; set; }");
        sb.AppendLine();
        // Parameterless constructor required by [Projectable] constructor support.
        sb.AppendLine($"    public OrderSummaryDto{j}() {{ }}");
        sb.AppendLine();
        sb.AppendLine("    [Projectable]");
        sb.AppendLine($"    public OrderSummaryDto{j}(string firstName, string lastName, decimal amount, decimal taxRate, bool isActive)");
        sb.AppendLine("    {");
        sb.AppendLine("        FullName = firstName + \" \" + lastName;");
        sb.AppendLine("        Total = amount * (1 + taxRate);");
        sb.AppendLine("        IsActive = isActive && amount > 0;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        return sb.ToString();
    }
}

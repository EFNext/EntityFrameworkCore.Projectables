using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace EntityFrameworkCore.Projectables.Generator.Tests;

public abstract class ProjectionExpressionGeneratorTestsBase
{
    protected readonly ITestOutputHelper _testOutputHelper;

    protected ProjectionExpressionGeneratorTestsBase(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Wraps <see cref="GeneratorDriverRunResult"/> and exposes <see cref="GeneratedTrees"/>
    /// as a filtered view that excludes the generated <c>ProjectionRegistry.g.cs</c> file.
    /// This keeps all existing tests working without modification after the registry was added.
    /// </summary>
    protected sealed class TestGeneratorRunResult
    {
        private readonly GeneratorDriverRunResult _inner;

        public TestGeneratorRunResult(GeneratorDriverRunResult inner)
        {
            _inner = inner;
        }

        /// <summary>
        /// Diagnostics from the generator run.
        /// </summary>
        public ImmutableArray<Diagnostic> Diagnostics => _inner.Diagnostics;

        /// <summary>
        /// Generated trees excluding <c>ProjectionRegistry.g.cs</c>.
        /// Existing tests use this and should continue to work without modification.
        /// </summary>
        public ImmutableArray<SyntaxTree> GeneratedTrees =>
            _inner.GeneratedTrees
                .Where(t => !t.FilePath.EndsWith("ProjectionRegistry.g.cs", StringComparison.Ordinal))
                .ToImmutableArray();

        /// <summary>
        /// All generated trees including <c>ProjectionRegistry.g.cs</c>.
        /// Use this in new tests that need to verify the registry.
        /// </summary>
        public ImmutableArray<SyntaxTree> AllGeneratedTrees => _inner.GeneratedTrees;

        /// <summary>
        /// The generated <c>ProjectionRegistry.g.cs</c> tree, or <c>null</c> if it was not generated.
        /// </summary>
        public SyntaxTree? RegistryTree =>
            _inner.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("ProjectionRegistry.g.cs", StringComparison.Ordinal));
    }

    protected IReadOnlyList<MetadataReference> GetDefaultReferences()
    {
        var references = Basic.Reference.Assemblies.
#if NET10_0
                Net100
#elif NET9_0
                Net90
#elif NET8_0
            Net80
#endif
            .References.All.ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(ProjectableAttribute).Assembly.Location));
        return references;
    }

    /// <summary>
    /// Creates a test compilation from multiple source strings (e.g., to simulate split partial classes
    /// across different files). Each element in <paramref name="sources"/> becomes a separate
    /// <see cref="SyntaxTree"/> so that cross-tree resolution paths are exercised.
    /// </summary>
    protected Compilation CreateCompilation(params string[] sources)
    {
        var references = GetDefaultReferences();

        var compilation = CSharpCompilation.Create("compilation",
            sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray(),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

#if DEBUG
        var compilationDiagnostics = compilation.GetDiagnostics();

        if (!compilationDiagnostics.IsEmpty)
        {
            _testOutputHelper.WriteLine($"Original compilation diagnostics produced:");

            foreach (var diagnostic in compilationDiagnostics)
            {
                _testOutputHelper.WriteLine($" > " + diagnostic.ToString());
            }

            if (compilationDiagnostics.Any(x => x.Severity == DiagnosticSeverity.Error))
            {
                Debug.Fail("Compilation diagnostics produced");
            }
        }
#endif

        return compilation;
    }

    protected Compilation CreateCompilation([StringSyntax("csharp")] string source)
    {
        var references = GetDefaultReferences();

        var compilation = CSharpCompilation.Create("compilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

#if DEBUG
        var compilationDiagnostics = compilation.GetDiagnostics();

        if (!compilationDiagnostics.IsEmpty)
        {
            _testOutputHelper.WriteLine($"Original compilation diagnostics produced:");

            foreach (var diagnostic in compilationDiagnostics)
            {
                _testOutputHelper.WriteLine($" > " + diagnostic.ToString());
            }

            if (compilationDiagnostics.Any(x => x.Severity == DiagnosticSeverity.Error))
            {
                Debug.Fail("Compilation diagnostics produced");
            }
        }
#endif

        return compilation;
    }

    protected TestGeneratorRunResult RunGenerator(Compilation compilation)
    {
        _testOutputHelper.WriteLine("Running generator and updating compilation...");

        var subject = new ProjectionExpressionGenerator();
        var driver = CSharpGeneratorDriver
            .Create(subject)
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var rawResult = driver.GetRunResult();
        var result = new TestGeneratorRunResult(rawResult);

        LogGeneratorResult(result, outputCompilation);

        return result;
    }

    /// <summary>
    /// Creates a new generator driver and runs the generator on the given compilation,
    /// returning both the driver and the run result. The driver can be passed to subsequent
    /// calls to <see cref="RunGeneratorWithDriver"/> to test incremental caching behavior.
    /// </summary>
    protected (GeneratorDriver Driver, TestGeneratorRunResult Result) CreateAndRunGenerator(Compilation compilation)
    {
        _testOutputHelper.WriteLine("Creating generator driver and running on initial compilation...");

        var subject = new ProjectionExpressionGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(subject);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var rawResult = driver.GetRunResult();
        var result = new TestGeneratorRunResult(rawResult);
        
        LogGeneratorResult(result, outputCompilation);

        return (driver, result);
    }

    /// <summary>
    /// Runs the generator using an existing driver (preserving incremental state from previous runs)
    /// on a new compilation, returning the updated driver and run result.
    /// </summary>
    protected (GeneratorDriver Driver, TestGeneratorRunResult Result) RunGeneratorWithDriver(
        GeneratorDriver driver, Compilation compilation)
    {
        _testOutputHelper.WriteLine("Running generator with existing driver on updated compilation...");

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        
        var rawResult = driver.GetRunResult();
        var result = new TestGeneratorRunResult(rawResult);
        
        LogGeneratorResult(result, outputCompilation);

        return (driver, result);
    }

    private void LogGeneratorResult(TestGeneratorRunResult result, Compilation outputCompilation)
    {
        if (result.Diagnostics.IsEmpty)
        {
            _testOutputHelper.WriteLine("Run did not produce diagnostics");
        }
        else
        {
            _testOutputHelper.WriteLine("Diagnostics produced:");

            foreach (var diagnostic in result.Diagnostics)
            {
                _testOutputHelper.WriteLine(" > " + diagnostic);
            }
        }

        foreach (var newSyntaxTree in result.AllGeneratedTrees)
        {
            _testOutputHelper.WriteLine($"Produced syntax tree with path produced: {newSyntaxTree.FilePath}");
            _testOutputHelper.WriteLine(newSyntaxTree.GetText().ToString());
        }

        // Verify that the generated code compiles without errors
        var hasGeneratorErrors = result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        if (!hasGeneratorErrors && result.AllGeneratedTrees.Length > 0)
        {
            _testOutputHelper.WriteLine("Checking that generated code compiles...");

            var compilationErrors = outputCompilation
                .GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();

            if (compilationErrors.Count > 0)
            {
                _testOutputHelper.WriteLine("Generated code produced compilation errors:");
                foreach (var error in compilationErrors)
                {
                    _testOutputHelper.WriteLine(" > " + error);
                }
            }

            Assert.Empty(compilationErrors);
        }
    }}

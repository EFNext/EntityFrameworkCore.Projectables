# GitHub Copilot Instructions — EntityFrameworkCore.Projectables

## Project Overview

**EntityFrameworkCore.Projectables** is a Roslyn incremental source generator that lets you annotate C# properties, methods, and constructors with `[Projectable]`. The generator then emits companion `Expression<TDelegate>` trees so that EF Core can translate those members to SQL in LINQ queries.

### Repository layout

```
src/
  EntityFrameworkCore.Projectables.Abstractions/   # [Projectable] attribute, enums
  EntityFrameworkCore.Projectables.Generator/      # Roslyn IIncrementalGenerator
  EntityFrameworkCore.Projectables/                # Runtime library (EF Core integration)
tests/
  EntityFrameworkCore.Projectables.Generator.Tests/   # Roslyn generator unit tests (Verify snapshots)
  EntityFrameworkCore.Projectables.FunctionalTests/   # End-to-end EF Core tests (Verify snapshots)
  EntityFrameworkCore.Projectables.Tests/             # Misc unit tests
benchmarks/                                           # BenchmarkDotNet benchmarks
samples/                                              # Readme sample project
```

---

## Build & SDK

| Setting | Value |
|---|---|
| .NET SDK | 10.0.x (`global.json`, `rollForward: latestMinor`) |
| Target frameworks | `net8.0` + `net10.0` (library); `net8.0;net9.0;net10.0` (functional tests) |
| C# language version | `12.0` on `net8.0`, `14.0` on `net10.0` |
| Nullable | `enable` |
| Implicit usings | `enable` |
| Warnings as errors | `TreatWarningsAsErrors = true` — **zero warnings allowed** |
| Suppressed warning | `CS1591` (missing XML doc) |
| Assembly signing | `Key.snk` (src projects only) |

The generator project targets `netstandard2.0` only (Roslyn analyzers requirement).

---

## Code Style (from `.editorconfig`)

### General
- **Indentation**: spaces (not tabs)
- **Brace style**: Allman — opening brace on a new line for `control_blocks`, `types`, `properties`, `accessors`, `methods`
- `else` on its own line
- Members of object initializers on separate lines
- Single-line blocks are preserved as-is (`csharp_preserve_single_line_blocks = true`)
- Always use curly braces even for single-line `if`/`for`/... (`csharp_prefer_braces = true`)

### Types & keywords
- Prefer `var` everywhere (for built-in types, when type is apparent, and elsewhere)
- Prefer language keywords over type names (`int` not `Int32`, etc.)
- Prefer `default` over `default(T)`

### Members
- **Expression-bodied** members preferred for **methods** and **properties**
- **Block bodies** preferred for **constructors**
- Prefer local functions over anonymous functions

### Ordering / access
- No `this.` qualification for fields, properties, or methods
- `using` directives: `System.*` first, sorted alphabetically, then other namespaces
- Preferred modifier order: `public protected private readonly override async sealed static abstract virtual`

### Naming
- Private instance fields: `_camelCase` (underscore prefix)
- Everything else: standard .NET PascalCase / camelCase conventions

### Patterns
- Prefer pattern matching over `is` + cast (`csharp_style_pattern_matching_over_as_with_null_check`)
- Use object initializers when possible
- Prefer inferred tuple names

---

## C# Language Features to Use

### Available in C# 12 (`net8.0`)
- Primary constructors
- Collection expressions (`[1, 2, 3]`)
- Inline arrays
- `ref readonly` parameters
- `nameof` in attribute arguments

### Additionally available in C# 14 (`net10.0`)
- Extension members (`extension` keyword in static classes)
- `field` keyword in property accessors
- Null-conditional assignment (`??=` in more contexts)

> The generator itself targets `netstandard2.0`; avoid C# 12+ features there unless guarded by `#if`.

### File-scoped namespaces
Use file-scoped namespaces (`namespace Foo;`) in all new files **except** when the existing file already uses block-scoped namespaces (be consistent per file).

---

## Testing Guidelines

### Test projects and frameworks
| Project | Framework | Library |
|---|---|---|
| `Generator.Tests` | xUnit 2 | `Verify.Xunit` snapshot testing |
| `FunctionalTests` | xUnit 2 + ScenarioTests | `Verify.Xunit` + `Microsoft.EntityFrameworkCore.SqlServer` |
| `Tests` | xUnit 2 | plain assertions |

### Verify.Xunit — snapshot testing

**Every test that calls `Verifier.Verify(...)` must:**
1. Return `Task` (not `void`)
2. Have `[UsesVerify]` on the class
3. Have a corresponding `.verified.txt` file committed alongside the test file

**Naming convention for verified files:**
`{ClassName}.{MethodName}.verified.txt`
With framework suffix when using `UniqueForTargetFrameworkAndVersion()`:
`{ClassName}.{MethodName}.DotNet9_0.verified.txt`

**When you add or change a test that uses `Verify`:**
- Delete the old `.verified.txt` file(s) if the output changes
- Run the tests; if you enable `AutoVerify` by setting `VERIFY_AUTO_APPROVE=true`, new snapshots are accepted automatically on your machine
- **Review the generated `.verified.txt` files** before committing — they are the ground truth

### AutoVerify & culture (developer machine)

Both `VerifyInit.cs` (Generator.Tests) and `ModuleInitializer.cs` (FunctionalTests):
- Enable `VerifierSettings.AutoVerify()` **only** when the environment variable `VERIFY_AUTO_APPROVE=true` is set — so normal test runs still fail on snapshot mismatches
- Force `CultureInfo.DefaultThreadCurrentCulture` / `CurrentUICulture` to `en-US` — ensures consistent English output regardless of the developer's OS locale

**Workflow when adding new tests (or intentionally changing generator output):**
```powershell
# 1. Run tests with auto-approve to generate / update .verified.txt files
$env:VERIFY_AUTO_APPROVE = "true"; dotnet test

# 2. Review every generated .verified.txt file carefully
# 3. Commit the verified snapshots

# Normal runs (no env var) — regressions fail as expected
dotnet test
```

> The CI build (`build.yml`) never sets `VERIFY_AUTO_APPROVE` — the committed `.verified.txt` files are used for comparison and mismatches fail the build.

### Writing Generator tests

```csharp
[UsesVerify]
public class MyTests : ProjectionExpressionGeneratorTestsBase
{
    public MyTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

    [Fact]
    public Task MyFeature_GeneratesCorrectExpression()
    {
        var compilation = CreateCompilation(@"
using EntityFrameworkCore.Projectables;
namespace Foo {
    class C {
        [Projectable]
        public int Bar() => 42;
    }
}");

        var result = RunGenerator(compilation);

        Assert.Empty(result.Diagnostics);
        Assert.Single(result.GeneratedTrees);

        return Verifier.Verify(result.GeneratedTrees[0].ToString());
    }
}
```

- Use `result.GeneratedTrees` (excludes `ProjectionRegistry.g.cs`) for most tests
- Use `result.AllGeneratedTrees` when you need to include the registry
- Use `result.RegistryTree` to verify the registry file specifically
- For incremental-generator caching tests, use `CreateAndRunGenerator` + `RunGeneratorWithDriver`

### Writing Functional tests

```csharp
[UsesVerify]
public class MyFunctionalTests
{
    public record Entity { public int Id { get; set; } [Projectable] public int Computed => Id; }

    [Fact]
    public Task FilterOnComputedProperty()
    {
        using var dbContext = new SampleDbContext<Entity>();
        var query = dbContext.Set<Entity>().Where(x => x.Computed == 1);
        return Verifier.Verify(query.ToQueryString());
    }
}
```

- `SampleDbContext<TEntity>` uses SQL Server with a fake connection string (no real DB needed for `ToQueryString()`)
- Functional tests use `VerifierSettings.UniqueForTargetFrameworkAndVersion()` (except `net8.0`) — so `.verified.txt` files are per-TFM

### Running tests

```powershell
# Run all tests (normal — snapshot mismatches fail)
dotnet test

# Run only generator unit tests
dotnet test tests/EntityFrameworkCore.Projectables.Generator.Tests

# Run only functional tests
dotnet test tests/EntityFrameworkCore.Projectables.FunctionalTests

# Force English output (important on non-English machines)
$env:DOTNET_CLI_UI_LANGUAGE = "en-US"; dotnet test

# Generate / update .verified.txt snapshots after adding or changing tests
$env:VERIFY_AUTO_APPROVE = "true"; dotnet test
# → Review every .verified.txt produced before committing
```

---

## Generator Architecture

### Key files in `EntityFrameworkCore.Projectables.Generator`

| File | Responsibility |
|---|---|
| `ProjectionExpressionGenerator.cs` | `IIncrementalGenerator` entry point — wires up the pipeline |
| `ProjectableInterpreter.cs` (+ partials) | Converts a `MemberDeclarationSyntax` into a `ProjectableDescriptor` |
| `ExpressionSyntaxRewriter.cs` (+ partials) | Rewrites expressions: null-conditionals, enum expansions, switch expressions |
| `DeclarationSyntaxRewriter.cs` | Rewrites declarations (fully-qualified names, etc.) |
| `BlockStatementConverter.cs` | Converts block-bodied methods to expression trees |
| `ProjectableDescriptor.cs` | Pure data record describing a projectable member |
| `ProjectableAttributeData.cs` | Serializable snapshot of `[Projectable]` attribute values (no live Roslyn objects) |
| `ProjectionRegistryEmitter.cs` | Emits `ProjectionRegistry.g.cs` |
| `Diagnostics.cs` | All `DiagnosticDescriptor` constants (EFP0001–EFP0009) |

### Incremental generator rules
- **Never capture live Roslyn objects** (`ISymbol`, `SemanticModel`, `Compilation`, `AttributeData`) in the incremental pipeline transforms — they break caching. Use `ProjectableAttributeData` (a plain struct) instead.
- `MemberDeclarationSyntaxAndCompilationEqualityComparer` is used to prevent unnecessary re-generation.

---

## Diagnostics Reference

| ID | Severity | Title |
|---|---|---|
| EFP0001 | Warning | Block-bodied member support is experimental |
| EFP0002 | Error | Null-conditional expression unsupported |
| EFP0003 | Warning | Unsupported statement in block-bodied method |
| EFP0004 | Error | Statement with side effects in block-bodied method |
| EFP0005 | Warning | Potential side effect in block-bodied method |
| EFP0006 | Error | Method/property should expose a body definition |
| EFP0007 | Warning | Non-projectable method call in block body |

---

## Common Patterns & Do's / Don'ts

### ✅ Do
- Use expression-bodied members for methods and properties in new code
- Use `var` for local variables
- Use file-scoped namespaces in new `.cs` files
- Prefer pattern matching (`is`, `switch` expressions) over casting
- Add XML doc comments to all `public` members in `src/` (doc file is generated)
- Always add/update tests when changing behavior
- When adding a new generator feature: add both a **Generator test** (snapshot) and a **Functional test** (EF Core query)
- Use `Assert.Empty(result.Diagnostics)` to confirm no unexpected diagnostics
- Keep `.verified.txt` files up-to-date and committed

### ❌ Don't
- Don't use `this.` prefix for member access
- Don't leave warnings — `TreatWarningsAsErrors` is on
- Don't use block bodies for methods when an expression body is natural
- Don't use expression bodies for constructors
- Don't store live Roslyn objects (`ISymbol`, `SemanticModel`) in incremental pipeline transforms
- Don't use `Thread.CurrentThread.CurrentCulture` — use `CultureInfo.DefaultThreadCurrentCulture` in module initializers
- Don't write tests that rely on a specific OS locale (culture is forced to `en-US` in test initializers)
- Don't add new packages without updating `Directory.Packages.props` with a `<PackageVersion>` entry

---

## Package Management

Central package version management is enabled (`ManagePackageVersionsCentrally = true`).

- **All package versions** are declared in `Directory.Packages.props` (root)
- In project files, use `<PackageReference Include="..." />` **without** a `Version` attribute
- Version entries in `Directory.Packages.props` may be conditional on `$(TargetFramework)` (e.g. EF Core 8/9/10)

---

## NuGet / Release

- Packages are signed with `Key.snk`
- SourceLink is configured for GitHub
- Pre-release packages are published to GitHub Packages on every push to `master`
- Version is set via `-p:PackageVersion=...` at pack time


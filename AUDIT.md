# Performance & Reliability Audit — EntityFrameworkCore.Projectables

*Date: 2026-07-07 — Scope: full static review of `src/`, `benchmarks/`, generator pipeline, CI workflows. No code was changed as part of this audit; this document is the deliverable and ends with a prioritized, actionable change plan.*

---

## 1. Executive summary

The library is in good shape overall: a lot of deliberate performance work has already landed (the generated `ProjectionRegistry` fast path, static `ConcurrentDictionary`/`ConditionalWeakTable` caches in the resolver, no-alloc fast paths in the visitor, `EquatableImmutableArray` for incremental-generator caching, a benchmark project). This audit therefore focuses on what is *left*:

**The single biggest remaining cost is the runtime query-rewrite hot path.** In the default `CompatibilityMode.Full`, `ProjectableExpressionReplacer` re-visits the full expression tree **on every query execution** (it runs in front of EF Core's compiled-query cache), and several per-node operations inside that visit do uncached reflection:

- `Type.GetConcreteMethod` / `Type.GetConcreteProperty` allocate and scan reflection metadata for **every instance method call and every property access node**, on every execution (§3.1).
- The member → expression cache (including the dominant *negative* lookups, i.e. "this member is not projectable") is **per-replacer-instance**, so it is rebuilt for every new `DbContext` (Full mode) and for every query compilation (Limited mode) (§3.2).
- The auto-`Select` root rewrite rebuilds its projection lambda with LINQ + reflection on every execution and has a latent crash for multi-root/element-type-changing queries (§3.3, §4.1).

Fixing §3.1 + §3.2 alone should collapse most of the remaining gap visible in the `ResolverOverhead` benchmark (fresh-`DbContext`-per-query scenario) and reduce steady-state per-execution allocations substantially, with small, low-risk diffs.

On the **correctness** side, the notable risks are: unbounded recursion for self/mutually-referential projectable methods and properties (constructors are guarded, methods/properties are not, §4.2); the tracking-flag and `Select`-detection heuristics in the root rewrite matching by *name* and by *innermost-wins* order (§4.3); and the root rewrite assuming the query element type equals the last-visited entity root (§4.1).

On the **generator** side, build-time behavior is reasonable but the pipeline still carries `Compilation` in the value table with a comparer that deliberately ignores cross-file semantic changes — a documented-in-code trade-off that can serve **stale generated output** until the annotated file itself is edited (§5.1). The registry branch also duplicates `GetSemanticModel`/`GetDeclaredSymbol` work already done by the source-output branch (§5.2).

Startup: the generated registry materializes **all** expression trees eagerly on first use (§3.5) — fine for small models, a measurable first-query spike for apps with hundreds of projectables.

Section 7 gives the full phased plan (P1–P5) with per-item files, approach, risk, and validation.

---

## 2. What already works well (keep as-is)

| Area | Observation |
|---|---|
| Registry fast path | `ProjectionRegistry.g.cs` keyed by `MethodHandle.Value` gives O(1) member → expression resolution and removes the name-composition/reflection slow path for the common case. |
| Resolver caches | `_expressionCache`, `_reflectionCache` (with null-sentinel), `_assemblyRegistries` (with sentinel delegate), `_typeNameCache` (CWT, ALC-friendly) are all correctly designed. |
| Replacer fast paths | Method-group argument scan avoids allocation until a replacement is found; `IsCompilerGeneratedClosure` does a cheap flag check before touching the attribute cache; closed `Select`/`Where` `MethodInfo`s are cached per CLR type in `ConditionalWeakTable`. |
| Incremental generator | `ProjectableAttributeData` / `ProjectableGlobalOptions` are plain record structs (no live Roslyn objects); `EquatableImmutableArray` fixes the classic `ImmutableArray<T>` reference-equality caching bug; registry emission uses `RegisterImplementationSourceOutput` (doesn't block IDE features). |
| Benchmarks | `benchmarks/` covers plain overhead, resolver cold start per fresh `DbContext`, and closure capture — a good base to extend. |

---

## 3. Findings — runtime performance

### 3.1 `GetConcreteMethod` / `GetConcreteProperty`: uncached reflection per node, per execution — **High impact, Small fix**

`src/EntityFrameworkCore.Projectables/Services/ProjectableExpressionReplacer.cs:172` calls `node.Object?.Type.GetConcreteMethod(node.Method)` for **every instance method call node**, and `ProjectableExpressionReplacer.cs:299` calls `nodeExpression.Type.GetConcreteProperty(property)` for **every property-access node** in every visited tree. In `CompatibilityMode.Full` that is every query *execution*, not just compilation.

Inside `src/EntityFrameworkCore.Projectables/Extensions/TypeExtensions.cs`:

- `GetOverridingProperty` (`TypeExtensions.cs:74`) always calls `propertyInfo.GetAccessors(true)` — an array allocation — even for the overwhelmingly common non-virtual case, and when a virtual accessor is found it calls `derivedType.GetProperties(Public|NonPublic|Instance)` (another array) plus `GetBaseDefinition()` and a LINQ `FirstOrDefault` with a closure.
- `GetImplementingMethod` (`TypeExtensions.cs:111`) calls `derivedType.GetInterfaceMap(...)` per call for interface-declared methods on concrete receivers.
- `MethodInfosEqual` builds `GetParameters().Select(...)` LINQ chains per comparison.

The results are pure functions of `(derivedType, memberInfo)` and are ideal cache candidates.

**Recommendation:** add a static two-level cache in `TypeExtensions` (or a dedicated `ConcreteMemberCache`): `ConditionalWeakTable<Type, ConcurrentDictionary<MemberInfo, MemberInfo>>` — CWT on the receiver type keeps collectible-ALC friendliness consistent with the existing caches; the inner map holds both method and property resolutions. Additionally, add an allocation-free pre-check in `GetOverridingProperty` (check `GetMethod`/`SetMethod` virtualness directly instead of `GetAccessors(true)`).

### 3.2 Member → expression cache is per instance; negative lookups repeated per DbContext — **High impact, Small fix**

`ProjectableExpressionReplacer._projectableMemberCache` (`ProjectableExpressionReplacer.cs:17`) is an instance `Dictionary<MemberInfo, LambdaExpression?>`:

- In **Full** mode, `CustomQueryCompiler` (scoped service → one per `DbContext` instance) creates one replacer, so every *new* `DbContext` re-derives attribute presence via `memberInfo.GetCustomAttribute<ProjectableAttribute>(false)` (`ProjectableExpressionReplacer.cs:51`) for every distinct member in its queries. Web workloads create a context per request; this is exactly the cost the `ResolverOverhead` benchmark shows.
- In **Limited** mode it is worse: `CustomQueryTranslationPreprocessor` → `Expression.ExpandProjectables()` (`src/.../Extensions/ExpressionExtensions.cs:12`) allocates a **new resolver + new replacer per query compilation**, discarding the cache each time.

The vast majority of entries are *negative* (`null` — "not projectable"), and both positive and negative results are process-stable (attributes don't change at runtime).

**Recommendation:** replace the instance dictionary with a static `ConcurrentDictionary<MemberInfo, LambdaExpression?>` (or CWT-based if ALC-unload parity is desired) shared by all replacer instances. Keep the method signature; the change is contained to `TryGetReflectedExpression`. Also make `ProjectionExpressionResolver` a singleton (`ProjectionExpressionResolver.Instance`) since it is stateless — `ExpressionExtensions.cs:12`, `CustomQueryCompiler.cs:64` and `ProjectablesExpandQueryFiltersConvention` all `new` it up today.

### 3.3 `_AddProjectableSelect` rebuilds the projection per execution — **Medium impact, Small/Medium fix**

`ProjectableExpressionReplacer.cs:331-375`: every time the root rewrite fires (queries over entities that have *writable* `[Projectable]` properties), it:

- enumerates `entityType.GetProperties()` + navigations + skip-navigations through a 4-stage LINQ chain,
- filters with an O(properties × projectables) nested `All(...)` doing string comparisons including a `$"<{y.Name}>k__BackingField"` interpolation *per pair*,
- rebuilds the whole `MemberInit` lambda.

The result depends only on the `IEntityType` (model metadata is immutable once built).

**Recommendation:** cache the built `LambdaExpression` (the member-init selector) per `IEntityType` in a `ConditionalWeakTable<IEntityType, LambdaExpression>`; `_AddProjectableSelect` then just wraps `node` in the cached closed `Select` call. Precompute the projectable-name set into a `HashSet<string>` (names + backing-field names) once.

Same file, `.First(predicate)` path (`ProjectableExpressionReplacer.cs:114-115`): `call.Method.DeclaringType?.GetMethods().FirstOrDefault(name + paramCount == 1)` scans `Queryable`'s ~200 methods per call and matches **by name and parameter count only** — cache per `(DeclaringType, Name)` and match the exact expected signature (see also §4.1).

### 3.4 Full-mode expansion runs before EF's compiled query cache — **structural, document + measure**

`CustomQueryCompiler.Execute/ExecuteAsync` (`CustomQueryCompiler.cs:67-77`) expands on **every execution**; in Limited mode the preprocessor only runs on compiled-query-cache misses. This is inherent to Full mode's design (closure inlining and root rewrite must happen before the cache key is computed), but it means every improvement in §3.1–§3.3 is multiplied by execution count, and it deserves explicit user-facing guidance.

**Recommendation:** (a) land §3.1–§3.3; (b) add a docs page ("Choosing a compatibility mode — performance characteristics") stating that Limited mode has near-zero steady-state overhead because expansion is cached by EF's query cache, while Full mode pays a per-execution visit; (c) extend `PlainOverhead` benchmarks to compare Full vs Limited steady-state so the trade-off is tracked over time.

### 3.5 Generated registry materializes all expressions eagerly — **Low/Medium impact (startup), Medium fix**

`ProjectionRegistryEmitter` emits `private static readonly Dictionary<nint, LambdaExpression> _map = Build();` (`ProjectionRegistryEmitter.cs:139`), and `Build()` reflects over **every** projectable member and invokes **every** generated `Expression()` factory at type-initialization time (first `TryGet` call). For an assembly with hundreds of projectables that is a one-time spike on the first query (expression-tree construction is not cheap, particularly constructor member-init trees).

**Recommendation:** consider emitting `Dictionary<nint, Func<LambdaExpression>>` and materializing lazily per entry (the runtime side already caches the resolved `LambdaExpression` per `MemberInfo` in `_expressionCache`, so each factory runs at most ~once). Measure first with a generated-large-model benchmark; if the spike is <10 ms for realistic model sizes, keep eager and close this item.

### 3.6 Micro items (bundle opportunistically)

- `ProjectionExpressionResolver._csharpKeywords` → `FrozenDictionary` on net8+/net10 targets (`#if`-guarded).
- `Replace()`'s root-rewrite `Expression.Parameter(entityType.ClrType)` — give it a name (`"x"`) for readable `ToQueryString()` output/snapshots.
- `CustomQueryCompiler` constructs a full base `QueryCompiler` *and* wraps the decorated one (two compilers per scope). Cost is small and the shadow-field hack for EFCore.BulkExtensions justifies inheriting; leave, but note it in code.

---

## 4. Findings — runtime correctness / robustness

### 4.1 Root rewrite can build an invalid `Select` for element-type-changing queries — **High severity (when triggered), Small fix**

`Replace()` (`ProjectableExpressionReplacer.cs:78-143`) triggers `_AddProjectableSelect(call, _entityType)` whenever the tree ends in a method call and `_entityType` (the **last visited** `EntityQueryRootExpression`, set unconditionally in `VisitExtension`) is non-null. It never verifies that the query's **element type** still equals `_entityType.ClrType`. Element-type-changing operators that are *not* named `Select` — `SelectMany`, `Join`, `GroupBy`, `Cast`, multi-root queries — keep the rewrite armed:

```csharp
// A has a writable [Projectable] property
db.A.SelectMany(x => x.Children).ToList();
// root rewrite wraps IQueryable<Child> in Select<A, A>(...) → ArgumentException at runtime
```

The feature only activates for entities with *writable* projectable properties, which narrows exposure, but the failure is an opaque `ArgumentException` from `Expression.Call`.

**Recommendation:** before wrapping, extract the sequence element type from `call.Type` / the source argument and bail out unless it equals `entityType.ClrType`. Add functional tests for `SelectMany`, `Join`, and `GroupBy` on an entity with a writable projectable.

Also in this switch: the `.First(pred)` overload resolution (§3.3) should match on exact parameter shape, and `call.Arguments.Count != 1 && true /* … */` plus the commented-out pseudo-code block (`ProjectableExpressionReplacer.cs:83-94, 105`) should be cleaned up or turned into real comments.

### 4.2 No recursion guard for projectable methods/properties — **Medium severity, Small fix**

`VisitNew` guards constructor expansion with `_expandingConstructors` (`ProjectableExpressionReplacer.cs:218-243`), but `VisitMethodCall`/`VisitMember` re-visit the inlined body (`base.Visit(updatedBody)`) with no cycle detection. A self-referential or mutually recursive pair of `[Projectable]` members produces a `StackOverflowException` (process-killing) at query time instead of a diagnosable error.

**Recommendation:** mirror the constructor guard with an expanding-member set (or a depth counter) covering methods and properties; on cycle, throw `InvalidOperationException` naming the member chain. Cheap: one `HashSet<MemberInfo>` add/remove in try/finally.

### 4.3 Tracking/`Select` detection is name-based and innermost-wins — **Medium severity, Small/Medium fix**

`VisitMethodCall` (`ProjectableExpressionReplacer.cs:174-186`):

- Flags are keyed on `methodInfo.Name` — **any** method named `Select`, `AsTracking`, `AsNoTracking` matches, including user extension methods and nested `Select` calls inside subqueries (`.Where(x => x.Children.Select(...).Any())` disables the root rewrite even though the root projection is unchanged).
- Visit order is outermost-first, so with chained calls the **innermost** tracking call wins: `q.AsNoTracking().AsTracking()` ends with `_disableRootRewrite == false` (rewrite enabled) although the effective EF behavior is tracking.

**Recommendation:** compare `methodInfo.GetGenericMethodDefinition()` against the cached `Queryable.Select` / `EntityFrameworkQueryableExtensions.AsTracking`/`AsNoTracking*` `MethodInfo`s, only honor them on the *root chain* (track depth: these calls matter only while visiting the spine of the query, before descending into lambda arguments), and make the first (outermost) tracking call encountered win.

### 4.4 `GetExpressionFromMemberBody` can throw `TargetException` — **Low severity, Trivial fix**

`ProjectionExpressionResolver.cs:130-132`: `declaringType.GetProperty(memberName, Static | Instance | ...)` followed by `exprProperty?.GetValue(null)`. If a user points `UseMemberBody` at an **instance** property and the generator fallback path is hit, `GetValue(null)` throws `TargetException` instead of the clean "unable to resolve" error. Guard with `exprProperty.GetMethod?.IsStatic == true`.

### 4.5 Swallow-all `catch { }` in closure evaluation — **Low severity, Trivial fix**

`ProjectableExpressionReplacer.cs:283-286` catches *everything* while probing closure fields. Narrow to `(TargetInvocationException, FieldAccessException, ArgumentException)` or at minimum exclude nothing-should-catch types; silent swallowing here can mask real bugs in user getters that the query then mis-inlines.

### 4.6 Misc

- `ProjectablesExpandQueryFiltersConvention` does `... as LambdaExpression` and passes a possible `null` to `SetQueryFilter`, which would silently *clear* a filter if the expansion ever returned a non-lambda. Use a hard cast or throw.
- `TypeExtensions.cs:120` throws `ApplicationException` — use `InvalidOperationException`.
- `CustomQueryCompiler.cs` has an XML doc of literally `Foo` and unused usings (`System.Transactions`, `System.Text`, …).
- `_AddProjectableSelect`, `_GetAccessor` method names violate the repo's own naming conventions (no underscore-prefixed methods).

---

## 5. Findings — source generator (build-time performance & correctness)

### 5.1 `Combine(CompilationProvider)` + lenient comparer → cached-but-stale output — **Design trade-off to (re)decide**

`ProjectionExpressionGenerator.cs:67-69` combines each member with `context.CompilationProvider`, keeping full `Compilation` instances in the incremental value table, and `MemberDeclarationSyntaxAndCompilationEqualityComparer` declares two snapshots *equal* when the member's own file, attribute data, global options and the compilation's `ExternalReferences` are unchanged.

Consequences:

1. **Staleness:** the generated expression depends on semantics *outside* the member's file — base classes, `UseMemberBody` targets in other partial-class files, delegated constructors in other files (`CollectDelegatedConstructorAssignments` explicitly crosses trees), types referenced by the body. Editing those files does **not** invalidate the cached output; the user sees stale generated code (or stale diagnostics) until the annotated file itself is touched or a rebuild happens.
2. **Memory:** equal-comparing pairs still *store* the newest pair; but because the tuple holds `Compilation`, the node table roots compilation snapshots between runs — the pattern Roslyn's incremental-generator guidance warns can pin large object graphs in IDE sessions.
3. `GetHashCode` walks all `ExternalReferences` per member per pass — O(members × references), minor but avoidable (hash length + first/last reference only; the comparer contract only needs equal ⇒ same hash).

**Recommendation:** treat this as an explicit design decision with tests. Options, in increasing effort: (a) keep, but document the staleness in code + a `docs/advanced` note, and add generator tests that pin the current cross-file-edit behavior so it is at least intentional; (b) strengthen `Equals` with a cheap semantic fingerprint of the member (e.g. the symbol's `ToDisplayString` of the containing type + resolved parameter/return types captured *at transform time* into the tuple, avoiding live symbols); (c) restructure so the transform extracts a fully-serialized model (no `Compilation` in the table) — the correct long-term shape but a large refactor given how much of the interpreter needs `SemanticModel`.

### 5.2 Registry branch duplicates semantic work — **Medium build-time win, Small fix**

Both `RegisterSourceOutput` (`ProjectionExpressionGenerator.cs:85-86`) and the registry `Select` (`ProjectionExpressionGenerator.cs:109-110`) independently call `compilation.GetSemanticModel(member.SyntaxTree)` + `GetDeclaredSymbol(member)` for the same members on every pass. Semantic-model creation and symbol resolution are among the most expensive per-member costs in the generator.

**Recommendation:** derive both outputs from one shared step: a single `Select` that resolves the symbol once and produces `(descriptor inputs, registry entry)`, feeding `RegisterSourceOutput` and `Collect()` from the same node. Note `GetSemanticModel` does cache per-tree within a `Compilation`, so the win is mostly the duplicated `GetDeclaredSymbol` and pipeline bookkeeping — measure with the existing `GeneratorBenchmarks`.

### 5.3 Whole-file `DescendantNodes()` to collect usings — **Small fix**

`ProjectableInterpreter.cs:135`: `member.SyntaxTree.GetRoot().DescendantNodes().OfType<UsingDirectiveSyntax>()` enumerates **every node in the file** to find using directives, per member. Use `root.Usings` plus the `Usings` of the member's ancestor `BaseNamespaceDeclarationSyntax` chain — O(depth) instead of O(file). Also materialize once (it's stored as a lazy `IEnumerable` on the descriptor today).

### 5.4 Enum method expansion has no size guard — **Small fix**

`ExpressionSyntaxRewriter.EnumMethodExpansion.cs` builds one ternary branch per enum member with no cap. A 100-member enum inside a hot projectable silently generates a 100-deep conditional chain (and correspondingly monstrous SQL `CASE`). Emit a warning diagnostic above a threshold (e.g. 20 members) so users opt in knowingly.

### 5.5 Micro / hygiene

- `Execute` builds `compilationUnit` with repeated `AddUsings` calls in a loop and helpers append parameters one-by-one via `AddParameters` (each call reallocates the list) — batch into single `AddUsings(params[])`/`SeparatedList` builds. Small, bounded n, but free to fix while touching the files.
- `NormalizeWhitespace()` on each generated file is the known dominant emission cost; the registry already uses `IndentedTextWriter`. Migrating member emission off `SyntaxFactory` is possible but low priority — only revisit if `GeneratorBenchmarks` shows emission dominating.
- Duplicate string building: `ToDisplayString(FullyQualifiedFormat)` is called repeatedly for the same symbols within one member's generation; hoist locals where trivially possible.

---

## 6. Findings — CI, benchmarks, packaging, docs

1. **Benchmarks are never executed in CI** — regressions in the paths this audit optimizes would go unnoticed. Add a `workflow_dispatch` (+ optional weekly cron) workflow that runs `dotnet run -c Release --project benchmarks/... -- --filter '*'` and uploads the artifacts; a short-job smoke variant (`--job short`) keeps runtime sane.
2. `build.yml` uses `actions/checkout@v2` (deprecated, Node 12 runtime) — bump to `@v4`; add a `concurrency` group to cancel superseded PR runs.
3. Benchmark matrix gap: nothing measures (a) Full vs Limited steady-state, (b) inheritance/interface receivers (the §3.1 path), (c) entities with writable projectables (the §3.3/§4.1 path), (d) a large-model registry startup (§3.5). Add these alongside the P1/P2 changes so improvements are provable.
4. Docs (`docs/`) lack a performance/compat-mode guidance page (§3.4). Per repo instructions, adding a page requires a sidebar entry in `docs/.vitepress/config.mts`.

---

## 7. Change plan

Phases are ordered by user-visible value; items within a phase are independent unless noted. Every phase must end with: `dotnet test` green on all TFMs, snapshots reviewed, and (for P1–P3) before/after BenchmarkDotNet numbers committed to the PR description.

### Phase P1 — Runtime hot path (highest ROI, small diffs)

| # | Change | Files | Risk | Validation |
|---|--------|-------|------|------------|
| P1.1 | Static cache for concrete-member resolution: `ConditionalWeakTable<Type, ConcurrentDictionary<MemberInfo, MemberInfo>>` fronting `GetConcreteMethod`/`GetConcreteProperty`; allocation-free virtualness pre-check in `GetOverridingProperty` (drop `GetAccessors(true)` for the non-virtual fast path) | `Extensions/TypeExtensions.cs` (+ call sites unchanged) | Low — pure memoization of pure functions | New unit tests for override/interface/explicit-impl resolution; extend `ResolverOverhead` with a derived-entity + interface-receiver benchmark; `[MemoryDiagnoser]` before/after |
| P1.2 | Share the member→expression cache (incl. negative results) across instances: static `ConcurrentDictionary<MemberInfo, LambdaExpression?>` replacing instance `_projectableMemberCache` | `Services/ProjectableExpressionReplacer.cs` | Low — results are process-stable; note collectible-ALC rooting in a comment (consistent with existing `_expressionCache`) | `ResolverOverhead` fresh-context benchmarks should approach baseline; functional tests unchanged |
| P1.3 | Singleton stateless resolver: `ProjectionExpressionResolver.Instance`; use it in `ExpressionExtensions`, `CustomQueryCompiler`, conventions | `Services/ProjectionExpressionResolver.cs`, `Extensions/ExpressionExtensions.cs`, `Infrastructure/Internal/CustomQueryCompiler.cs`, `ProjectablesExpandQueryFiltersConvention.cs` | Trivial | Compile + tests |
| P1.4 | Cache the `_AddProjectableSelect` member-init selector per `IEntityType` (CWT); precompute projectable name/backing-field `HashSet`; cache the `.First(pred)`-style overload lookup per `(DeclaringType, Name)` | `Services/ProjectableExpressionReplacer.cs` | Low-Medium — verify model immutability assumption (model is frozen post-build; CWT keeps per-model isolation) | New functional tests: repeated execution of root-rewritten query; snapshot of `ToQueryString()` unchanged |

### Phase P2 — Runtime correctness (do together with or right after P1, same files)

| # | Change | Files | Risk | Validation |
|---|--------|-------|------|------------|
| P2.1 | Element-type guard in root rewrite: only wrap when the sequence element type == `_entityType.ClrType` (covers `SelectMany`/`Join`/`GroupBy`/multi-root) | `ProjectableExpressionReplacer.cs` (`Replace`) | Low | Functional tests: `SelectMany`, `Join`, `GroupBy` over an entity with a writable projectable — must not throw, must not rewrite |
| P2.2 | Recursion guard for method/property expansion (mirror `_expandingConstructors`); throw descriptive `InvalidOperationException` on cycles | `ProjectableExpressionReplacer.cs` | Low | Unit test: two mutually recursive `[Projectable]` methods → clean exception, not `StackOverflowException` |
| P2.3 | Identity-based (not name-based) detection of `Queryable.Select` / `AsTracking` / `AsNoTracking*`; restrict to the root chain; outermost tracking call wins | `ProjectableExpressionReplacer.cs` | Medium — behavior change for exotic chains; keep old behavior for anything ambiguous | Functional tests for `AsNoTracking().AsTracking()`, nested subquery `Select`, user-defined `Select` extension |
| P2.4 | Exact-signature matching for the `.First(predicate)`-family rewrite | `ProjectableExpressionReplacer.cs` | Low | Existing + new functional tests (`First`, `Single`, `FirstOrDefault` with predicate) |
| P2.5 | Robustness bundle: static-guard in `GetExpressionFromMemberBody` (§4.4); narrow closure-probe catch (§4.5); hard cast in query-filter convention (§4.6); `ApplicationException` → `InvalidOperationException`; remove dead pseudo-code and `&& true`, rename `_AddProjectableSelect`/`_GetAccessor`, fix `CustomQueryCompiler` doc/usings | resolver, replacer, `TypeExtensions.cs`, conventions, `CustomQueryCompiler.cs` | Trivial | Unit test for instance-property `UseMemberBody` fallback; compile clean (warnings-as-errors) |

### Phase P3 — Startup (measure first, then decide)

| # | Change | Files | Risk | Validation |
|---|--------|-------|------|------------|
| P3.1 | Add a registry-startup benchmark: generated assembly with ~200 projectables; measure first-`TryGet` latency | `benchmarks/` | None | Numbers recorded in repo |
| P3.2 | *If* P3.1 shows a meaningful spike (>~10 ms realistic): emit `Dictionary<nint, Func<LambdaExpression>>` and materialize lazily per entry (runtime `_expressionCache` already dedupes) | `Generator/Registry/ProjectionRegistryEmitter.cs` + generator snapshot tests | Medium — changes generated-code shape; update all registry `.verified.txt` snapshots deliberately (`VERIFY_AUTO_APPROVE` + review) | Generator snapshot tests; functional tests; re-run P3.1 |

### Phase P4 — Generator build-time & incrementality

| # | Change | Files | Risk | Validation |
|---|--------|-------|------|------------|
| P4.1 | Single shared semantic-resolution step feeding both source output and registry entries (remove the duplicated `GetSemanticModel`/`GetDeclaredSymbol`) | `ProjectionExpressionGenerator.cs` | Medium — pipeline restructuring; keep the existing comparer semantics | Existing incremental-caching tests (`CreateAndRunGenerator`/`RunGeneratorWithDriver`); `GeneratorBenchmarks` before/after |
| P4.2 | Decide + document the `CompilationProvider` comparer trade-off (§5.1). Minimum: add generator tests pinning cross-file-edit behavior + an internals doc note; preferred: capture a semantic fingerprint (containing-type + parameter/return display strings) in the transform tuple and include it in `Equals`, and stop hashing all `ExternalReferences` in `GetHashCode` | `Comparers/*.cs`, `ProjectionExpressionGenerator.cs`, `docs/advanced/` | Medium — under-invalidation ↔ over-invalidation balance; fingerprint must be computed in the transform (no live symbols in the table) | New incremental tests: edit base-class file / `UseMemberBody` partial file / delegated-ctor file → assert regeneration (or assert current behavior if option (a)) |
| P4.3 | Targeted using collection (`root.Usings` + ancestor namespaces) materialized once; batch `AddUsings`/`AddParameters`; enum-expansion size-warning diagnostic (new EFP id + docs page entry) | `ProjectableInterpreter.cs`, `ProjectionExpressionGenerator.cs`, `ExpressionSyntaxRewriter.EnumMethodExpansion.cs`, `Diagnostics.cs`, `docs/reference/` | Low | Generator snapshot tests (usings order must stay stable — review snapshots); new diagnostic test |

### Phase P5 — CI, benchmarks-as-guardrail, docs

| # | Change | Files | Risk | Validation |
|---|--------|-------|------|------------|
| P5.1 | Benchmark workflow (`workflow_dispatch` + weekly cron, short-job smoke on PR label), artifact upload | `.github/workflows/benchmarks.yml` | None | Manual dispatch run |
| P5.2 | CI hygiene: `checkout@v4`, `concurrency` group on `build.yml` | `.github/workflows/build.yml` | None | Green CI |
| P5.3 | New benchmarks: Full vs Limited steady-state; derived/interface receivers; writable-projectable root rewrite | `benchmarks/` | None | Committed baseline numbers |
| P5.4 | Docs: "Performance & compatibility modes" page (guidance from §3.4) + sidebar entry | `docs/guide` or `docs/reference`, `docs/.vitepress/config.mts` | None | `npm run build` in docs |
| P5.5 | Micro-perf bundle: `FrozenDictionary` for keyword map (TFM-guarded), named parameter in root rewrite | resolver, replacer | Trivial | Tests + snapshots |

### Sequencing & sizing

- **P1 + P2** are one or two PRs touching the same two files plus tests — highest value, ~1–2 days including benchmark runs. Do these first.
- **P3** is gated on its own measurement (P3.1 first, cheap).
- **P4.1/P4.3** are safe standalone PRs; **P4.2** needs a maintainer decision on the invalidation trade-off before code is written.
- **P5** can proceed in parallel at any time.

### Global validation protocol (applies to every phase)

1. `dotnet test` across net8.0/net9.0/net10.0 (snapshot mismatches fail; regenerate snapshots only with `VERIFY_AUTO_APPROVE=true` and review every `.verified.txt` diff).
2. BenchmarkDotNet before/after for the touched path (`PlainOverhead`, `ResolverOverhead`, `GeneratorBenchmarks` as relevant), `[MemoryDiagnoser]` allocations included in the PR description.
3. Zero new warnings (`TreatWarningsAsErrors` is on).
4. README feature table and `docs/` updated whenever behavior visible to users changes (per repo contribution rules).

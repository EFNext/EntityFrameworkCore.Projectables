# Query Compiler Pipeline

This page explains how EF Core Projectables integrates with EF Core's internal query compilation pipeline, and the differences between Full and Limited compatibility modes.

## EF Core's Query Pipeline (Background)

When you execute a LINQ query against a `DbContext`, EF Core runs it through a multi-stage pipeline:

```
LINQ Expression (IQueryable)
    ↓
QueryCompiler.Execute()
    ↓
Query Translation Preprocessor
    ↓
Query Translator (LINQ → SQL model)
    ↓
SQL Generator
    ↓
SQL + Parameters → Database
```

Projectables hooks into this pipeline at different points depending on the selected compatibility mode.

## Full Compatibility Mode

In Full mode, expansion happens **before** the query reaches EF Core's pipeline:

```
LINQ Expression
    ↓
CustomQueryCompiler.Execute() / CreateCompiledQuery()
    ↓  ← [Projectables expansion happens HERE]
ProjectableExpressionReplacer.Replace()
    ↓
Expanded LINQ Expression
    ↓
(Delegated to the original EF Core QueryCompiler)
    ↓
Standard EF Core pipeline...
    ↓
SQL
```

### `CustomQueryCompiler`

The `CustomQueryCompiler` class wraps EF Core's default `QueryCompiler`. It overrides all execution entry points:

```csharp
public override TResult Execute<TResult>(Expression query)
    => _decoratedQueryCompiler.Execute<TResult>(Expand(query));

public override TResult ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken)
    => _decoratedQueryCompiler.ExecuteAsync<TResult>(Expand(query), cancellationToken);

public override Func<QueryContext, TResult> CreateCompiledQuery<TResult>(Expression query)
    => _decoratedQueryCompiler.CreateCompiledQuery<TResult>(Expand(query));
```

The `Expand()` method calls `ProjectableExpressionReplacer.Replace()` on the raw expression before passing it downstream.

### Query Cache Implications

Because expansion happens before EF Core sees the query, the expanded expression is what gets compiled and cached. This means:

- EF Core's query cache works on the **expanded** expression.
- Two queries that differ only in which projectable member they call will produce **different cache keys**, even if the expanded SQL is the same.
- Each unique LINQ query shape goes through expansion on **every execution** — there is no caching of the expansion step itself.

## Limited Compatibility Mode

In Limited mode, expansion happens **inside** EF Core's query translation preprocessor:

```
LINQ Expression
    ↓
EF Core QueryCompiler (default)
    ↓
CustomQueryTranslationPreprocessor.Process()
    ↓  ← [Projectables expansion happens HERE]
ProjectableExpressionReplacer (via ExpandProjectables() extension)
    ↓
Expanded expression (now stored in EF Core's query cache)
    ↓
Standard EF Core query translator...
    ↓
SQL
```

### `CustomQueryTranslationPreprocessor`

This class wraps EF Core's default `QueryTranslationPreprocessor` and overrides the `Process()` method:

```csharp
public override Expression Process(Expression query)
    => _decoratedPreprocessor.Process(query.ExpandProjectables());
```

`ExpandProjectables()` is an extension method on `Expression` that runs the `ProjectableExpressionReplacer` over the expression tree.

### Query Cache Benefits

Because the expansion happens **inside** EF Core's own preprocessing step, EF Core compiles the resulting expanded expression and stores it in its query cache. On subsequent executions with the same query shape:

1. EF Core computes the cache key from the original (unexpanded) query.
2. It finds the cached compiled query.
3. It executes the cached query directly — **no expansion needed**.

This is why Limited mode can outperform both Full mode and vanilla EF Core for repeated queries.

### Dynamic Parameter Caveat

The downside of Limited mode is that EF Core's query cache key is based on the **original** LINQ expression. If your projectable member captures external state (a closure variable that changes between calls), the cache may not distinguish between calls with different values.

**Safe with Limited mode:**
```csharp
// The threshold is a query parameter — EF Core handles it correctly
dbContext.Orders.Where(o => o.ExceedsThreshold(threshold))
```

**Potentially unsafe with Limited mode:**
```csharp
// If GetCurrentUserRegion() returns a different value per call
// and the result is baked into the expression tree at expansion time
// (not captured as a standard EF Core parameter), this may be stale.
dbContext.Orders.Where(o => o.Region == GetCurrentUserRegion())
```

## How Expansion Works

In both modes, the core expansion logic is in `ProjectableExpressionReplacer`:

1. **Visit the expression tree** — The replacer inherits from `ExpressionVisitor` and recursively visits every node.
2. **Detect projectable calls** — For each `MemberExpression` (property access) or `MethodCallExpression`, it checks if the member has a `[ProjectableAttribute]`.
3. **Load the generated expression** — Uses `ProjectionExpressionResolver` to find the auto-generated companion class and invoke its `Expression()` factory method via reflection.
4. **Cache the resolved expression** — The resolved `LambdaExpression` is cached in a per-replacer dictionary to avoid redundant reflection calls within the same query expansion.
5. **Substitute arguments** — Uses `ExpressionArgumentReplacer` to replace the lambda's parameters with the actual arguments from the call site.
6. **Recurse** — The substituted expression body is itself visited, expanding any nested projectable calls.

## Registering the Infrastructure

Both modes use the same EF Core extension mechanism. `ProjectionOptionsExtension` implements `IDbContextOptionsExtension` and registers the appropriate services:

```csharp
// Full mode — registers CustomQueryCompiler
services.AddScoped<IQueryCompiler, CustomQueryCompiler>();

// Limited mode — registers CustomQueryTranslationPreprocessorFactory  
services.AddScoped<IQueryTranslationPreprocessorFactory, 
                   CustomQueryTranslationPreprocessorFactory>();
```

The `CustomConventionSetPlugin` also registers the `ProjectablePropertiesNotMappedConvention`, which ensures EF Core's model builder ignores `[Projectable]` properties (they are computed — not mapped to database columns).

## Query Filters

The `ProjectablesExpandQueryFiltersConvention` handles the case where global query filters reference projectable members. It ensures that query filters are also expanded when Projectables is active.


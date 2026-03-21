# Compatibility Mode

Compatibility mode controls **when** and **how** EF Core Projectables expands your projectable members during query execution. The choice affects both performance and query caching behavior.

## Configuration

Set the compatibility mode when registering Projectables:

```csharp
options.UseProjectables(projectables =>
    projectables.CompatibilityMode(CompatibilityMode.Limited));
```

## Modes

### `Full` (Default)

```csharp
options.UseProjectables(); // Full is the default

// Or explicitly:
options.UseProjectables(p => p.CompatibilityMode(CompatibilityMode.Full));
```

In Full mode, the expression tree is **expanded on every individual query invocation**, before being passed to EF Core. This is similar to how libraries like LinqKit work.

**Flow:**
```
LINQ query
  → [Projectables expands all member calls]
  → Expanded query sent to EF Core compiler
  → SQL generated and executed
```

**Characteristics:**
- ✅ Works with **dynamic parameters** — captures fresh parameter values on each execution.
- ✅ Maximum compatibility — works in all EF Core scenarios.
- ⚠️ Slight overhead per query invocation (expression tree walking + expansion).
- ⚠️ EF Core's query cache key changes with expanded expressions, so the compiled query cache may be less effective.

**When to use Full:**
- When you're running into query compilation errors with Limited mode.
- When your projectable members depend on dynamic expressions that change between calls.
- As a safe default while getting started.

---

### `Limited`

```csharp
options.UseProjectables(p => p.CompatibilityMode(CompatibilityMode.Limited));
```

In Limited mode, expansion happens inside **EF Core's query translation preprocessor** — after EF Core accepts the query and before it compiles it. The expanded query is then stored in EF Core's query cache. Subsequent executions with the same query shape skip the expansion step entirely.

**Flow:**
```
LINQ query
  → EF Core query preprocessor
  → [Projectables expands member calls here]
  → Expanded query compiled and stored in query cache
  → SQL generated and executed

Second execution with same query shape:
  → EF Core query cache hit
  → Compiled query reused directly (no expansion needed)
```

**Characteristics:**
- ✅ **Better performance** — after the first execution, cached queries bypass expansion entirely.
- ✅ Often **outperforms vanilla EF Core** for repeated queries.
- ⚠️ Dynamic parameters captured as closures may not work correctly — the expanded query is cached with the parameter values from the first execution.
- ⚠️ If a projectable member uses external runtime state (not EF Core query parameters), the cached expansion may be stale.

**When to use Limited:**
- When all your projectable members' logic is deterministic given the query parameters.
- In production environments where query performance is critical.
- When queries are executed many times with the same shape.

## Performance Comparison

| Scenario                       | Full                        | Limited                              | Vanilla EF Core |
|--------------------------------|-----------------------------|--------------------------------------|-----------------|
| First query execution          | Slower (expansion overhead) | Slower (expansion + compile)         | Baseline        |
| Subsequent executions          | Slower (expansion overhead) | **Faster** (cache hit, no expansion) | Baseline        |
| Dynamic projectable parameters | ✅ Correct                   | ⚠️ May be stale                      | N/A             |

## Choosing a Mode

```
Start with Full (default)
  ↓
Is performance critical?
  → No: Stay on Full
  → Yes: Try Limited
      ↓
    Do your queries produce correct results with Limited?
      → Yes: Use Limited
      → No: Stay on Full
```

## Troubleshooting

### Queries returning wrong results in Limited mode

If you're using projectable members that depend on values computed at runtime (outside of EF Core's parameter system), Limited mode may cache the wrong expansion. Switch to Full mode.

### Query compilation errors in Full mode

If Full mode causes compilation errors related to expression tree translation, check that your projectable members only use EF Core-translatable expressions. Refer to [Limitations](/advanced/limitations).


# Limitations & Known Issues

This page documents the current limitations of EF Core Projectables and guidance on how to work around them.


## Members Must Have a Body

A `[Projectable]` member must have an **expression body** or a **block body** (with `AllowBlockBody = true`). Abstract members, interface declarations, and auto-properties without accessors are not supported and produce error EFP0006.

```csharp
// ❌ Error EFP0006 — no body
[Projectable]
public string FullName { get; set; }

// ✅ Expression-bodied property
[Projectable]
public string FullName => FirstName + " " + LastName;
```

Use [`UseMemberBody`](/reference/use-member-body) to delegate to another member if the projectable itself can't have a body.

## Null-Conditional Operators Require Configuration

The null-conditional operator (`?.`) cannot be used in projectable members unless `NullConditionalRewriteSupport` is set. The default (`None`) produces error EFP0002.

```csharp
// ❌ Error EFP0002
[Projectable]
public string? City => Address?.City;

// ✅ Configured
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
public string? City => Address?.City;
```

See [Null-Conditional Rewrite](/reference/null-conditional-rewrite).

## Block Body Restrictions

When using block-bodied members (experimental), the following constructs are **not supported**:

- `while`, `for`, `foreach` loops (EFP0003)
- `try` / `catch` / `finally` blocks (EFP0003)
- `throw` statements (EFP0003)
- Local variables inside nested blocks (only top-level variable declarations are supported)

```csharp
// ❌ Not supported
[Projectable(AllowBlockBody = true)]
public int Process()
{
    for (int i = 0; i < 10; i++) { ... }  // EFP0003
    return result;
}

// ✅ Use LINQ
[Projectable]
public int Process() => Items.Take(10).Sum(i => i.Value);
```

## Local Variables Are Inlined (No De-duplication)

In block-bodied members, local variables are **inlined at every usage point**. If a variable is used multiple times, the initializer expression is duplicated. This can:

- Increase SQL complexity.
- Change semantics if the initializer has observable side effects (though side effects are detected as EFP0004/EFP0005).

```csharp
// The initializer "Value * 2" appears twice in the generated expression
[Projectable(AllowBlockBody = true)]
public int Foo()
{
    var doubled = Value * 2;
    return doubled + doubled;  // → (Value * 2) + (Value * 2)
}
```

## Expression Tree Restrictions Apply

Since projectable members are ultimately compiled to expression trees, all standard expression tree limitations apply:

- **No `dynamic` typing** — expression trees must be statically typed.
- **No `ref` or `out` parameters**.
- **No named/optional parameters in LINQ** — parameters must be passed positionally in query expressions.
- **No multi-statement lambdas** — expression-bodied members must be single expressions (block bodies go through the converter, but with the limitations above).
- **Only EF Core-translatable operations** — the generated expression will ultimately be translated to SQL by EF Core. Any operation that EF Core cannot translate (e.g., calling a .NET method that has no SQL equivalent) will cause a runtime query translation error.

## EF Core Translatable Operations Only

The body of a projectable member can only use:

- Mapped entity properties and navigation properties.
- Other `[Projectable]` members (transitively expanded).
- EF Core built-in functions (e.g., `EF.Functions.Like(...)`, `DateTime.Now`, string methods EF Core knows).
- LINQ methods EF Core supports (`Where`, `Sum`, `Any`, `Select`, etc.).

```csharp
// ❌ Path.Combine has no SQL equivalent — runtime error
[Projectable]
public string FilePath => Path.Combine(Directory, FileName);

// ✅ String concatenation — translated by EF Core
[Projectable]
public string FilePath => Directory + "/" + FileName;
```

## Limited Compatibility Mode and Dynamic State

[Limited mode](/reference/compatibility-mode) caches the expanded query after the first execution. If a projectable member's expansion depends on external state that changes between calls (not through standard EF Core query parameters), the cached expansion may be stale.

## No Support for Generic Type Parameters on Methods

Generic method parameters are not supported on projectable methods:

```csharp
// ❌ Not supported
[Projectable]
public T GetValue<T>() => ...;
```

Generic **class** type parameters (on the containing entity) are supported.

## Performance: First-Execution Overhead

Both compatibility modes have a one-time cost on first execution:

- **Full mode:** Expression walking + expansion on every execution.
- **Limited mode:** Expression walking + expansion on first execution; subsequent calls use EF Core's query cache.

For performance-critical code paths, consider Limited mode to amortize this cost.


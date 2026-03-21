# Diagnostics & Code Fixes

The Projectables source generator emits diagnostics (warnings and errors) during compilation to help you identify and fix issues with your projectable members. Many diagnostics also have **IDE code fixes** that resolve them automatically.

## Overview

| ID | Severity | Title | Code Fix |
|---|---|---|---|
| [EFP0001](#efp0001) | ⚠️ Warning | Block-bodied member support is experimental | [Add `AllowBlockBody = true`](#efp0001-fix) |
| [EFP0002](#efp0002) | ❌ Error | Null-conditional expression not configured | [Set `NullConditionalRewriteSupport`](#efp0002-fix) |
| [EFP0003](#efp0003) | ⚠️ Warning | Unsupported statement in block-bodied method | — |
| [EFP0004](#efp0004) | ❌ Error | Statement with side effects in block-bodied method | — |
| [EFP0005](#efp0005) | ⚠️ Warning | Potential side effect in block-bodied method | — |
| [EFP0006](#efp0006) | ❌ Error | Method or property requires a body definition | — |
| [EFP0007](#efp0007) | ❌ Error | Unsupported pattern in projectable expression | — |
| [EFP0008](#efp0008) | ❌ Error | Target class is missing a parameterless constructor | [Add parameterless constructor](#efp0008-fix) |
| [EFP0009](#efp0009) | ❌ Error | Delegated constructor cannot be analyzed for projection | — |
| [EFP0010](#efp0010) | ❌ Error | UseMemberBody target member not found | — |
| [EFP0011](#efp0011) | ❌ Error | UseMemberBody target member is incompatible | — |
| [EFP0012](#efp0012) | ℹ️ Info | [Projectable] factory method can be converted to a constructor | [Convert to constructor](#efp0012-fix) |

---

## EFP0001 — Block-bodied member support is experimental {#efp0001}

**Severity:** Warning  
**Category:** Design

### Message

```
Block-bodied member '{0}' is using an experimental feature. 
Set AllowBlockBody = true on the Projectable attribute to suppress this warning.
```

### Cause

A `[Projectable]` member uses a block body (`{ ... }`) instead of an expression body (`=>`), which is an experimental feature.

### Fix {#efp0001-fix}

The IDE offers a quick-fix to add `AllowBlockBody = true` automatically. You can also apply it manually:

```csharp
// Before (warning)
[Projectable]
public string GetCategory()
{
    if (Value > 100) return "High";
    return "Low";
}

// After (warning suppressed)
[Projectable(AllowBlockBody = true)]
public string GetCategory()
{
    if (Value > 100) return "High";
    return "Low";
}
```

Or convert to an expression-bodied member:

```csharp
[Projectable]
public string GetCategory() => Value > 100 ? "High" : "Low";
```

---

## EFP0002 — Null-conditional expression not configured {#efp0002}

**Severity:** Error  
**Category:** Design

### Message

```
'{0}' has a null-conditional expression exposed but is not configured to rewrite this 
(Consider configuring a strategy using the NullConditionalRewriteSupport property 
on the Projectable attribute)
```

### Cause

The projectable member's body contains a null-conditional operator (`?.`) but `NullConditionalRewriteSupport` is not configured (defaults to `None`).

### Fix {#efp0002-fix}

The IDE offers two code-fix options — **Ignore** and **Rewrite** — which set the attribute property automatically:

```csharp
// ❌ Error
[Projectable]
public string? FullAddress => Location?.AddressLine1 + " " + Location?.City;

// ✅ Option 1: Ignore (strips the ?. — safe for SQL Server)
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
public string? FullAddress => Location?.AddressLine1 + " " + Location?.City;

// ✅ Option 2: Rewrite (explicit null checks)
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
public string? FullAddress => Location?.AddressLine1 + " " + Location?.City;

// ✅ Option 3: Rewrite the expression manually
[Projectable]
public string? FullAddress =>
    Location != null ? Location.AddressLine1 + " " + Location.City : null;
```

See [Null-Conditional Rewrite](/reference/null-conditional-rewrite) for details.

---

## EFP0003 — Unsupported statement in block-bodied method {#efp0003}

**Severity:** Warning  
**Category:** Design

### Message

```
Method '{0}' contains an unsupported statement: {1}
```

### Cause

A block-bodied `[Projectable]` member contains a statement type that cannot be converted to an expression tree (e.g., loops, try-catch, throw, new object instantiation in statement position).

### Unsupported Statements

- `while`, `for`, `foreach` loops
- `try`/`catch`/`finally` blocks
- `throw` statements
- Object instantiation as a statement (not in a `return`)

### Fix

Refactor to use only supported constructs (`if`/`else`, `switch`, local variables, `return`), or convert to an expression-bodied member:

```csharp
// ❌ Warning: loops are not supported
[Projectable(AllowBlockBody = true)]
public int SumItems()
{
    int total = 0;
    foreach (var item in Items)  // EFP0003
        total += item.Price;
    return total;
}

// ✅ Use LINQ instead
[Projectable]
public int SumItems() => Items.Sum(i => i.Price);
```

---

## EFP0004 — Statement with side effects in block-bodied method {#efp0004}

**Severity:** Error  
**Category:** Design

### Message

Context-specific — one of:

- `Property assignment '{0}' has side effects and cannot be used in projectable methods`
- `Compound assignment operator '{0}' has side effects`
- `Increment/decrement operator '{0}' has side effects`

### Cause

A block-bodied projectable member modifies state. Expression trees cannot represent side effects.

### Triggers

```csharp
// ❌ Property assignment
Bar = 10;

// ❌ Compound assignment
Bar += 10;

// ❌ Increment / Decrement
Bar++;
--Count;
```

### Fix

Remove the side-effecting statement. Projectable members must be **pure functions** — they can only read data and return a value.

```csharp
// ❌ Error: has side effects
[Projectable(AllowBlockBody = true)]
public int Foo()
{
    Bar = 10;       // EFP0004
    return Bar;
}

// ✅ Read-only computation
[Projectable]
public int Foo() => Bar + 10;
```

---

## EFP0005 — Potential side effect in block-bodied method {#efp0005}

**Severity:** Warning  
**Category:** Design

### Message

```
Method call '{0}' may have side effects and cannot be guaranteed to be safe in projectable methods
```

### Cause

A block-bodied projectable member calls a method that is **not** itself marked with `[Projectable]`. Such calls may have side effects that cannot be represented in an expression tree.

### Example

```csharp
[Projectable(AllowBlockBody = true)]
public int Foo()
{
    Console.WriteLine("test");  // ⚠️ EFP0005 — may have side effects
    return Bar;
}
```

### Fix

- Remove the method call if it is not needed in a query context.
- If the method is safe to use in queries, mark it with `[Projectable]`.

---

## EFP0006 — Method or property requires a body definition {#efp0006}

**Severity:** Error  
**Category:** Design

### Message

```
Method or property '{0}' should expose a body definition (e.g. an expression-bodied member 
or a block-bodied method) to be used as the source for the generated expression tree.
```

### Cause

A `[Projectable]` member has no body — it is abstract, an interface declaration, or an auto-property without an expression.

### Fix

Provide a body, or use [`UseMemberBody`](/reference/use-member-body) to delegate to another member:

```csharp
// ❌ Error: no body
[Projectable]
public string FullName { get; set; }

// ✅ Expression-bodied property
[Projectable]
public string FullName => FirstName + " " + LastName;

// ✅ Delegate to another member
[Projectable(UseMemberBody = nameof(ComputeFullName))]
public string FullName => ComputeFullName();
private string ComputeFullName() => FirstName + " " + LastName;
```

---

## EFP0007 — Unsupported pattern in projectable expression {#efp0007}

**Severity:** Error  
**Category:** Design

### Message

```
The pattern '{0}' cannot be rewritten into an expression tree.
Simplify the pattern or restructure the projectable member body.
```

### Cause

A pattern used inside a `[Projectable]` member (e.g. in a `switch` expression or an `is` expression) cannot be translated into an expression tree. Unsupported patterns include positional/deconstruct patterns and variable designations outside switch arms.

### Supported Patterns

| Pattern | Example |
|---|---|
| Constant | `1 => "one"` |
| Discard / default | `_ => "other"` |
| Type | `GroupItem g => …` |
| Relational | `>= 90 => "A"` |
| `and` / `or` combined | `>= 80 and < 90 => "B"` |
| `when` guard | `4 when Prop == 12 => …` |
| Property | `entity is { IsActive: true }` |
| `not null` / `not` | `Name is not null` |

### Fix

Rewrite using a supported pattern or convert to an explicit conditional expression:

```csharp
// ❌ Error — positional pattern not supported
[Projectable]
public bool IsOrigin(Point p) => p is (0, 0);

// ✅ Use a property pattern instead
[Projectable]
public bool IsOrigin(Point p) => p is { X: 0, Y: 0 };
```

See also: [Pattern Matching](/reference/pattern-matching).

---

## EFP0008 — Target class is missing a parameterless constructor {#efp0008}

**Severity:** Error  
**Category:** Design

### Message

```
Class '{0}' must have a parameterless constructor to be used with a [Projectable] constructor.
The generated projection uses 'new {0}() { ... }' (object-initializer syntax),
which requires an accessible parameterless constructor.
```

### Cause

A constructor is marked `[Projectable]`, but the class does not expose a public, internal, or protected-internal parameterless constructor. The generator emits `new T() { … }` syntax which requires one.

### Fix {#efp0008-fix}

The IDE inserts the parameterless constructor automatically. You can also add it manually:

```csharp
public class CustomerDto
{
    public CustomerDto() { }   // ← inserted by code fix

    [Projectable]
    public CustomerDto(Customer customer) { … }
}
```

See also: [Constructor Projections](/guide/projectable-constructors).

---

## EFP0009 — Delegated constructor cannot be analyzed for projection {#efp0009}

**Severity:** Error  
**Category:** Design

### Message

```
The delegated constructor '{0}' in type '{1}' has no source available and cannot be analyzed.
Base/this initializer in member '{2}' will not be projected.
```

### Cause

A `[Projectable]` constructor delegates to another constructor via `: base(…)` or `: this(…)`, but the target constructor's source code is not available in the current compilation (e.g. it lives in a referenced binary).

### Fix

Ensure the delegated constructor's source is available (e.g. move it to the same project), or restructure to avoid the delegation.

---

## EFP0010 — UseMemberBody target member not found {#efp0010}

**Severity:** Error  
**Category:** Design

### Message

```
Member '{1}' referenced by UseMemberBody on '{0}' was not found on type '{2}'
```

### Cause

The name passed to `UseMemberBody` does not match any member on the containing type.

### Fix

Verify the member name — use `nameof(...)` to avoid typos:

```csharp
// ❌ Error — "ComputeFullname" (lowercase n) doesn't exist
[Projectable(UseMemberBody = "ComputeFullname")]
public string FullName => …;

// ✅ Use nameof to catch mistakes at compile time
[Projectable(UseMemberBody = nameof(ComputeFullName))]
public string FullName => …;
private string ComputeFullName => FirstName + " " + LastName;
```

See also: [Use Member Body](/reference/use-member-body).

---

## EFP0011 — UseMemberBody target member is incompatible {#efp0011}

**Severity:** Error  
**Category:** Design

### Message

```
Member '{1}' referenced by UseMemberBody on '{0}' has an incompatible type or signature
```

### Cause

A member with the given name exists on the type but its return type or parameter list is incompatible with the `[Projectable]` member.

### Fix

Align the target member's signature with the projectable member. For `Expression<Func<…>>` properties the lambda parameter types must correspond to the projectable's parameters:

```csharp
// ❌ Error — return type mismatch (string vs int)
[Projectable(UseMemberBody = nameof(ComputeExpr))]
public int Computed => …;
private static Expression<Func<Entity, string>> ComputeExpr => @this => @this.Name;

// ✅ Matching return type
private static Expression<Func<Entity, int>> ComputeExpr => @this => @this.Id * 2;
```

See also: [Use Member Body](/reference/use-member-body).

---

## EFP0012 — [Projectable] factory method can be converted to a constructor {#efp0012}

**Severity:** Info  
**Category:** Design

### Message

```
Factory method '{0}' creates and returns an instance of the containing class via object initializer.
Consider converting it to a [Projectable] constructor.
```

### Cause

A `[Projectable]` static or instance method returns a `new T { … }` object initializer where `T` is the containing class. This is equivalent to a `[Projectable]` constructor and the IDE can convert it automatically.

### Fix {#efp0012-fix}

Two IDE actions are available — as a quick-fix on the diagnostic, or as a refactoring (always available even when the diagnostic is suppressed via the IDE's refactoring menu):

| Action | Scope |
|---|---|
| Convert to constructor | Current document only |
| Convert to constructor (and update callers) | Entire solution |

```csharp
// Before — factory method
public class CustomerDto
{
    [Projectable]
    public static CustomerDto FromCustomer(Customer c) => new CustomerDto
    {
        Id = c.Id,
        Name = c.FirstName + " " + c.LastName
    };
}

// After — projectable constructor (converted by IDE)
public class CustomerDto
{
    public CustomerDto() { }

    [Projectable]
    public CustomerDto(Customer c)
    {
        Id = c.Id;
        Name = c.FirstName + " " + c.LastName;
    }
}
```

See also: [Constructor Projections](/guide/projectable-constructors).

---

## Suppressing Diagnostics

Individual warnings can be suppressed with standard C# pragma directives:

```csharp
#pragma warning disable EFP0001
[Projectable]
public string GetValue()
{
    if (IsActive) return "Active";
    return "Inactive";
}
#pragma warning restore EFP0001
```

Or via `.editorconfig` / `Directory.Build.props`:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);EFP0001;EFP0003</NoWarn>
</PropertyGroup>
```
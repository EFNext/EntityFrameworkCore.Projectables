# Pattern Matching

> [!NOTE]
> Pattern matching support is available starting from version 6.x.

The source generator rewrites C# pattern-matching constructs into expression-tree-compatible ternary and binary expressions that EF Core can translate to SQL `CASE` expressions.

## Switch Expressions

### Relational and Constant Patterns

```csharp
[Projectable]
public string GetGrade() => Score switch
{
    >= 90 => "A",
    >= 80 => "B",
    >= 70 => "C",
    _     => "F",
};
```

Generated expression:
```csharp
(@this) => @this.Score >= 90 ? "A"
         : @this.Score >= 80 ? "B"
         : @this.Score >= 70 ? "C"
         : "F"
```

Which EF Core translates to:
```sql
SELECT CASE
    WHEN [e].[Score] >= 90 THEN N'A'
    WHEN [e].[Score] >= 80 THEN N'B'
    WHEN [e].[Score] >= 70 THEN N'C'
    ELSE N'F'
END
```

### `and` / `or` Combined Patterns

```csharp
[Projectable]
public string GetBand() => Score switch
{
    >= 90 and <= 100 => "Excellent",
    >= 70 and < 90   => "Good",
    _                => "Poor",
};
```

### `when` Guards

```csharp
[Projectable]
public string Classify() => Value switch
{
    4 when IsSpecial => "Special Four",
    4               => "Regular Four",
    _               => "Other",
};
```

### Type Patterns

Type patterns in switch arms produce a cast and type-check expression:

```csharp
[Projectable]
public static ItemData ToData(this Item item) =>
    item switch
    {
        GroupItem g    => new GroupData(g.Id, g.Name, g.Description),
        DocumentItem d => new DocumentData(d.Id, d.Name, d.Priority),
        _              => null!
    };
```

### Discard / Default

The discard pattern (`_`) maps to the final `else` branch of the generated ternary chain.

---

## `is` Patterns in Expression Bodies

### Relational `and` / `or`

```csharp
// Range check
[Projectable]
public bool IsInRange => Value is >= 1 and <= 100;
// → Value >= 1 && Value <= 100

// Alternative values
[Projectable]
public bool IsEdge => Value is 0 or 100;
// → Value == 0 || Value == 100
```

### `not null` / `not`

```csharp
[Projectable]
public bool HasName => Name is not null;
// → !(Name == null)
```

### Property Patterns

```csharp
[Projectable]
public static bool IsActiveAndPositive(this Entity entity) =>
    entity is { IsActive: true, Value: > 0 };
// → entity != null && entity.IsActive == true && entity.Value > 0
```

---

## Supported Pattern Summary

| Pattern           | Context          | Example                                      |
|-------------------|------------------|----------------------------------------------|
| Constant          | switch arm, `is` | `1 => "one"`, `Value is 42`                  |
| Discard / default | switch arm       | `_ => "other"`                               |
| Relational        | switch arm, `is` | `>= 90 => "A"`, `Value is > 0`               |
| `and` combined    | switch arm, `is` | `>= 80 and < 90`, `Value is >= 1 and <= 100` |
| `or` combined     | switch arm, `is` | `1 or 2 => "low"`, `Value is 0 or > 100`     |
| `not`             | `is`             | `Name is not null`                           |
| `when` guard      | switch arm       | `4 when Prop == 12 => …`                     |
| Type              | switch arm       | `GroupItem g => …`                           |
| Property          | `is`             | `entity is { IsActive: true }`               |

---

## Unsupported Patterns

The following patterns **cannot** be translated into expression trees and produce diagnostic **EFP0007**:

- Positional / deconstruct patterns: `(0, 0) => …`
- Variable designations outside switch arms: `item is GroupItem g` in an `if` condition
- List patterns: `[1, 2, ..]`
- `var` patterns

```csharp
// ❌ EFP0007 — positional pattern not supported
[Projectable]
public bool IsOrigin(Point p) => p is (0, 0);

// ✅ Use a property pattern instead
[Projectable]
public bool IsOrigin(Point p) => p is { X: 0, Y: 0 };
```

See [Diagnostics Reference](/reference/diagnostics#efp0007) for the full EFP0007 description.


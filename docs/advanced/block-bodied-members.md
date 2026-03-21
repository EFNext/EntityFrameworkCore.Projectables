# Block-Bodied Members

As of v6.x, EF Core Projectables supports **block-bodied** properties and methods decorated with `[Projectable]`, in addition to expression-bodied members (`=>`).

::: warning Experimental Feature
Block-bodied member support is currently **experimental**. Set `AllowBlockBody = true` on the attribute to acknowledge this and suppress warning EFP0001.
:::

## Why Block Bodies?

Expression-bodied members are concise but can become hard to read with complex conditional logic:

```csharp
// Hard to read as a nested ternary
[Projectable]
public string Level() => Value > 100 ? "High" : Value > 50 ? "Medium" : "Low";

// Much easier to read as a block body
[Projectable(AllowBlockBody = true)]
public string Level()
{
    if (Value > 100)
        return "High";
    else if (Value > 50)
        return "Medium";
    else
        return "Low";
}
```

Both generate **identical SQL** — the block body is converted to a ternary expression internally.

## Enabling Block Bodies

Add `AllowBlockBody = true` to suppress the experimental warning:

```csharp
[Projectable(AllowBlockBody = true)]
public string GetCategory()
{
    if (Value > 100)
        return "High";
    else
        return "Low";
}
```

## Supported Constructs

### Simple Return Statements

```csharp
[Projectable(AllowBlockBody = true)]
public int GetConstant()
{
    return 42;
}
```

---

### If-Else Statements

If-else chains are converted to ternary (`? :`) expressions:

```csharp
[Projectable(AllowBlockBody = true)]
public string GetCategory()
{
    if (Value > 100)
        return "High";
    else if (Value > 50)
        return "Medium";
    else
        return "Low";
}
// Converted to: Value > 100 ? "High" : Value > 50 ? "Medium" : "Low"
```

---

### If Without Else (Fallback Return)

An `if` statement without an `else` is supported when followed by a fallback `return`:

```csharp
// Pattern 1: explicit fallback return
[Projectable(AllowBlockBody = true)]
public string GetStatus()
{
    if (IsActive)
        return "Active";
    return "Inactive";  // Fallback
}

// Pattern 2: explicit null return
[Projectable(AllowBlockBody = true)]
public int? GetPremium()
{
    if (IsActive)
        return Value * 2;
    return null;
}
```

---

### Multiple Early Returns

Multiple independent early-return `if` statements are converted to a nested ternary chain:

```csharp
[Projectable(AllowBlockBody = true)]
public string GetValueCategory()
{
    if (Value > 100) return "Very High";
    if (Value > 50)  return "High";
    if (Value > 10)  return "Medium";
    return "Low";
}
// → Value > 100 ? "Very High" : (Value > 50 ? "High" : (Value > 10 ? "Medium" : "Low"))
```

---

### Switch Statements

Switch statements are converted to nested ternary expressions:

```csharp
[Projectable(AllowBlockBody = true)]
public string GetValueLabel()
{
    switch (Value)
    {
        case 1: return "One";
        case 2: return "Two";
        case 3: return "Three";
        default: return "Many";
    }
}
```

Multiple cases mapping to the same result are collapsed:

```csharp
switch (Value)
{
    case 1:
    case 2:
        return "Low";
    case 3:
    case 4:
    case 5:
        return "Medium";
    default:
        return "High";
}
```

Generated SQL:
```sql
SELECT CASE
    WHEN [e].[Value] IN (1, 2) THEN N'Low'
    WHEN [e].[Value] IN (3, 4, 5) THEN N'Medium'
    ELSE N'High'
END
FROM [Entity] AS [e]
```

---

### Local Variables

Local variables declared at the method body level are **inlined** at each usage point:

```csharp
[Projectable(AllowBlockBody = true)]
public int CalculateDouble()
{
    var doubled = Value * 2;
    return doubled + 5;
}
// → (Value * 2) + 5
```

Transitive inlining is supported:

```csharp
[Projectable(AllowBlockBody = true)]
public int CalculateComplex()
{
    var a = Value * 2;
    var b = a + 5;
    return b + 10;
}
// → ((Value * 2) + 5) + 10
```

::: warning Variable Duplication
If a local variable is referenced **multiple times**, its initializer is duplicated at each reference point. This can affect performance (and semantics if the initializer has side effects):

```csharp
[Projectable(AllowBlockBody = true)]
public int Foo()
{
    var x = ExpensiveComputation();  // Inlined at each use
    return x + x;                    // → ExpensiveComputation() + ExpensiveComputation()
}
```
:::

**Local variables are only supported at the method body level** — not inside nested blocks (inside `if`, `switch`, etc.).

## SQL Output Examples

### If-Else → CASE WHEN

```csharp
public record Entity
{
    public int Value { get; set; }
    public bool IsActive { get; set; }

    [Projectable(AllowBlockBody = true)]
    public int GetAdjustedValue()
    {
        if (IsActive && Value > 0)
            return Value * 2;
        else
            return 0;
    }
}
```

Generated SQL:
```sql
SELECT CASE
    WHEN [e].[IsActive] = CAST(1 AS bit) AND [e].[Value] > 0 
    THEN [e].[Value] * 2
    ELSE 0
END
FROM [Entity] AS [e]
```

### Switch → CASE WHEN IN

```csharp
[Projectable(AllowBlockBody = true)]
public string Category
{
    get
    {
        switch (Status)
        {
            case 1: case 2: return "Low";
            case 3: case 4: case 5: return "Medium";
            default: return "High";
        }
    }
}
```

Generated SQL:
```sql
SELECT CASE
    WHEN [e].[Status] IN (1, 2) THEN N'Low'
    WHEN [e].[Status] IN (3, 4, 5) THEN N'Medium'
    ELSE N'High'
END
FROM [Entity] AS [e]
```

## Limitations and Unsupported Constructs

The following statement types produce **warning EFP0003** and are not supported:

| Construct                             | Reason                                             |
|---------------------------------------|----------------------------------------------------|
| `while` / `for` / `foreach` loops     | Cannot be represented as expression trees          |
| `try` / `catch` / `finally`           | Cannot be represented as expression trees          |
| `throw` statements                    | Cannot be represented as expression trees          |
| `new MyClass()` in statement position | Object instantiation not supported in this context |

```csharp
// ❌ Warning EFP0003 — loops are not supported
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

## Side Effect Detection

The generator actively detects statements with side effects and reports them as errors (EFP0004) or warnings (EFP0005). See [Diagnostics](/reference/diagnostics) for the full list.

| Code                      | Diagnostic                               |
|---------------------------|------------------------------------------|
| `Bar = 10;`               | ❌ EFP0004 — property assignment          |
| `Bar += 10;`              | ❌ EFP0004 — compound assignment          |
| `Bar++;`                  | ❌ EFP0004 — increment/decrement          |
| `Console.WriteLine("x");` | ⚠️ EFP0005 — non-projectable method call |

## How the Conversion Works

The `BlockStatementConverter` class in the source generator:

1. Collects all local variable declarations at the method body level.
2. Identifies the `return` statements and their conditions.
3. Converts `if`/`else` chains into ternary expression syntax nodes.
4. Converts `switch` statements into nested ternary expressions (or `case IN (...)` optimized forms).
5. Substitutes local variable references with their initializer expressions (via `VariableReplacementRewriter`).
6. Passes the resulting expression syntax to the standard expression rewriter pipeline.

The output is equivalent to what would have been produced by an expression-bodied member with the same logic.


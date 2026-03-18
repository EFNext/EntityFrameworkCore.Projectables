# EntityFrameworkCore.Projectables
Flexible projection magic for EF Core

[![NuGet version (EntityFrameworkCore.Projectables)](https://img.shields.io/nuget/v/EntityFrameworkCore.Projectables.Abstractions.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables.Abstractions/)
[![.NET](https://github.com/koenbeuk/EntityFrameworkCore.Projectables/actions/workflows/build.yml/badge.svg)](https://github.com/koenbeuk/EntityFrameworkCore.Projectables/actions/workflows/build.yml)

## NuGet packages
- EntityFrameworkCore.Projectables.Abstractions [![NuGet version](https://img.shields.io/nuget/v/EntityFrameworkCore.Projectables.Abstractions.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables.Abstractions/) [![NuGet](https://img.shields.io/nuget/dt/EntityFrameworkCore.Projectables.Abstractions.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables.Abstractions/)
- EntityFrameworkCore.Projectables [![NuGet version](https://img.shields.io/nuget/v/EntityFrameworkCore.Projectables.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables/) [![NuGet](https://img.shields.io/nuget/dt/EntityFrameworkCore.Projectables.svg?style=flat-square)](https://www.nuget.org/packages/EntityFrameworkCore.Projectables/)

> Starting with V2 of this project we're binding against **EF Core 6**. If you're targeting **EF Core 5** or **EF Core 3.1** then you can use the latest v1 release. These are functionally equivalent.


## Getting started
1. Install the package from [NuGet](https://www.nuget.org/packages/EntityFrameworkCore.Projectables/)
2. Enable Projectables in your DbContext by adding: `dbContextOptions.UseProjectables()`
3. Implement projectable properties and methods, marking them with the `[Projectable]` attribute.
4. Explore our [samples](https://github.com/koenbeuk/EntityFrameworkCore.Projectables/tree/master/samples) and checkout our [Blog Post](https://onthedrift.com/posts/efcore-projectables/) for further guidance.

### Example
Assuming this sample:

```csharp
class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedDate { get; set; }

    public decimal TaxRate { get; set; }
    
    public User User { get; set; } 
    public ICollection<OrderItem> Items { get; set; }

    [Projectable] public decimal Subtotal => Items.Sum(item => item.Product.ListPrice * item.Quantity);
    [Projectable] public decimal Tax => Subtotal * TaxRate;
    [Projectable] public decimal GrandTotal => Subtotal + Tax;
}

public static class UserExtensions
{
    [Projectable]
    public static Order GetMostRecentOrderForUser(this User user, DateTime? cutoffDate) => 
        user.Orders
            .Where(x => cutoffDate == null || x.CreatedDate >= cutoffDate)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefault();
}

var result = _dbContext.Users
    .Where(x => x.UserName == "Jon")
    .Select(x => new {
        x.GetMostRecentOrderForUser(DateTime.UtcNow.AddDays(-30)).GrandTotal
    });
    .FirstOrDefault();
```

The following query gets generated (assuming SQL Server as a database provider)
```sql
DECLARE @__sampleUser_UserName_0 nvarchar(4000) = N'Jon';

SELECT (
    SELECT COALESCE(SUM([p].[ListPrice] * CAST([o].[Quantity] AS decimal(18,2))), 0.0)
    FROM [OrderItem] AS [o]
    INNER JOIN [Products] AS [p] ON [o].[ProductId] = [p].[Id]
    WHERE (
        SELECT TOP(1) [o0].[Id]
        FROM [Orders] AS [o0]
        WHERE [u].[Id] = [o0].[UserId] AND [o0].[FulfilledDate] IS NOT NULL
        ORDER BY [o0].[CreatedDate] DESC) IS NOT NULL AND (
        SELECT TOP(1) [o1].[Id]
        FROM [Orders] AS [o1]
        WHERE [u].[Id] = [o1].[UserId] AND [o1].[FulfilledDate] IS NOT NULL
        ORDER BY [o1].[CreatedDate] DESC) = [o].[OrderId]) * (
    SELECT TOP(1) [o2].[TaxRate]
    FROM [Orders] AS [o2]
    WHERE [u].[Id] = [o2].[UserId] AND [o2].[FulfilledDate] IS NOT NULL
    ORDER BY [o2].[CreatedDate] DESC) AS [GrandTotal]
FROM [Users] AS [u]
WHERE [u].[UserName] = @__sampleUser_UserName_0
```

Projectable properties and methods have been inlined! the generated SQL could be improved but this is what EF Core (v8) gives us.

### How it works
Essentially, there are two components: We have a source generator that can write companion expressions for properties and methods marked with the Projectable attribute. Then, we have a runtime component that intercepts any query and translates any call to a property or method marked with the Projectable attribute, translating the query to use the generated expression instead.

### FAQ

#### Are there currently any known limitations?
Currently, there is no support for overloaded methods. Each method name needs to be unique within a given type.

#### Is this specific to a database provider?
No, the runtime component injects itself into the EFCore query compilation pipeline, thus having no impact on the database provider used. Of course, you're still limited to whatever your database provider can do.

#### Are there performance implications that I should be aware of?
There are two compatibility modes: Limited and Full (Default). Most of the time, limited compatibility mode is sufficient. However, if you are running into issues with failed query compilation, then you may want to stick with Full compatibility mode. With Full compatibility mode, each query will first be expanded (any calls to Projectable properties and methods will be replaced by their respective expression) before being handed off to EFCore. (This is similar to how LinqKit/LinqExpander/Expressionify works.) Because of this additional step, there is a small performance impact. Limited compatibility mode is smart about things and only expands the query after it has been accepted by EF. The expanded query will then be stored in the Query Cache. With Limited compatibility, you will likely see increased performance over EFCore without Projectables.

#### Can I call additional properties and methods from my Projectable properties and methods?
Yes, you can! Any projectable property/method can call into other properties and methods as long as those properties/methods are native to EFCore or marked with a Projectable attribute.

#### Can I use projectable extensions methods on non-entity types?
Yes you can. It's perfectly acceptable to have the following code:
```csharp
[Projectable]
public static int Squared(this int i) => i * i;
```
Any call to squared given any int will perfectly translate to SQL.

#### How do I deal with nullable properties
Expressions and Lambdas are different and not equal. Expressions can only express a subset of valid C# statements that are allowed in lambda's and arrow functions. One obvious limitation is the null-conditional operator. Consider the following example:
```csharp
[Projectable]
public static string? GetFullAddress(this User? user) => user?.Location?.AddressLine1 + " " + user?.Location.AddressLine2;
```
This is a perfectly valid arrow function, but it can't be translated directly to an expression tree. This Project will generate an error by default and suggest 2 solutions: Either you rewrite the function to explicitly check for nullables or you let the generator do that for you!

Starting from the official release of V2, we can now hint the generator in how to translate this arrow function to an expression tree. We can say:
```csharp
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Ignore)]
``` 
which will simply generate an expression tree that ignores the null-conditional operator. This generates: 
```csharp
user.Location.AddressLine1 + " " + user.Location.AddressLine2
```
This is perfect for a database like SQL Server where nullability is implicit and if any of the arguments were to be null, the resulting value will be null. If you are dealing with CosmosDB (which may result to client-side evaluation) or want to be explicit about things. You can configure your projectable as such: 
```csharp
[Projectable(NullConditionalRewriteSupport = NullConditionalRewriteSupport.Rewrite)]
```
This will rewrite your expression to explicitly check for nullables. In the former example, this will be rewritten to: 
```csharp 
(user != null ? user.Location != null ? user.Location?.AddressLine1 + (user != null ? user.Location != null ? user.Location.AddressLine2 : null) : null)
```
Note that using rewrite (not ignore) may increase the actual SQL query complexity being generated with some database providers such as SQL Server

#### Can I use Projectables in any part of my query?
Certainly, consider the following example: 
```csharp
public class User
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }

    [Projectable]
    public string FullName => FirstName + " " + LastName;
}

var query = dbContext.Users
    .Where(x => x.FullName.Contains("Jon"))
    .GroupBy(x => x.FullName)
    .OrderBy(x => x.Key)
    .Select(x => x.Key);
```
Which generates the following SQL (SQLite syntax)
```sql
SELECT (COALESCE("u"."FirstName", '') || ' ') || COALESCE("u"."LastName", '')
FROM "Users" AS "u"
WHERE ('Jon' = '') OR (instr((COALESCE("u"."FirstName", '') || ' ') || COALESCE("u"."LastName", ''), 'Jon') > 0)
GROUP BY (COALESCE("u"."FirstName", '') || ' ') || COALESCE("u"."LastName", '')
ORDER BY (COALESCE("u"."FirstName", '') || ' ') || COALESCE("u"."LastName", '')
```

#### Can I use block-bodied members instead of expression-bodied members?

> [!NOTE]
> This feature is available starting from version 6.x and is considered experimental. 

Yes! you can now use traditional block-bodied members with `[Projectable]`. This makes code more readable when dealing with complex conditional logic:

```csharp
// Expression-bodied (still supported)
[Projectable]
public string Level() => Value > 100 ? "High" : Value > 50 ? "Medium" : "Low";

// Block-bodied (now also supported!)
[Projectable(AllowBlockBody = true)] // Note: AllowBlockBody is required to remove the warning for experimental feature usage
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

> This is an experimental feature and may have some limitations. Please refer to the documentation for details.

Both generate identical SQL. Block-bodied members support:
- If-else statements (converted to ternary/CASE expressions)
- Switch statements
- Local variables (automatically inlined)
- Simple return statements

The generator will also detect and report side effects (assignments, method calls to non-projectable members, etc.) with precise error messages. See [Block-Bodied Members Documentation](docs/BlockBodiedMembers.md) for complete details.

#### Can I use `[Projectable]` on a constructor?

> [!NOTE]
> This feature is available starting from version 6.x.

Yes! constructors can now be marked with `[Projectable]`. The generator will produce a member-init expression (`new T() { Prop = value, … }`) that EF Core can translate to a SQL projection.

**Requirements:**
- The class must expose an accessible **parameterless constructor** (public, internal, or protected-internal), because the generated code relies on `new T() { … }` syntax.
- If a parameterless constructor is missing, the generator reports **EFP0008**.

```csharp
public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsActive { get; set; }
    public ICollection<Order> Orders { get; set; }
}
    
public class Order
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public class CustomerDto
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public bool IsActive { get; set; }
    public int OrderCount { get; set; }

    public CustomerDto() { }   // required parameterless ctor

    [Projectable]
    public CustomerDto(Customer customer)
    {
        Id = customer.Id;
        FullName = customer.FirstName + " " + customer.LastName;
        IsActive = customer.IsActive;
        OrderCount = customer.Orders.Count();
    }
}

// Usage — the constructor call is translated directly to SQL
var customers = dbContext.Customers
    .Select(c => new CustomerDto(c))
    .ToList();
```

The generator produces an expression equivalent to:
```csharp
(Customer customer) => new CustomerDto()
{
    Id = customer.Id,
    FullName = customer.FirstName + " " + customer.LastName,
    IsActive = customer.IsActive,
    OrderCount = customer.Orders.Count()
}
```

**Supported in constructor bodies:**
- Simple property assignments (`FullName = customer.FirstName + " " + customer.LastName;`)
- Local variable declarations (inlined at usage points)
- If/else and chained if/else-if statements (converted to ternary expressions)
- Switch expressions
- Base/this initializer chains – the generator recursively inlines the delegated constructor's assignments

The base/this initializer chain is particularly useful when you have a DTO inheritance hierarchy:

```csharp
public class PersonDto
{
    public string FullName { get; set; }
    public string Email { get; set; }

    public PersonDto() { }

    [Projectable]
    public PersonDto(Person person)
    {
        FullName = person.FirstName + " " + person.LastName;
        Email = person.Email;
    }
}

public class EmployeeDto : PersonDto
{
    public string Department { get; set; }
    public string Grade { get; set; }

    public EmployeeDto() { }

    [Projectable]
    public EmployeeDto(Employee employee) : base(employee)   // PersonDto assignments are inlined automatically
    {
        Department = employee.Department.Name;
        Grade = employee.YearsOfService >= 10 ? "Senior" : "Junior";
    }
}

// Usage
var employees = dbContext.Employees
    .Select(e => new EmployeeDto(e))
    .ToList();
```

The generated expression inlines both the base constructor and the derived constructor body:
```csharp
(Employee employee) => new EmployeeDto()
{
    FullName = employee.FirstName + " " + employee.LastName,
    Email = employee.Email,
    Department = employee.Department.Name,
    Grade = employee.YearsOfService >= 10 ? "Senior" : "Junior"
}
```

Multiple `[Projectable]` constructors (overloads) per class are fully supported.

> [!NOTE]
> If the delegated constructor's source is not available in the current compilation, the generator reports **EFP0009** and skips the projection.

#### Can I redirect the expression body to a different member with `UseMemberBody`?

Yes! The `UseMemberBody` property on `[Projectable]` lets you redirect the source of the generated expression to a *different* member on the same type.

This is useful when you want to:

- keep a regular C# implementation for in-memory use while maintaining a separate, cleaner expression for EF Core
- supply the body as a pre-built `Expression<Func<...>>` property for full control over the generated tree

##### Delegating to a method or property body

The simplest case — point `UseMemberBody` at another method or property that has the **same return type and parameter signature**. The generator uses the body of the target member instead:

```csharp
public class Entity
{
    public int Id { get; set; }

    // EF-side: generates an expression from ComputedImpl
    [Projectable(UseMemberBody = nameof(ComputedImpl))]
    public int Computed => Id;            // original body is ignored

    // In-memory implementation (or a different algorithm)
    private int ComputedImpl => Id * 2;
}
```

The generated expression is `(@this) => @this.Id * 2`, so `Computed` projects as `Id * 2` in SQL even though the arrow body says `Id`.

> [!NOTE]
> When delegating to a regular method or property body the target member must be declared in the **same source file** as the `[Projectable]` member so the generator can read its body.

##### Using an `Expression<Func<...>>` property as the body

For even more control you can supply the body as a typed `Expression<Func<...>>` property. This lets you write the expression once and reuse it from both the `[Projectable]` member and any runtime code that needs the expression tree directly:

```csharp
public class Entity
{
    public int Id { get; set; }

    [Projectable(UseMemberBody = nameof(Computed4))]
    public int Computed3 => Id;   // body is replaced at compile time

    // The expression tree is picked up by the generator and by the runtime resolver
    private static Expression<Func<Entity, int>> Computed4 => x => x.Id * 3;
}
```

Unlike regular method/property delegation, `Expression<Func<...>>` backing properties may be declared in a **different file** — for example in a separate part of a `partial class`:

```csharp
// File: Entity.cs
public partial class Entity
{
    public int Id { get; set; }

    [Projectable(UseMemberBody = nameof(IdDoubledExpr))]
    public int Computed => Id;
}

// File: Entity.Expressions.cs
public partial class Entity
{
    private static Expression<Func<Entity, int>> IdDoubledExpr => @this => @this.Id * 2;
}
```

For **instance methods**, the generator automatically aligns lambda parameter names with the method's own parameter names, so you are free to choose any names in the lambda. Using `@this` for the receiver is conventional and avoids any renaming:

```csharp
public class Entity
{
    public int Value { get; set; }

    [Projectable(UseMemberBody = nameof(IsPositiveExpr))]
    public bool IsPositive() => Value > 0;

    // Any receiver name works; @this is conventional
    private static Expression<Func<Entity, bool>> IsPositiveExpr => @this => @this.Value > 0;
}
```

If the lambda parameter names differ from the method's parameter names the generator renames them automatically:

```csharp
// Lambda uses (c, t) but method parameter is named threshold — generated code uses threshold
private static Expression<Func<Entity, int, bool>> ExceedsThresholdExpr =>
    (c, t) => c.Value > t;
```

##### Static extension methods

`UseMemberBody` works equally well on static extension methods. Name the lambda parameters to match the method's parameter names:

```csharp
public static class FooExtensions
{
    [Projectable(UseMemberBody = nameof(NameEqualsExpr))]
    public static bool NameEquals(this Foo a, Foo b) => a.Name == b.Name;

    private static Expression<Func<Foo, Foo, bool>> NameEqualsExpr =>
        (a, b) => a.Name == b.Name;
}
```

The generated expression is `(Foo a, Foo b) => a.Name == b.Name` — the same lambda that EF Core receives at query time. The two implementations are kept in sync in one place.

##### Diagnostics

| Code        | Severity | Cause                                                                                                |
|-------------|----------|------------------------------------------------------------------------------------------------------|
| **EFP0010** | Error    | The name given to `UseMemberBody` does not match any member on the containing type                   |
| **EFP0011** | Error    | A member with that name exists but its type or signature is incompatible with the projectable member |

#### Can I use pattern matching in projectable members?

> [!NOTE]
> This feature is available starting from version 6.x.

Yes! the generator supports a rich set of C# pattern-matching constructs and rewrites them into expression-tree-compatible ternary/binary expressions that EF Core can translate to SQL CASE expressions.

**Switch expressions** with the following arm patterns are supported:

| Pattern               | Example                  |
|-----------------------|--------------------------|
| Constant              | `1 => "one"`             |
| Discard / default     | `_ => "other"`           |
| Type                  | `GroupItem g => …`       |
| Relational            | `>= 90 => "A"`           |
| `and` / `or` combined | `>= 80 and < 90 => "B"`  |
| `when` guard          | `4 when Prop == 12 => …` |

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

Generated expression (which EF Core translates to a SQL CASE):
```csharp
(@this) => @this.Score >= 90 ? "A" : @this.Score >= 80 ? "B" : @this.Score >= 70 ? "C" : "F"
```

**`is` patterns** in expression-bodied members are also supported:

```csharp
// Range check using 'and'
[Projectable]
public bool IsInRange => Value is >= 1 and <= 100;

// Alternative-value check using 'or'
[Projectable]
public bool IsOutOfRange => Value is 0 or > 100;

// Null check using 'not'
[Projectable]
public bool HasName => Name is not null;

// Property pattern
[Projectable]
public static bool IsActiveAndPositive(this Entity entity) =>
    entity is { IsActive: true, Value: > 0 };
```

These are all rewritten into plain binary/unary expressions that expression trees support:
```csharp
// Value is >= 1 and <= 100  →  Value >= 1 && Value <= 100
// Name is not null          →  !(Name == null)
// entity is { IsActive: true, Value: > 0 }
//   → entity != null && entity.IsActive == true && entity.Value > 0
```

**Type patterns in switch arms** produce a cast + type-check:
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

Unsupported patterns (e.g. positional/deconstruct patterns, variable designations outside switch arms) are reported as **EFP0007**.

#### How do I expand enum extension methods?

> [!NOTE]
> This feature is available starting from version 6.x.

When you have an enum property and want to call an extension method on it (like getting a display name from a `[Display]` attribute), you can use the `ExpandEnumMethods` property on the `[Projectable]` attribute. This will expand the enum method call into a chain of ternary expressions for each enum value, allowing EF Core to translate it to SQL CASE expressions.

```csharp
public enum OrderStatus
{
    [Display(Name = "Pending Review")]
    Pending,
    
    [Display(Name = "Approved")]
    Approved,
    
    [Display(Name = "Rejected")]
    Rejected
}

public static class EnumExtensions
{
    public static string GetDisplayName(this OrderStatus value)
    {
        // Your implementation here
        return value.ToString();
    }
    
    public static bool IsApproved(this OrderStatus value)
    {
        return value == OrderStatus.Approved;
    }
    
    public static int GetSortOrder(this OrderStatus value)
    {
        return (int)value;
    }
    
    public static string Format(this OrderStatus value, string prefix)
    {
        return prefix + value.ToString();
    }
}

public class Order
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    
    [Projectable(ExpandEnumMethods = true)]
    public string StatusName => Status.GetDisplayName();
    
    [Projectable(ExpandEnumMethods = true)]
    public bool IsStatusApproved => Status.IsApproved();
    
    [Projectable(ExpandEnumMethods = true)]
    public int StatusOrder => Status.GetSortOrder();
    
    [Projectable(ExpandEnumMethods = true)]
    public string FormattedStatus => Status.Format("Status: ");
}
```

This generates expression trees equivalent to:
```csharp
// For StatusName
@this.Status == OrderStatus.Pending ? GetDisplayName(OrderStatus.Pending) 
    : @this.Status == OrderStatus.Approved ? GetDisplayName(OrderStatus.Approved) 
    : @this.Status == OrderStatus.Rejected ? GetDisplayName(OrderStatus.Rejected) 
    : null

// For IsStatusApproved (boolean)
@this.Status == OrderStatus.Pending ? false 
    : @this.Status == OrderStatus.Approved ? true 
    : @this.Status == OrderStatus.Rejected ? false 
    : default(bool)
```

Which EF Core translates to SQL CASE expressions:
```sql
SELECT CASE
    WHEN [o].[Status] = 0 THEN N'Pending Review'
    WHEN [o].[Status] = 1 THEN N'Approved'
    WHEN [o].[Status] = 2 THEN N'Rejected'
END AS [StatusName]
FROM [Orders] AS [o]
```

The `ExpandEnumMethods` feature supports:
- **String return types** - returns `null` as the default fallback
- **Boolean return types** - returns `default(bool)` (false) as the default fallback
- **Integer return types** - returns `default(int)` (0) as the default fallback
- **Other value types** - returns `default(T)` as the default fallback
- **Nullable enum types** - wraps the expansion in a null check
- **Methods with parameters** - parameters are passed through to each enum value call
- **Enum properties on navigation properties** - works with nested navigation


#### How does this relate to [Expressionify](https://github.com/ClaveConsulting/Expressionify)?
Expressionify is a project that was launched before this project. It has some overlapping features and uses similar approaches. When I first published this project, I was not aware of its existence, so shame on me. Currently, Expressionify targets a more focused scope of what this project is doing, and thereby it seems to be more limiting in its capabilities. Check them out though!

#### How does this relate to LinqKit/LinqExpander/...?
There are a few projects like [LinqKit](https://github.com/scottksmith95/LINQKit) that were created before we had source generators in .NET. These are great options if you're stuck with classical EF or don't want to rely on code generation. Otherwise, I would suggest that EntityFrameworkCore.Projectables and Expressionify are superior approaches as they can rely on SourceGenerators to do most of the hard work.

#### Is the available for EFCore 3.1, 5 and 6?
V1 is targeting EF Core 5 and 3.1. V2 and V3 are targeting EF Core 6 and are compatible with EF Core 7. You can upgrade/downgrade between these versions based on your EF Core version requirements.

#### What is next for this project?
TBD... However, one thing I'd like to improve is our expression generation logic as it's currently making a few assumptions (have yet to experience it breaking). Community contributions are very welcome!

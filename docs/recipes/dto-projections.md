# DTO Projections with Constructors

This recipe shows how to use `[Projectable]` constructors to project database rows directly into DTOs inside your LINQ queries — with no boilerplate `Select` expressions and full SQL translation.

## The Problem

Projecting entities into DTOs usually requires writing a `Select` expression that repeats the mapping logic:

```csharp
// ❌ Repetitive — mapping duplicated in every query
var customers = dbContext.Customers
    .Select(c => new CustomerDto
    {
        Id = c.Id,
        FullName = c.FirstName + " " + c.LastName,
        IsActive = c.IsActive,
        OrderCount = c.Orders.Count()
    })
    .ToList();
```

If the mapping changes you must update every `Select` that uses it.

## The Solution: `[Projectable]` Constructor

Mark a constructor with `[Projectable]` and call `new CustomerDto(c)` directly in your query:

```csharp
public class CustomerDto
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public bool IsActive { get; set; }
    public int OrderCount { get; set; }

    public CustomerDto() { }   // required by the generator

    [Projectable]
    public CustomerDto(Customer c)
    {
        Id = c.Id;
        FullName = c.FirstName + " " + c.LastName;
        IsActive = c.IsActive;
        OrderCount = c.Orders.Count();
    }
}
```

```csharp
// ✅ Clean — mapping defined once, used everywhere
var customers = dbContext.Customers
    .Where(c => c.IsActive)
    .Select(c => new CustomerDto(c))
    .ToList();
```

The constructor body is inlined as SQL — no data is fetched to memory for the projection.

## Using Conditional Logic in the Constructor

Constructor bodies support `if`/`else` chains and local variables:

```csharp
public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string PriceTier { get; set; }

    public ProductDto() { }

    [Projectable]
    public ProductDto(Product p)
    {
        Id = p.Id;
        Name = p.Name;
        Price = p.SalePrice;
        PriceTier = p.SalePrice switch
        {
            > 500 => "Premium",
            > 100 => "Standard",
            _     => "Budget"
        };
    }
}
```

## Inheritance — Reusing Base Mappings

When your DTOs form an inheritance hierarchy, use `: base(…)` to avoid duplicating base-class assignments:

```csharp
public class PersonDto
{
    public string FullName { get; set; }
    public string Email { get; set; }

    public PersonDto() { }

    [Projectable]
    public PersonDto(Person p)
    {
        FullName = p.FirstName + " " + p.LastName;
        Email = p.Email;
    }
}

public class EmployeeDto : PersonDto
{
    public string Department { get; set; }
    public string Grade { get; set; }

    public EmployeeDto() { }

    [Projectable]
    public EmployeeDto(Employee e) : base(e)   // PersonDto assignments are inlined automatically
    {
        Department = e.Department.Name;
        Grade = e.YearsOfService >= 10 ? "Senior" : "Junior";
    }
}
```

```csharp
var employees = dbContext.Employees
    .Select(e => new EmployeeDto(e))
    .ToList();
```

The generated SQL projects all fields in a single query — `FullName`, `Email`, `Department`, and `Grade` are all computed in the database.

## Multiple Overloads

If you need different projections from the same DTO, use constructor overloads — each gets its own generated expression:

```csharp
public class OrderSummaryDto
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public string CustomerName { get; set; }

    public OrderSummaryDto() { }

    // Full projection (with customer name — requires navigation)
    [Projectable]
    public OrderSummaryDto(Order o)
    {
        Id = o.Id;
        Total = o.GrandTotal;
        CustomerName = o.Customer.FirstName + " " + o.Customer.LastName;
    }

    // Lightweight projection (no navigation join needed)
    [Projectable]
    public OrderSummaryDto(Order o, bool lightweight)
    {
        Id = o.Id;
        Total = o.GrandTotal;
        CustomerName = null;
    }
}
```

## Converting a Factory Method

If you already have a `[Projectable]` static factory method, the IDE offers a **one-click refactoring** to convert it to a constructor:

```csharp
// Before — factory method
[Projectable]
public static CustomerDto From(Customer c) => new CustomerDto { … };

// After — projectable constructor (via IDE refactoring or EFP0012 code fix)
[Projectable]
public CustomerDto(Customer c) { … }
```

## Tips

- **Always add a parameterless constructor** — the generator emits `new T() { … }` syntax; if the parameterless constructor is missing you get **EFP0008** (IDE inserts it for you).
- **Keep mappings pure** — no side effects, no calls to non-projectable methods.
- **Prefer constructors over factory methods** — constructors are the idiomatic Projectables pattern; factory methods trigger the **EFP0012** suggestion.

See also: [Constructor Projections guide](/guide/projectable-constructors), [Diagnostics](/reference/diagnostics#efp0008).


# Constructor Projections

> [!NOTE]
> Constructor projections are available starting from version 6.x.

You can mark a constructor with `[Projectable]` to project your DTOs directly inside LINQ queries. The generator emits a member-init expression (`new T() { Prop = value, … }`) that EF Core can translate to SQL.

## Basic Example

```csharp
public class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public bool IsActive { get; set; }
    public ICollection<Order> Orders { get; set; }
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

// The constructor call is translated directly to SQL
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

## Requirements

- The class must expose an accessible **parameterless constructor** (public, internal, or protected-internal), because the generated code relies on `new T() { … }` syntax.
- If a parameterless constructor is missing, the generator reports **EFP0008** — an IDE quick-fix can insert it automatically.

## Supported Constructs in Constructor Bodies

| Construct                    | Notes                                                       |
|------------------------------|-------------------------------------------------------------|
| Simple property assignments  | `FullName = customer.FirstName + " " + customer.LastName;`  |
| Local variable declarations  | Inlined at each usage point                                 |
| If/else chains               | Converted to ternary expressions                            |
| Switch expressions           | Translated to nested ternary / CASE                         |
| Base/this initializer chains | Recursively inlines the delegated constructor's assignments |

## Inheritance — Base/This Initializer Chains

The generator recursively inlines the delegated constructor's assignments, which is particularly useful with DTO inheritance hierarchies:

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
    public EmployeeDto(Employee employee) : base(employee)   // PersonDto assignments inlined automatically
    {
        Department = employee.Department.Name;
        Grade = employee.YearsOfService >= 10 ? "Senior" : "Junior";
    }
}

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

> [!NOTE]
> If the delegated constructor's source is not available in the current compilation, the generator reports **EFP0009** and skips the projection.

## Constructor Overloads

Multiple `[Projectable]` constructors (overloads) per class are fully supported — each overload generates its own expression class distinguished by parameter types.

## Converting a Factory Method to a Constructor

If you have an existing `[Projectable]` factory method that returns `new T { … }`, the generator emits diagnostic **EFP0012** suggesting a conversion. The IDE provides a one-click refactoring that:

1. Converts the factory method to a `[Projectable]` constructor.
2. Optionally updates all call sites throughout the solution to use `new T(…)` instead.

See [Diagnostics](/reference/diagnostics) for details.

## Diagnostics

| Code        | Severity | Cause                                            |
|-------------|----------|--------------------------------------------------|
| **EFP0008** | ❌ Error  | Class is missing a parameterless constructor     |
| **EFP0009** | ❌ Error  | Delegated constructor source not available       |
| **EFP0012** | ℹ️ Info  | Factory method can be converted to a constructor |


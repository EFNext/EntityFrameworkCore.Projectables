using Microsoft.EntityFrameworkCore;
using ReadmeSample;
using ReadmeSample.Dtos;
using ReadmeSample.Entities;
using ReadmeSample.Extensions;

// ─────────────────────────────────────────────────────────────────────────────
// Bootstrap — create (or recreate) the SQLite database automatically
// ─────────────────────────────────────────────────────────────────────────────
await using var dbContext = new ApplicationDbContext();

dbContext.Database.EnsureDeleted();
dbContext.Database.EnsureCreated();

// ─────────────────────────────────────────────────────────────────────────────
// Seed data
// ─────────────────────────────────────────────────────────────────────────────
var user = new User { UserName = "Jon", EmailAddress = "jon@doe.com" };

var supplier = new Supplier { Name = "Acme Stationery" };    // linked to pen, not to book

var pen  = new Product { Name = "Blue Pen",    ListPrice = 1.50m,  Supplier = supplier };
var book = new Product { Name = "C# in Depth", ListPrice = 35.99m };  // no supplier → null-conditional demo

var fulfilledOrder = new Order
{
    User          = user,
    TaxRate       = .19m,
    Status        = OrderStatus.Fulfilled,
    CreatedDate   = DateTime.UtcNow.AddDays(-2),
    FulfilledDate = DateTime.UtcNow.AddDays(-1),
    Items =
    [
        new OrderItem { Product = pen,  Quantity = 5 },
        new OrderItem { Product = book, Quantity = 1 },
    ],
};

var pendingOrder = new Order
{
    User          = user,
    TaxRate       = .19m,
    Status        = OrderStatus.Pending,
    CreatedDate   = DateTime.UtcNow,
    FulfilledDate = null,
    Items =
    [
        new OrderItem { Product = pen, Quantity = 2 },
    ],
};

dbContext.AddRange(user, supplier, pen, book, fulfilledOrder, pendingOrder);
dbContext.SaveChanges();

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('─', 72));
    Console.WriteLine($"  {title}");
    Console.WriteLine(new string('─', 72));
}

// ─────────────────────────────────────────────────────────────────────────────
// Feature 1 — Properties & methods
//   [Projectable] properties compose each other recursively.
//   [Projectable] methods accept parameters.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 1: Properties & methods");

var totalsQuery = dbContext.Orders
    .Select(o => new
    {
        o.Id,
        o.Subtotal,
        o.Tax,
        o.GrandTotal,
        Discounted10Pct = o.GetDiscountedTotal(0.10m),  // [Projectable] method
    });

Console.WriteLine(totalsQuery.ToQueryString());
foreach (var row in totalsQuery)
{
    Console.WriteLine($"  Order #{row.Id}: subtotal={row.Subtotal:C}  tax={row.Tax:C}"
        + $"  grand total={row.GrandTotal:C}  −10%={row.Discounted10Pct:C}");
}

// ─────────────────────────────────────────────────────────────────────────────
// Feature 2 — Extension methods
//   [Projectable] extension methods are inlined as correlated subqueries.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 2: Extension methods");

var recentQuery = dbContext.Users
    .Where(u => u.UserName == "Jon")
    .Select(u => new
    {
        u.UserName,
        LatestOrderGrandTotal = u.GetMostRecentOrder()!.GrandTotal,
    });

Console.WriteLine(recentQuery.ToQueryString());
var recent = recentQuery.First();
Console.WriteLine($"  {recent.UserName}'s most recent order: {recent.LatestOrderGrandTotal:C}");

// ─────────────────────────────────────────────────────────────────────────────
// Feature 3 — Constructor projections
//   [Projectable] constructor maps an entity to a DTO entirely in SQL.
//   No client-side evaluation — the full SELECT is generated from the constructor body.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 3: Constructor projections");

var dtoQuery = dbContext.Orders
    .Select(o => new OrderSummaryDto(o));

Console.WriteLine(dtoQuery.ToQueryString());
foreach (var dto in dtoQuery)
{
    Console.WriteLine($"  [{dto.Id}] {dto.UserName} — {dto.GrandTotal:C} ({dto.StatusName}) priority={dto.PriorityLabel}");
}

// ─────────────────────────────────────────────────────────────────────────────
// Feature 4 — Method overloads
//   Both overloads of GetMostRecentOrderForUser are independently supported.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 4: Method overloads");

var withPendingQuery = dbContext.Users
    .Where(u => u.UserName == "Jon")
    .Select(u => new
    {
        u.UserName,
        LatestAnyOrderTotal = u.GetMostRecentOrderForUser(true)!.GrandTotal,
    });

Console.WriteLine(withPendingQuery.ToQueryString());
var withPending = withPendingQuery.First();
Console.WriteLine($"  {withPending.UserName}'s most recent order (incl. pending): {withPending.LatestAnyOrderTotal:C}");

// ─────────────────────────────────────────────────────────────────────────────
// Feature 5 — Pattern matching (switch expression)
//   The switch expression is rewritten into SQL CASE WHEN expressions.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 5: Pattern matching — switch expression → SQL CASE WHEN");

var priorityQuery = dbContext.Orders
    .Select(o => new { o.Id, o.GrandTotal, o.PriorityLabel });

Console.WriteLine(priorityQuery.ToQueryString());
foreach (var row in priorityQuery)
{
    Console.WriteLine($"  Order #{row.Id}: {row.GrandTotal:C} → priority={row.PriorityLabel}");
}

// ─────────────────────────────────────────────────────────────────────────────
// Feature 6 — Block-bodied members (experimental)
//   if/else block bodies are converted to ternary expressions → SQL CASE WHEN.
//   AllowBlockBody = true on the attribute suppresses warning EFP0001.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 6: Block-bodied members (AllowBlockBody = true)");

var shippingQuery = dbContext.Orders
    .Select(o => new { o.Id, ShippingCategory = o.GetShippingCategory() });

Console.WriteLine(shippingQuery.ToQueryString());
foreach (var row in shippingQuery)
{
    Console.WriteLine($"  Order #{row.Id}: shipping={row.ShippingCategory}");
}

// ─────────────────────────────────────────────────────────────────────────────
// Feature 7 — Null-conditional rewriting
//   Supplier?.Name — the ?. is stripped (Ignore mode) and EF Core uses a LEFT JOIN.
//   Result is NULL when the product has no linked supplier.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 7: Null-conditional rewriting (NullConditionalRewriteSupport.Ignore)");

var supplierQuery = dbContext.Products
    .Select(p => new { p.Name, p.SupplierName });

Console.WriteLine(supplierQuery.ToQueryString());
foreach (var row in supplierQuery)
{
    Console.WriteLine($"  {row.Name}: supplier={row.SupplierName ?? "(none)"}");
}

// ─────────────────────────────────────────────────────────────────────────────
// Feature 8 — Enum method expansion
//   GetDisplayName() is evaluated at compile time for every enum value.
//   The results are baked into a SQL CASE expression — no C# runs at query time.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 8: Enum method expansion (ExpandEnumMethods = true)");

var statusQuery = dbContext.Orders
    .Select(o => new { o.Id, o.Status, o.StatusDisplayName });

Console.WriteLine(statusQuery.ToQueryString());
foreach (var row in statusQuery)
{
    Console.WriteLine($"  Order #{row.Id}: status={row.Status} → \"{row.StatusDisplayName}\"");
}

// ─────────────────────────────────────────────────────────────────────────────
// Feature 9 — UseMemberBody
//   IsHighValueOrder's body is replaced at compile time by IsHighValueOrderImpl's body.
//   Useful when the public member has a different in-memory implementation.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 9: UseMemberBody — expression sourced from a private member");

var highValueQuery = dbContext.Orders
    .Select(o => new { o.Id, o.GrandTotal, o.IsHighValueOrder });

Console.WriteLine(highValueQuery.ToQueryString());
foreach (var row in highValueQuery)
{
    Console.WriteLine($"  Order #{row.Id}: {row.GrandTotal:C} → high-value={row.IsHighValueOrder}");
}

// ─────────────────────────────────────────────────────────────────────────────
// Feature 10 — Compatibility mode (configured in ApplicationDbContext.cs)
//   Full (default) : expands on every query invocation — maximum compatibility.
//   Limited        : expands once, caches — better performance for repeated queries.
//   See ApplicationDbContext.OnConfiguring for how to switch.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 10: Compatibility mode");
Console.WriteLine("  See ApplicationDbContext.OnConfiguring for configuration.");
Console.WriteLine("  Full (default): UseProjectables()");
Console.WriteLine("  Limited:        UseProjectables(p => p.CompatibilityMode(CompatibilityMode.Limited))");

// ─────────────────────────────────────────────────────────────────────────────
// Feature 11 — Roslyn analyzers & code fixes (EFP0001–EFP0012)
//   Compile-time only — not demonstrated at runtime.
//   Examples: EFP0001 warns on block bodies without AllowBlockBody = true,
//             EFP0008 reports missing parameterless constructor on DTO classes.
// ─────────────────────────────────────────────────────────────────────────────
Section("Feature 11: Roslyn analyzers & code fixes");
Console.WriteLine("  Compile-time feature — see source comments on [Projectable] members.");
Console.WriteLine("  Diagnostics EFP0001–EFP0012 are reported in the IDE and provide quick-fix actions.");

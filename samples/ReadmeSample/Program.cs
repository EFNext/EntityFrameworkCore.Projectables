using Microsoft.EntityFrameworkCore;
using ReadmeSample;
using ReadmeSample.Dtos;
using ReadmeSample.Extensions;
using Spectre.Console;
using static ReadmeSample.ConsoleHelper;

// ─────────────────────────────────────────────────────────────────────────────
// Banner
// ─────────────────────────────────────────────────────────────────────────────
AnsiConsole.Write(new Panel(
    "[bold yellow]EntityFrameworkCore.Projectables[/] — Feature Tour\n"
    + "[dim]SQ Server · .NET 10 · All 11 features from the README demonstrated[/]")
{
    Border = BoxBorder.Double,
    BorderStyle = Style.Parse("yellow dim"),
    Padding = new Padding(2, 0, 2, 0),
});

// ─────────────────────────────────────────────────────────────────────────────
// Bootstrap
// ─────────────────────────────────────────────────────────────────────────────
await using var dbContext = new ApplicationDbContext();

// ─────────────────────────────────────────────────────────────────────────────
// Feature 1 — Properties & methods
// ─────────────────────────────────────────────────────────────────────────────
Section(1, "Properties & methods");

var totalsQuery = dbContext.Orders.Select(o => new
{
    o.Id, o.Subtotal, o.Tax, o.GrandTotal,
    Discounted10Pct = o.GetDiscountedTotal(0.10m),
});

ShowSql(totalsQuery.ToQueryString());

// ─────────────────────────────────────────────────────────────────────────────
// Feature 2 — Extension methods
// ─────────────────────────────────────────────────────────────────────────────
Section(2, "Extension methods");

var recentQuery = dbContext.Users
    .Where(u => u.UserName == "Jon")
    .Select(u => new { u.UserName, LatestOrderGrandTotal = u.GetMostRecentOrder()!.GrandTotal });

ShowSql(recentQuery.ToQueryString());

// ─────────────────────────────────────────────────────────────────────────────
// Feature 3 — Constructor projections
// ─────────────────────────────────────────────────────────────────────────────
Section(3, "Constructor projections  →  new OrderSummaryDto(o)");

var dtoQuery = dbContext.Orders.Select(o => new OrderSummaryDto(o));

ShowSql(dtoQuery.ToQueryString());

// ─────────────────────────────────────────────────────────────────────────────
// Feature 4 — Method overloads
// ─────────────────────────────────────────────────────────────────────────────
Section(4, "Method overloads");

var withPendingQuery = dbContext.Users
    .Where(u => u.UserName == "Jon")
    .Select(u => new { u.UserName, LatestAnyOrderTotal = u.GetMostRecentOrderForUser(true)!.GrandTotal });

ShowSql(withPendingQuery.ToQueryString());

// ─────────────────────────────────────────────────────────────────────────────
// Feature 5 — Pattern matching
// ─────────────────────────────────────────────────────────────────────────────
Section(5, "Pattern matching  →  switch expression becomes SQL CASE WHEN");

var priorityQuery = dbContext.Orders.Select(o => new { o.Id, o.GrandTotal, o.PriorityLabel });

ShowSql(priorityQuery.ToQueryString());

// ─────────────────────────────────────────────────────────────────────────────
// Feature 6 — Block-bodied members
// ─────────────────────────────────────────────────────────────────────────────
Section(6, "Block-bodied members  →  AllowBlockBody = true");

var shippingQuery = dbContext.Orders.Select(o => new { o.Id, ShippingCategory = o.GetShippingCategory() });

ShowSql(shippingQuery.ToQueryString());

// ─────────────────────────────────────────────────────────────────────────────
// Feature 7 — Null-conditional rewriting
// ─────────────────────────────────────────────────────────────────────────────
Section(7, "Null-conditional rewriting  →  NullConditionalRewriteSupport.Ignore");

var supplierQuery = dbContext.Products.Select(p => new { p.Name, p.SupplierName });

ShowSql(supplierQuery.ToQueryString());

// ─────────────────────────────────────────────────────────────────────────────
// Feature 8 — Enum method expansion
// ─────────────────────────────────────────────────────────────────────────────
Section(8, "Enum method expansion  →  ExpandEnumMethods = true");

var statusQuery = dbContext.Orders.Select(o => new { o.Id, o.Status, o.StatusDisplayName });

ShowSql(statusQuery.ToQueryString());

// ─────────────────────────────────────────────────────────────────────────────
// Feature 9 — UseMemberBody
// ─────────────────────────────────────────────────────────────────────────────
Section(9, "UseMemberBody  →  expression sourced from a private member");

var highValueQuery = dbContext.Orders.Select(o => new { o.Id, o.GrandTotal, o.IsHighValueOrder });

ShowSql(highValueQuery.ToQueryString());

// ─────────────────────────────────────────────────────────────────────────────
// Feature 10 — Compatibility mode
// ─────────────────────────────────────────────────────────────────────────────
Section(10, "Compatibility mode  →  UseProjectables(p => p.CompatibilityMode(...))");

var modeTable = new Table { Border = TableBorder.Rounded, BorderStyle = Style.Parse("grey") };
modeTable.AddColumn(new TableColumn("[bold]Mode[/]"));
modeTable.AddColumn(new TableColumn("[bold]When expansion runs[/]"));
modeTable.AddColumn(new TableColumn("[bold]Query cache[/]"));
modeTable.AddColumn(new TableColumn("[bold]Performance[/]"));
modeTable.AddRow(
    "[bold]Full[/] [dim](default)[/]",
    "Every query invocation",
    "Per query shape",
    "Baseline");
modeTable.AddRow(
    "[bold]Limited[/]",
    "First invocation, then cached",
    "[bold green]Reused[/]",
    "[bold chartreuse1]Often faster than vanilla EF[/]");
AnsiConsole.Write(modeTable);
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine("  Configure in [cyan]ApplicationDbContext.OnConfiguring[/]:");
AnsiConsole.MarkupLine("  [dim]// Full (default)[/]");
AnsiConsole.MarkupLine("  [cyan]optionsBuilder.UseProjectables()[/]");
AnsiConsole.MarkupLine("  [dim]// Limited[/]");
AnsiConsole.MarkupLine("  [cyan]optionsBuilder.UseProjectables(p => p.CompatibilityMode(CompatibilityMode.Limited))[/]");

// ─────────────────────────────────────────────────────────────────────────────
// Feature 11 — Roslyn analyzers & code fixes
// ─────────────────────────────────────────────────────────────────────────────
Section(11, "Roslyn analyzers & code fixes  →  EFP0001–EFP0012  (compile-time)");

var diagTable = new Table { Border = TableBorder.Rounded, BorderStyle = Style.Parse("grey") };
diagTable.AddColumn(new TableColumn("[bold]Code[/]").Centered());
diagTable.AddColumn(new TableColumn("[bold]Triggered when…[/]"));
diagTable.AddColumn(new TableColumn("[bold]IDE quick-fix[/]"));
diagTable.AddRow(
    "[bold yellow]EFP0001[/]",
    "Block body without [cyan]AllowBlockBody = true[/]",
    "Add [cyan]AllowBlockBody = true[/]");
diagTable.AddRow(
    "[bold red1]EFP0002[/]",
    "[cyan]?.[/] used without [cyan]NullConditionalRewriteSupport[/]",
    "Choose [cyan]Ignore[/] or [cyan]Rewrite[/]");
diagTable.AddRow(
    "[bold red1]EFP0008[/]",
    "DTO class missing parameterless constructor",
    "Insert parameterless constructor");
diagTable.AddRow(
    "[bold blue]EFP0012[/]",
    "Factory method can be converted to a [cyan][[Projectable]][/] constructor",
    "Convert & update call sites");
AnsiConsole.Write(diagTable);
AnsiConsole.WriteLine();
AnsiConsole.MarkupLine(
    "  [dim]Diagnostics are reported at compile time in the IDE — see source comments for examples.[/]");
AnsiConsole.WriteLine();

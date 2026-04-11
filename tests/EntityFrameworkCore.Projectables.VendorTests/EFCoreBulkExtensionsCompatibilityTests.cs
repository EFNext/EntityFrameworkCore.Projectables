using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityFrameworkCore.Projectables.VendorTests;

/// <summary>
/// Tests that verify Projectables is compatible with EFCore.BulkExtensions batch
/// delete/update operations.
///
/// Background: EFCore.BulkExtensions' <c>BatchUtil.GetDbContext</c> discovers the
/// DbContext via reflection by accessing the IQueryCompiler instance stored inside
/// EntityQueryProvider and then reading its private <c>_queryContextFactory</c> field.
/// Because C# reflection does not surface private fields from base classes when
/// GetField is called on a derived type, without an explicit shadow field in
/// <c>CustomQueryCompiler</c> the lookup returns null and the next GetValue(null)
/// call throws a <c>TargetException</c> ("Non-static method requires a target").
/// The shadow field added to <c>CustomQueryCompiler</c> fixes this.
/// </summary>
public class EFCoreBulkExtensionsCompatibilityTests : IDisposable
{
    readonly TestDbContext _context;

    public EFCoreBulkExtensionsCompatibilityTests()
    {
        _context = new TestDbContext();
        _context.Database.EnsureCreated();
        _context.SeedData();
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public void GetDbContext_WithProjectablesEnabled_DoesNotThrow()
    {
        // Arrange
        var query = _context.Set<Order>().Where(o => o.IsCompleted);

        // Act – BatchUtil.GetDbContext is the method that was previously throwing
        // "Non-static method requires a target" because _queryContextFactory was not
        // discoverable via reflection on CustomQueryCompiler.
        var exception = Record.Exception(() => BatchUtil.GetDbContext(query));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void GetDbContext_WithProjectablesEnabled_ReturnsCorrectContext()
    {
        // Arrange
        var query = _context.Set<Order>().Where(o => o.IsCompleted);

        // Act
        var dbContext = BatchUtil.GetDbContext(query);

        // Assert – must return the same DbContext, not null
        Assert.NotNull(dbContext);
        Assert.Same(_context, dbContext);
    }

    [Fact]
    public void GetDbContext_WithProjectableProperty_DoesNotThrow()
    {
        // Arrange – entity with a [Projectable] property so that CustomQueryCompiler is
        // exercised with actual projectable expression expansion.
        var query = _context.Set<Order>().Where(o => o.IsCompleted);

        // Act
        var exception = Record.Exception(() => BatchUtil.GetDbContext(query));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task BatchDeleteAsync_WithProjectablesEnabled_DoesNotThrowTargetException()
    {
        // Arrange
        var query = _context.Set<Order>().Where(o => o.IsCompleted);

        // Act – previously this would throw TargetException with message
        // "Non-static method requires a target" when Projectables 3.x was used.
#pragma warning disable CS0618 // BatchDeleteAsync is marked obsolete in favour of EF 7 ExecuteDeleteAsync, but we
                               // specifically need to test EFCore.BulkExtensions' own batch path.
        var exception = await Record.ExceptionAsync(
            () => query.BatchDeleteAsync(TestContext.Current.CancellationToken));
#pragma warning restore CS0618

        // Assert – a TargetException means the reflection-based DbContext discovery
        // inside EFCore.BulkExtensions failed.  All other exceptions (e.g. SQL syntax
        // differences on SQLite) are acceptable because they come from actual SQL
        // execution, not from the broken reflection chain.
        Assert.False(
            exception is System.Reflection.TargetException,
            $"BatchDeleteAsync threw TargetException: {exception?.Message}");
        Assert.False(
            exception?.Message?.Contains("Non-static method requires a target") == true,
            $"BatchDeleteAsync threw 'Non-static method requires a target': {exception?.Message}");
    }

    [Fact]
    public async Task BatchUpdateAsync_WithProjectablesEnabled_DoesNotThrowTargetException()
    {
        // Arrange
        var query = _context.Set<Order>().Where(o => o.IsCompleted);

        // Act
#pragma warning disable CS0618 // BatchUpdateAsync is marked obsolete in favour of EF 7 ExecuteUpdateAsync
        var exception = await Record.ExceptionAsync(
            () => query.BatchUpdateAsync(
                o => new Order { Total = o.Total * 2 },
                cancellationToken: TestContext.Current.CancellationToken));
#pragma warning restore CS0618

        // Assert – same as above: only TargetException is a regression.
        Assert.False(
            exception is System.Reflection.TargetException,
            $"BatchUpdateAsync threw TargetException: {exception?.Message}");
        Assert.False(
            exception?.Message?.Contains("Non-static method requires a target") == true,
            $"BatchUpdateAsync threw 'Non-static method requires a target': {exception?.Message}");
    }
}

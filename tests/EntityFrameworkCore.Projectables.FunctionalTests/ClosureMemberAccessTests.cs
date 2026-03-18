using EntityFrameworkCore.Projectables.FunctionalTests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.Projectables.FunctionalTests;

/// <summary>
/// Validates closure-variable handling in <c>ProjectableExpressionReplacer.VisitMember</c>.
///
/// <b>How the current implementation works:</b>
/// When a <see cref="System.Linq.Expressions.MemberExpression"/> accesses a member of a
/// compiler-generated closure object the replacer first checks the member's <em>declared type</em>
/// (<c>FieldInfo.FieldType</c> / <c>PropertyInfo.PropertyType</c>).  Only when that declared type
/// is assignable to <see cref="IEnumerable"/> does it call <c>GetValue()</c> to read the runtime
/// value.  If the value is an <see cref="IQueryable"/> whose provider matches the current query
/// provider, the captured query's expression tree is inlined into the outer query.
///
/// <see cref="IEnumerable"/> (not <see cref="IQueryable"/>) is used as the gate because a variable
/// declared as <c>IEnumerable&lt;T&gt;</c> may legally hold an EF Core <c>IQueryable&lt;T&gt;</c>
/// at runtime; using <c>IQueryable</c> alone would miss that case.
///
/// For scalar captures (e.g., <c>int</c>, <c>bool</c>) the declared type is <em>not</em>
/// assignable to <see cref="IEnumerable"/>, so <c>GetValue()</c> is never called; the closure
/// member expression is passed through unchanged and EF Core resolves it as a normal query
/// parameter via its own parameter-extraction pipeline.
///
/// Note on the <c>PropertyInfo</c> branch: standard C# compiler closures always use fields, not
/// properties, so the <c>PropertyInfo prop =&gt; prop.GetValue(...)</c> arm is a defensive path
/// that cannot be reached through ordinary C# lambdas.  It is covered by a direct unit test in
/// <c>ProjectableExpressionReplacerTests</c> that constructs the expression tree manually.
///
/// <b>Scenarios covered:</b>
///   1. Closure capturing an <c>int</c> field – scalar, NOT via reflection; EF Core handles it as a parameter
///   2. Closure capturing a <c>string</c> field – GetValue() is called (string implements IEnumerable&lt;char&gt;),
///      but the runtime value is not IQueryable so it falls through; EF Core handles it as a parameter
///   3. Closure capturing two <c>int</c> fields – multiple scalars, NOT via reflection
///   4. Closure capturing an <c>IQueryable&lt;Entity&gt;</c> field – FieldInfo.GetValue() path, inlined via .Any()
///   5. Closure capturing an <c>IQueryable&lt;Entity&gt;</c> field – FieldInfo.GetValue() path, inlined in .Count() projection
///   6. Closure capturing both an <c>int</c> field and an <c>IQueryable&lt;Entity&gt;</c> field – combined paths
///   7. Closure capturing an <c>IEnumerable&lt;Entity&gt;</c> field holding an EF query – GetValue() is called
///      because IEnumerable&lt;T&gt; satisfies the type gate; runtime value is IQueryable, so the subquery is inlined
/// </summary>
[UsesVerify]
public class ClosureMemberAccessTests
{
    public record Entity
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        [Projectable]
        public bool IsWithinRange(int min, int max) => Id >= min && Id <= max;

        [Projectable]
        public bool HasName(string name) => Name == name;

        [Projectable]
        public int Doubled => Id * 2;
    }

    // -----------------------------------------------------------------------
    // 1. Closure captures a single int field – scalar, EF Core parameter path.
    //    Because int is not assignable to IQueryable, the declared-type check
    //    in VisitMember does NOT invoke GetValue(); the compiler-generated
    //    closure member expression falls through unchanged and EF Core's own
    //    ParameterExtractingExpressionVisitor turns it into a SQL parameter.
    // -----------------------------------------------------------------------
    [Fact]
    public Task CapturedIntField_UsedInProjectableMethod()
    {
        using var dbContext = new SampleDbContext<Entity>();

        var lowerBound = 3;
        var query = dbContext.Set<Entity>()
            .Where(x => x.IsWithinRange(lowerBound, 10));

        return Verifier.Verify(query.ToQueryString());
    }

    // -----------------------------------------------------------------------
    // 2. Closure captures a string field – EF Core parameter path.
    //    string implements IEnumerable<char>, so the IEnumerable gate in
    //    VisitMember is satisfied and GetValue() IS called.  However, the
    //    runtime value is a string (not IQueryable), so the provider check
    //    fails and the code falls through; EF Core handles it as a parameter.
    // -----------------------------------------------------------------------
    [Fact]
    public Task CapturedStringField_UsedInProjectableMethod()
    {
        using var dbContext = new SampleDbContext<Entity>();

        var targetName = "Alice";
        var query = dbContext.Set<Entity>()
            .Where(x => x.HasName(targetName));

        return Verifier.Verify(query.ToQueryString());
    }

    // -----------------------------------------------------------------------
    // 3. Closure captures two int fields – multiple scalars, EF Core parameter path.
    //    Neither `lower` nor `upper` triggers GetValue() (int is not assignable to
    //    IQueryable); EF Core emits a separate SQL parameter for each captured int.
    // -----------------------------------------------------------------------
    [Fact]
    public Task CapturedMultipleIntFields_UsedInProjectableMethod()
    {
        using var dbContext = new SampleDbContext<Entity>();

        var lower = 2;
        var upper = 8;
        var query = dbContext.Set<Entity>()
            .Where(x => x.IsWithinRange(lower, upper));

        return Verifier.Verify(query.ToQueryString());
    }

    // -----------------------------------------------------------------------
    // 4. Closure captures an IQueryable subquery (FieldInfo branch, IQueryable
    //    result → sub-expression inlining).
    //    When FieldInfo.GetValue() returns an IQueryable that shares the same
    //    provider, ProjectableExpressionReplacer inlines the subquery's
    //    expression tree rather than treating the variable as a parameter.
    //    The projectable [Projectable] method inside the subquery must also be
    //    expanded in the final SQL.
    // -----------------------------------------------------------------------
    [Fact]
    public Task CapturedIQueryableField_SubqueryInlinedViaAny()
    {
        using var dbContext = new SampleDbContext<Entity>();

        var validEntities = dbContext.Set<Entity>()
            .Where(x => x.IsWithinRange(1, 5));

        var query = dbContext.Set<Entity>()
            .Where(x => validEntities.Any(s => s.Id == x.Id));

        return Verifier.Verify(query.ToQueryString());
    }

    // -----------------------------------------------------------------------
    // 5. Closure captures an IQueryable subquery used in a Count() projection.
    //    The captured IQueryable (filtered via a [Projectable] property) is
    //    inlined as a correlated sub-select inside a SELECT projection.
    // -----------------------------------------------------------------------
    [Fact]
    public Task CapturedIQueryableField_SubqueryInlinedViaCount()
    {
        using var dbContext = new SampleDbContext<Entity>();

        var subset = dbContext.Set<Entity>()
            .Where(x => x.Doubled > 4);

        var query = dbContext.Set<Entity>()
            .Select(x => new { x.Id, SubsetCount = subset.Count() });

        return Verifier.Verify(query.ToQueryString());
    }

    // -----------------------------------------------------------------------
    // 6. Closure captures both a value-type field AND an IQueryable.
    //    Exercises both branches in the same expression: the int is read as a
    //    FieldInfo value and turned into a SQL parameter; the IQueryable is
    //    also read as a FieldInfo value and inlined as a sub-expression.
    // -----------------------------------------------------------------------
    [Fact]
    public Task CapturedMixedFields_IntAndIQueryable_BothResolvedCorrectly()
    {
        using var dbContext = new SampleDbContext<Entity>();

        var minCount = 1;
        var highIds = dbContext.Set<Entity>()
            .Where(x => x.IsWithinRange(10, 100));

        var query = dbContext.Set<Entity>()
            .Where(x => x.IsWithinRange(minCount, 50) || highIds.Any(h => h.Id == x.Id));

        return Verifier.Verify(query.ToQueryString());
    }

    // -----------------------------------------------------------------------
    // 7. Closure captures an IQueryable declared as IEnumerable<Entity>.
    //    The IEnumerable gate in VisitMember is satisfied (IEnumerable<T> is
    //    assignable to IEnumerable), so GetValue() IS called.  The runtime
    //    value is the EF Core IQueryable<Entity>, whose provider matches, so
    //    the subquery is inlined — identical result to scenario 4.
    // -----------------------------------------------------------------------
    [Fact]
    public Task CapturedIQueryable_DeclaredAsIEnumerable_IsInlined()
    {
        using var dbContext = new SampleDbContext<Entity>();

        // Declared as IEnumerable<Entity> but assigned an EF Core query.
        // With the IEnumerable gate the replacer calls GetValue(), recognises
        // the runtime value as an IQueryable with a matching provider and
        // inlines the subquery expression — no translation error.
        IEnumerable<Entity> subsetAsEnumerable = dbContext.Set<Entity>()
            .Where(x => x.IsWithinRange(1, 5));

        var query = dbContext.Set<Entity>()
            .Where(x => subsetAsEnumerable.Any(s => s.Id == x.Id));

        return Verifier.Verify(query.ToQueryString());
    }
}



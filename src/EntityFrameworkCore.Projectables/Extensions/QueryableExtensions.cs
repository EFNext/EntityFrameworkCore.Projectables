namespace EntityFrameworkCore.Projectables.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Replaces all calls to properties and methods that are marked with the <C>Projectable</C> attribute with their respective expression tree
    /// </summary>
    public static IQueryable<TModel> ExpandProjectables<TModel>(this IQueryable<TModel> query)
        => query.Provider.CreateQuery<TModel>(query.Expression.ExpandProjectables());

    /// <summary>
    /// Ensures that all writable <c>[Projectable]</c> properties on <typeparamref name="TModel"/> are populated
    /// in query results by automatically generating a SELECT projection that merges all EF-mapped columns with
    /// the expression-expanded value of every writable projectable property.
    /// <para>
    /// This method is particularly useful when fetching full entities (e.g. <c>FirstAsync()</c>,
    /// <c>ToListAsync()</c>) on a tracking context where the automatic select injection is otherwise
    /// suppressed to preserve change-tracking semantics. Call this method after any <c>Where</c>
    /// or <c>OrderBy</c> clauses and before terminal operators.
    /// </para>
    /// <para>
    /// Read-only projectable properties (those without a setter) are not included in the generated projection
    /// because EF Core's materializer cannot set them on the resulting entity.
    /// </para>
    /// </summary>
    public static IQueryable<TModel> AsExpandedProperties<TModel>(this IQueryable<TModel> query)
        where TModel : class
        => query.ExpandProjectables();
}
namespace EntityFrameworkCore.Projectables.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Replaces all calls to properties and methods that are marked with the <C>Projectable</C> attribute with their respective expression tree
    /// </summary>
    public static IQueryable<TModel> ExpandProjectables<TModel>(this IQueryable<TModel> query)
        => query.Provider.CreateQuery<TModel>(query.Expression.ExpandProjectables());
}
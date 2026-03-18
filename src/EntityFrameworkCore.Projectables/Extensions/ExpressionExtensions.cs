using System.Linq.Expressions;
using EntityFrameworkCore.Projectables.Services;

namespace EntityFrameworkCore.Projectables.Extensions;

public static class ExpressionExtensions
{
    /// <summary>
    /// Replaces all calls to properties and methods that are marked with the <C>Projectable</C> attribute with their respective expression tree
    /// </summary>
    public static Expression ExpandProjectables(this Expression expression)
        => new ProjectableExpressionReplacer(new ProjectionExpressionResolver(), false).Replace(expression);
}
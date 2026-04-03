using EntityFrameworkCore.Projectables;
using ReadmeSample.Entities;

namespace ReadmeSample.Extensions;

public static class UserExtensions
{
    /// <summary>
    /// Returns the most recent fulfilled order for the user.
    /// Matches the README example — inlined into SQL via [Projectable].
    /// </summary>
    [Projectable]
    public static Order? GetMostRecentOrder(this User user) =>
        user.Orders
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefault();

    /// <summary>
    /// Returns the most recent order with an optional filter on fulfillment status.
    /// Demonstrates method overloads: both variants are supported.
    /// </summary>
    [Projectable]
    public static Order? GetMostRecentOrderForUser(this User user, bool includeUnfulfilled) =>
        user.Orders
            .Where(x => includeUnfulfilled || x.FulfilledDate != null)
            .OrderByDescending(x => x.CreatedDate)
            .FirstOrDefault();
}

namespace ReadmeSample.Entities;

public enum OrderStatus
{
    Pending,
    Fulfilled,
    Cancelled,
}

public static class OrderStatusExtensions
{
    /// <summary>
    /// Plain C# method — not [Projectable]. Used with ExpandEnumMethods = true.
    /// The generator evaluates this at compile time for every enum value and bakes
    /// the results into a CASE expression EF Core can translate to SQL.
    /// </summary>
    public static string GetDisplayName(this OrderStatus status) =>
        status switch
        {
            OrderStatus.Pending   => "Pending Review",
            OrderStatus.Fulfilled => "Fulfilled",
            OrderStatus.Cancelled => "Cancelled",
            _                     => status.ToString(),
        };
}


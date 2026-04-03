using EntityFrameworkCore.Projectables;
using ReadmeSample.Entities;

namespace ReadmeSample.Dtos;

/// <summary>
/// DTO with a [Projectable] constructor — the entire mapping is inlined into SQL by the source generator.
/// </summary>
public class OrderSummaryDto
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public decimal GrandTotal { get; set; }
    public string? StatusName { get; set; }
    public string? PriorityLabel { get; set; }

    /// <summary>Required parameterless constructor (EFP0008 ensures its presence).</summary>
    public OrderSummaryDto() { }

    [Projectable]
    public OrderSummaryDto(Order order)
    {
        Id           = order.Id;
        UserName     = order.User.UserName;
        GrandTotal   = order.GrandTotal;
        StatusName   = order.StatusDisplayName;
        PriorityLabel = order.PriorityLabel;
    }
}


using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class RestaurantOrder : BaseEntity
{
    public Guid TableId { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? TransactionId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = RestaurantConstants.OrderOpen;
    public string? CancellationReason { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public Tenant? Tenant { get; set; }
    public RestaurantTable? Table { get; set; }
    public User? OpenedByUser { get; set; }
    public Transaction? Transaction { get; set; }

    public ICollection<RestaurantOrderItem> Items { get; set; } =
        new List<RestaurantOrderItem>();
}

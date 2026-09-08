using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class RestaurantOrderItem : BaseEntity
{
    public Guid RestaurantOrderId { get; set; }
    public Guid ProductId { get; set; }

    public string Nama { get; set; } = string.Empty;
    public decimal HargaJual { get; set; }
    public int Qty { get; set; }
    public string? Note { get; set; }
    public string KitchenStatus { get; set; } =
        RestaurantConstants.KitchenDraft;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? QueuedAt { get; set; }
    public DateTime? PreparingAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? ServedAt { get; set; }

    public Tenant? Tenant { get; set; }
    public RestaurantOrder? RestaurantOrder { get; set; }
    public Product? Product { get; set; }
}

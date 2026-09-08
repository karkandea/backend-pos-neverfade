using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Restaurant;

public sealed class RestaurantTableDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public bool Active { get; set; }
    public int SortOrder { get; set; }
    public string Status { get; set; } = "available";
    public Guid? OpenOrderId { get; set; }
    public string? OpenOrderNumber { get; set; }
    public decimal OpenOrderSubtotal { get; set; }
    public int KitchenPendingItems { get; set; }
}

public sealed class UpsertRestaurantTableRequestDto
{
    [Required, StringLength(30, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(1, 100)]
    public int Capacity { get; set; } = 4;

    public bool Active { get; set; } = true;

    [Range(0, 10000)]
    public int SortOrder { get; set; }
}

public sealed class RestaurantOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Nama { get; set; } = string.Empty;
    public decimal HargaJual { get; set; }
    public int Qty { get; set; }
    public decimal Subtotal { get; set; }
    public string Note { get; set; } = string.Empty;
    public string KitchenStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? QueuedAt { get; set; }
    public DateTime? PreparingAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? ServedAt { get; set; }
}

public sealed class RestaurantOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid TableId { get; set; }
    public string TableCode { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? TransactionId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public decimal Subtotal { get; set; }
    public List<RestaurantOrderItemDto> Items { get; set; } = new();
}

public sealed class OpenRestaurantOrderRequestDto
{
    public Guid TableId { get; set; }
}

public sealed class AddRestaurantOrderItemRequestDto
{
    public Guid ProductId { get; set; }

    [Range(1, 999)]
    public int Qty { get; set; } = 1;

    [StringLength(500)]
    public string? Note { get; set; }
}

public sealed class UpdateRestaurantOrderItemRequestDto
{
    [Range(1, 999)]
    public int Qty { get; set; } = 1;

    [StringLength(500)]
    public string? Note { get; set; }
}

public sealed class UpdateKitchenStatusRequestDto
{
    [Required]
    public string Status { get; set; } = string.Empty;
}

public sealed class CancelRestaurantOrderRequestDto
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CloseRestaurantOrderRequestDto
{
    public Guid TransactionId { get; set; }
}

public sealed class KitchenQueueOrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid TableId { get; set; }
    public string TableCode { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public List<RestaurantOrderItemDto> Items { get; set; } = new();
}

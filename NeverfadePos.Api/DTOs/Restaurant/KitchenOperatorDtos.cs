namespace NeverfadePos.Api.DTOs.Restaurant;

/// <summary>Price-free kitchen ticket. No transaction, payment or customer fields.</summary>
public sealed class KitchenOperatorOrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid TableId { get; set; }
    public string TableCode { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public List<KitchenOperatorItemDto> Items { get; set; } = [];

    public static KitchenOperatorOrderDto From(KitchenQueueOrderDto order) => new()
    {
        OrderId = order.OrderId, OrderNumber = order.OrderNumber,
        TableId = order.TableId, TableCode = order.TableCode,
        TableName = order.TableName, OpenedAt = order.OpenedAt,
        Items = order.Items.Select(KitchenOperatorItemDto.From).ToList()
    };
}

public sealed class KitchenOperatorItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Nama { get; set; } = string.Empty;
    public int Qty { get; set; }
    public string Note { get; set; } = string.Empty;
    public string KitchenStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? QueuedAt { get; set; }
    public DateTime? PreparingAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? ServedAt { get; set; }

    public static KitchenOperatorItemDto From(RestaurantOrderItemDto item) => new()
    {
        Id = item.Id, ProductId = item.ProductId, Nama = item.Nama,
        Qty = item.Qty, Note = item.Note, KitchenStatus = item.KitchenStatus,
        CreatedAt = item.CreatedAt, QueuedAt = item.QueuedAt,
        PreparingAt = item.PreparingAt, ReadyAt = item.ReadyAt, ServedAt = item.ServedAt
    };
}

namespace NeverfadePos.Api.DTOs.Laundry;

/// <summary>Work queue without payment, price or unnecessary contact details.</summary>
public sealed class LaundryOperatorOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public DateTime PromisedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<LaundryOperatorItemDto> Items { get; set; } = [];

    public static LaundryOperatorOrderDto From(LaundryWorkOrderDto source) => new()
    {
        Id = source.Id, OrderNumber = source.OrderNumber,
        CustomerName = source.CustomerName, Status = source.Status,
        Notes = source.Notes, ReceivedAt = source.ReceivedAt,
        PromisedAt = source.PromisedAt, UpdatedAt = source.UpdatedAt,
        Items = source.Items.Select(LaundryOperatorItemDto.From).ToList()
    };
}

public sealed class LaundryOperatorItemDto
{
    public string Nama { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }

    public static LaundryOperatorItemDto From(LaundryWorkOrderItemDto source) => new()
    {
        Nama = source.Nama, Unit = source.Unit, Quantity = source.Quantity
    };
}

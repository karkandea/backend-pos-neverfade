using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Laundry;

public sealed class LaundryWorkOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public Guid? TransactionId { get; set; }
    public decimal Total { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }
    public DateTime ReceivedAt { get; set; }
    public DateTime PromisedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<LaundryWorkOrderItemDto> Items { get; set; } = new();
    public List<LaundryWorkOrderStatusHistoryDto> StatusHistory { get; set; } = new();
}

public sealed class LaundryWorkOrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Nama { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int QuantityPrecision { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public sealed class LaundryWorkOrderStatusHistoryDto
{
    public Guid Id { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime At { get; set; }
}

public sealed class CreateLaundryWorkOrderRequestDto
{
    public Guid CustomerId { get; set; }

    [MinLength(1)]
    public List<CreateLaundryWorkOrderItemRequestDto> Items { get; set; } = new();

    public DateTime PromisedAt { get; set; }

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;
}

public sealed class CreateLaundryWorkOrderItemRequestDto
{
    public Guid ProductId { get; set; }

    [Range(0.001, double.MaxValue)]
    public decimal Quantity { get; set; }
}

public sealed class UpdateLaundryWorkOrderStatusRequestDto
{
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CompleteLaundryPaymentRequestDto
{
    public Guid TransactionId { get; set; }
}

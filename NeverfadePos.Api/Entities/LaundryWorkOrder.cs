using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class LaundryWorkOrder : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? TransactionId { get; set; }

    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = LaundryConstants.StatusReceived;
    public string PaymentStatus { get; set; } = LaundryConstants.PaymentUnpaid;
    public string Notes { get; set; } = string.Empty;
    public string? CancellationReason { get; set; }

    public DateTime PromisedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public Tenant? Tenant { get; set; }
    public Customer? Customer { get; set; }
    public User? CreatedByUser { get; set; }
    public Transaction? Transaction { get; set; }

    public ICollection<LaundryWorkOrderItem> Items { get; set; } =
        new List<LaundryWorkOrderItem>();

    public ICollection<LaundryWorkOrderStatusHistory> StatusHistory { get; set; } =
        new List<LaundryWorkOrderStatusHistory>();
}

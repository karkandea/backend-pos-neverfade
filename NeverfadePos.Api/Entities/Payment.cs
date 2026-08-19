using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class Payment : BaseEntity
{
    public Guid TransactionId { get; set; }

    public string Provider { get; set; } = "xendit";

    public string ProviderReferenceId { get; set; } = string.Empty;

    public string? ProviderPaymentRequestId { get; set; }

    public string? ProviderPaymentId { get; set; }

    public string Method { get; set; } = "qris";

    public string Currency { get; set; } = "IDR";

    public decimal Amount { get; set; }

    public string Status { get; set; } = "pending";

    public string? FailureCode { get; set; }

    public string? QrString { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public Tenant? Tenant { get; set; }

    public Transaction? Transaction { get; set; }

    public ICollection<PaymentLedgerEntry> LedgerEntries { get; set; } =
        new List<PaymentLedgerEntry>();

    public ICollection<PaymentWebhookEvent> WebhookEvents { get; set; } =
        new List<PaymentWebhookEvent>();
}

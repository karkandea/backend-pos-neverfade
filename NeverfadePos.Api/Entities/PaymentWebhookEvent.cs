using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class PaymentWebhookEvent : BaseEntity
{
    public Guid PaymentId { get; set; }

    public string ProviderEventKey { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string ProviderPaymentId { get; set; } = string.Empty;

    public string ProcessingStatus { get; set; } = "processed";

    public Tenant? Tenant { get; set; }

    public Payment? Payment { get; set; }
}

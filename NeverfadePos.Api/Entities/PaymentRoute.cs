namespace NeverfadePos.Api.Entities;

public sealed class PaymentRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid PaymentId { get; set; }

    public string Provider { get; set; } = "xendit";

    public string? ProviderPaymentRequestId { get; set; }

    public string? ProviderSessionId { get; set; }

    public string? ProviderReferenceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }

    public Payment? Payment { get; set; }
}

namespace NeverfadePos.Api.DTOs.Payment;

public sealed class HostedPaymentDto
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public string ProviderSessionId { get; set; } = string.Empty;

    public string ProviderReferenceId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string CheckoutUrl { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }
}

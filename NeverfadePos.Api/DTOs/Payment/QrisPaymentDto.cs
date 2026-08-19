namespace NeverfadePos.Api.DTOs.Payment;

public sealed class QrisPaymentDto
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public string ProviderPaymentRequestId { get; set; } = string.Empty;

    public string ProviderReferenceId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? QrString { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

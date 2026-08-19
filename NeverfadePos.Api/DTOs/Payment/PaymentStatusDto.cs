namespace NeverfadePos.Api.DTOs.Payment;

public sealed class PaymentStatusDto
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string ProviderPaymentRequestId { get; set; } = string.Empty;

    public string ProviderReferenceId { get; set; } = string.Empty;

    public string? QrString { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? FailureCode { get; set; }

    public DateTime UpdatedAt { get; set; }
}

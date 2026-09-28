namespace NeverfadePos.Api.DTOs.Payment;

/// <summary>Read-only operator triage. This response NEVER means it is safe to retry a charge.</summary>
public sealed class PaymentAttentionResponseDto
{
    public Guid OutletId { get; set; }
    public int Total { get; set; }
    public bool HasMore { get; set; }
    public List<PaymentAttentionItemDto> Items { get; set; } = [];
}

public sealed class PaymentAttentionItemDto
{
    public Guid PaymentId { get; set; }
    public Guid TransactionId { get; set; }
    public string ProviderReferenceId { get; set; } = string.Empty;
    public string? ProviderPaymentRequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

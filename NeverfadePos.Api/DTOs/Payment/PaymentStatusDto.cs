namespace NeverfadePos.Api.DTOs.Payment;

public sealed class PaymentStatusDto
{
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public string Status { get; set; } = string.Empty;
}

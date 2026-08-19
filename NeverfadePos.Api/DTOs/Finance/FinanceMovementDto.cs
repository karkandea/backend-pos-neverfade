namespace NeverfadePos.Api.DTOs.Finance;

public sealed class FinanceMovementDto
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateTime Timestamp { get; set; }

    public string Reference { get; set; } = string.Empty;

    public Guid? PaymentId { get; set; }

    public Guid? TransactionId { get; set; }

    public Guid? WithdrawalId { get; set; }
}

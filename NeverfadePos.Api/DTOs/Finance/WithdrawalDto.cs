namespace NeverfadePos.Api.DTOs.Finance;

public class WithdrawalDto
{
    public Guid Id { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}

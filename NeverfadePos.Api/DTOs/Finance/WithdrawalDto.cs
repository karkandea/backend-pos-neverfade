namespace NeverfadePos.Api.DTOs.Finance;

public class WithdrawalDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string DestinationBankName { get; set; } = string.Empty;
    public string DestinationAccountMask { get; set; } = string.Empty;
    public string DestinationAccountHolderName { get; set; } = string.Empty;
    public string? TransferReference { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
}

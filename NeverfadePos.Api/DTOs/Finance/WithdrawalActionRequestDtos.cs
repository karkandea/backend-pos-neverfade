namespace NeverfadePos.Api.DTOs.Finance;

public sealed class MarkWithdrawalPaidRequestDto
{
    public string TransferReference { get; set; } = string.Empty;
    public string? EvidenceMetadata { get; set; }
    public bool ConfirmedTransferred { get; set; }
}

public sealed class RejectWithdrawalRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class VerifyWithdrawalBankAccountRequestDto
{
    public bool Verified { get; set; }
    public string? Reason { get; set; }
}

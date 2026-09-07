using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Finance;

public sealed class MarkWithdrawalPaidRequestDto
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string TransferReference { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? EvidenceMetadata { get; set; }

    public bool ConfirmedTransferred { get; set; }
}

public sealed class RejectWithdrawalRequestDto
{
    [Required, StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class VerifyWithdrawalBankAccountRequestDto
{
    public bool Verified { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }
}

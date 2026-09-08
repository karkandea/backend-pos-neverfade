using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Finance;

public class WithdrawalBankAccountDto
{
    public string BankName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerificationNote { get; set; }
}

public sealed class PlatformWithdrawalBankAccountDto
    : WithdrawalBankAccountDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
}

public sealed class UpdateWithdrawalBankAccountRequestDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string BankName { get; set; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 6)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string AccountHolderName { get; set; } = string.Empty;
}

public sealed class WithdrawalSettingsDto
{
    public decimal MinimumAmount { get; set; }
    public string ProcessingEstimate { get; set; } = string.Empty;
}

namespace NeverfadePos.Api.DTOs.Finance;

public sealed class WithdrawalBankAccountDto
{
    public string BankName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

public sealed class UpdateWithdrawalBankAccountRequestDto
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
}

public sealed class WithdrawalSettingsDto
{
    public decimal MinimumAmount { get; set; }
    public string ProcessingEstimate { get; set; } = string.Empty;
}

using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class WithdrawalBankAccount : BaseEntity
{
    public string BankName { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    public string AccountHolderName { get; set; } = string.Empty;

    public string VerificationStatus { get; set; } = "pending";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? VerifiedAt { get; set; }

    public Guid? VerifiedByPlatformUserId { get; set; }

    public Tenant? Tenant { get; set; }

    public PlatformUser? VerifiedByPlatformUser { get; set; }
}

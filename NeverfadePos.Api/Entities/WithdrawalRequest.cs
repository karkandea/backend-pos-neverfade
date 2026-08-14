using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class WithdrawalRequest : BaseEntity
{
    public decimal Amount { get; set; }

    public string Status { get; set; } =
        WithdrawalConstants.StatusRequested;

    public Guid RequestedByUserId { get; set; }

    public Guid? ProcessedByPlatformUserId { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }

    public Tenant? Tenant { get; set; }

    public User? RequestedByUser { get; set; }

    public PlatformUser? ProcessedByPlatformUser { get; set; }

    public PaymentLedgerEntry? LedgerEntry { get; set; }
}

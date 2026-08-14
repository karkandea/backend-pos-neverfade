using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class PaymentLedgerEntry : BaseEntity
{
    public Guid? PaymentId { get; set; }

    public Guid? TransactionId { get; set; }

    public Guid? WithdrawalRequestId { get; set; }

    public string EntryType { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "IDR";

    public string? ProviderReference { get; set; }

    public Tenant? Tenant { get; set; }

    public Payment? Payment { get; set; }

    public Transaction? Transaction { get; set; }

    public WithdrawalRequest? WithdrawalRequest { get; set; }
}

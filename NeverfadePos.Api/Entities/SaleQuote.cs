using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

/// <summary>A server-computed, immutable price snapshot. Not a sale, tender, or stock reservation.</summary>
public sealed class SaleQuote : BaseEntity
{
    public Guid OutletId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid QuoteVersion { get; set; } = Guid.NewGuid();
    public DateTime ExpiresAt { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = "quoted";
    public string? IdempotencyKey { get; set; }
    public string? IdempotencyRequestHash { get; set; }
    // Persist the original cash attempt BEFORE dispatch so a second device
    // can recover the exact quote/version/key/amount even if HTTP fails.
    public DateTime? PreparedAt { get; set; }
    public decimal? PreparedAmountReceived { get; set; }
    public Guid? ConsumedTransactionId { get; set; }
}

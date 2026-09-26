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
}

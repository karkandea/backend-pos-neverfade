using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

/// <summary>
/// Authoritative per-outlet stock state. Legacy Product.Stok / ProductVariant.Stok
/// remain aggregate compatibility projections while v1/v2 converge on this ledger.
/// </summary>
public sealed class StockBalance : BaseEntity
{
    public Guid OutletId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public Guid? LotId { get; set; }

    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal Quarantine { get; set; }
    public decimal Available { get; set; }
    public decimal AverageCost { get; set; }

    public DateTime AsOf { get; set; } = DateTime.UtcNow;
    public long Version { get; set; } = 1;

    public Outlet? Outlet { get; set; }
    public Product? Product { get; set; }
    public ProductVariant? ProductVariant { get; set; }
}

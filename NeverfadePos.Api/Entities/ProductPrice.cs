using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class ProductPrice : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public Guid PriceLevelId { get; set; }
    public decimal MinQuantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }

    public Tenant? Tenant { get; set; }
    public Product? Product { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public PriceLevel? PriceLevel { get; set; }
}

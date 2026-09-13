using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public class TransactionItem : BaseEntity
{
    public Guid TransactionId { get; set; }

    public Guid ProductId { get; set; }

    public string Nama { get; set; } = string.Empty;

    public decimal HargaJual { get; set; }

    public Guid? ProductVariantId { get; set; }

    public string VariantSku { get; set; } = string.Empty;

    public string VariantLabel { get; set; } = string.Empty;

    public decimal BasePrice { get; set; }

    public Guid? PriceLevelId { get; set; }

    public string PriceLevelName { get; set; } = string.Empty;

    public int Qty { get; set; }

    public decimal Quantity { get; set; }

    public string ProductType { get; set; } = ProductTypes.Goods;

    public bool TracksStock { get; set; } = true;

    public int QuantityPrecision { get; set; }

    public string Unit { get; set; } = string.Empty;

    public decimal Subtotal { get; set; }

    public Tenant? Tenant { get; set; }

    public Transaction? Transaction { get; set; }

    public Product? Product { get; set; }
}

using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.DTOs.Transaction;

public sealed class TransactionItemDto
{
    public Guid Id { get; set; }

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
}

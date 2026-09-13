using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class ProductVariant : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Option1Name { get; set; } = string.Empty;
    public string Option1Value { get; set; } = string.Empty;
    public string Option2Name { get; set; } = string.Empty;
    public string Option2Value { get; set; } = string.Empty;
    public string Option3Name { get; set; } = string.Empty;
    public string Option3Value { get; set; } = string.Empty;
    public decimal? HargaModal { get; set; }
    public decimal? HargaJual { get; set; }
    public int Stok { get; set; }
    public bool Active { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public Product? Product { get; set; }
    public ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();
}

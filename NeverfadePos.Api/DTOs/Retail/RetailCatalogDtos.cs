namespace NeverfadePos.Api.DTOs.Retail;

public sealed class RetailCatalogProductDto
{
    public Guid Id { get; set; }
    public string Kode { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Nama { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public decimal HargaModal { get; set; }
    public decimal HargaJual { get; set; }
    public int Stok { get; set; }
    public string Supplier { get; set; } = string.Empty;
    public string Satuan { get; set; } = string.Empty;
    public string Deskripsi { get; set; } = string.Empty;
    public string Type { get; set; } = "goods";
    public bool TracksStock { get; set; }
    public int QuantityPrecision { get; set; }
    public List<ProductVariantDto> Variants { get; set; } = new();
    public List<ProductPriceDto> Prices { get; set; } = new();
}

public sealed class RetailCatalogDto
{
    public List<PriceLevelDto> PriceLevels { get; set; } = new();
    public List<RetailCatalogProductDto> Products { get; set; } = new();
}

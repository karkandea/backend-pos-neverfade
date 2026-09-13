using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Retail;

public sealed class ProductVariantDto
{
    public Guid Id { get; set; }
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
    public bool Active { get; set; }
}

public class CreateProductVariantDto
{
    public Guid ProductId { get; set; }
    [Required, MaxLength(100)] public string Sku { get; set; } = string.Empty;
    [MaxLength(100)] public string Barcode { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Label { get; set; } = string.Empty;
    [MaxLength(50)] public string Option1Name { get; set; } = string.Empty;
    [MaxLength(100)] public string Option1Value { get; set; } = string.Empty;
    [MaxLength(50)] public string Option2Name { get; set; } = string.Empty;
    [MaxLength(100)] public string Option2Value { get; set; } = string.Empty;
    [MaxLength(50)] public string Option3Name { get; set; } = string.Empty;
    [MaxLength(100)] public string Option3Value { get; set; } = string.Empty;
    [Range(0, double.MaxValue)] public decimal? HargaModal { get; set; }
    [Range(0, double.MaxValue)] public decimal? HargaJual { get; set; }
    [Range(0, int.MaxValue)] public int Stok { get; set; }
}

public sealed class UpdateProductVariantDto : CreateProductVariantDto
{
    public bool Active { get; set; } = true;
}

public sealed class AdjustVariantStockDto
{
    [Required, MaxLength(50)]
    public string Tipe { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Jumlah { get; set; }

    [Range(0, int.MaxValue)]
    public int? StokFinal { get; set; }

    [MaxLength(500)]
    public string Keterangan { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Product;

public sealed class CreateProductDto
{
    [Required]
    [MaxLength(100)]
    public string Kode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Barcode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Nama { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Kategori { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal HargaModal { get; set; }

    [Range(0, double.MaxValue)]
    public decimal HargaJual { get; set; }

    [Range(0, int.MaxValue)]
    public int Stok { get; set; }

    [MaxLength(200)]
    public string Supplier { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Satuan { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Deskripsi { get; set; } = string.Empty;
}

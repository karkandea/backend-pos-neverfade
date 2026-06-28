namespace NeverfadePos.Api.DTOs.Product;

public sealed class UpdateProductDto
{
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
}

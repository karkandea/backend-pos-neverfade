using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.StockHistory;

public sealed class CreateStockHistoryDto
{
    [Required]
    public Guid ProdukId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Tipe { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Jumlah { get; set; }

    [Range(0, int.MaxValue)]
    public int? StokFinal { get; set; }

    [MaxLength(500)]
    public string Keterangan { get; set; } = string.Empty;
}

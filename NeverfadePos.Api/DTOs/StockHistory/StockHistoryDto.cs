namespace NeverfadePos.Api.DTOs.StockHistory;

public sealed class StockHistoryDto
{
    public Guid Id { get; set; }

    public Guid ProdukId { get; set; }

    public string ProdukNama { get; set; } = string.Empty;

    public string Tipe { get; set; } = string.Empty;

    public int Jumlah { get; set; }

    public int StokAkhir { get; set; }

    public string Keterangan { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;

    // API field "tanggal" berasal dari CreatedAt entity.
    public DateTime Tanggal { get; set; }
}

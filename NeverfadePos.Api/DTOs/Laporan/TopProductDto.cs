namespace NeverfadePos.Api.DTOs.Laporan;

public sealed class TopProductDto
{
    public string Nama { get; set; } = string.Empty;

    public int Qty { get; set; }

    public decimal Revenue { get; set; }
}

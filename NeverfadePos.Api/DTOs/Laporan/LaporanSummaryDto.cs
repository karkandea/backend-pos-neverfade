namespace NeverfadePos.Api.DTOs.Laporan;

public sealed class LaporanSummaryDto
{
    public decimal Omzet { get; set; }

    public int Transaksi { get; set; }

    public decimal Avg { get; set; }

    public int Pelanggan { get; set; }
}

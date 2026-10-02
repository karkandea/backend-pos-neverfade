namespace NeverfadePos.Api.DTOs.Laporan;

public sealed class LaporanSummaryDto
{
    public decimal Omzet { get; set; }

    public int Transaksi { get; set; }

    public decimal Avg { get; set; }

    public int Pelanggan { get; set; }

    // Never display an invented profit for historical/missing cost snapshots.
    public bool CostIncomplete { get; set; }
    public decimal? EstimatedGrossProfit { get; set; }
}

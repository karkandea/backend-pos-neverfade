namespace NeverfadePos.Api.DTOs.Laporan;

public sealed class LaporanChartDto
{
    // Format: YYYY-MM-DD
    public string Date { get; set; } = string.Empty;

    // Sen, Sel, Rab, Kam, Jum, Sab, Min
    public string Label { get; set; } = string.Empty;

    public decimal Total { get; set; }
}

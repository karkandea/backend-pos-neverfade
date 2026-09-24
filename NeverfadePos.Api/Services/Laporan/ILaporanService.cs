using NeverfadePos.Api.DTOs.Laporan;

namespace NeverfadePos.Api.Services.Laporan;

public interface ILaporanService
{
    Task<LaporanSummaryDto> GetSummaryAsync(
        string period,
        CancellationToken cancellationToken = default,
        DateOnly? startDate = null,
        DateOnly? endDate = null);

    Task<List<LaporanChartDto>> GetChartAsync(
        string period = "mingguan",
        CancellationToken cancellationToken = default,
        DateOnly? startDate = null,
        DateOnly? endDate = null);

    Task<List<TopProductDto>> GetTopProductsAsync(
        string period,
        CancellationToken cancellationToken = default,
        DateOnly? startDate = null,
        DateOnly? endDate = null);
}

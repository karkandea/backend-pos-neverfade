using NeverfadePos.Api.DTOs.Laporan;

namespace NeverfadePos.Api.Services.Laporan;

public interface ILaporanService
{
    Task<LaporanSummaryDto> GetSummaryAsync(
        string period,
        CancellationToken cancellationToken = default);

    Task<List<LaporanChartDto>> GetChartAsync(
        CancellationToken cancellationToken = default);

    Task<List<TopProductDto>> GetTopProductsAsync(
        string period,
        CancellationToken cancellationToken = default);
}

using NeverfadePos.Api.DTOs.StockHistory;

namespace NeverfadePos.Api.Services.StockHistory;

public interface IStockHistoryService
{
    Task<List<StockHistoryDto>> GetAllAsync(
        Guid? produkId,
        CancellationToken cancellationToken = default);

    Task<StockHistoryDto> CreateAsync(
        CreateStockHistoryDto request,
        CancellationToken cancellationToken = default);
}

using NeverfadePos.Api.DTOs.Retail;

namespace NeverfadePos.Api.Services.Retail;

public interface IRetailReturnService
{
    Task<List<RetailReturnDto>> GetByTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task<RetailReturnDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<RetailReturnDto> CreateAsync(
        CreateRetailReturnDto request,
        CancellationToken cancellationToken = default);
}

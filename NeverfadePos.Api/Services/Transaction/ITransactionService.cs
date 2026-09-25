using NeverfadePos.Api.DTOs.Transaction;

namespace NeverfadePos.Api.Services.Transaction;

public interface ITransactionService
{
    Task<List<TransactionDto>> GetAllAsync(
        string? search,
        DateTime? startDate,
        DateTime? endDate,
        Guid outletId,
        CancellationToken cancellationToken = default);

    Task<TransactionDto> GetByIdAsync(
        Guid id,
        Guid outletId,
        CancellationToken cancellationToken = default);

    Task<TransactionDto> CreateAsync(
        CreateTransactionDto request,
        CancellationToken cancellationToken = default);
}

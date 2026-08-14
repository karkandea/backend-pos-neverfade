using NeverfadePos.Api.DTOs.Finance;

namespace NeverfadePos.Api.Services.Finance;

public interface ITenantFinanceService
{
    Task<FinanceSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WithdrawalDto>> GetWithdrawalsAsync(
        CancellationToken cancellationToken = default);

    Task<WithdrawalDto> CreateWithdrawalAsync(
        CreateWithdrawalRequestDto request,
        CancellationToken cancellationToken = default);
}

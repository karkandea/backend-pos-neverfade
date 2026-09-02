using NeverfadePos.Api.DTOs.Finance;

namespace NeverfadePos.Api.Services.Finance;

public interface ITenantFinanceService
{
    Task<FinanceSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WithdrawalDto>> GetWithdrawalsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FinanceMovementDto>> GetMovementsAsync(CancellationToken cancellationToken = default);
    Task<WithdrawalSettingsDto> GetWithdrawalSettingsAsync(CancellationToken cancellationToken = default);
    Task<WithdrawalBankAccountDto?> GetBankAccountAsync(CancellationToken cancellationToken = default);
    Task<WithdrawalBankAccountDto> PutBankAccountAsync(UpdateWithdrawalBankAccountRequestDto request, CancellationToken cancellationToken = default);
    Task<WithdrawalDto> CreateWithdrawalAsync(CreateWithdrawalRequestDto request, CancellationToken cancellationToken = default);
    Task<WithdrawalDto> CancelWithdrawalAsync(Guid withdrawalId, CancellationToken cancellationToken = default);
}

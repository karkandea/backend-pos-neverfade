using NeverfadePos.Api.DTOs.Finance;

namespace NeverfadePos.Api.Services.Finance;

public interface IPlatformWithdrawalService
{
    Task<IReadOnlyList<PlatformWithdrawalDto>> GetAllAsync(string? status, CancellationToken cancellationToken = default);
    Task<PlatformWithdrawalDto> StartProcessingAsync(Guid withdrawalId, CancellationToken cancellationToken = default);
    Task<PlatformWithdrawalDto> MarkPaidAsync(Guid withdrawalId, MarkWithdrawalPaidRequestDto request, CancellationToken cancellationToken = default);
    Task<PlatformWithdrawalDto> RejectAsync(Guid withdrawalId, RejectWithdrawalRequestDto request, CancellationToken cancellationToken = default);
    Task<WithdrawalBankAccountDto> ReviewBankAccountAsync(Guid tenantId, VerifyWithdrawalBankAccountRequestDto request, CancellationToken cancellationToken = default);
}

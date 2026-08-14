using NeverfadePos.Api.DTOs.Finance;

namespace NeverfadePos.Api.Services.Finance;

public interface IPlatformWithdrawalService
{
    Task<IReadOnlyList<PlatformWithdrawalDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<PlatformWithdrawalDto> MarkPaidAsync(
        Guid withdrawalId,
        CancellationToken cancellationToken = default);

    Task<PlatformWithdrawalDto> RejectAsync(
        Guid withdrawalId,
        CancellationToken cancellationToken = default);
}

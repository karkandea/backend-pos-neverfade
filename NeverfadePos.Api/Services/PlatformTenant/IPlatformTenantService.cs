using NeverfadePos.Api.DTOs.PlatformTenant;

namespace NeverfadePos.Api.Services.PlatformTenant;

public interface IPlatformTenantService
{
    Task<IReadOnlyList<PlatformTenantDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<PlatformTenantDto> GetByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<PlatformTenantDto> CreateAsync(
        CreatePlatformTenantRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PlatformTenantDto> ActivateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<PlatformTenantDto> SuspendAsync(
        Guid tenantId,
        SuspendPlatformTenantRequestDto? request,
        CancellationToken cancellationToken = default);
}

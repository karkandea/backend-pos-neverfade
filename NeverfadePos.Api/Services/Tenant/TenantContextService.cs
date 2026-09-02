using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Tenant;

namespace NeverfadePos.Api.Services.Tenant;

public interface ITenantContextService
{
    Task<TenantContextDto> GetAsync(
        CancellationToken cancellationToken = default);
}

public interface ITenantCapabilityService
{
    Task RequireAsync(
        string capability,
        CancellationToken cancellationToken = default);
}

internal sealed class TenantContextService(
    AppDbContext db,
    CurrentUser currentUser)
    : ITenantContextService, ITenantCapabilityService
{
    public async Task<TenantContextDto> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var (tenantId, role) = RequireIdentity();
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new TenantApiException(
                StatusCodes.Status404NotFound,
                "TENANT_NOT_FOUND",
                "Tenant tidak ditemukan.");

        return new TenantContextDto
        {
            TenantId = tenant.Id,
            NamaToko = tenant.NamaToko,
            BusinessType = tenant.BusinessType,
            Capabilities = BusinessCapabilityPresets.Resolve(tenant.BusinessType),
            Role = role
        };
    }

    public async Task RequireAsync(
        string capability,
        CancellationToken cancellationToken = default)
    {
        var context = await GetAsync(cancellationToken);
        if (!context.Capabilities.Contains(
            capability,
            StringComparer.Ordinal))
        {
            throw new TenantApiException(
                StatusCodes.Status403Forbidden,
                "CAPABILITY_NOT_ENABLED",
                "Fitur ini tidak aktif untuk tipe bisnis tenant.");
        }
    }

    private (Guid TenantId, string Role) RequireIdentity()
    {
        if (!currentUser.TenantId.HasValue ||
            currentUser.TenantId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(currentUser.Role))
        {
            throw new TenantApiException(
                StatusCodes.Status401Unauthorized,
                "TENANT_AUTHENTICATION_REQUIRED",
                "Autentikasi tenant diperlukan.");
        }

        return (currentUser.TenantId.Value, currentUser.Role);
    }
}

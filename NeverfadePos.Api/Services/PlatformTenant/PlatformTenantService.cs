using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.PlatformTenant;
using NeverfadePos.Api.Entities;
using TenantEntity = NeverfadePos.Api.Entities.Tenant;

namespace NeverfadePos.Api.Services.PlatformTenant;

internal sealed class PlatformTenantService(
    AppDbContext db,
    ITrustedTenantExecutionScope trustedTenantScope,
    PlatformCurrentUser currentUser,
    TenantProvisioningService provisioningService)
    : IPlatformTenantService
{
    public async Task<IReadOnlyList<PlatformTenantDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await RequireActiveActorAsync(cancellationToken);

        var tenants = await db.Tenants
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var result = new List<PlatformTenantDto>(tenants.Count);
        foreach (var tenant in tenants)
        {
            result.Add(TenantProvisioningService.Map(
                tenant,
                await GetOwnerAsync(tenant.Id, cancellationToken)));
        }

        return result;
    }

    public async Task<PlatformTenantDto> GetByIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await RequireActiveActorAsync(cancellationToken);
        return await GetTenantDtoAsync(tenantId, cancellationToken);
    }

    public Task<PlatformTenantDto> CreateAsync(
        CreatePlatformTenantRequestDto request,
        CancellationToken cancellationToken = default) =>
        provisioningService.CreateAsync(request, cancellationToken);

    public async Task<PlatformTenantDto> UpdateBusinessProfileAsync(
        Guid tenantId,
        UpdateTenantBusinessProfileRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var actorId = await RequireActiveActorAsync(cancellationToken);
        var requestedBusinessType = request.BusinessType?.Trim();
        if (request.AdditionalProperties?.Count > 0 ||
            !BusinessTypes.IsValid(requestedBusinessType))
        {
            throw ValidationError();
        }

        var tenant = await RequireTenantAsync(tenantId, cancellationToken);
        var nextBusinessType = requestedBusinessType!;

        if (tenant.BusinessType == nextBusinessType)
        {
            return TenantProvisioningService.Map(
                tenant,
                await GetOwnerAsync(tenant.Id, cancellationToken));
        }

        var previousBusinessType = tenant.BusinessType;
        var now = DateTime.UtcNow;

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        tenant.BusinessType = nextBusinessType;
        tenant.UpdatedAt = now;
        db.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorPlatformUserId = actorId,
            TenantId = tenant.Id,
            EventType = "TENANT_BUSINESS_PROFILE_CHANGED",
            CreatedAt = now,
            Metadata = JsonSerializer.Serialize(new
            {
                previousBusinessType,
                businessType = nextBusinessType,
                capabilities = BusinessCapabilityPresets.Resolve(nextBusinessType)
            })
        });

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return TenantProvisioningService.Map(
            tenant,
            await GetOwnerAsync(tenant.Id, cancellationToken));
    }

    public async Task<PlatformTenantDto> ActivateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var actorId = await RequireActiveActorAsync(cancellationToken);
        var tenant = await RequireTenantAsync(tenantId, cancellationToken);

        if (tenant.Status == "active")
        {
            throw new PlatformApiException(
                StatusCodes.Status409Conflict,
                "TENANT_ALREADY_ACTIVE",
                "Tenant sudah aktif.");
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        tenant.Status = "active";
        tenant.UpdatedAt = DateTime.UtcNow;
        db.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorPlatformUserId = actorId,
            TenantId = tenant.Id,
            EventType = "TENANT_ACTIVATED",
            CreatedAt = tenant.UpdatedAt
        });

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return TenantProvisioningService.Map(
            tenant,
            await GetOwnerAsync(tenant.Id, cancellationToken));
    }

    public async Task<PlatformTenantDto> SuspendAsync(
        Guid tenantId,
        SuspendPlatformTenantRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        var actorId = await RequireActiveActorAsync(cancellationToken);
        var tenant = await RequireTenantAsync(tenantId, cancellationToken);

        if (tenant.Status == "suspended")
        {
            throw new PlatformApiException(
                StatusCodes.Status409Conflict,
                "TENANT_ALREADY_SUSPENDED",
                "Tenant sudah ditangguhkan.");
        }

        var reason = NormalizeReason(request?.Reason);
        var metadata = reason is null
            ? null
            : JsonSerializer.Serialize(new { reason });

        if (metadata is not null &&
            System.Text.Encoding.UTF8.GetByteCount(metadata) > 2048)
        {
            throw ValidationError();
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        tenant.Status = "suspended";
        tenant.UpdatedAt = DateTime.UtcNow;
        db.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorPlatformUserId = actorId,
            TenantId = tenant.Id,
            EventType = "TENANT_SUSPENDED",
            CreatedAt = tenant.UpdatedAt,
            Metadata = metadata
        });

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return TenantProvisioningService.Map(
            tenant,
            await GetOwnerAsync(tenant.Id, cancellationToken));
    }

    private async Task<Guid> RequireActiveActorAsync(
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            throw new PlatformApiException(
                StatusCodes.Status401Unauthorized,
                "PLATFORM_AUTHENTICATION_REQUIRED",
                "Autentikasi platform diperlukan.");
        }

        var active = await db.PlatformUsers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == currentUser.UserId.Value && x.Active,
                cancellationToken);

        if (!active)
        {
            throw new PlatformApiException(
                StatusCodes.Status403Forbidden,
                "PLATFORM_USER_INACTIVE",
                "Platform user tidak aktif.");
        }

        return currentUser.UserId.Value;
    }

    private async Task<TenantEntity> RequireTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(
            x => x.Id == tenantId,
            cancellationToken);

        return tenant ?? throw TenantNotFound();
    }

    private async Task<PlatformTenantDto> GetTenantDtoAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == tenantId,
                cancellationToken) ?? throw TenantNotFound();

        return TenantProvisioningService.Map(
            tenant,
            await GetOwnerAsync(tenant.Id, cancellationToken));
    }

    private async Task<User?> GetOwnerAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        using var scope = trustedTenantScope.Begin(
            tenantId,
            "PLATFORM_TENANT_OWNER_SUMMARY");

        return await db.Users
            .AsNoTracking()
            .Where(x => x.Role == "owner")
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string? NormalizeReason(string? reason)
    {
        if (reason is null)
        {
            return null;
        }

        var normalized = reason.Trim();
        if (normalized.Length is < 1 or > 500 ||
            normalized.Any(char.IsControl) ||
            ContainsSensitiveMaterial(normalized))
        {
            throw ValidationError();
        }

        return normalized;
    }

    private static bool ContainsSensitiveMaterial(string value) =>
        Regex.IsMatch(
            value,
            @"(?i)\b(password|passwd|token|secret|authorization|api[_ -]?key|connectionstring|jwt)\b\s*[:=]|\bbearer\s+\S+|\b\d{10,20}\b",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private static PlatformApiException TenantNotFound() =>
        new(
            StatusCodes.Status404NotFound,
            "TENANT_NOT_FOUND",
            "Tenant tidak ditemukan.");

    private static PlatformApiException ValidationError() =>
        new(
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            "Data tenant tidak valid.");
}

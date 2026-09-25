using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.PlatformTenant;
using NeverfadePos.Api.Entities;
using TenantEntity = NeverfadePos.Api.Entities.Tenant;

namespace NeverfadePos.Api.Services.PlatformTenant;

internal sealed class TenantProvisioningService(
    AppDbContext db,
    ITrustedTenantExecutionScope trustedTenantScope,
    PlatformCurrentUser currentUser)
{
    public async Task<PlatformTenantDto> CreateAsync(
        CreatePlatformTenantRequestDto request,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null)
    {
        var actorId = await RequireActiveActorAsync(cancellationToken);
        ValidateRequest(request);
        var key = ValidateIdempotencyKey(idempotencyKey);
        var requestHash = key is null ? null : HashProvisioningRequest(request);
        if (key is not null)
        {
            var replay = await FindExistingAsync(actorId, key, requestHash!, cancellationToken);
            if (replay is not null) return replay;
        }

        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var namaToko = request.NamaToko.Trim();
        var businessType = request.BusinessType.Trim();
        var ownerRequest = request.Owner!;
        var ownerNama = ownerRequest.Nama.Trim();
        var ownerUsername = ownerRequest.Username.Trim();

        if (await OwnerUsernameExistsAsync(
            ownerUsername,
            cancellationToken))
        {
            // Concurrent retry can commit while username lookup is running.
            if (key is not null)
            {
                var replay = await FindExistingAsync(actorId, key, requestHash!, cancellationToken);
                if (replay is not null) return replay;
            }
            throw new PlatformApiException(
                StatusCodes.Status409Conflict,
                "OWNER_USERNAME_CONFLICT",
                "Username owner sudah digunakan.");
        }

        var slug = await GenerateSlugAsync(
            namaToko,
            tenantId,
            cancellationToken);

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var tenant = new TenantEntity
        {
            Id = tenantId,
            NamaToko = namaToko,
            Slug = slug,
            Status = "active",
            BusinessType = businessType,
            CreatedAt = now,
            UpdatedAt = now
        };

        var owner = new User
        {
            TenantId = tenantId,
            Nama = ownerNama,
            Username = ownerUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                ownerRequest.Password),
            Role = "owner",
            Active = true,
            CreatedAt = now
        };

        var settings = new NeverfadePos.Api.Entities.Settings
        {
            TenantId = tenantId,
            NamaToko = namaToko,
            Alamat = string.Empty,
            Telepon = string.Empty,
            Email = string.Empty,
            Website = string.Empty,
            HeaderStruk = string.Empty,
            FooterStruk = string.Empty,
            ShowTax = false,
            ShowPoint = false,
            DefaultTax = 0,
            MinStok = 0,
            PoinRate = 0,
            CreatedAt = now
        };

        var defaultOutlet = new NeverfadePos.Api.Entities.Outlet
        {
            TenantId = tenantId,
            Code = "MAIN",
            Name = namaToko,
            Address = string.Empty,
            Phone = string.Empty,
            IsDefault = true,
            Active = true,
            CreatedAt = now
        };

        db.Tenants.Add(tenant);
        if (key is not null)
            db.PlatformProvisioningRequests.Add(new PlatformProvisioningRequest
            {
                ActorPlatformUserId = actorId,
                Key = key,
                RequestHash = requestHash!,
                TenantId = tenantId,
                CreatedAt = now
            });
        db.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorPlatformUserId = actorId,
            TenantId = tenantId,
            EventType = "TENANT_PROVISIONED",
            CreatedAt = now
        });

        try
        {
            using (trustedTenantScope.Begin(tenantId, "TENANT_PROVISIONING"))
            {
                db.Users.Add(owner);
                db.Settings.Add(settings);
                db.Outlets.Add(defaultOutlet);
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            // A simultaneous same-key POST can only win after the other transaction
            // commits; read its receipt after rollback, never duplicate the tenant.
            if (key is not null)
            {
                // A replay may surface any of the tenant/owner/key unique constraints
                // depending on insert order. Resolve only a committed matching receipt.
                var replay = await FindExistingAsync(actorId, key, requestHash!, cancellationToken);
                if (replay is not null) return replay;
            }
            throw MapConflict(exception);
        }

        return Map(tenant, owner);
    }

    private static string? ValidateIdempotencyKey(string? key)
    {
        if (key is null) return null;
        if (key.Length is < 12 or > 128 || key.Any(c =>
            !(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_' or ':' or '.')))
            throw new PlatformApiException(StatusCodes.Status400BadRequest,
                "INVALID_IDEMPOTENCY_KEY", "Idempotency-Key tidak valid.");
        return key;
    }

    private static string HashProvisioningRequest(CreatePlatformTenantRequestDto request)
    {
        // Only the digest is persisted. Include password so a modified retry conflicts.
        var canonical = JsonSerializer.Serialize(new
        {
            NamaToko = request.NamaToko.Trim(),
            BusinessType = request.BusinessType.Trim(),
            OwnerNama = request.Owner!.Nama.Trim(),
            OwnerUsername = request.Owner.Username.Trim(),
            OwnerPassword = request.Owner.Password
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private async Task<PlatformTenantDto?> FindExistingAsync(Guid actorId, string key,
        string requestHash, CancellationToken cancellationToken)
    {
        var receipt = await db.PlatformProvisioningRequests.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ActorPlatformUserId == actorId && x.Key == key,
                cancellationToken);
        if (receipt is null) return null;
        if (!string.Equals(receipt.RequestHash, requestHash, StringComparison.Ordinal))
            throw new PlatformApiException(StatusCodes.Status409Conflict,
                "IDEMPOTENCY_KEY_REUSED", "Idempotency-Key sudah digunakan untuk request berbeda.");
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(
            x => x.Id == receipt.TenantId, cancellationToken);
        using (trustedTenantScope.Begin(tenant.Id, "TENANT_PROVISIONING_REPLAY"))
        {
            var owner = await db.Users.AsNoTracking().SingleAsync(
                x => x.Role == "owner", cancellationToken);
            return Map(tenant, owner);
        }
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

        var actorExists = await db.PlatformUsers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == currentUser.UserId.Value && x.Active,
                cancellationToken);

        if (!actorExists)
        {
            throw new PlatformApiException(
                StatusCodes.Status403Forbidden,
                "PLATFORM_USER_INACTIVE",
                "Platform user tidak aktif.");
        }

        return currentUser.UserId.Value;
    }

    private static void ValidateRequest(
        CreatePlatformTenantRequestDto request)
    {
        if (request.Owner is null ||
            request.AdditionalProperties?.Count > 0 ||
            request.Owner.AdditionalProperties?.Count > 0 ||
            string.IsNullOrWhiteSpace(request.NamaToko) ||
            request.NamaToko.Trim().Length > 200 ||
            !BusinessTypes.IsValid(request.BusinessType?.Trim()) ||
            string.IsNullOrWhiteSpace(request.Owner.Nama) ||
            request.Owner.Nama.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(request.Owner.Username) ||
            request.Owner.Username.Trim().Length > 100 ||
            string.IsNullOrWhiteSpace(request.Owner.Password) ||
            request.Owner.Password.Length is < 8 or > 100)
        {
            throw ValidationError();
        }
    }

    private async Task<string> GenerateSlugAsync(
        string namaToko,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var normalized = namaToko
            .Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var separatorPending = false;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (separatorPending && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(lower);
                separatorPending = false;
            }
            else
            {
                separatorPending = true;
            }
        }

        var baseSlug = builder.Length == 0
            ? "tenant"
            : builder.ToString();
        baseSlug = baseSlug[..Math.Min(baseSlug.Length, 100)];

        if (!await db.Tenants.AnyAsync(
            x => x.Slug == baseSlug,
            cancellationToken))
        {
            return baseSlug;
        }

        var suffix = $"-{tenantId:N}";
        var maxBaseLength = 100 - suffix.Length;
        return baseSlug[..Math.Min(baseSlug.Length, maxBaseLength)] + suffix;
    }

    private async Task<bool> OwnerUsernameExistsAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var tenantIds = await db.Tenants
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            using var scope = trustedTenantScope.Begin(
                tenantId,
                "TENANT_PROVISIONING_USERNAME_CHECK");

            if (await db.Users.AsNoTracking().AnyAsync(
                x => x.Username == username,
                cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static Exception MapConflict(DbUpdateException exception)
    {
        var constraintName =
            (exception.InnerException as PostgresException)?.ConstraintName ??
            exception.InnerException?.Message ??
            exception.Message;

        if (constraintName.Contains(
            "Username",
            StringComparison.OrdinalIgnoreCase))
        {
            return new PlatformApiException(
                StatusCodes.Status409Conflict,
                "OWNER_USERNAME_CONFLICT",
                "Username owner sudah digunakan.");
        }

        if (constraintName.Contains(
            "Slug",
            StringComparison.OrdinalIgnoreCase))
        {
            return new PlatformApiException(
                StatusCodes.Status409Conflict,
                "TENANT_SLUG_CONFLICT",
                "Slug tenant sudah digunakan.");
        }

        return exception;
    }

    private static PlatformApiException ValidationError() =>
        new(
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            "Data tenant tidak valid.");

    internal static PlatformTenantDto Map(
        TenantEntity tenant,
        User? owner)
    {
        return new PlatformTenantDto
        {
            Id = tenant.Id,
            NamaToko = tenant.NamaToko,
            Slug = tenant.Slug,
            Status = tenant.Status,
            BusinessType = tenant.BusinessType,
            Capabilities = BusinessCapabilityPresets.Resolve(tenant.BusinessType),
            Owner = owner is null
                ? null
                : new TenantOwnerSummaryDto
                {
                    Id = owner.Id,
                    Nama = owner.Nama,
                    Username = owner.Username,
                    Active = owner.Active
                },
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        };
    }
}
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.PlatformTenant;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.PlatformTenant;

internal sealed class TenantProvisioningService(
    AppDbContext db,
    ITrustedTenantExecutionScope trustedTenantScope,
    PlatformCurrentUser currentUser)
{
    public async Task<PlatformTenantDto> CreateAsync(
        CreatePlatformTenantRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var actorId = await RequireActiveActorAsync(cancellationToken);
        ValidateRequest(request);

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

        var tenant = new Tenant
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

        db.Tenants.Add(tenant);
        db.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorPlatformUserId = actorId,
            TenantId = tenantId,
            EventType = "TENANT_PROVISIONED",
            CreatedAt = now
        });

        using (trustedTenantScope.Begin(
            tenantId,
            "TENANT_PROVISIONING"))
        {
            db.Users.Add(owner);
            db.Settings.Add(settings);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (DbUpdateException exception)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                throw MapConflict(exception);
            }
        }

        return Map(tenant, owner);
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
        Tenant tenant,
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

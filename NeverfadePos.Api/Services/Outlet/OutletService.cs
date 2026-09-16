using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Outlet;

namespace NeverfadePos.Api.Services.Outlet;

public interface IOutletService
{
    Task<List<OutletDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<OutletDto> CreateAsync(
        CreateOutletDto request,
        CancellationToken cancellationToken = default);

    Task<OutletDto> UpdateAsync(
        Guid id,
        UpdateOutletDto request,
        CancellationToken cancellationToken = default);

    Task<NeverfadePos.Api.Entities.Outlet> ResolveAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default);
}

public sealed class OutletService(
    AppDbContext db,
    ITenantExecutionContext tenantContext)
    : IOutletService
{
    public async Task<List<OutletDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureDefaultOutletAsync(cancellationToken);

        return await db.Outlets
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<OutletDto> CreateAsync(
        CreateOutletDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var code = NormalizeCode(request.Code);
        var name = RequireName(request.Name);

        if (await db.Outlets.AnyAsync(
            x => x.Code == code,
            cancellationToken))
        {
            throw new InvalidOperationException(
                "Kode outlet sudah digunakan.");
        }

        var firstOutlet = !await db.Outlets.AnyAsync(cancellationToken);
        var shouldBeDefault = request.IsDefault || firstOutlet;

        if (shouldBeDefault)
        {
            await ClearDefaultAsync(null, cancellationToken);
        }

        var entity = new NeverfadePos.Api.Entities.Outlet
        {
            TenantId = tenantId,
            Code = code,
            Name = name,
            Address = request.Address.Trim(),
            Phone = request.Phone.Trim(),
            IsDefault = shouldBeDefault,
            Active = true
        };

        db.Outlets.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<OutletDto> UpdateAsync(
        Guid id,
        UpdateOutletDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Outlets
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Outlet tidak ditemukan.");

        var code = NormalizeCode(request.Code);
        var name = RequireName(request.Name);

        if (await db.Outlets.AnyAsync(
            x => x.Id != id && x.Code == code,
            cancellationToken))
        {
            throw new InvalidOperationException(
                "Kode outlet sudah digunakan.");
        }

        if (entity.IsDefault && !request.Active)
        {
            throw new InvalidOperationException(
                "Outlet default tidak dapat dinonaktifkan. Jadikan outlet lain sebagai default terlebih dahulu.");
        }

        if (entity.IsDefault && !request.IsDefault)
        {
            throw new InvalidOperationException(
                "Outlet default tidak dapat dilepas tanpa memilih outlet default pengganti.");
        }

        if (request.IsDefault && !entity.IsDefault)
        {
            if (!request.Active)
            {
                throw new InvalidOperationException(
                    "Outlet nonaktif tidak dapat dijadikan default.");
            }

            await ClearDefaultAsync(entity.Id, cancellationToken);
            entity.IsDefault = true;
        }

        entity.Code = code;
        entity.Name = name;
        entity.Address = request.Address.Trim();
        entity.Phone = request.Phone.Trim();
        entity.Active = request.Active;

        await db.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<NeverfadePos.Api.Entities.Outlet> ResolveAsync(
        Guid? outletId,
        CancellationToken cancellationToken = default)
    {
        if (outletId.HasValue && outletId.Value != Guid.Empty)
        {
            return await db.Outlets
                .SingleOrDefaultAsync(
                    x => x.Id == outletId.Value && x.Active,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    "Outlet aktif tidak ditemukan.");
        }

        return await EnsureDefaultOutletAsync(cancellationToken);
    }

    private async Task<NeverfadePos.Api.Entities.Outlet> EnsureDefaultOutletAsync(
        CancellationToken cancellationToken)
    {
        var active = await db.Outlets
            .Where(x => x.Active)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (active is not null)
        {
            if (!active.IsDefault)
            {
                await ClearDefaultAsync(active.Id, cancellationToken);
                active.IsDefault = true;
                await db.SaveChangesAsync(cancellationToken);
            }

            return active;
        }

        var tenantId = RequireTenantId();
        var tenant = await db.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == tenantId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Tenant tidak ditemukan.");

        var entity = new NeverfadePos.Api.Entities.Outlet
        {
            TenantId = tenantId,
            Code = "MAIN",
            Name = tenant.NamaToko,
            Address = string.Empty,
            Phone = string.Empty,
            IsDefault = true,
            Active = true
        };

        db.Outlets.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return entity;
    }

    private async Task ClearDefaultAsync(
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var defaults = await db.Outlets
            .Where(x =>
                x.IsDefault &&
                (!exceptId.HasValue || x.Id != exceptId.Value))
            .ToListAsync(cancellationToken);

        foreach (var outlet in defaults)
        {
            outlet.IsDefault = false;
        }
    }

    private Guid RequireTenantId()
    {
        return tenantContext.TargetTenantId
            ?? throw new UnauthorizedAccessException();
    }

    private static string NormalizeCode(string value)
    {
        var code = value.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Kode outlet wajib diisi.");
        }

        return code;
    }

    private static string RequireName(string value)
    {
        var name = value.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nama outlet wajib diisi.");
        }

        return name;
    }

    private static OutletDto Map(
        NeverfadePos.Api.Entities.Outlet outlet)
    {
        return new OutletDto
        {
            Id = outlet.Id,
            Code = outlet.Code,
            Name = outlet.Name,
            Address = outlet.Address,
            Phone = outlet.Phone,
            IsDefault = outlet.IsDefault,
            Active = outlet.Active
        };
    }
}

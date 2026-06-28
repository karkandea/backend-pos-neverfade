using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Karyawan;
using KaryawanEntity = NeverfadePos.Api.Entities.Karyawan;

namespace NeverfadePos.Api.Services.Karyawan;

public sealed class KaryawanService(
    AppDbContext db,
    CurrentUser currentUser)
    : IKaryawanService
{
    public async Task<List<KaryawanDto>> GetAllAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = db.Karyawans.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Nama.Contains(search) ||
                x.Jabatan.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        return await query
            .OrderBy(x => x.Nama)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<KaryawanDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Karyawans
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync(cancellationToken);

        return entity
            ?? throw new KeyNotFoundException("Karyawan tidak ditemukan.");
    }

    public async Task<KaryawanDto> CreateAsync(
        CreateKaryawanDto request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue)
            throw new UnauthorizedAccessException();

        var entity = new KaryawanEntity
        {
            TenantId = currentUser.TenantId.Value,
            Nama = request.Nama,
            Jabatan = request.Jabatan,
            Telepon = request.Telepon,
            Email = request.Email,
            Gaji = request.Gaji,
            TanggalMasuk = request.TanggalMasuk,
            Status = request.Status,
            Catatan = request.Catatan
        };

        db.Karyawans.Add(entity);

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<KaryawanDto> UpdateAsync(
        Guid id,
        UpdateKaryawanDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Karyawans
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Karyawan tidak ditemukan.");

        entity.Nama = request.Nama;
        entity.Jabatan = request.Jabatan;
        entity.Telepon = request.Telepon;
        entity.Email = request.Email;
        entity.Gaji = request.Gaji;
        entity.TanggalMasuk = request.TanggalMasuk;
        entity.Status = request.Status;
        entity.Catatan = request.Catatan;

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Karyawans
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Karyawan tidak ditemukan.");

        db.Karyawans.Remove(entity);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<KaryawanEntity, KaryawanDto>> MapToDto()
    {
        return x => new KaryawanDto
        {
            Id = x.Id,
            Nama = x.Nama,
            Jabatan = x.Jabatan,
            Telepon = x.Telepon,
            Email = x.Email,
            Gaji = x.Gaji,
            TanggalMasuk = x.TanggalMasuk,
            Status = x.Status,
            Catatan = x.Catatan
        };
    }
}

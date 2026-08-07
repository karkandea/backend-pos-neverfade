using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Absensi;

namespace NeverfadePos.Api.Services.Absensi;

public sealed class AbsensiService(
    AppDbContext db,
    CurrentUser currentUser)
    : IAbsensiService
{
    private static readonly TimeZoneInfo Wib =
        TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows()
                ? "SE Asia Standard Time"
                : "Asia/Jakarta");

    public async Task<AbsensiResultDto> CheckInAsync(
        CreateAbsensiDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureKaryawanExistsAsync(
            request.KaryawanId,
            cancellationToken);

        var now =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                Wib);

        var today =
            DateOnly.FromDateTime(now);

        var nowTime =
            TimeOnly.FromDateTime(now);

        var absensi = await db.Absensis
            .FirstOrDefaultAsync(
                x =>
                    x.KaryawanId ==
                    request.KaryawanId &&
                    x.Tanggal == today,
                cancellationToken);

        if (absensi is null)
        {
            absensi =
                new Entities.Absensi
                {
                    TenantId =
                        currentUser.TenantId
                        ?? throw new UnauthorizedAccessException(),

                    KaryawanId =
                        request.KaryawanId,

                    Tanggal =
                        today,

                    CheckIn =
                        nowTime
                };

            db.Absensis.Add(absensi);
        }
        else if (absensi.CheckIn is null)
        {
            absensi.CheckIn = nowTime;
        }

        await db.SaveChangesAsync(
            cancellationToken);

        return new AbsensiResultDto
        {
            Ok = true,

            CheckIn =
                absensi.CheckIn?
                    .ToString("HH:mm"),

            FotoUrl = null
        };
    }

    public async Task<AbsensiResultDto> CheckOutAsync(
        CreateAbsensiDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureKaryawanExistsAsync(
            request.KaryawanId,
            cancellationToken);

        var now =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                Wib);

        var today =
            DateOnly.FromDateTime(now);

        var nowTime =
            TimeOnly.FromDateTime(now);

        var absensi = await db.Absensis
            .FirstOrDefaultAsync(
                x =>
                    x.KaryawanId ==
                    request.KaryawanId &&
                    x.Tanggal == today,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Belum check-in hari ini.");

        if (absensi.CheckIn is null)
        {
            throw new InvalidOperationException(
                "Belum check-in hari ini.");
        }

        if (absensi.CheckOut is not null)
        {
            throw new InvalidOperationException(
                "Sudah check-out hari ini.");
        }

        absensi.CheckOut = nowTime;

        await db.SaveChangesAsync(
            cancellationToken);

        return new AbsensiResultDto
        {
            Ok = true,

            CheckOut =
                absensi.CheckOut?
                    .ToString("HH:mm"),

            FotoUrl = null
        };
    }

    public async Task<List<AbsensiDto>> GetAllAsync(
        Guid? karyawanId,
        DateOnly? tanggal,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        var query =
            from a in db.Absensis.AsNoTracking()
            join k in db.Karyawans.AsNoTracking()
                on a.KaryawanId equals k.Id
            select new
            {
                a,
                k
            };

        if (karyawanId.HasValue)
        {
            query = query.Where(
                x =>
                    x.a.KaryawanId ==
                    karyawanId.Value);
        }

        if (tanggal.HasValue)
        {
            query = query.Where(
                x =>
                    x.a.Tanggal ==
                    tanggal.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(
                x =>
                    x.a.Tanggal >=
                    startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(
                x =>
                    x.a.Tanggal <=
                    endDate.Value);
        }

        return await query
            .OrderByDescending(
                x => x.a.Tanggal)
            .ThenByDescending(
                x => x.a.CheckIn)
            .Select(
                x => new AbsensiDto
                {
                    Id =
                        x.a.Id,

                    KaryawanId =
                        x.a.KaryawanId,

                    KaryawanNama =
                        x.k.Nama,

                    Jabatan =
                        x.k.Jabatan,

                    Tanggal =
                        x.a.Tanggal
                            .ToString(
                                "yyyy-MM-dd"),

                    CheckIn =
                        x.a.CheckIn.HasValue
                            ? x.a.CheckIn.Value
                                .ToString("HH:mm")
                            : null,

                    CheckOut =
                        x.a.CheckOut.HasValue
                            ? x.a.CheckOut.Value
                                .ToString("HH:mm")
                            : null
                })
            .ToListAsync(
                cancellationToken);
    }

    private async Task EnsureKaryawanExistsAsync(
        Guid karyawanId,
        CancellationToken cancellationToken)
    {
        var exists =
            await db.Karyawans
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == karyawanId,
                    cancellationToken);

        if (!exists)
        {
            throw new KeyNotFoundException(
                "Karyawan tidak ditemukan.");
        }
    }
}

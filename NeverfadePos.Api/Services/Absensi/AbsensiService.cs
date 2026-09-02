using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Absensi;
using NeverfadePos.Api.Services.Attendance;

namespace NeverfadePos.Api.Services.Absensi;

public sealed class AbsensiService(
    AppDbContext db,
    CurrentUser currentUser)
    : IAbsensiService
{
    private static readonly TimeZoneInfo Wib = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Jakarta");

    public async Task<AbsensiResultDto> CheckInAsync(
        CreateAbsensiDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureKaryawanExistsAsync(request.KaryawanId, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var now = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Wib);
        var today = DateOnly.FromDateTime(now);
        var nowTime = TimeOnly.FromDateTime(now);
        var absensi = await db.Absensis.FirstOrDefaultAsync(
            x => x.KaryawanId == request.KaryawanId && x.Tanggal == today,
            cancellationToken);

        if (absensi?.CheckIn is null)
        {
            var schedule = await GetEffectiveScheduleAsync(request.KaryawanId, today, cancellationToken);
            absensi ??= new Entities.Absensi
            {
                TenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException(),
                KaryawanId = request.KaryawanId,
                Tanggal = today
            };

            absensi.CheckIn = nowTime;
            absensi.CheckInAtUtc = nowUtc;
            absensi.OutsideSchedule = !schedule.IsScheduled;

            if (db.Entry(absensi).State == EntityState.Detached)
                db.Absensis.Add(absensi);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                absensi = await db.Absensis.AsNoTracking().FirstOrDefaultAsync(
                    x => x.KaryawanId == request.KaryawanId && x.Tanggal == today,
                    cancellationToken);
                if (absensi?.CheckIn is null)
                    throw;
            }
        }

        return new AbsensiResultDto
        {
            Ok = true,
            CheckIn = absensi.CheckIn?.ToString("HH:mm"),
            FotoUrl = null
        };
    }

    public async Task<AbsensiResultDto> CheckOutAsync(
        CreateAbsensiDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureKaryawanExistsAsync(request.KaryawanId, cancellationToken);

        var nowUtc = DateTime.UtcNow;
        var now = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, Wib);
        var today = DateOnly.FromDateTime(now);
        var nowTime = TimeOnly.FromDateTime(now);
        var absensi = await db.Absensis.FirstOrDefaultAsync(
            x => x.KaryawanId == request.KaryawanId && x.Tanggal == today,
            cancellationToken)
            ?? throw new InvalidOperationException("Belum check-in hari ini.");

        if (absensi.CheckIn is null)
            throw new InvalidOperationException("Belum check-in hari ini.");
        if (absensi.CheckOut is not null)
            throw new InvalidOperationException("Sudah check-out hari ini.");

        absensi.CheckOut = nowTime;
        absensi.CheckOutAtUtc = nowUtc;
        await db.SaveChangesAsync(cancellationToken);

        return new AbsensiResultDto
        {
            Ok = true,
            CheckOut = absensi.CheckOut?.ToString("HH:mm"),
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
        var query = from a in db.Absensis.AsNoTracking()
                    join k in db.Karyawans.AsNoTracking() on a.KaryawanId equals k.Id
                    select new { a, k };

        if (karyawanId.HasValue)
            query = query.Where(x => x.a.KaryawanId == karyawanId.Value);
        if (tanggal.HasValue)
            query = query.Where(x => x.a.Tanggal == tanggal.Value);
        if (startDate.HasValue)
            query = query.Where(x => x.a.Tanggal >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(x => x.a.Tanggal <= endDate.Value);

        return await query
            .OrderByDescending(x => x.a.Tanggal)
            .ThenByDescending(x => x.a.CheckIn)
            .Select(x => new AbsensiDto
            {
                Id = x.a.Id,
                KaryawanId = x.a.KaryawanId,
                KaryawanNama = x.k.Nama,
                Jabatan = x.k.Jabatan,
                Tanggal = x.a.Tanggal.ToString("yyyy-MM-dd"),
                CheckIn = x.a.CheckIn.HasValue ? x.a.CheckIn.Value.ToString("HH:mm") : null,
                CheckOut = x.a.CheckOut.HasValue ? x.a.CheckOut.Value.ToString("HH:mm") : null
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<EffectiveSchedule> GetEffectiveScheduleAsync(
        Guid karyawanId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var weekly = await db.EmployeeWeeklySchedules.AsNoTracking().FirstOrDefaultAsync(
            x => x.KaryawanId == karyawanId && x.DayOfWeek == (int)date.DayOfWeek,
            cancellationToken);
        var exception = await db.EmployeeScheduleExceptions.AsNoTracking().FirstOrDefaultAsync(
            x => x.KaryawanId == karyawanId && x.Date == date,
            cancellationToken);
        return AttendanceStatusRules.ResolveSchedule(weekly, exception);
    }

    private async Task EnsureKaryawanExistsAsync(Guid karyawanId, CancellationToken cancellationToken)
    {
        var exists = await db.Karyawans.AsNoTracking().AnyAsync(x => x.Id == karyawanId, cancellationToken);
        if (!exists)
            throw new KeyNotFoundException("Karyawan tidak ditemukan.");
    }
}

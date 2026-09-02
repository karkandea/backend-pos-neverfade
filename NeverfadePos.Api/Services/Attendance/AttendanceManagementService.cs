using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Attendance;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Attendance;

public interface IAttendanceManagementService
{
    Task<AttendancePolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<AttendancePolicyDto> UpdatePolicyAsync(UpdateAttendancePolicyRequestDto request, CancellationToken cancellationToken = default);
    Task<List<WeeklyScheduleDayDto>> GetWeeklyScheduleAsync(Guid karyawanId, CancellationToken cancellationToken = default);
    Task<List<WeeklyScheduleDayDto>> ReplaceWeeklyScheduleAsync(Guid karyawanId, ReplaceWeeklyScheduleRequestDto request, CancellationToken cancellationToken = default);
    Task<List<ScheduleExceptionDto>> GetExceptionsAsync(Guid? karyawanId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<ScheduleExceptionDto> UpsertExceptionAsync(UpsertScheduleExceptionRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteExceptionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AttendanceDashboardDto> GetDashboardAsync(DateOnly date, Guid? karyawanId, string? status, CancellationToken cancellationToken = default);
    Task<AttendanceCorrectionDto> CorrectAsync(CorrectAttendanceRequestDto request, CancellationToken cancellationToken = default);
}

internal sealed class AttendanceManagementService(
    AppDbContext db,
    CurrentUser currentUser)
    : IAttendanceManagementService
{
    private static readonly TimeZoneInfo Wib = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Jakarta");

    public async Task<AttendancePolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        var policy = await db.AttendancePolicies.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return new AttendancePolicyDto
        {
            GraceMinutes = policy?.GraceMinutes ?? 10,
            AbsenceThresholdMinutes = policy?.AbsenceThresholdMinutes ?? 120
        };
    }

    public async Task<AttendancePolicyDto> UpdatePolicyAsync(
        UpdateAttendancePolicyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var policy = await db.AttendancePolicies.FirstOrDefaultAsync(cancellationToken);
        if (policy is null)
        {
            policy = new AttendancePolicy { TenantId = tenantId };
            db.AttendancePolicies.Add(policy);
        }

        policy.GraceMinutes = request.GraceMinutes;
        policy.AbsenceThresholdMinutes = request.AbsenceThresholdMinutes;
        policy.UpdatedAt = DateTime.UtcNow;
        AddAudit("ATTENDANCE_POLICY_UPDATED", new
        {
            policy.GraceMinutes,
            policy.AbsenceThresholdMinutes
        });

        await db.SaveChangesAsync(cancellationToken);
        return await GetPolicyAsync(cancellationToken);
    }

    public async Task<List<WeeklyScheduleDayDto>> GetWeeklyScheduleAsync(
        Guid karyawanId,
        CancellationToken cancellationToken = default)
    {
        await EnsureEmployeeAsync(karyawanId, cancellationToken);
        var rows = await db.EmployeeWeeklySchedules
            .AsNoTracking()
            .Where(x => x.KaryawanId == karyawanId)
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, 7)
            .Select(day =>
            {
                var row = rows.FirstOrDefault(x => x.DayOfWeek == day);
                return new WeeklyScheduleDayDto
                {
                    DayOfWeek = day,
                    IsWorkingDay = row?.IsWorkingDay ?? false,
                    StartTime = row?.StartTime,
                    EndTime = row?.EndTime
                };
            })
            .ToList();
    }

    public async Task<List<WeeklyScheduleDayDto>> ReplaceWeeklyScheduleAsync(
        Guid karyawanId,
        ReplaceWeeklyScheduleRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureEmployeeAsync(karyawanId, cancellationToken);
        if (request.Days.Select(x => x.DayOfWeek).Distinct().Count() != request.Days.Count)
            throw new ArgumentException("Hari jadwal tidak boleh duplikat.");

        foreach (var day in request.Days)
            ValidateScheduleDay(day.IsWorkingDay, day.StartTime, day.EndTime);

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var existing = await db.EmployeeWeeklySchedules
            .Where(x => x.KaryawanId == karyawanId)
            .ToListAsync(cancellationToken);
        db.EmployeeWeeklySchedules.RemoveRange(existing);

        foreach (var day in request.Days)
        {
            db.EmployeeWeeklySchedules.Add(new EmployeeWeeklySchedule
            {
                TenantId = tenantId,
                KaryawanId = karyawanId,
                DayOfWeek = day.DayOfWeek,
                IsWorkingDay = day.IsWorkingDay,
                StartTime = day.IsWorkingDay ? day.StartTime : null,
                EndTime = day.IsWorkingDay ? day.EndTime : null
            });
        }

        AddAudit("EMPLOYEE_SCHEDULE_REPLACED", new { karyawanId, dayCount = request.Days.Count });
        await db.SaveChangesAsync(cancellationToken);
        return await GetWeeklyScheduleAsync(karyawanId, cancellationToken);
    }

    public async Task<List<ScheduleExceptionDto>> GetExceptionsAsync(
        Guid? karyawanId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var query = db.EmployeeScheduleExceptions.AsNoTracking();
        if (karyawanId.HasValue)
            query = query.Where(x => x.KaryawanId == karyawanId.Value);
        if (from.HasValue)
            query = query.Where(x => x.Date >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.Date <= to.Value);

        return await query
            .OrderBy(x => x.Date)
            .ThenBy(x => x.KaryawanId)
            .Select(x => new ScheduleExceptionDto
            {
                Id = x.Id,
                KaryawanId = x.KaryawanId,
                Date = x.Date,
                Type = x.Type,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                Note = x.Note
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ScheduleExceptionDto> UpsertExceptionAsync(
        UpsertScheduleExceptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureEmployeeAsync(request.KaryawanId, cancellationToken);
        if (request.Type == "changed_shift")
            ValidateScheduleDay(true, request.StartTime, request.EndTime);

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var entity = await db.EmployeeScheduleExceptions
            .FirstOrDefaultAsync(
                x => x.KaryawanId == request.KaryawanId && x.Date == request.Date,
                cancellationToken);

        if (entity is null)
        {
            entity = new EmployeeScheduleException
            {
                TenantId = tenantId,
                KaryawanId = request.KaryawanId,
                Date = request.Date
            };
            db.EmployeeScheduleExceptions.Add(entity);
        }

        entity.Type = request.Type;
        entity.StartTime = request.Type == "changed_shift" ? request.StartTime : null;
        entity.EndTime = request.Type == "changed_shift" ? request.EndTime : null;
        entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        AddAudit("EMPLOYEE_SCHEDULE_EXCEPTION_UPSERTED", new
        {
            request.KaryawanId,
            date = request.Date.ToString("yyyy-MM-dd"),
            request.Type
        });
        await db.SaveChangesAsync(cancellationToken);

        return new ScheduleExceptionDto
        {
            Id = entity.Id,
            KaryawanId = entity.KaryawanId,
            Date = entity.Date,
            Type = entity.Type,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            Note = entity.Note
        };
    }

    public async Task DeleteExceptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await db.EmployeeScheduleExceptions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Pengecualian jadwal tidak ditemukan.");
        db.EmployeeScheduleExceptions.Remove(entity);
        AddAudit("EMPLOYEE_SCHEDULE_EXCEPTION_DELETED", new
        {
            exceptionId = entity.Id,
            entity.KaryawanId,
            date = entity.Date.ToString("yyyy-MM-dd")
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AttendanceDashboardDto> GetDashboardAsync(
        DateOnly date,
        Guid? karyawanId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var employeesQuery = db.Karyawans.AsNoTracking().Where(x => x.Status == "aktif");
        if (karyawanId.HasValue)
            employeesQuery = employeesQuery.Where(x => x.Id == karyawanId.Value);
        var employees = await employeesQuery.OrderBy(x => x.Nama).ToListAsync(cancellationToken);
        var employeeIds = employees.Select(x => x.Id).ToList();
        var dayOfWeek = (int)date.DayOfWeek;

        var schedules = await db.EmployeeWeeklySchedules.AsNoTracking()
            .Where(x => employeeIds.Contains(x.KaryawanId) && x.DayOfWeek == dayOfWeek)
            .ToListAsync(cancellationToken);
        var exceptions = await db.EmployeeScheduleExceptions.AsNoTracking()
            .Where(x => employeeIds.Contains(x.KaryawanId) && x.Date == date)
            .ToListAsync(cancellationToken);
        var attendances = await db.Absensis.AsNoTracking()
            .Where(x => employeeIds.Contains(x.KaryawanId) && x.Tanggal == date)
            .ToListAsync(cancellationToken);
        var policy = await GetPolicyAsync(cancellationToken);
        var nowWib = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Wib);

        var rows = new List<AttendanceDashboardRowDto>();
        foreach (var employee in employees)
        {
            var weekly = schedules.FirstOrDefault(x => x.KaryawanId == employee.Id);
            var exception = exceptions.FirstOrDefault(x => x.KaryawanId == employee.Id);
            var attendance = attendances.FirstOrDefault(x => x.KaryawanId == employee.Id);
            var effective = AttendanceStatusRules.ResolveSchedule(weekly, exception);
            var state = AttendanceStatusRules.BuildState(
                date,
                attendance,
                effective,
                policy.GraceMinutes,
                policy.AbsenceThresholdMinutes,
                nowWib);

            rows.Add(new AttendanceDashboardRowDto
            {
                KaryawanId = employee.Id,
                KaryawanNama = employee.Nama,
                Jabatan = employee.Jabatan,
                Status = state.Status,
                ScheduleStart = state.ScheduleStart,
                ScheduleEnd = state.ScheduleEnd,
                CheckIn = state.CheckIn,
                CheckOut = state.CheckOut,
                OutsideSchedule = state.OutsideSchedule,
                ExceptionType = state.ExceptionType,
                ExceptionNote = exception?.Note
            });
        }

        if (!string.IsNullOrWhiteSpace(status))
            rows = rows.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();

        var summaryRows = rows;
        return new AttendanceDashboardDto
        {
            Date = date.ToString("yyyy-MM-dd"),
            Summary = new AttendanceDashboardSummaryDto
            {
                Scheduled = summaryRows.Count(x => x.Status == "scheduled"),
                Present = summaryRows.Count(x => x.Status == "present"),
                Late = summaryRows.Count(x => x.Status == "late"),
                Absent = summaryRows.Count(x => x.Status == "absent"),
                Working = summaryRows.Count(x => x.Status == "working"),
                MissingCheckout = summaryRows.Count(x => x.Status == "missing_checkout")
            },
            Employees = rows
        };
    }

    public async Task<AttendanceCorrectionDto> CorrectAsync(
        CorrectAttendanceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureEmployeeAsync(request.KaryawanId, cancellationToken);
        if (request.CheckOut.HasValue && !request.CheckIn.HasValue)
            throw new ArgumentException("Check-out tidak boleh ada tanpa check-in.");
        if (request.CheckIn.HasValue && request.CheckOut.HasValue && request.CheckOut <= request.CheckIn)
            throw new ArgumentException("Check-out harus setelah check-in.");

        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var actorUserId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var attendance = await db.Absensis
            .FirstOrDefaultAsync(
                x => x.KaryawanId == request.KaryawanId && x.Tanggal == request.Date,
                cancellationToken);

        var beforeData = SerializeAttendance(attendance);
        if (attendance is null)
        {
            if (!request.CheckIn.HasValue)
                throw new ArgumentException("Correction tanpa check-in tidak memiliki perubahan.");
            attendance = new Absensi
            {
                TenantId = tenantId,
                KaryawanId = request.KaryawanId,
                Tanggal = request.Date
            };
            db.Absensis.Add(attendance);
        }

        attendance.CheckIn = request.CheckIn;
        attendance.CheckOut = request.CheckOut;
        attendance.CheckInAtUtc = request.CheckIn.HasValue
            ? LocalToUtc(request.Date, request.CheckIn.Value)
            : null;
        attendance.CheckOutAtUtc = request.CheckOut.HasValue
            ? LocalToUtc(request.Date, request.CheckOut.Value)
            : null;

        var weekly = await db.EmployeeWeeklySchedules.AsNoTracking().FirstOrDefaultAsync(
            x => x.KaryawanId == request.KaryawanId && x.DayOfWeek == (int)request.Date.DayOfWeek,
            cancellationToken);
        var exception = await db.EmployeeScheduleExceptions.AsNoTracking().FirstOrDefaultAsync(
            x => x.KaryawanId == request.KaryawanId && x.Date == request.Date,
            cancellationToken);
        attendance.OutsideSchedule = request.CheckIn.HasValue &&
            !AttendanceStatusRules.ResolveSchedule(weekly, exception).IsScheduled;

        var correction = new AttendanceCorrection
        {
            TenantId = tenantId,
            AbsensiId = attendance.Id,
            CorrectedByUserId = actorUserId,
            Reason = request.Reason.Trim(),
            BeforeData = beforeData,
            AfterData = SerializeAttendance(attendance)
        };
        db.AttendanceCorrections.Add(correction);
        AddAudit("ATTENDANCE_CORRECTED", new
        {
            correctionId = correction.Id,
            request.KaryawanId,
            date = request.Date.ToString("yyyy-MM-dd")
        });

        await db.SaveChangesAsync(cancellationToken);
        return new AttendanceCorrectionDto
        {
            Id = correction.Id,
            AbsensiId = correction.AbsensiId,
            CorrectedByUserId = correction.CorrectedByUserId,
            Reason = correction.Reason,
            BeforeData = correction.BeforeData,
            AfterData = correction.AfterData,
            CreatedAt = correction.CreatedAt
        };
    }

    private async Task EnsureEmployeeAsync(Guid karyawanId, CancellationToken cancellationToken)
    {
        if (!await db.Karyawans.AsNoTracking().AnyAsync(x => x.Id == karyawanId, cancellationToken))
            throw new KeyNotFoundException("Karyawan tidak ditemukan.");
    }

    private void AddAudit(string eventType, object metadata)
    {
        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException(),
            ActorUserId = currentUser.UserId,
            EventType = eventType,
            Metadata = JsonSerializer.Serialize(metadata)
        });
    }

    private static void ValidateScheduleDay(bool working, TimeOnly? start, TimeOnly? end)
    {
        if (!working)
            return;
        if (!start.HasValue || !end.HasValue)
            throw new ArgumentException("Hari kerja wajib memiliki jam mulai dan selesai.");
        if (end.Value <= start.Value)
            throw new ArgumentException("Jam selesai harus setelah jam mulai.");
    }

    private static DateTime LocalToUtc(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Wib);
    }

    private static string SerializeAttendance(Absensi? attendance) =>
        attendance is null
            ? "null"
            : JsonSerializer.Serialize(new
            {
                attendance.CheckIn,
                attendance.CheckOut,
                attendance.CheckInAtUtc,
                attendance.CheckOutAtUtc,
                attendance.OutsideSchedule
            });
}

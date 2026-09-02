using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.SharedPos;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Attendance;
using Npgsql;
using KaryawanEntity = NeverfadePos.Api.Entities.Karyawan;

namespace NeverfadePos.Api.Services.SharedPos;

public interface ISharedPosService
{
    Task<List<SharedPosDeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default);
    Task<RegisteredSharedPosDeviceDto> RegisterDeviceAsync(RegisterSharedPosDeviceRequestDto request, CancellationToken cancellationToken = default);
    Task DeactivateDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task<SharedPosUnlockResponseDto> UnlockAsync(string deviceToken, SharedPosUnlockRequestDto request, CancellationToken cancellationToken = default);
    Task<SharedPosSessionDto> GetSessionAsync(string sessionToken, CancellationToken cancellationToken = default);
    Task LockAsync(string sessionToken, CancellationToken cancellationToken = default);
    Task<SharedAttendanceResultDto> CheckInAsync(string sessionToken, CancellationToken cancellationToken = default);
    Task<SharedAttendanceResultDto> CheckOutAsync(string sessionToken, CancellationToken cancellationToken = default);
}

internal sealed class SharedPosService(
    AppDbContext db,
    CurrentUser currentUser,
    ITrustedTenantExecutionScope trustedTenantScope,
    SharedPosSecurity security,
    ISharedPosJwtService sharedPosJwtService)
    : ISharedPosService
{
    private const string AttendanceUniqueConstraint =
        "IX_absensis_TenantId_KaryawanId_Tanggal";

    private static readonly TimeZoneInfo Wib = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Jakarta");

    private static readonly string DummyPinHash = BCrypt.Net.BCrypt.HashPassword("000000", workFactor: 12);

    public async Task<List<SharedPosDeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        return await db.SharedPosDevices
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SharedPosDeviceDto
            {
                Id = x.Id,
                Name = x.Name,
                Active = x.Active,
                LastUsedAt = x.LastUsedAt,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<RegisteredSharedPosDeviceDto> RegisterDeviceAsync(
        RegisterSharedPosDeviceRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Nama perangkat wajib diisi.");

        var token = SharedPosSecurity.GenerateOpaqueToken();
        var entity = new SharedPosDevice
        {
            TenantId = tenantId,
            Name = name,
            TokenHash = SharedPosSecurity.HashToken(token),
            Active = true,
            CreatedByUserId = userId
        };

        db.SharedPosDevices.Add(entity);
        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = userId,
            EventType = "SHARED_POS_DEVICE_REGISTERED",
            Metadata = JsonSerializer.Serialize(new { deviceId = entity.Id, name = entity.Name })
        });

        await db.SaveChangesAsync(cancellationToken);

        return new RegisteredSharedPosDeviceDto
        {
            Device = MapDevice(entity),
            DeviceToken = token
        };
    }

    public async Task DeactivateDeviceAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        var device = await db.SharedPosDevices
            .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken)
            ?? throw new KeyNotFoundException("Perangkat shared POS tidak ditemukan.");

        if (!device.Active)
            return;

        device.Active = false;
        var now = DateTime.UtcNow;
        var sessions = await db.SharedPosSessions
            .Where(x => x.DeviceId == device.Id && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            session.RevokedAtUtc = now;

        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException(),
            ActorUserId = currentUser.UserId,
            EventType = "SHARED_POS_DEVICE_DEACTIVATED",
            Metadata = JsonSerializer.Serialize(new { deviceId = device.Id, device.Name })
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SharedPosUnlockResponseDto> UnlockAsync(
        string deviceToken,
        SharedPosUnlockRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            throw AuthenticationFailed();

        var tokenHash = SharedPosSecurity.HashToken(deviceToken.Trim());
        var seed = await db.SharedPosDevices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.Active && x.TokenHash == tokenHash)
            .Select(x => new { x.Id, x.TenantId })
            .FirstOrDefaultAsync(cancellationToken);

        if (seed is null)
            throw AuthenticationFailed();

        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == seed.TenantId, cancellationToken);
        if (tenant is null ||
            !string.Equals(tenant.Status, "active", StringComparison.Ordinal) ||
            !BusinessCapabilityPresets.HasCapability(tenant.BusinessType, TenantCapabilities.Attendance))
        {
            throw AuthenticationFailed();
        }

        using var scope = trustedTenantScope.Begin(seed.TenantId, "shared-pos-unlock");
        var device = await db.SharedPosDevices
            .FirstOrDefaultAsync(x => x.Id == seed.Id && x.Active, cancellationToken)
            ?? throw AuthenticationFailed();

        var now = DateTime.UtcNow;
        if (device.LockedUntilUtc.HasValue && device.LockedUntilUtc.Value > now)
        {
            throw new TenantApiException(
                StatusCodes.Status429TooManyRequests,
                "SHARED_DEVICE_TEMPORARILY_LOCKED",
                "Terlalu banyak percobaan. Coba lagi beberapa menit lagi.");
        }

        if (device.LockedUntilUtc.HasValue && device.LockedUntilUtc.Value <= now)
        {
            device.LockedUntilUtc = null;
            device.FailedUnlockCount = 0;
        }

        var pin = request.Pin.Trim();
        var fingerprint = security.FingerprintPin(seed.TenantId, pin);
        var employee = await db.Karyawans
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.PinFingerprint == fingerprint, cancellationToken);

        var hashToVerify = employee?.PinHash ?? DummyPinHash;
        var pinValid = SharedPosSecurity.VerifyPin(pin, hashToVerify);
        var employeeActive = employee is not null &&
            string.Equals(employee.Status, "aktif", StringComparison.OrdinalIgnoreCase);
        var linkedUserActive = employee?.UserId is null || employee.User?.Active == true;

        if (!pinValid || !employeeActive || !linkedUserActive)
        {
            await RecordUnlockFailureAsync(device, cancellationToken);
            throw AuthenticationFailed();
        }

        var activeSessions = await db.SharedPosSessions
            .Where(x => x.DeviceId == device.Id && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var activeSession in activeSessions)
            activeSession.RevokedAtUtc = now;

        device.FailedUnlockCount = 0;
        device.LockedUntilUtc = null;
        device.LastUsedAt = now;

        var sessionToken = SharedPosSecurity.GenerateOpaqueToken();
        var session = new SharedPosSession
        {
            TenantId = seed.TenantId,
            DeviceId = device.Id,
            KaryawanId = employee!.Id,
            UserId = employee.UserId,
            TokenHash = SharedPosSecurity.HashToken(sessionToken),
            ExpiresAtUtc = now.AddMinutes(30)
        };
        db.SharedPosSessions.Add(session);

        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = seed.TenantId,
            ActorKaryawanId = employee.Id,
            ActorUserId = employee.UserId,
            EventType = "SHARED_POS_UNLOCK_SUCCEEDED",
            Metadata = JsonSerializer.Serialize(new { deviceId = device.Id, sessionId = session.Id })
        });

        await db.SaveChangesAsync(cancellationToken);

        SharedPosJwtResult? posJwt = null;
        if (employee.User is not null)
            posJwt = sharedPosJwtService.Generate(employee.User, employee.Id, session.Id);

        var attendance = await GetAttendanceStateAsync(employee.Id, DateOnly.FromDateTime(ToWib(now)), now, cancellationToken);
        return new SharedPosUnlockResponseDto
        {
            SessionToken = sessionToken,
            ExpiresAtUtc = session.ExpiresAtUtc,
            Employee = MapEmployee(employee),
            Attendance = attendance,
            PosToken = posJwt?.Token,
            PosExpiresAtUtc = posJwt?.ExpiresAtUtc
        };
    }

    public async Task<SharedPosSessionDto> GetSessionAsync(
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        var seed = await FindSessionSeedAsync(sessionToken, cancellationToken);
        using var scope = trustedTenantScope.Begin(seed.TenantId, "shared-pos-session");
        var session = await GetActiveSessionAsync(seed.SessionId, cancellationToken);
        var now = DateTime.UtcNow;

        return new SharedPosSessionDto
        {
            ExpiresAtUtc = session.ExpiresAtUtc,
            Employee = MapEmployee(session.Karyawan!),
            Attendance = await GetAttendanceStateAsync(
                session.KaryawanId,
                DateOnly.FromDateTime(ToWib(now)),
                now,
                cancellationToken)
        };
    }

    public async Task LockAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        var seed = await FindSessionSeedAsync(sessionToken, cancellationToken);
        using var scope = trustedTenantScope.Begin(seed.TenantId, "shared-pos-lock");
        var session = await db.SharedPosSessions
            .FirstOrDefaultAsync(x => x.Id == seed.SessionId, cancellationToken)
            ?? throw SessionInvalid();

        if (session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<SharedAttendanceResultDto> CheckInAsync(
        string sessionToken,
        CancellationToken cancellationToken = default) =>
        PunchAsync(sessionToken, checkIn: true, cancellationToken);

    public Task<SharedAttendanceResultDto> CheckOutAsync(
        string sessionToken,
        CancellationToken cancellationToken = default) =>
        PunchAsync(sessionToken, checkIn: false, cancellationToken);

    private async Task<SharedAttendanceResultDto> PunchAsync(
        string sessionToken,
        bool checkIn,
        CancellationToken cancellationToken)
    {
        var seed = await FindSessionSeedAsync(sessionToken, cancellationToken);
        using var scope = trustedTenantScope.Begin(seed.TenantId, checkIn ? "shared-pos-checkin" : "shared-pos-checkout");
        var session = await GetActiveSessionAsync(seed.SessionId, cancellationToken);
        var sessionEmployeeId = session.KaryawanId;
        var sessionDeviceId = session.DeviceId;
        var sessionUserId = session.UserId;
        var nowUtc = DateTime.UtcNow;
        var nowWib = ToWib(nowUtc);
        var date = DateOnly.FromDateTime(nowWib);
        var localTime = TimeOnly.FromDateTime(nowWib);
        var schedule = await GetEffectiveScheduleAsync(sessionEmployeeId, date, cancellationToken);

        var attendance = await db.Absensis
            .FirstOrDefaultAsync(x => x.KaryawanId == sessionEmployeeId && x.Tanggal == date, cancellationToken);

        if (checkIn)
        {
            if (attendance?.CheckIn is null)
            {
                attendance ??= new Absensi
                {
                    TenantId = seed.TenantId,
                    KaryawanId = sessionEmployeeId,
                    Tanggal = date
                };

                attendance.CheckIn = localTime;
                attendance.CheckInAtUtc = nowUtc;
                attendance.OutsideSchedule = !schedule.IsScheduled;
                if (db.Entry(attendance).State == EntityState.Detached)
                    db.Absensis.Add(attendance);
            }
        }
        else
        {
            if (attendance?.CheckIn is null)
            {
                throw new TenantApiException(
                    StatusCodes.Status409Conflict,
                    "ATTENDANCE_CHECKIN_REQUIRED",
                    "Belum check-in hari ini.");
            }

            if (attendance.CheckOut is null)
            {
                attendance.CheckOut = localTime;
                attendance.CheckOutAtUtc = nowUtc;
            }
        }

        var recordedAt = checkIn
            ? attendance?.CheckIn?.ToString("HH:mm") ?? localTime.ToString("HH:mm")
            : attendance?.CheckOut?.ToString("HH:mm") ?? localTime.ToString("HH:mm");

        session.RevokedAtUtc = nowUtc;
        AddPunchAudit(
            seed.TenantId,
            sessionEmployeeId,
            sessionUserId,
            sessionDeviceId,
            date,
            checkIn);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (checkIn && IsAttendanceUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            var existing = await db.Absensis
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.KaryawanId == sessionEmployeeId && x.Tanggal == date,
                    cancellationToken);

            if (existing?.CheckIn is null)
                throw;

            recordedAt = existing.CheckIn.Value.ToString("HH:mm");

            var persistedSession = await db.SharedPosSessions
                .FirstOrDefaultAsync(x => x.Id == seed.SessionId, cancellationToken)
                ?? throw SessionInvalid();

            if (persistedSession.RevokedAtUtc is null)
            {
                persistedSession.RevokedAtUtc = nowUtc;
                AddPunchAudit(
                    seed.TenantId,
                    sessionEmployeeId,
                    sessionUserId,
                    sessionDeviceId,
                    date,
                    checkIn: true);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        var state = await GetAttendanceStateAsync(sessionEmployeeId, date, nowUtc, cancellationToken);
        return new SharedAttendanceResultDto
        {
            Ok = true,
            RecordedAt = recordedAt,
            Attendance = state
        };
    }

    private void AddPunchAudit(
        Guid tenantId,
        Guid karyawanId,
        Guid? userId,
        Guid deviceId,
        DateOnly date,
        bool checkIn)
    {
        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = tenantId,
            ActorKaryawanId = karyawanId,
            ActorUserId = userId,
            EventType = checkIn ? "ATTENDANCE_CHECKED_IN" : "ATTENDANCE_CHECKED_OUT",
            Metadata = JsonSerializer.Serialize(new
            {
                deviceId,
                date = date.ToString("yyyy-MM-dd")
            })
        });
    }

    private static bool IsAttendanceUniqueViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgres &&
               postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
               string.Equals(
                   postgres.ConstraintName,
                   AttendanceUniqueConstraint,
                   StringComparison.Ordinal);
    }

    private async Task RecordUnlockFailureAsync(SharedPosDevice device, CancellationToken cancellationToken)
    {
        device.FailedUnlockCount += 1;
        if (device.FailedUnlockCount >= 5)
            device.LockedUntilUtc = DateTime.UtcNow.AddMinutes(5);

        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = device.TenantId,
            EventType = "SHARED_POS_UNLOCK_FAILED",
            Metadata = JsonSerializer.Serialize(new
            {
                deviceId = device.Id,
                failedUnlockCount = device.FailedUnlockCount,
                lockedUntilUtc = device.LockedUntilUtc
            })
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Guid SessionId, Guid TenantId)> FindSessionSeedAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
            throw SessionInvalid();

        var hash = SharedPosSecurity.HashToken(sessionToken.Trim());
        var seed = await db.SharedPosSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TokenHash == hash)
            .Select(x => new { x.Id, x.TenantId })
            .FirstOrDefaultAsync(cancellationToken);

        return seed is null
            ? throw SessionInvalid()
            : (seed.Id, seed.TenantId);
    }

    private async Task<SharedPosSession> GetActiveSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var session = await db.SharedPosSessions
            .Include(x => x.Device)
            .Include(x => x.Karyawan)
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

        if (session is null ||
            session.RevokedAtUtc is not null ||
            session.ExpiresAtUtc <= now ||
            session.Device?.Active != true ||
            !string.Equals(session.Karyawan?.Status, "aktif", StringComparison.OrdinalIgnoreCase) ||
            (session.UserId.HasValue && session.User?.Active != true))
        {
            throw SessionInvalid();
        }

        return session;
    }

    private async Task<SharedAttendanceStateDto> GetAttendanceStateAsync(
        Guid karyawanId,
        DateOnly date,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var attendance = await db.Absensis
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.KaryawanId == karyawanId && x.Tanggal == date, cancellationToken);
        var schedule = await GetEffectiveScheduleAsync(karyawanId, date, cancellationToken);
        var policy = await db.AttendancePolicies.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        return AttendanceStatusRules.BuildState(
            date,
            attendance,
            schedule,
            policy?.GraceMinutes ?? 10,
            policy?.AbsenceThresholdMinutes ?? 120,
            ToWib(nowUtc));
    }

    private async Task<EffectiveSchedule> GetEffectiveScheduleAsync(
        Guid karyawanId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var day = (int)date.DayOfWeek;
        var weekly = await db.EmployeeWeeklySchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.KaryawanId == karyawanId && x.DayOfWeek == day, cancellationToken);
        var exception = await db.EmployeeScheduleExceptions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.KaryawanId == karyawanId && x.Date == date, cancellationToken);
        return AttendanceStatusRules.ResolveSchedule(weekly, exception);
    }

    private static SharedPosDeviceDto MapDevice(SharedPosDevice x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Active = x.Active,
        LastUsedAt = x.LastUsedAt,
        CreatedAt = x.CreatedAt
    };

    private static SharedEmployeeDto MapEmployee(KaryawanEntity x) => new()
    {
        Id = x.Id,
        Nama = x.Nama,
        Jabatan = x.Jabatan,
        Role = x.User?.Role,
        CanAccessPos = x.User?.Active == true
    };

    private static DateTime ToWib(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(utc, Wib);

    private static TenantApiException AuthenticationFailed() => new(
        StatusCodes.Status401Unauthorized,
        "SHARED_POS_AUTH_FAILED",
        "Perangkat atau PIN tidak valid.");

    private static TenantApiException SessionInvalid() => new(
        StatusCodes.Status401Unauthorized,
        "SHARED_SESSION_INVALID",
        "Sesi shared POS sudah tidak aktif. Masukkan PIN lagi.");
}
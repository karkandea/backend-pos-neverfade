using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.SharedPos;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.SharedPos;

public interface IEmployeeSharedAccessService
{
    Task<EmployeeSharedAccessDto> GetAsync(Guid karyawanId, CancellationToken cancellationToken = default);
    Task<EmployeeSharedAccessDto> UpdateAsync(Guid karyawanId, UpdateEmployeeSharedAccessRequestDto request, CancellationToken cancellationToken = default);
}

internal sealed class EmployeeSharedAccessService(
    AppDbContext db,
    CurrentUser currentUser,
    SharedPosSecurity security)
    : IEmployeeSharedAccessService
{
    public async Task<EmployeeSharedAccessDto> GetAsync(
        Guid karyawanId,
        CancellationToken cancellationToken = default)
    {
        return await db.Karyawans
            .AsNoTracking()
            .Where(x => x.Id == karyawanId)
            .Select(x => new EmployeeSharedAccessDto
            {
                KaryawanId = x.Id,
                UserId = x.UserId,
                LinkedUsername = x.User == null ? null : x.User.Username,
                HasPin = x.PinHash != null,
                PinUpdatedAt = x.PinUpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Karyawan tidak ditemukan.");
    }

    public async Task<EmployeeSharedAccessDto> UpdateAsync(
        Guid karyawanId,
        UpdateEmployeeSharedAccessRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClearUserLink && request.UserId.HasValue)
        {
            throw new ArgumentException("Pilih link user baru atau hapus link, bukan keduanya.");
        }

        if (request.ClearPin && !string.IsNullOrWhiteSpace(request.Pin))
        {
            throw new ArgumentException("Isi PIN baru atau hapus PIN, bukan keduanya.");
        }

        var employee = await db.Karyawans
            .FirstOrDefaultAsync(x => x.Id == karyawanId, cancellationToken)
            ?? throw new KeyNotFoundException("Karyawan tidak ditemukan.");

        if (!string.Equals(employee.Status, "aktif", StringComparison.OrdinalIgnoreCase) &&
            (!string.IsNullOrWhiteSpace(request.Pin) || request.UserId.HasValue))
        {
            throw new TenantApiException(
                StatusCodes.Status409Conflict,
                "EMPLOYEE_INACTIVE",
                "Aktifkan karyawan sebelum memberi akses shared POS.");
        }

        if (request.ClearUserLink)
        {
            employee.UserId = null;
        }
        else if (request.UserId.HasValue)
        {
            var user = await db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.UserId.Value, cancellationToken)
                ?? throw new TenantApiException(
                    StatusCodes.Status400BadRequest,
                    "INVALID_EMPLOYEE_USER_LINK",
                    "User POS tidak ditemukan pada tenant ini.");

            if (!user.Active)
            {
                throw new TenantApiException(
                    StatusCodes.Status409Conflict,
                    "LINKED_USER_INACTIVE",
                    "User POS harus aktif sebelum dihubungkan ke karyawan.");
            }

            var linkedElsewhere = await db.Karyawans
                .AsNoTracking()
                .AnyAsync(x => x.Id != employee.Id && x.UserId == user.Id, cancellationToken);

            if (linkedElsewhere)
            {
                throw new TenantApiException(
                    StatusCodes.Status409Conflict,
                    "USER_ALREADY_LINKED",
                    "User POS ini sudah terhubung ke karyawan lain.");
            }

            employee.UserId = user.Id;
        }

        if (request.ClearPin)
        {
            employee.PinHash = null;
            employee.PinFingerprint = null;
            employee.PinUpdatedAt = null;
        }
        else if (!string.IsNullOrWhiteSpace(request.Pin))
        {
            var pin = request.Pin.Trim();
            if (pin.Length is < 4 or > 6 || pin.Any(x => !char.IsDigit(x)))
            {
                throw new ArgumentException("PIN harus 4-6 digit angka.");
            }

            var tenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException();
            var fingerprint = security.FingerprintPin(tenantId, pin);
            var duplicate = await db.Karyawans
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id != employee.Id && x.PinFingerprint == fingerprint,
                    cancellationToken);

            if (duplicate)
            {
                throw new TenantApiException(
                    StatusCodes.Status409Conflict,
                    "EMPLOYEE_PIN_ALREADY_USED",
                    "PIN sudah digunakan karyawan lain. Pilih PIN berbeda.");
            }

            employee.PinHash = SharedPosSecurity.HashPin(pin);
            employee.PinFingerprint = fingerprint;
            employee.PinUpdatedAt = DateTime.UtcNow;
        }

        var actorUserId = currentUser.UserId ?? throw new UnauthorizedAccessException();
        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = currentUser.TenantId ?? throw new UnauthorizedAccessException(),
            ActorUserId = actorUserId,
            ActorKaryawanId = employee.Id,
            EventType = "EMPLOYEE_SHARED_ACCESS_CHANGED",
            Metadata = JsonSerializer.Serialize(new
            {
                employeeId = employee.Id,
                userId = employee.UserId,
                hasPin = employee.PinHash != null,
                pinChanged = request.ClearPin || !string.IsNullOrWhiteSpace(request.Pin)
            })
        });

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(employee.Id, cancellationToken);
    }
}

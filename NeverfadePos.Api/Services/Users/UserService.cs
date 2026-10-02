using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.DTOs.User;
using Npgsql;
using UserEntity = NeverfadePos.Api.Entities.User;

namespace NeverfadePos.Api.Services.Users;

public sealed class UserService(
    AppDbContext db,
    CurrentUser currentUser)
    : IUserService
{
    private static readonly string[] AllowedRoles = { "owner", "admin", "kasir", "dapur", "laundry_operator" };

    public async Task<List<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Users.AsNoTracking().OrderBy(x => x.Nama).Select(MapToDto()).ToListAsync(cancellationToken);
    }

    public async Task<UserDto> CreateAsync(CreateUserDto request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue)
            throw new UnauthorizedAccessException();

        var nama = request.Nama.Trim();
        var username = request.Username.Trim();
        var role = NormalizeRole(request.Role);
        if (role == "owner" && currentUser.Role != "owner")
            throw new TenantApiException(403, "OWNER_ROLE_RESTRICTED", "Hanya owner dapat membuat akun owner.");
        if (await db.Users.AnyAsync(x => x.Username == username, cancellationToken))
            throw new InvalidOperationException("Username sudah digunakan.");

        var entity = new UserEntity
        {
            TenantId = currentUser.TenantId.Value,
            Nama = nama,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            Active = true
        };
        db.Users.Add(entity);
        if (role != "owner")
        {
            var defaultOutlet = await db.Outlets.Where(x => x.Active)
                .OrderByDescending(x => x.IsDefault).ThenBy(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (defaultOutlet is not null)
                db.UserOutletAssignments.Add(new NeverfadePos.Api.Entities.UserOutletAssignment
                {
                    TenantId = currentUser.TenantId.Value,
                    UserId = entity.Id,
                    OutletId = defaultOutlet.Id
                });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException("Username sudah digunakan.");
        }

        return MapToDto(entity);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto request, CancellationToken cancellationToken = default)
    {
        var entity = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("User tidak ditemukan.");
        var nama = request.Nama.Trim();
        var username = request.Username.Trim();
        var role = NormalizeRole(request.Role);
        if (entity.Role == "owner" && currentUser.Role != "owner")
            throw new TenantApiException(403, "OWNER_ACCOUNT_PROTECTED", "Admin tidak dapat mengubah akun owner.");
        if (role == "owner" && currentUser.Role != "owner")
            throw new TenantApiException(403, "OWNER_ROLE_RESTRICTED", "Hanya owner dapat menetapkan role owner.");
        if (entity.Role == "owner" && (role != "owner" || !request.Active))
            throw new InvalidOperationException("Akun owner tidak dapat dinonaktifkan atau diturunkan rolenya melalui pengelolaan user.");

        if (await db.Users.AnyAsync(x => x.Id != id && x.Username == username, cancellationToken))
            throw new InvalidOperationException("Username sudah digunakan.");

        entity.Nama = nama;
        entity.Username = username;
        entity.Role = role;
        entity.Active = request.Active;
        // Any credential, role, or status management invalidates old JWTs.
        entity.TokenVersion++;
        if (!string.IsNullOrWhiteSpace(request.Password))
            entity.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        await RevokeSharedSessionsAsync(entity.Id, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException("Username sudah digunakan.");
        }

        return MapToDto(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();
        if (currentUser.UserId.Value == id)
            throw new InvalidOperationException("Akun yang sedang digunakan tidak dapat dihapus.");

        var entity = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("User tidak ditemukan.");

        if (entity.Role == "owner")
            throw new TenantApiException(403, "OWNER_ACCOUNT_PROTECTED", "Akun owner tidak dapat dihapus melalui pengelolaan user.");

        var linkedEmployees = await db.Karyawans.Where(x => x.UserId == id).ToListAsync(cancellationToken);
        foreach (var employee in linkedEmployees)
            employee.UserId = null;

        await RevokeSharedSessionsAsync(id, cancellationToken);
        db.Users.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAsync(Guid id, string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();
        var key = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length is < 8 or > 128)
            throw new TenantApiException(400, "IDEMPOTENCY_KEY_REQUIRED",
                "Idempotency-Key 8 sampai 128 karakter wajib disertakan.");
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("User tidak ditemukan.");
        if (user.Role == "owner" && currentUser.Role != "owner")
            throw new TenantApiException(403, "OWNER_ACCOUNT_PROTECTED",
                "Admin tidak dapat mencabut sesi owner.");
        var previous = await db.UserSessionRevocations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
        if (previous is not null)
        {
            if (previous.UserId != id)
                throw new TenantApiException(409, "IDEMPOTENCY_KEY_REUSED",
                    "Kunci yang sama tidak boleh digunakan untuk pengguna berbeda.");
            return;
        }
        user.TokenVersion++;
        db.UserSessionRevocations.Add(new NeverfadePos.Api.Entities.UserSessionRevocation
        {
            TenantId = currentUser.TenantId.Value,
            UserId = id,
            IdempotencyKey = key,
        });
        await RevokeSharedSessionsAsync(id, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new TenantApiException(409, "IDEMPOTENCY_KEY_REUSED",
                "Kunci sudah diproses. Periksa status sesi sebelum mengulang.");
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new TenantApiException(409, "REVOCATION_CONFLICT",
                "Versi sesi berubah. Periksa ulang status sebelum mengulangi request.");
        }
    }

    private async Task RevokeSharedSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var sessions = await db.SharedPosSessions
            .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var session in sessions)
            session.RevokedAtUtc = now;
    }

    private static string NormalizeRole(string role)
    {
        var normalized = role.Trim().ToLowerInvariant();
        if (!AllowedRoles.Contains(normalized, StringComparer.Ordinal))
            throw new InvalidOperationException("Role harus owner, admin, kasir, dapur, atau laundry_operator.");
        return normalized;
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres &&
        postgres.SqlState == PostgresErrorCodes.UniqueViolation;

    private static UserDto MapToDto(UserEntity entity) => new()
    {
        Id = entity.Id,
        Nama = entity.Nama,
        Username = entity.Username,
        Role = entity.Role,
        Active = entity.Active,
        CreatedAt = entity.CreatedAt
    };

    private static System.Linq.Expressions.Expression<Func<UserEntity, UserDto>> MapToDto() =>
        x => new UserDto
        {
            Id = x.Id,
            Nama = x.Nama,
            Username = x.Username,
            Role = x.Role,
            Active = x.Active,
            CreatedAt = x.CreatedAt
        };
}

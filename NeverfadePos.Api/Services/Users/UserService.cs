using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.User;
using Npgsql;
using UserEntity = NeverfadePos.Api.Entities.User;

namespace NeverfadePos.Api.Services.Users;

public sealed class UserService(
    AppDbContext db,
    CurrentUser currentUser)
    : IUserService
{
    private static readonly string[] AllowedRoles =
    {
        "owner",
        "admin",
        "kasir"
    };

    public async Task<List<UserDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.Users
            .AsNoTracking()
            .OrderBy(x => x.Nama)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<UserDto> CreateAsync(
        CreateUserDto request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException();
        }

        var nama = request.Nama.Trim();
        var username = request.Username.Trim();
        var role = NormalizeRole(request.Role);

        if (await db.Users.AnyAsync(
            x => x.Username == username,
            cancellationToken))
        {
            throw new InvalidOperationException(
                "Username sudah digunakan.");
        }

        var entity = new UserEntity
        {
            TenantId = currentUser.TenantId.Value,
            Nama = nama,
            Username = username,
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    request.Password),
            Role = role,
            Active = true
        };

        db.Users.Add(entity);

        try
        {
            await db.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException(
                "Username sudah digunakan.");
        }

        return MapToDto(entity);
    }

    public async Task<UserDto> UpdateAsync(
        Guid id,
        UpdateUserDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Users
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "User tidak ditemukan.");

        var nama = request.Nama.Trim();
        var username = request.Username.Trim();
        var role = NormalizeRole(request.Role);

        if (await db.Users.AnyAsync(
            x =>
                x.Id != id &&
                x.Username == username,
            cancellationToken))
        {
            throw new InvalidOperationException(
                "Username sudah digunakan.");
        }

        entity.Nama = nama;
        entity.Username = username;
        entity.Role = role;
        entity.Active = request.Active;

        if (!string.IsNullOrWhiteSpace(
            request.Password))
        {
            entity.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    request.Password);
        }

        try
        {
            await db.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            throw new InvalidOperationException(
                "Username sudah digunakan.");
        }

        return MapToDto(entity);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException();
        }

        if (currentUser.UserId.Value == id)
        {
            throw new InvalidOperationException(
                "Akun yang sedang digunakan tidak dapat dihapus.");
        }

        var entity = await db.Users
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "User tidak ditemukan.");

        db.Users.Remove(entity);

        await db.SaveChangesAsync(
            cancellationToken);
    }

    private static string NormalizeRole(
        string role)
    {
        var normalized =
            role.Trim().ToLowerInvariant();

        if (!AllowedRoles.Contains(
            normalized,
            StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Role harus owner, admin, atau kasir.");
        }

        return normalized;
    }

    private static bool IsUniqueViolation(
        DbUpdateException exception)
    {
        return exception.InnerException
            is PostgresException postgres &&
            postgres.SqlState ==
            PostgresErrorCodes.UniqueViolation;
    }

    private static UserDto MapToDto(
        UserEntity entity)
    {
        return new UserDto
        {
            Id = entity.Id,
            Nama = entity.Nama,
            Username = entity.Username,
            Role = entity.Role,
            Active = entity.Active,
            CreatedAt = entity.CreatedAt
        };
    }

    private static System.Linq.Expressions.Expression<
        Func<UserEntity, UserDto>>
        MapToDto()
    {
        return x => new UserDto
        {
            Id = x.Id,
            Nama = x.Nama,
            Username = x.Username,
            Role = x.Role,
            Active = x.Active,
            CreatedAt = x.CreatedAt
        };
    }
}

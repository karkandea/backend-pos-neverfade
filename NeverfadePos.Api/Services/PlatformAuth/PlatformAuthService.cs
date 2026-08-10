using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.PlatformAuth;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.PlatformAuth;

public sealed class PlatformAuthService(
    AppDbContext db,
    IPlatformJwtService jwtService,
    PlatformCurrentUser currentUser)
    : IPlatformAuthService
{
    public async Task<PlatformLoginResponseDto> LoginAsync(
        PlatformLoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();

        var user = await db.PlatformUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Username == username,
                cancellationToken);

        if (user is null ||
            !BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash))
        {
            throw InvalidCredentials();
        }

        if (!user.Active)
        {
            throw InactiveUser();
        }

        return new PlatformLoginResponseDto
        {
            Token = jwtService.GenerateToken(user),
            User = Map(user)
        };
    }

    public async Task<PlatformUserDto> MeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.UserId.HasValue)
        {
            throw new PlatformApiException(
                StatusCodes.Status401Unauthorized,
                "PLATFORM_AUTHENTICATION_REQUIRED",
                "Autentikasi platform diperlukan.");
        }

        var user = await db.PlatformUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == currentUser.UserId.Value,
                cancellationToken);

        if (user is null || !user.Active)
        {
            throw InactiveUser();
        }

        return Map(user);
    }

    private static PlatformUserDto Map(
        PlatformUser user)
    {
        return new PlatformUserDto
        {
            Id = user.Id,
            Nama = user.Nama,
            Username = user.Username,
            Role = user.Role
        };
    }

    private static PlatformApiException
        InvalidCredentials()
    {
        return new PlatformApiException(
            StatusCodes.Status401Unauthorized,
            "PLATFORM_INVALID_CREDENTIALS",
            "Username atau password salah.");
    }

    private static PlatformApiException InactiveUser()
    {
        return new PlatformApiException(
            StatusCodes.Status403Forbidden,
            "PLATFORM_USER_INACTIVE",
            "Platform user tidak aktif.");
    }
}

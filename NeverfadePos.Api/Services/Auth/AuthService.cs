using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;

namespace NeverfadePos.Api.Services.Auth;

public sealed class AuthService(
    AppDbContext db,
    IJwtService jwtService,
    CurrentUser currentUser)
    : IAuthService
{
    public async Task<LoginResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Username == request.Username && x.Active,
                cancellationToken);

        if (user is null)
            throw new UnauthorizedAccessException("Username atau password salah.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Username atau password salah.");

        var token = jwtService.GenerateToken(user);

        return new LoginResponseDto
        {
            Token = token,
            User = new LoginUserDto
            {
                Id = user.Id,
                Nama = user.Nama,
                Username = user.Username,
                Role = user.Role
            }
        };
    }

    public async Task<MeResponseDto> MeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();

        var user = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == currentUser.UserId.Value,
                cancellationToken);

        if (user is null)
            throw new UnauthorizedAccessException();

        return new MeResponseDto
        {
            Id = user.Id,
            Nama = user.Nama,
            Username = user.Username,
            Role = user.Role
        };
    }
}

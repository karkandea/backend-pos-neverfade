using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DemoMode;
using NeverfadePos.Api.DTOs.Auth;

namespace NeverfadePos.Api.Services.Auth;

public sealed class AuthService(
    AppDbContext db,
    IJwtService jwtService,
    CurrentUser currentUser,
    ITrustedTenantExecutionScope trustedTenantExecutionScope,
    IConfiguration configuration)
    : IAuthService
{
    public async Task<LoginResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (configuration.GetValue<bool>("DemoMode:Enabled") &&
            string.Equals(
                request.Username,
                DemoModeDefaults.Username,
                StringComparison.Ordinal))
        {
            await DemoModeBootstrap.EnsureReadyAsync(
                db,
                trustedTenantExecutionScope,
                configuration,
                cancellationToken);
        }

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

        var tenantStatus = await db.Tenants
            .AsNoTracking()
            .Where(x => x.Id == user.TenantId)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantStatus == "suspended")
        {
            throw new PlatformApiException(
                StatusCodes.Status403Forbidden,
                "TENANT_SUSPENDED",
                "Tenant sedang ditangguhkan.");
        }

        if (tenantStatus is null)
            throw new UnauthorizedAccessException();

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

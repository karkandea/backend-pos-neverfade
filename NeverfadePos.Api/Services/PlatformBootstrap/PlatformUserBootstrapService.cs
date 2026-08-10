using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.PlatformBootstrap;

public sealed class PlatformUserBootstrapService(
    AppDbContext db,
    IConfiguration configuration,
    ILogger<PlatformUserBootstrapService> logger)
{
    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>(
            "PlatformBootstrap:Enabled"))
        {
            return;
        }

        if (await db.PlatformUsers.AnyAsync(
            cancellationToken))
        {
            logger.LogWarning(
                "Platform bootstrap skipped because a platform user already exists.");
            return;
        }

        var nama = Require(
            "PlatformBootstrap:Nama",
            200);
        var username = Require(
            "PlatformBootstrap:Username",
            100);
        var password = Require(
            "PlatformBootstrap:Password",
            100,
            trim: false);

        if (password.Length < 12)
        {
            throw new InvalidOperationException(
                "PlatformBootstrap:Password must contain at least 12 characters.");
        }

        db.PlatformUsers.Add(new PlatformUser
        {
            Nama = nama,
            Username = username,
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(password),
            Role = PlatformAuthConstants.SuperAdminRole,
            Active = true
        });

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Initial platform user bootstrap completed.");
    }

    private string Require(
        string key,
        int maxLength,
        bool trim = true)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} missing.");
        }

        var normalized = trim
            ? value.Trim()
            : value;

        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException(
                $"{key} exceeds {maxLength} characters.");
        }

        return normalized;
    }
}

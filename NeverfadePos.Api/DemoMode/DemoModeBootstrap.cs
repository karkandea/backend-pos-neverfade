using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Data.Seed;

namespace NeverfadePos.Api.DemoMode;

internal static class DemoModeBootstrap
{
    public static async Task EnsureReadyAsync(
        AppDbContext db,
        ITrustedTenantExecutionScope trustedTenantScope,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled"))
        {
            return;
        }

        if (string.Equals(
                configuration["Payments:Mode"],
                "Live",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "DemoMode cannot run while Payments:Mode is Live.");
        }

        var resetIntervalMinutes =
            configuration.GetValue<int?>("DemoMode:ResetIntervalMinutes") ?? 180;

        if (resetIntervalMinutes is < 30 or > 1440)
        {
            throw new InvalidOperationException(
                "DemoMode:ResetIntervalMinutes must be between 30 and 1440 minutes.");
        }

        await DemoSeedData.InitializeAsync(
            db,
            trustedTenantScope);

        using var tenantScope = trustedTenantScope.Begin(
            DemoModeDefaults.TenantId,
            "demo-credential-bootstrap");

        var demoUserExists = await db.Users
            .AnyAsync(
                x => x.Username == DemoModeDefaults.Username,
                cancellationToken);

        if (!demoUserExists)
        {
            throw new InvalidOperationException(
                "Demo user is missing after demo seed initialization.");
        }

        var changed = false;

        var seededHistory = await db.Transactions
            .Where(x => x.NoTrx.StartsWith("DEMO-"))
            .ToListAsync(cancellationToken);

        foreach (var transaction in seededHistory)
        {
            if (transaction.CreatedAt == transaction.Tanggal)
            {
                continue;
            }

            transaction.CreatedAt = transaction.Tanggal;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

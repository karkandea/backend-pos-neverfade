using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Data.Seed;

namespace NeverfadePos.Api.DemoMode;

internal static class DemoResetScheduler
{
    private static int _started;

    public static void Start(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled") ||
            Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        var intervalMinutes =
            configuration.GetValue<int?>("DemoMode:ResetIntervalMinutes") ?? 180;
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        var logger = loggerFactory.CreateLogger("DemoResetScheduler");

        _ = Task.Run(
            () => RunAsync(
                scopeFactory,
                interval,
                logger,
                applicationLifetime.ApplicationStopping),
            applicationLifetime.ApplicationStopping);
    }

    private static async Task RunAsync(
        IServiceScopeFactory scopeFactory,
        TimeSpan interval,
        ILogger logger,
        CancellationToken stoppingToken)
    {
        var lastBaselineReset = DateTime.UtcNow;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                await DemoSessionLifecycle.CreationLock.WaitAsync(stoppingToken);
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var removed = await DemoSessionLifecycle.CleanupExpiredAsync(
                        db,
                        scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>(),
                        stoppingToken);
                    if (removed > 0)
                        logger.LogInformation("Cleaned up {Count} expired demo visitor tenants.", removed);

                    if (DateTime.UtcNow - lastBaselineReset >= interval)
                    {
                        await ResetAsync(scopeFactory, stoppingToken);
                        lastBaselineReset = DateTime.UtcNow;
                        logger.LogInformation("Reserved demo baseline reset completed.");
                    }
                }
                finally
                {
                    DemoSessionLifecycle.CreationLock.Release();
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "NeverFade demo maintenance failed; next check retries.");
            }
        }
    }

    internal static async Task ResetAsync(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trustedTenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>();

        if (!await DemoTenantIntegrity.IsIsolatedAsync(db, cancellationToken))
        {
            throw new InvalidOperationException(
                "Demo reset aborted because the database is not an isolated NeverFade multi-business demo database.");
        }

        // Analytics intentionally survives the three-hour demo data reset.
        // Delete old anonymous rows in small batches to bound storage without
        // retaining personal data or blocking live demo sessions.
        var expiredEvents = await db.DemoConversionEvents
            .Where(x => x.CreatedAt < DateTime.UtcNow.AddDays(-90))
            .OrderBy(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        if (expiredEvents.Count > 0)
        {
            db.DemoConversionEvents.RemoveRange(expiredEvents);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Visitor tenants are never globally reset while their session is active.
        // Expired visitor tenants are deleted separately with all their data.
        await DemoSessionLifecycle.CleanupExpiredAsync(db, trustedTenantScope, cancellationToken);

        foreach (var profile in DemoModeDefaults.Profiles)
        {
            using var tenantScope = trustedTenantScope.Begin(
                profile.TenantId,
                $"public-demo-periodic-reset:{profile.Key}");

            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            await DemoSeedData.ResetMutableStateAsync(
                db,
                profile,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }
}

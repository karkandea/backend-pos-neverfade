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
        while (!stoppingToken.IsCancellationRequested)
        {
            // Initial reset is awaited during application bootstrap. Do not
            // race the first user requests with a second background reset.
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await ResetAsync(scopeFactory, stoppingToken);
                logger.LogInformation(
                    "NeverFade public multi-business demo state reset completed.");
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "NeverFade public demo reset failed; the next scheduled reset will retry.");
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

        var tenantIds = await db.Tenants
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (!DemoModeDefaults.IsExactDemoTenantSet(tenantIds))
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

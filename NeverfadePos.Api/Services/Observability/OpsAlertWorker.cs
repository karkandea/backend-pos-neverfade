using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.Services.Observability;

// Opt-in: configured OFF by default, no payment/provider side effects.
public sealed class OpsAlertWorker(
    IServiceScopeFactory scopes,
    ILogger<OpsAlertWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ScanAllAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ScanAllAsync(stoppingToken);
    }

    private async Task ScanAllAsync(CancellationToken ct)
    {
        try
        {
            List<Guid> tenants;
            using (var scope = scopes.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                tenants = await db.Tenants.AsNoTracking()
                    .Where(x => x.Status == "active").Select(x => x.Id)
                    .ToListAsync(ct);
            }
            foreach (var tenantId in tenants)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var scope = scopes.CreateScope();
                    using var tenantScope = scope.ServiceProvider
                        .GetRequiredService<ITrustedTenantExecutionScope>()
                        .Begin(tenantId, "OPS_ALERT_SCAN");
                    var scanner = scope.ServiceProvider.GetRequiredService<OpsAlertScanner>();
                    var added = await scanner.ScanAsync(DateTime.UtcNow, ct);
                    if (added > 0)
                        logger.LogWarning("Persisted {Count} alerts for tenant {TenantId}",
                            added, tenantId);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ops scanner failed for tenant {TenantId}", tenantId);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ops scanner cycle failed");
        }
    }
}

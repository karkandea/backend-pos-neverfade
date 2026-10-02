using Microsoft.EntityFrameworkCore;
using Npgsql;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Observability;

// Persists scoped alerts; never retries a payment or sends a notification.
public sealed class OpsAlertScanner(AppDbContext db, ITenantExecutionContext context)
{
    public async Task<int> ScanAsync(DateTime now, CancellationToken ct = default)
    {
        if (context.Mode != TenantExecutionMode.TrustedSystem ||
            !context.TargetTenantId.HasValue)
            throw new InvalidOperationException("Trusted tenant scope required.");

        var cutoff = now.AddMinutes(-5);
        var payments = await db.Payments.AsNoTracking()
            .Where(x => (x.Status == PaymentConstants.StatusCreating && x.CreatedAt < cutoff) ||
                (x.Status == PaymentConstants.StatusPending &&
                 x.ExpiresAt.HasValue && x.ExpiresAt < now))
            .OrderByDescending(x => x.CreatedAt).Select(x => x.Id)
            .Take(500).ToListAsync(ct);
        var webhooks = await db.PaymentWebhookEvents.AsNoTracking()
            .Where(x => x.ProcessingStatus != "processed")
            .OrderByDescending(x => x.CreatedAt).Select(x => x.Id)
            .Take(500).ToListAsync(ct);
        var jobs = await db.Jobs.AsNoTracking()
            .Where(x => (x.State == "queued" || x.State == "running") &&
                 x.UpdatedAt < cutoff)
            .OrderByDescending(x => x.CreatedAt).Select(x => x.Id)
            .Take(500).ToListAsync(ct);

        var count = 0;
        foreach (var id in payments)
            if (await InsertOnceAsync("payment_stalled", id, ct)) count++;
        foreach (var id in webhooks)
            if (await InsertOnceAsync("webhook_unprocessed", id, ct)) count++;
        foreach (var id in jobs)
            if (await InsertOnceAsync("job_stalled", id, ct)) count++;
        return count;
    }

    private async Task<bool> InsertOnceAsync(string kind, Guid sourceId, CancellationToken ct)
    {
        if (await db.OpsAlerts.AsNoTracking()
            .AnyAsync(x => x.SourceKind == kind && x.SourceId == sourceId, ct))
            return false;
        var alert = new OpsAlert
        {
            TenantId = context.TargetTenantId!.Value,
            SourceKind = kind,
            SourceId = sourceId,
            CorrelationId = $"ops:{kind}:{sourceId:N}"
        };
        db.OpsAlerts.Add(alert);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException pg &&
                  pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            // Unique index fences concurrent scans and worker restarts.
            db.Entry(alert).State = EntityState.Detached;
            return false;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data.Seed;

namespace NeverfadePos.Api.DemoMode;

internal static class DemoSessionLifecycle
{
    internal const string CookieName = "nf_demo_visitor";
    internal const int MaxActiveBusinessSessions = 500;
    internal static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    // One demo container is deployed. Serialization avoids duplicate tenant creation
    // when the same browser issues simultaneous session requests. The DB unique
    // constraint remains the cross-process integrity guard.
    internal static readonly SemaphoreSlim CreationLock = new(1, 1);

    internal static async Task<int> CleanupExpiredAsync(
        AppDbContext db,
        ITrustedTenantExecutionScope trustedScope,
        CancellationToken cancellationToken = default)
    {
        if (!await DemoTenantIntegrity.IsIsolatedAsync(db, cancellationToken))
            throw new InvalidOperationException("Demo cleanup requires an isolated demo database.");

        var expired = await db.DemoVisitorSessions.AsNoTracking()
            .Where(x => x.ExpiresAt <= DateTime.UtcNow)
            .OrderBy(x => x.ExpiresAt)
            .Take(50)
            .Select(x => x.TenantId)
            .ToListAsync(cancellationToken);

        foreach (var tenantId in expired)
        {
            if (DemoModeDefaults.IsDemoTenant(tenantId))
                throw new InvalidOperationException("Refusing to remove a reserved demo tenant.");

            // Remove workflow children before the tenant, because several tenant
            // FKs intentionally use Restrict (e.g. order -> customer/user/table).
            using (trustedScope.Begin(tenantId, "expire-public-demo-visitor"))
            {
                await DemoSeedData.RemoveMutableDataAsync(db, cancellationToken);
                db.ProductPrices.RemoveRange(await db.ProductPrices.ToListAsync(cancellationToken));
                db.ProductVariants.RemoveRange(await db.ProductVariants.ToListAsync(cancellationToken));
                await db.SaveChangesAsync(cancellationToken);
            }

            var visitor = await db.DemoVisitorSessions.SingleAsync(
                x => x.TenantId == tenantId, cancellationToken);
            db.DemoVisitorSessions.Remove(visitor);
            var tenant = await db.Tenants.SingleAsync(x => x.Id == tenantId, cancellationToken);
            db.Tenants.Remove(tenant);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        return expired.Count;
    }
}

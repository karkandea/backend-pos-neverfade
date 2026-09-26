using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.DemoMode;

internal static class DemoTenantIntegrity
{
    public static async Task<bool> IsIsolatedAsync(
        AppDbContext db, CancellationToken cancellationToken = default)
    {
        var tenants = await db.Tenants.AsNoTracking()
            .Select(x => new { x.Id, x.BusinessType })
            .ToListAsync(cancellationToken);
        var visitors = await db.DemoVisitorSessions.AsNoTracking()
            .Select(x => new { x.TenantId, x.BusinessType })
            .ToListAsync(cancellationToken);
        if (!DemoModeDefaults.Profiles.All(p =>
            tenants.Count(t => t.Id == p.TenantId && t.BusinessType == p.BusinessType) == 1))
            return false;

        var baseIds = DemoModeDefaults.Profiles.Select(x => x.TenantId).ToHashSet();
        var extraTenants = tenants.Where(x => !baseIds.Contains(x.Id)).ToList();
        if (visitors.Count != extraTenants.Count ||
            visitors.Select(x => x.TenantId).Distinct().Count() != visitors.Count)
            return false;

        return extraTenants.All(t => visitors.Any(v =>
            v.TenantId == t.Id && v.BusinessType == t.BusinessType &&
            DemoModeDefaults.Profiles.Any(p => p.Key == v.BusinessType)));
    }
}

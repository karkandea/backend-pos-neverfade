using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.Services.Finance;

internal static class FinanceTenantLock
{
    public static async Task AcquireAsync(
        AppDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return;
        }

        _ = await db.Tenants
            .FromSqlInterpolated(
                $"SELECT * FROM \"tenants\" WHERE \"Id\" = {tenantId} FOR UPDATE")
            .AsNoTracking()
            .SingleAsync(cancellationToken);
    }
}

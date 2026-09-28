using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.Services.Sales;

/// <summary>
/// PostgreSQL transaction-scoped tenant sale lock. Legacy cash, v2 cash,
/// and authenticated QRIS-paid finalization use the same advisory key before
/// writing stock. Real concurrent PostgreSQL cash-vs-QRIS execution remains a
/// separate mandatory release gate; common source calls do not prove it passed.
/// </summary>
internal static class TenantStockLock
{
    public static async Task AcquireAsync(AppDbContext db, Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational()) return;
        if (!string.Equals(db.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
            throw new InvalidOperationException("Sale stock lock requires PostgreSQL.");
        var active = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Sale stock lock requires an active database transaction.");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = active.GetDbTransaction();
        command.CommandText = "SELECT pg_advisory_xact_lock(hashtext(@tenant), 371)";
        var tenant = command.CreateParameter();
        tenant.ParameterName = "tenant";
        tenant.Value = tenantId.ToString("N");
        command.Parameters.Add(tenant);
        await command.ExecuteScalarAsync(cancellationToken);
    }
}

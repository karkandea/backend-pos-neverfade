using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.Services.Sales;

/// <summary>
/// PostgreSQL transaction-scoped tenant sale lock. Both legacy cash and v2 cash
/// writers use the same advisory key before resolving stock or generating a sale
/// number. The shared QRIS-paid finalizer must join this protocol in S2 before GA.
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

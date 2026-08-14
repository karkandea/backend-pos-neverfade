using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Finance;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Finance;

internal sealed class PlatformWithdrawalService(
    AppDbContext db,
    PlatformCurrentUser platformCurrentUser,
    ITrustedTenantExecutionScope trustedTenantScope)
    : IPlatformWithdrawalService
{
    public async Task<IReadOnlyList<PlatformWithdrawalDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        RequirePlatformUser();

        var routes = await db.WithdrawalRoutes
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var tenantIds = routes
            .Select(x => x.TenantId)
            .Distinct()
            .ToList();
        var tenantNames = await db.Tenants
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.NamaToko,
                cancellationToken);
        var result = new List<PlatformWithdrawalDto>();

        foreach (var tenantRoutes in routes.GroupBy(x => x.TenantId))
        {
            using var tenantScope = trustedTenantScope.Begin(
                tenantRoutes.Key,
                "platform-list-withdrawals");
            var withdrawalIds = tenantRoutes
                .Select(x => x.WithdrawalRequestId)
                .ToList();
            var withdrawals = await db.WithdrawalRequests
                .AsNoTracking()
                .Include(x => x.RequestedByUser)
                .Where(x => withdrawalIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            result.AddRange(withdrawals.Select(withdrawal => Map(
                withdrawal,
                tenantNames.GetValueOrDefault(tenantRoutes.Key, string.Empty))));
        }

        return result
            .OrderByDescending(x => x.RequestedAt)
            .ToList();
    }

    public Task<PlatformWithdrawalDto> MarkPaidAsync(
        Guid withdrawalId,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(
            withdrawalId,
            WithdrawalConstants.StatusPaid,
            cancellationToken);

    public Task<PlatformWithdrawalDto> RejectAsync(
        Guid withdrawalId,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(
            withdrawalId,
            WithdrawalConstants.StatusRejected,
            cancellationToken);

    private async Task<PlatformWithdrawalDto> ChangeStatusAsync(
        Guid withdrawalId,
        string targetStatus,
        CancellationToken cancellationToken)
    {
        var actorId = RequirePlatformUser();
        var route = await db.WithdrawalRoutes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.WithdrawalRequestId == withdrawalId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Permintaan pencairan tidak ditemukan.");
        var tenantName = await db.Tenants
            .AsNoTracking()
            .Where(x => x.Id == route.TenantId)
            .Select(x => x.NamaToko)
            .SingleAsync(cancellationToken);

        using var tenantScope = trustedTenantScope.Begin(
            route.TenantId,
            $"platform-withdrawal-{targetStatus}");
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await FinanceTenantLock.AcquireAsync(
            db,
            route.TenantId,
            cancellationToken);

        var withdrawal = await db.WithdrawalRequests
            .Include(x => x.RequestedByUser)
            .SingleAsync(x => x.Id == withdrawalId, cancellationToken);

        if (withdrawal.Status == targetStatus)
        {
            return Map(withdrawal, tenantName);
        }

        if (withdrawal.Status != WithdrawalConstants.StatusRequested)
        {
            throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "WITHDRAWAL_INVALID_STATE",
                "Status permintaan pencairan tidak dapat diubah.");
        }

        var processedAt = DateTime.UtcNow;
        withdrawal.Status = targetStatus;
        withdrawal.ProcessedByPlatformUserId = actorId;
        withdrawal.ProcessedAt = processedAt;
        withdrawal.UpdatedAt = processedAt;

        if (targetStatus == WithdrawalConstants.StatusPaid)
        {
            db.PaymentLedgerEntries.Add(new PaymentLedgerEntry
            {
                TenantId = route.TenantId,
                WithdrawalRequestId = withdrawal.Id,
                EntryType = PaymentConstants.LedgerWithdrawalDebit,
                Amount = withdrawal.Amount,
                Currency = PaymentConstants.CurrencyIdr
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Map(withdrawal, tenantName);
    }

    private Guid RequirePlatformUser()
    {
        if (!platformCurrentUser.UserId.HasValue ||
            platformCurrentUser.Scope !=
                PlatformAuthConstants.PlatformScope ||
            platformCurrentUser.Role !=
                PlatformAuthConstants.SuperAdminRole)
        {
            throw new UnauthorizedAccessException();
        }

        return platformCurrentUser.UserId.Value;
    }

    private static PlatformWithdrawalDto Map(
        WithdrawalRequest withdrawal,
        string tenantName) => new()
    {
        Id = withdrawal.Id,
        TenantId = withdrawal.TenantId,
        TenantName = tenantName,
        Amount = withdrawal.Amount,
        Status = withdrawal.Status,
        RequestedByUserId = withdrawal.RequestedByUserId,
        RequestedByName = withdrawal.RequestedByUser?.Nama ?? string.Empty,
        RequestedByUsername =
            withdrawal.RequestedByUser?.Username ?? string.Empty,
        RequestedAt = withdrawal.CreatedAt,
        ProcessedAt = withdrawal.ProcessedAt
    };
}

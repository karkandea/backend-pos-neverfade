using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Finance;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Finance;

internal sealed class TenantFinanceService(
    AppDbContext db,
    CurrentUser currentUser)
    : ITenantFinanceService
{
    public async Task<FinanceSummaryDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        RequireTenantOwner();
        return await CalculateSummaryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WithdrawalDto>> GetWithdrawalsAsync(
        CancellationToken cancellationToken = default)
    {
        RequireTenantOwner();

        return await db.WithdrawalRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new WithdrawalDto
            {
                Id = x.Id,
                Amount = x.Amount,
                Status = x.Status,
                RequestedAt = x.CreatedAt,
                ProcessedAt = x.ProcessedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinanceMovementDto>> GetMovementsAsync(
        CancellationToken cancellationToken = default)
    {
        RequireTenantOwner();

        var credits = await db.PaymentLedgerEntries
            .AsNoTracking()
            .Where(x =>
                x.EntryType == PaymentConstants.LedgerPaymentCredit)
            .Select(x => new FinanceMovementDto
            {
                Id = x.Id,
                Type = "qris_credit",
                Status = "paid",
                Amount = x.Amount,
                Timestamp = x.CreatedAt,
                Reference = x.ProviderReference ?? string.Empty,
                PaymentId = x.PaymentId,
                TransactionId = x.TransactionId
            })
            .ToListAsync(cancellationToken);

        var withdrawals = await db.WithdrawalRequests
            .AsNoTracking()
            .Select(x => new FinanceMovementDto
            {
                Id = x.Id,
                Type = "withdrawal",
                Status = x.Status,
                Amount = x.Amount,
                Timestamp = x.ProcessedAt ?? x.CreatedAt,
                Reference = x.Id.ToString(),
                WithdrawalId = x.Id
            })
            .ToListAsync(cancellationToken);

        return credits
            .Concat(withdrawals)
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .ToList();
    }

    public async Task<WithdrawalDto> CreateWithdrawalAsync(
        CreateWithdrawalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireTenantOwner();
        var amount = Money(request.Amount);

        if (amount <= 0)
        {
            throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "WITHDRAWAL_INVALID_AMOUNT",
                "Jumlah pencairan harus lebih dari nol.");
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await FinanceTenantLock.AcquireAsync(
            db,
            tenantId,
            cancellationToken);

        var summary = await CalculateSummaryAsync(cancellationToken);
        if (amount > summary.AvailableBalance)
        {
            throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "WITHDRAWAL_INSUFFICIENT_BALANCE",
                "Saldo tersedia tidak mencukupi untuk pencairan ini.");
        }

        var withdrawal = new WithdrawalRequest
        {
            TenantId = tenantId,
            Amount = amount,
            Status = WithdrawalConstants.StatusRequested,
            RequestedByUserId = userId
        };

        db.WithdrawalRequests.Add(withdrawal);
        db.WithdrawalRoutes.Add(new WithdrawalRoute
        {
            TenantId = tenantId,
            WithdrawalRequestId = withdrawal.Id
        });

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Map(withdrawal);
    }

    private async Task<FinanceSummaryDto> CalculateSummaryAsync(
        CancellationToken cancellationToken)
    {
        var income = await db.PaymentLedgerEntries
            .Where(x =>
                x.EntryType == PaymentConstants.LedgerPaymentCredit)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var withdrawn = await db.PaymentLedgerEntries
            .Where(x =>
                x.EntryType == PaymentConstants.LedgerWithdrawalDebit)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var pending = await db.WithdrawalRequests
            .Where(x =>
                x.Status == WithdrawalConstants.StatusRequested)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        return new FinanceSummaryDto
        {
            TotalSuccessfulNonCashIncome = income,
            TotalWithdrawn = withdrawn,
            PendingWithdrawalAmount = pending,
            AvailableBalance = income - withdrawn - pending
        };
    }

    private (Guid TenantId, Guid UserId) RequireTenantOwner()
    {
        if (!currentUser.TenantId.HasValue ||
            !currentUser.UserId.HasValue ||
            currentUser.Role != "owner")
        {
            throw new UnauthorizedAccessException();
        }

        return (
            currentUser.TenantId.Value,
            currentUser.UserId.Value);
    }

    private static WithdrawalDto Map(WithdrawalRequest withdrawal) => new()
    {
        Id = withdrawal.Id,
        Amount = withdrawal.Amount,
        Status = withdrawal.Status,
        RequestedAt = withdrawal.CreatedAt,
        ProcessedAt = withdrawal.ProcessedAt
    };

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

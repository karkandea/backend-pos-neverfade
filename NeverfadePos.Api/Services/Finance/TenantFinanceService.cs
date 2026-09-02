using System.Text.Json;
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
    public async Task<FinanceSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        RequireTenantOwner();
        return await CalculateSummaryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WithdrawalDto>> GetWithdrawalsAsync(CancellationToken cancellationToken = default)
    {
        RequireTenantOwner();
        return await db.WithdrawalRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => MapProjection(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinanceMovementDto>> GetMovementsAsync(CancellationToken cancellationToken = default)
    {
        RequireTenantOwner();

        var credits = await db.PaymentLedgerEntries
            .AsNoTracking()
            .Where(x => x.EntryType == PaymentConstants.LedgerPaymentCredit)
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
                Timestamp = x.ProcessedAt ?? x.CancelledAt ?? x.UpdatedAt,
                Reference = x.TransferReference ?? x.Id.ToString(),
                WithdrawalId = x.Id
            })
            .ToListAsync(cancellationToken);

        return credits.Concat(withdrawals)
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .ToList();
    }

    public Task<WithdrawalSettingsDto> GetWithdrawalSettingsAsync(CancellationToken cancellationToken = default)
    {
        RequireTenantOwner();
        return Task.FromResult(new WithdrawalSettingsDto
        {
            MinimumAmount = WithdrawalConstants.MinimumAmount,
            ProcessingEstimate = "Maksimal 1 hari kerja"
        });
    }

    public async Task<WithdrawalBankAccountDto?> GetBankAccountAsync(CancellationToken cancellationToken = default)
    {
        RequireTenantOwner();
        var account = await db.WithdrawalBankAccounts.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return account is null ? null : MapBank(account);
    }

    public async Task<WithdrawalBankAccountDto> PutBankAccountAsync(
        UpdateWithdrawalBankAccountRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireTenantOwner();
        var bankName = Required(request.BankName, 2, 100, "Nama bank tidak valid.");
        var holderName = Required(request.AccountHolderName, 2, 200, "Nama pemilik rekening tidak valid.");
        var accountNumber = NormalizeAccountNumber(request.AccountNumber);
        var now = DateTime.UtcNow;

        var account = await db.WithdrawalBankAccounts.SingleOrDefaultAsync(cancellationToken);
        if (account is null)
        {
            account = new WithdrawalBankAccount
            {
                TenantId = tenantId,
                BankName = bankName,
                AccountNumber = accountNumber,
                AccountHolderName = holderName,
                VerificationStatus = WithdrawalConstants.BankPending,
                UpdatedAt = now
            };
            db.WithdrawalBankAccounts.Add(account);
        }
        else
        {
            account.BankName = bankName;
            account.AccountNumber = accountNumber;
            account.AccountHolderName = holderName;
            account.VerificationStatus = WithdrawalConstants.BankPending;
            account.VerifiedAt = null;
            account.VerifiedByPlatformUserId = null;
            account.UpdatedAt = now;
        }

        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = userId,
            EventType = "WITHDRAWAL_BANK_ACCOUNT_UPDATED",
            Metadata = JsonSerializer.Serialize(new
            {
                bankName,
                last4 = Last4(accountNumber),
                verificationStatus = WithdrawalConstants.BankPending
            })
        });

        await db.SaveChangesAsync(cancellationToken);
        return MapBank(account);
    }

    public async Task<WithdrawalDto> CreateWithdrawalAsync(
        CreateWithdrawalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireTenantOwner();
        var amount = Money(request.Amount);

        if (amount < WithdrawalConstants.MinimumAmount)
        {
            throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "WITHDRAWAL_BELOW_MINIMUM",
                $"Jumlah pencairan minimum adalah Rp{WithdrawalConstants.MinimumAmount:N0}.");
        }

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await FinanceTenantLock.AcquireAsync(db, tenantId, cancellationToken);

        var destination = await db.WithdrawalBankAccounts.SingleOrDefaultAsync(cancellationToken)
            ?? throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "WITHDRAWAL_BANK_ACCOUNT_REQUIRED",
                "Simpan rekening tujuan sebelum mengajukan penarikan.");

        if (destination.VerificationStatus != WithdrawalConstants.BankVerified)
        {
            throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "WITHDRAWAL_BANK_ACCOUNT_NOT_VERIFIED",
                "Rekening tujuan belum terverifikasi.");
        }

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
            RequestedByUserId = userId,
            DestinationBankName = destination.BankName,
            DestinationAccountLast4 = Last4(destination.AccountNumber),
            DestinationAccountHolderName = destination.AccountHolderName,
            UpdatedAt = DateTime.UtcNow
        };

        db.WithdrawalRequests.Add(withdrawal);
        db.WithdrawalRoutes.Add(new WithdrawalRoute
        {
            TenantId = tenantId,
            WithdrawalRequestId = withdrawal.Id
        });

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Map(withdrawal);
    }

    public async Task<WithdrawalDto> CancelWithdrawalAsync(
        Guid withdrawalId,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireTenantOwner();
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await FinanceTenantLock.AcquireAsync(db, tenantId, cancellationToken);
        var withdrawal = await db.WithdrawalRequests.SingleOrDefaultAsync(
            x => x.Id == withdrawalId,
            cancellationToken) ?? throw new KeyNotFoundException("Permintaan pencairan tidak ditemukan.");

        if (withdrawal.Status == WithdrawalConstants.StatusCancelled)
            return Map(withdrawal);

        if (withdrawal.Status != WithdrawalConstants.StatusRequested)
        {
            throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "WITHDRAWAL_INVALID_STATE",
                "Penarikan hanya dapat dibatalkan sebelum mulai diproses.");
        }

        var now = DateTime.UtcNow;
        withdrawal.Status = WithdrawalConstants.StatusCancelled;
        withdrawal.CancelledAt = now;
        withdrawal.UpdatedAt = now;
        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = userId,
            EventType = "WITHDRAWAL_CANCELLED",
            Metadata = JsonSerializer.Serialize(new { withdrawalId, amount = withdrawal.Amount })
        });

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return Map(withdrawal);
    }

    private async Task<FinanceSummaryDto> CalculateSummaryAsync(CancellationToken cancellationToken)
    {
        var income = await db.PaymentLedgerEntries
            .Where(x => x.EntryType == PaymentConstants.LedgerPaymentCredit)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var withdrawn = await db.PaymentLedgerEntries
            .Where(x => x.EntryType == PaymentConstants.LedgerWithdrawalDebit)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var held = await db.WithdrawalRequests
            .Where(x => x.Status == WithdrawalConstants.StatusRequested ||
                        x.Status == WithdrawalConstants.StatusProcessing)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        return new FinanceSummaryDto
        {
            TotalSuccessfulNonCashIncome = income,
            TotalWithdrawn = withdrawn,
            PendingWithdrawalAmount = held,
            AvailableBalance = income - withdrawn - held
        };
    }

    private (Guid TenantId, Guid UserId) RequireTenantOwner()
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue || currentUser.Role != "owner")
            throw new UnauthorizedAccessException();
        return (currentUser.TenantId.Value, currentUser.UserId.Value);
    }

    private static WithdrawalBankAccountDto MapBank(WithdrawalBankAccount account) => new()
    {
        BankName = account.BankName,
        MaskedAccountNumber = $"•••• {Last4(account.AccountNumber)}",
        AccountHolderName = account.AccountHolderName,
        VerificationStatus = account.VerificationStatus,
        UpdatedAt = account.UpdatedAt,
        VerifiedAt = account.VerifiedAt
    };

    private static WithdrawalDto Map(WithdrawalRequest x) => new()
    {
        Id = x.Id,
        Amount = x.Amount,
        Status = x.Status,
        DestinationBankName = x.DestinationBankName,
        DestinationAccountMask = $"•••• {x.DestinationAccountLast4}",
        DestinationAccountHolderName = x.DestinationAccountHolderName,
        TransferReference = x.TransferReference,
        RejectionReason = x.RejectionReason,
        RequestedAt = x.CreatedAt,
        ProcessingStartedAt = x.ProcessingStartedAt,
        ProcessedAt = x.ProcessedAt,
        CancelledAt = x.CancelledAt
    };

    private static WithdrawalDto MapProjection(WithdrawalRequest x) => Map(x);

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Last4(string value) => value.Length <= 4 ? value : value[^4..];

    private static string NormalizeAccountNumber(string value)
    {
        var normalized = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (normalized.Length is < 6 or > 30)
            throw new PaymentApiException(StatusCodes.Status400BadRequest, "WITHDRAWAL_BANK_ACCOUNT_INVALID", "Nomor rekening tidak valid.");
        return normalized;
    }

    private static string Required(string? value, int min, int max, string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < min || normalized.Length > max || normalized.Any(char.IsControl))
            throw new PaymentApiException(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", message);
        return normalized;
    }
}

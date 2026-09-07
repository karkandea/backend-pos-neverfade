using System.Text.Json;
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
        string? status,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformUser();
        var normalizedStatus = NormalizeWithdrawalStatusFilter(status);

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

            var query = db.WithdrawalRequests
                .AsNoTracking()
                .Include(x => x.RequestedByUser)
                .Where(x => withdrawalIds.Contains(x.Id));

            if (normalizedStatus is not null)
            {
                query = query.Where(x => x.Status == normalizedStatus);
            }

            var withdrawals = await query.ToListAsync(cancellationToken);

            result.AddRange(withdrawals.Select(withdrawal => Map(
                withdrawal,
                tenantNames.GetValueOrDefault(
                    tenantRoutes.Key,
                    string.Empty))));
        }

        return result
            .OrderByDescending(x => x.RequestedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<PlatformWithdrawalBankAccountDto>>
        GetBankAccountsAsync(
            string? status,
            CancellationToken cancellationToken = default)
    {
        RequirePlatformUser();
        var normalizedStatus = NormalizeBankStatusFilter(status);

        var tenants = await db.Tenants
            .AsNoTracking()
            .OrderBy(x => x.NamaToko)
            .Select(x => new
            {
                x.Id,
                x.NamaToko
            })
            .ToListAsync(cancellationToken);

        var result = new List<PlatformWithdrawalBankAccountDto>();

        foreach (var tenant in tenants)
        {
            using var tenantScope = trustedTenantScope.Begin(
                tenant.Id,
                "platform-list-withdrawal-bank-accounts");

            var account = await db.WithdrawalBankAccounts
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            if (account is null)
            {
                continue;
            }

            if (normalizedStatus is not null &&
                account.VerificationStatus != normalizedStatus)
            {
                continue;
            }

            result.Add(MapBank(account, tenant.NamaToko));
        }

        return result;
    }

    public async Task<PlatformWithdrawalBankAccountDto>
        ReviewBankAccountAsync(
            Guid tenantId,
            VerifyWithdrawalBankAccountRequestDto request,
            CancellationToken cancellationToken = default)
    {
        var actorId = RequirePlatformUser();
        var reason = CleanOptional(request.Reason, 500);

        if (!request.Verified && string.IsNullOrWhiteSpace(reason))
        {
            throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "WITHDRAWAL_BANK_REJECTION_REASON_REQUIRED",
                "Alasan penolakan rekening wajib diisi.");
        }

        var tenantName = await db.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.NamaToko)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Tenant tidak ditemukan.");

        using var tenantScope = trustedTenantScope.Begin(
            tenantId,
            "platform-review-withdrawal-bank-account");

        var account = await db.WithdrawalBankAccounts
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException(
                "Rekening pencairan tenant belum tersedia.");

        var targetStatus = request.Verified
            ? WithdrawalConstants.BankVerified
            : WithdrawalConstants.BankRejected;

        if (account.VerificationStatus == targetStatus &&
            account.VerificationNote == reason)
        {
            return MapBank(account, tenantName);
        }

        var now = DateTime.UtcNow;
        account.VerificationStatus = targetStatus;
        account.VerificationNote = reason;
        account.VerifiedByPlatformUserId = actorId;
        account.VerifiedAt = request.Verified
            ? now
            : null;
        account.UpdatedAt = now;

        db.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorPlatformUserId = actorId,
            TenantId = tenantId,
            EventType = request.Verified
                ? "WITHDRAWAL_BANK_ACCOUNT_VERIFIED"
                : "WITHDRAWAL_BANK_ACCOUNT_REJECTED",
            Metadata = JsonSerializer.Serialize(new
            {
                bankName = account.BankName,
                last4 = Last4(account.AccountNumber),
                reason
            })
        });

        await db.SaveChangesAsync(cancellationToken);
        return MapBank(account, tenantName);
    }

    public async Task<PlatformWithdrawalDto> StartProcessingAsync(
        Guid withdrawalId,
        CancellationToken cancellationToken = default)
    {
        var actorId = RequirePlatformUser();
        var route = await GetRouteAsync(
            withdrawalId,
            cancellationToken);
        var tenantName = await GetTenantNameAsync(
            route.TenantId,
            cancellationToken);

        using var tenantScope = trustedTenantScope.Begin(
            route.TenantId,
            "platform-withdrawal-processing");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await FinanceTenantLock.AcquireAsync(
            db,
            route.TenantId,
            cancellationToken);

        var withdrawal = await GetWithdrawalAsync(
            withdrawalId,
            cancellationToken);

        if (withdrawal.Status ==
            WithdrawalConstants.StatusProcessing)
        {
            return Map(withdrawal, tenantName);
        }

        if (withdrawal.Status !=
            WithdrawalConstants.StatusRequested)
        {
            throw InvalidState(
                "Pencairan hanya dapat mulai diproses dari status menunggu.");
        }

        var now = DateTime.UtcNow;
        withdrawal.Status = WithdrawalConstants.StatusProcessing;
        withdrawal.ProcessedByPlatformUserId = actorId;
        withdrawal.ProcessingStartedAt = now;
        withdrawal.UpdatedAt = now;

        AddPlatformAudit(
            actorId,
            route.TenantId,
            "WITHDRAWAL_PROCESSING_STARTED",
            new
            {
                withdrawalId,
                amount = withdrawal.Amount
            });

        await db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Map(withdrawal, tenantName);
    }

    public async Task<PlatformWithdrawalDto> MarkPaidAsync(
        Guid withdrawalId,
        MarkWithdrawalPaidRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var actorId = RequirePlatformUser();

        if (!request.ConfirmedTransferred)
        {
            throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "WITHDRAWAL_TRANSFER_CONFIRMATION_REQUIRED",
                "Konfirmasi transfer wajib dicentang sebelum menandai pencairan sebagai dibayar.");
        }

        var transferReference = Required(
            request.TransferReference,
            2,
            200,
            "Referensi transfer wajib diisi.");
        var evidence = CleanOptional(
            request.EvidenceMetadata,
            2000);

        var route = await GetRouteAsync(
            withdrawalId,
            cancellationToken);
        var tenantName = await GetTenantNameAsync(
            route.TenantId,
            cancellationToken);

        using var tenantScope = trustedTenantScope.Begin(
            route.TenantId,
            "platform-withdrawal-paid");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await FinanceTenantLock.AcquireAsync(
            db,
            route.TenantId,
            cancellationToken);

        var withdrawal = await GetWithdrawalAsync(
            withdrawalId,
            cancellationToken);

        if (withdrawal.Status ==
            WithdrawalConstants.StatusPaid)
        {
            return Map(withdrawal, tenantName);
        }

        if (withdrawal.Status !=
            WithdrawalConstants.StatusProcessing)
        {
            throw InvalidState(
                "Pencairan harus berstatus diproses sebelum dapat ditandai dibayar.");
        }

        var now = DateTime.UtcNow;
        withdrawal.Status = WithdrawalConstants.StatusPaid;
        withdrawal.ProcessedByPlatformUserId = actorId;
        withdrawal.TransferReference = transferReference;
        withdrawal.EvidenceMetadata = evidence;
        withdrawal.ProcessedAt = now;
        withdrawal.UpdatedAt = now;

        db.PaymentLedgerEntries.Add(new PaymentLedgerEntry
        {
            TenantId = route.TenantId,
            WithdrawalRequestId = withdrawal.Id,
            EntryType = PaymentConstants.LedgerWithdrawalDebit,
            Amount = withdrawal.Amount,
            Currency = PaymentConstants.CurrencyIdr
        });

        AddPlatformAudit(
            actorId,
            route.TenantId,
            "WITHDRAWAL_PAID",
            new
            {
                withdrawalId,
                amount = withdrawal.Amount,
                transferReference
            });

        await db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Map(withdrawal, tenantName);
    }

    public async Task<PlatformWithdrawalDto> RejectAsync(
        Guid withdrawalId,
        RejectWithdrawalRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var actorId = RequirePlatformUser();
        var reason = Required(
            request.Reason,
            3,
            500,
            "Alasan penolakan wajib diisi.");

        var route = await GetRouteAsync(
            withdrawalId,
            cancellationToken);
        var tenantName = await GetTenantNameAsync(
            route.TenantId,
            cancellationToken);

        using var tenantScope = trustedTenantScope.Begin(
            route.TenantId,
            "platform-withdrawal-rejected");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        await FinanceTenantLock.AcquireAsync(
            db,
            route.TenantId,
            cancellationToken);

        var withdrawal = await GetWithdrawalAsync(
            withdrawalId,
            cancellationToken);

        if (withdrawal.Status ==
            WithdrawalConstants.StatusRejected)
        {
            return Map(withdrawal, tenantName);
        }

        if (withdrawal.Status is not (
            WithdrawalConstants.StatusRequested or
            WithdrawalConstants.StatusProcessing))
        {
            throw InvalidState(
                "Status pencairan tidak dapat ditolak.");
        }

        var now = DateTime.UtcNow;
        withdrawal.Status = WithdrawalConstants.StatusRejected;
        withdrawal.ProcessedByPlatformUserId = actorId;
        withdrawal.RejectionReason = reason;
        withdrawal.ProcessedAt = now;
        withdrawal.UpdatedAt = now;

        AddPlatformAudit(
            actorId,
            route.TenantId,
            "WITHDRAWAL_REJECTED",
            new
            {
                withdrawalId,
                amount = withdrawal.Amount,
                reason
            });

        await db.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Map(withdrawal, tenantName);
    }

    private async Task<WithdrawalRoute> GetRouteAsync(
        Guid withdrawalId,
        CancellationToken cancellationToken) =>
        await db.WithdrawalRoutes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.WithdrawalRequestId == withdrawalId,
                cancellationToken)
        ?? throw new KeyNotFoundException(
            "Permintaan pencairan tidak ditemukan.");

    private async Task<string> GetTenantNameAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await db.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.NamaToko)
            .SingleAsync(cancellationToken);

    private async Task<WithdrawalRequest> GetWithdrawalAsync(
        Guid withdrawalId,
        CancellationToken cancellationToken) =>
        await db.WithdrawalRequests
            .Include(x => x.RequestedByUser)
            .SingleAsync(
                x => x.Id == withdrawalId,
                cancellationToken);

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

    private void AddPlatformAudit(
        Guid actorId,
        Guid tenantId,
        string eventType,
        object metadata)
    {
        db.PlatformAuditEvents.Add(new PlatformAuditEvent
        {
            ActorPlatformUserId = actorId,
            TenantId = tenantId,
            EventType = eventType,
            Metadata = JsonSerializer.Serialize(metadata)
        });
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
        RequestedByName =
            withdrawal.RequestedByUser?.Nama ?? string.Empty,
        RequestedByUsername =
            withdrawal.RequestedByUser?.Username ?? string.Empty,
        DestinationBankName = withdrawal.DestinationBankName,
        DestinationAccountMask =
            string.IsNullOrEmpty(withdrawal.DestinationAccountNumber)
                ? string.Empty
                : $"•••• {Last4(withdrawal.DestinationAccountNumber)}",
        DestinationAccountNumber =
            withdrawal.DestinationAccountNumber,
        DestinationAccountHolderName =
            withdrawal.DestinationAccountHolderName,
        TransferReference = withdrawal.TransferReference,
        EvidenceMetadata = withdrawal.EvidenceMetadata,
        RejectionReason = withdrawal.RejectionReason,
        RequestedAt = withdrawal.CreatedAt,
        ProcessingStartedAt = withdrawal.ProcessingStartedAt,
        ProcessedAt = withdrawal.ProcessedAt,
        CancelledAt = withdrawal.CancelledAt
    };

    private static PlatformWithdrawalBankAccountDto MapBank(
        WithdrawalBankAccount account,
        string tenantName) => new()
    {
        TenantId = account.TenantId,
        TenantName = tenantName,
        BankName = account.BankName,
        AccountNumber = account.AccountNumber,
        MaskedAccountNumber =
            $"•••• {Last4(account.AccountNumber)}",
        AccountHolderName = account.AccountHolderName,
        VerificationStatus = account.VerificationStatus,
        VerificationNote = account.VerificationNote,
        UpdatedAt = account.UpdatedAt,
        VerifiedAt = account.VerifiedAt
    };

    private static string? NormalizeWithdrawalStatusFilter(
        string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var normalized = status.Trim().ToLowerInvariant();

        return normalized switch
        {
            WithdrawalConstants.StatusRequested => normalized,
            WithdrawalConstants.StatusProcessing => normalized,
            WithdrawalConstants.StatusPaid => normalized,
            WithdrawalConstants.StatusRejected => normalized,
            WithdrawalConstants.StatusCancelled => normalized,
            _ => throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "WITHDRAWAL_STATUS_INVALID",
                "Filter status pencairan tidak valid.")
        };
    }

    private static string? NormalizeBankStatusFilter(
        string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var normalized = status.Trim().ToLowerInvariant();

        return normalized switch
        {
            WithdrawalConstants.BankPending => normalized,
            WithdrawalConstants.BankVerified => normalized,
            WithdrawalConstants.BankRejected => normalized,
            _ => throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "WITHDRAWAL_BANK_STATUS_INVALID",
                "Filter status rekening tidak valid.")
        };
    }

    private static PaymentApiException InvalidState(
        string message) => new(
            StatusCodes.Status409Conflict,
            "WITHDRAWAL_INVALID_STATE",
            message);

    private static string Required(
        string? value,
        int min,
        int max,
        string message)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length < min ||
            normalized.Length > max ||
            normalized.Any(char.IsControl))
        {
            throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                message);
        }

        return normalized;
    }

    private static string? CleanOptional(
        string? value,
        int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > max ||
            normalized.Any(char.IsControl))
        {
            throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                "Nilai input tidak valid.");
        }

        return normalized;
    }

    private static string Last4(string value) =>
        value.Length <= 4
            ? value
            : value[^4..];
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.DTOs.Finance;
using NeverfadePos.Api.DTOs.PlatformAuth;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class TenantFinanceWithdrawalTests
{
    [Fact]
    public async Task FinanceSummary_HoldsRequestedAndProcessingWithdrawals()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");

        await SeedCreditAsync(factory, 300_000m);
        await SeedWithdrawalAsync(
            factory,
            30_000m,
            WithdrawalConstants.StatusPaid,
            includeDebit: true);
        await SeedWithdrawalAsync(
            factory,
            20_000m,
            WithdrawalConstants.StatusProcessing);

        var summary = await owner.GetFromJsonAsync<FinanceSummaryDto>(
            "/api/finance/summary");

        Assert.NotNull(summary);
        Assert.Equal(250_000m, summary.AvailableBalance);
        Assert.Equal(300_000m, summary.TotalSuccessfulNonCashIncome);
        Assert.Equal(30_000m, summary.TotalWithdrawn);
        Assert.Equal(20_000m, summary.PendingWithdrawalAmount);
    }

    [Fact]
    public async Task FinanceMovements_ExposeExpandedWithdrawalStates()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");

        await SeedCreditAsync(factory, 500_000m);
        await SeedWithdrawalAsync(
            factory,
            100_000m,
            WithdrawalConstants.StatusPaid,
            includeDebit: true);
        await SeedWithdrawalAsync(
            factory,
            100_000m,
            WithdrawalConstants.StatusRejected);
        await SeedWithdrawalAsync(
            factory,
            100_000m,
            WithdrawalConstants.StatusCancelled);
        await SeedWithdrawalAsync(
            factory,
            100_000m,
            WithdrawalConstants.StatusProcessing);

        var movements = await owner.GetFromJsonAsync<List<FinanceMovementDto>>(
            "/api/finance/movements");

        Assert.NotNull(movements);
        Assert.Equal(5, movements.Count);
        Assert.Contains(movements, x =>
            x.Type == "qris_credit" &&
            x.Status == "paid" &&
            x.Amount == 500_000m);
        Assert.Contains(movements, x =>
            x.Type == "withdrawal" &&
            x.Status == WithdrawalConstants.StatusProcessing);
        Assert.Contains(movements, x =>
            x.Type == "withdrawal" &&
            x.Status == WithdrawalConstants.StatusCancelled);
    }

    [Fact]
    public async Task OwnerBankAccount_IsMasked_AndPlatformCanVerifyIt()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        using var platform = await CreatePlatformClientAsync(factory);

        var savedResponse = await owner.PutAsJsonAsync(
            "/api/finance/bank-account",
            new
            {
                bankName = "BCA",
                accountNumber = "1234 5678 90",
                accountHolderName = "Owner NeverFade"
            });

        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);

        var saved = await savedResponse.Content
            .ReadFromJsonAsync<WithdrawalBankAccountDto>();

        Assert.NotNull(saved);
        Assert.Equal("•••• 7890", saved.MaskedAccountNumber);
        Assert.Equal(WithdrawalConstants.BankPending, saved.VerificationStatus);

        var pending = await platform.GetFromJsonAsync<
            List<PlatformWithdrawalBankAccountDto>>(
                "/api/platform/withdrawals/bank-accounts?status=pending");

        Assert.Single(pending!);
        Assert.Equal("1234567890", pending![0].AccountNumber);

        var review = await platform.PostAsJsonAsync(
            $"/api/platform/withdrawals/bank-accounts/{pending[0].TenantId}/review",
            new
            {
                verified = true,
                reason = "Nama dan rekening sesuai."
            });

        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        var ownerView = await owner.GetFromJsonAsync<WithdrawalBankAccountDto>(
            "/api/finance/bank-account");

        Assert.NotNull(ownerView);
        Assert.Equal(
            WithdrawalConstants.BankVerified,
            ownerView.VerificationStatus);
        Assert.Equal("•••• 7890", ownerView.MaskedAccountNumber);
    }

    [Fact]
    public async Task Withdrawal_RequiresVerifiedBankAccount_AndMinimumAmount()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        using var platform = await CreatePlatformClientAsync(factory);

        await SeedCreditAsync(factory, 500_000m);

        var noBank = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 100_000m });

        Assert.Equal(HttpStatusCode.Conflict, noBank.StatusCode);
        Assert.Contains(
            "WITHDRAWAL_BANK_ACCOUNT_REQUIRED",
            await noBank.Content.ReadAsStringAsync());

        await owner.PutAsJsonAsync(
            "/api/finance/bank-account",
            new
            {
                bankName = "BCA",
                accountNumber = "1234567890",
                accountHolderName = "Owner NeverFade"
            });

        var pendingBank = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 100_000m });

        Assert.Equal(HttpStatusCode.Conflict, pendingBank.StatusCode);
        Assert.Contains(
            "WITHDRAWAL_BANK_ACCOUNT_NOT_VERIFIED",
            await pendingBank.Content.ReadAsStringAsync());

        var bank = (await platform.GetFromJsonAsync<
            List<PlatformWithdrawalBankAccountDto>>(
                "/api/platform/withdrawals/bank-accounts"))!.Single();

        await platform.PostAsJsonAsync(
            $"/api/platform/withdrawals/bank-accounts/{bank.TenantId}/review",
            new { verified = true });

        var belowMinimum = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 99_999m });

        Assert.Equal(HttpStatusCode.BadRequest, belowMinimum.StatusCode);
        Assert.Contains(
            "WITHDRAWAL_BELOW_MINIMUM",
            await belowMinimum.Content.ReadAsStringAsync());

        var valid = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 100_000m });

        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        var withdrawal = await valid.Content
            .ReadFromJsonAsync<WithdrawalDto>();

        Assert.NotNull(withdrawal);
        Assert.Equal("BCA", withdrawal.DestinationBankName);
        Assert.Equal("•••• 7890", withdrawal.DestinationAccountMask);
        Assert.Equal(
            "Owner NeverFade",
            withdrawal.DestinationAccountHolderName);
    }

    [Fact]
    public async Task ChangingVerifiedBankAccount_ResetsVerification()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        using var platform = await CreatePlatformClientAsync(factory);

        await SeedVerifiedBankAccountAsync(factory);

        var updated = await owner.PutAsJsonAsync(
            "/api/finance/bank-account",
            new
            {
                bankName = "Mandiri",
                accountNumber = "9988776655",
                accountHolderName = "Owner NeverFade"
            });

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var body = await updated.Content
            .ReadFromJsonAsync<WithdrawalBankAccountDto>();

        Assert.NotNull(body);
        Assert.Equal(WithdrawalConstants.BankPending, body.VerificationStatus);
        Assert.Null(body.VerifiedAt);
        Assert.Null(body.VerificationNote);

        var pending = await platform.GetFromJsonAsync<
            List<PlatformWithdrawalBankAccountDto>>(
                "/api/platform/withdrawals/bank-accounts?status=pending");

        Assert.Single(pending!);
        Assert.Equal("9988776655", pending![0].AccountNumber);
    }

    [Fact]
    public async Task MultipleHeldWithdrawals_CannotExceedBalance()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");

        await SeedCreditAsync(factory, 250_000m);
        await SeedVerifiedBankAccountAsync(factory);

        var first = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 150_000m });

        var second = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 150_000m });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var summary = await owner.GetFromJsonAsync<FinanceSummaryDto>(
            "/api/finance/summary");

        Assert.NotNull(summary);
        Assert.Equal(100_000m, summary.AvailableBalance);
        Assert.Equal(150_000m, summary.PendingWithdrawalAmount);
    }

    [Fact]
    public async Task Owner_CanCancelRequestedWithdrawal_AndReleaseHold()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");

        await SeedCreditAsync(factory, 200_000m);
        await SeedVerifiedBankAccountAsync(factory);

        var withdrawal = await CreateWithdrawalAsync(owner, 100_000m);

        var held = await owner.GetFromJsonAsync<FinanceSummaryDto>(
            "/api/finance/summary");
        Assert.Equal(100_000m, held!.AvailableBalance);

        var cancelled = await owner.PostAsync(
            $"/api/finance/withdrawals/{withdrawal.Id}/cancel",
            null);

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        var body = await cancelled.Content
            .ReadFromJsonAsync<WithdrawalDto>();

        Assert.Equal(
            WithdrawalConstants.StatusCancelled,
            body!.Status);

        var released = await owner.GetFromJsonAsync<FinanceSummaryDto>(
            "/api/finance/summary");

        Assert.Equal(200_000m, released!.AvailableBalance);
        Assert.Equal(0m, released.PendingWithdrawalAmount);
    }

    [Fact]
    public async Task WithdrawalList_IsTenantIsolated()
    {
        await using var factory = new FinanceApiFactory();
        using var ownerA = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        var other = await SeedOtherTenantAsync(factory);
        using var ownerB = await CreateTenantClientAsync(
            factory,
            other.Username,
            other.Password);

        await SeedWithdrawalAsync(
            factory,
            100_000m,
            WithdrawalConstants.StatusRequested,
            tenantId: other.TenantId,
            ownerId: other.OwnerId);

        var tenantAList = await ownerA.GetFromJsonAsync<List<WithdrawalDto>>(
            "/api/finance/withdrawals");
        var tenantBList = await ownerB.GetFromJsonAsync<List<WithdrawalDto>>(
            "/api/finance/withdrawals");

        Assert.Empty(tenantAList!);
        Assert.Single(tenantBList!);
    }

    [Fact]
    public async Task NonOwner_CannotRequestWithdrawal()
    {
        await using var factory = new FinanceApiFactory();
        using var admin = await CreateTenantClientAsync(
            factory,
            "admin",
            "admin123");

        var response = await admin.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 100_000m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Platform_ProcessesPaysAndRejects_WithOperationalProof()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        using var platform = await CreatePlatformClientAsync(factory);

        await SeedCreditAsync(factory, 500_000m);
        await SeedVerifiedBankAccountAsync(factory);

        var paidCandidate = await CreateWithdrawalAsync(owner, 200_000m);
        var rejectedCandidate = await CreateWithdrawalAsync(owner, 100_000m);

        var processing = await platform.PostAsync(
            $"/api/platform/withdrawals/{paidCandidate.Id}/start-processing",
            null);

        Assert.Equal(HttpStatusCode.OK, processing.StatusCode);

        var missingConfirmation = await platform.PostAsJsonAsync(
            $"/api/platform/withdrawals/{paidCandidate.Id}/mark-paid",
            new
            {
                confirmedTransferred = false,
                transferReference = "TRF-001"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            missingConfirmation.StatusCode);

        var paid = await platform.PostAsJsonAsync(
            $"/api/platform/withdrawals/{paidCandidate.Id}/mark-paid",
            new
            {
                confirmedTransferred = true,
                transferReference = "TRF-001",
                evidenceMetadata = "manual-transfer"
            });

        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);

        var paidBody = await paid.Content
            .ReadFromJsonAsync<PlatformWithdrawalDto>();

        Assert.Equal(WithdrawalConstants.StatusPaid, paidBody!.Status);
        Assert.Equal("TRF-001", paidBody.TransferReference);
        Assert.Equal("1234567890", paidBody.DestinationAccountNumber);

        var rejected = await platform.PostAsJsonAsync(
            $"/api/platform/withdrawals/{rejectedCandidate.Id}/reject",
            new { reason = "Data transfer perlu diperbaiki." });

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        var rejectedBody = await rejected.Content
            .ReadFromJsonAsync<PlatformWithdrawalDto>();

        Assert.Equal(
            WithdrawalConstants.StatusRejected,
            rejectedBody!.Status);
        Assert.Equal(
            "Data transfer perlu diperbaiki.",
            rejectedBody.RejectionReason);

        var summary = await owner.GetFromJsonAsync<FinanceSummaryDto>(
            "/api/finance/summary");

        Assert.Equal(300_000m, summary!.AvailableBalance);
        Assert.Equal(200_000m, summary.TotalWithdrawn);
        Assert.Equal(0m, summary.PendingWithdrawalAmount);
    }

    [Fact]
    public async Task DuplicateMarkPaid_IsIdempotentAndCreatesOneDebit()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        using var platform = await CreatePlatformClientAsync(factory);

        await SeedCreditAsync(factory, 200_000m);
        await SeedVerifiedBankAccountAsync(factory);

        var withdrawal = await CreateWithdrawalAsync(owner, 100_000m);

        var processing = await platform.PostAsync(
            $"/api/platform/withdrawals/{withdrawal.Id}/start-processing",
            null);
        Assert.Equal(HttpStatusCode.OK, processing.StatusCode);

        var payload = new
        {
            confirmedTransferred = true,
            transferReference = "TRF-IDEMPOTENT"
        };

        var first = await platform.PostAsJsonAsync(
            $"/api/platform/withdrawals/{withdrawal.Id}/mark-paid",
            payload);
        var duplicate = await platform.PostAsJsonAsync(
            $"/api/platform/withdrawals/{withdrawal.Id}/mark-paid",
            payload);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.WithdrawalRoutes
            .Where(x => x.WithdrawalRequestId == withdrawal.Id)
            .Select(x => x.TenantId)
            .SingleAsync();

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "verify-withdrawal-debit");

        var debits = await db.PaymentLedgerEntries
            .Where(x =>
                x.WithdrawalRequestId == withdrawal.Id &&
                x.EntryType == PaymentConstants.LedgerWithdrawalDebit)
            .ToListAsync();

        Assert.Single(debits);
        Assert.Equal(100_000m, debits[0].Amount);
    }

    [Fact]
    public async Task Owner_CannotCancelWithdrawal_AfterProcessingStarts()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        using var platform = await CreatePlatformClientAsync(factory);

        await SeedCreditAsync(factory, 200_000m);
        await SeedVerifiedBankAccountAsync(factory);

        var withdrawal = await CreateWithdrawalAsync(owner, 100_000m);

        await platform.PostAsync(
            $"/api/platform/withdrawals/{withdrawal.Id}/start-processing",
            null);

        var cancel = await owner.PostAsync(
            $"/api/finance/withdrawals/{withdrawal.Id}/cancel",
            null);

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Contains(
            "WITHDRAWAL_INVALID_STATE",
            await cancel.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PlatformBankRejection_RequiresReason()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        using var platform = await CreatePlatformClientAsync(factory);

        await owner.PutAsJsonAsync(
            "/api/finance/bank-account",
            new
            {
                bankName = "BCA",
                accountNumber = "1234567890",
                accountHolderName = "Owner NeverFade"
            });

        var bank = (await platform.GetFromJsonAsync<
            List<PlatformWithdrawalBankAccountDto>>(
                "/api/platform/withdrawals/bank-accounts"))!.Single();

        var response = await platform.PostAsJsonAsync(
            $"/api/platform/withdrawals/bank-accounts/{bank.TenantId}/review",
            new { verified = false, reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "WITHDRAWAL_BANK_REJECTION_REASON_REQUIRED",
            await response.Content.ReadAsStringAsync());
    }

    private static async Task<WithdrawalDto> CreateWithdrawalAsync(
        HttpClient owner,
        decimal amount)
    {
        var response = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount });

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());

        return (await response.Content
            .ReadFromJsonAsync<WithdrawalDto>())!;
    }

    private static async Task<HttpClient> CreateTenantClientAsync(
        FinanceApiFactory factory,
        string username,
        string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content
            .ReadFromJsonAsync<LoginResponseDto>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login!.Token);

        return client;
    }

    private static async Task<HttpClient> CreatePlatformClientAsync(
        FinanceApiFactory factory)
    {
        await SeedPlatformUserAsync(factory);

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/login",
            new
            {
                username = "finance.superadmin",
                password = "FinancePlatformPassword123!"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content
            .ReadFromJsonAsync<PlatformLoginResponseDto>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login!.Token);

        return client;
    }

    private static async Task SeedPlatformUserAsync(
        FinanceApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.PlatformUsers.AnyAsync())
        {
            return;
        }

        db.PlatformUsers.Add(new PlatformUser
        {
            Nama = "Finance Super Admin",
            Username = "finance.superadmin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                "FinancePlatformPassword123!"),
            Role = PlatformAuthConstants.SuperAdminRole,
            Active = true
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedVerifiedBankAccountAsync(
        FinanceApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await GetDemoTenantIdAsync(db);

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "seed-verified-withdrawal-bank");

        var existing = await db.WithdrawalBankAccounts
            .SingleOrDefaultAsync();

        if (existing is not null)
        {
            existing.BankName = "BCA";
            existing.AccountNumber = "1234567890";
            existing.AccountHolderName = "Owner NeverFade";
            existing.VerificationStatus = WithdrawalConstants.BankVerified;
            existing.VerifiedAt = DateTime.UtcNow;
            existing.VerificationNote = "Seed verified";
        }
        else
        {
            db.WithdrawalBankAccounts.Add(new WithdrawalBankAccount
            {
                TenantId = tenantId,
                BankName = "BCA",
                AccountNumber = "1234567890",
                AccountHolderName = "Owner NeverFade",
                VerificationStatus = WithdrawalConstants.BankVerified,
                VerifiedAt = DateTime.UtcNow,
                VerificationNote = "Seed verified"
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedCreditAsync(
        FinanceApiFactory factory,
        decimal amount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await GetDemoTenantIdAsync(db);
        var ownerId = await db.Users
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == tenantId &&
                x.Role == "owner")
            .Select(x => x.Id)
            .SingleAsync();

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "seed-finance-credit");

        var transaction = new NeverfadePos.Api.Entities.Transaction
        {
            TenantId = tenantId,
            NoTrx = $"TRX-FIN-{Guid.NewGuid():N}",
            Kasir = "Finance QA",
            KasirId = ownerId,
            Total = amount,
            Dibayar = amount,
            MetodePembayaran = "QRIS",
            Status = TransactionStatuses.Paid,
            FinalizedAt = DateTime.UtcNow
        };

        var payment = new Payment
        {
            TenantId = tenantId,
            TransactionId = transaction.Id,
            ProviderReferenceId = $"nf-{Guid.NewGuid():N}",
            ProviderPaymentRequestId = $"pr-{Guid.NewGuid():N}",
            ProviderPaymentId = $"py-{Guid.NewGuid():N}",
            Amount = amount,
            Status = PaymentConstants.StatusPaid,
            PaidAt = DateTime.UtcNow
        };

        db.AddRange(
            transaction,
            payment,
            new PaymentLedgerEntry
            {
                TenantId = tenantId,
                PaymentId = payment.Id,
                TransactionId = transaction.Id,
                EntryType = PaymentConstants.LedgerPaymentCredit,
                Amount = amount,
                ProviderReference = payment.ProviderPaymentId
            });

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedWithdrawalAsync(
        FinanceApiFactory factory,
        decimal amount,
        string status,
        bool includeDebit = false,
        Guid? tenantId = null,
        Guid? ownerId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var targetTenantId = tenantId ?? await GetDemoTenantIdAsync(db);
        var targetOwnerId = ownerId ?? await db.Users
            .IgnoreQueryFilters()
            .Where(x =>
                x.TenantId == targetTenantId &&
                x.Role == "owner")
            .Select(x => x.Id)
            .SingleAsync();

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(targetTenantId, "seed-finance-withdrawal");

        var now = DateTime.UtcNow;
        var withdrawal = new WithdrawalRequest
        {
            TenantId = targetTenantId,
            Amount = amount,
            Status = status,
            RequestedByUserId = targetOwnerId,
            DestinationBankName = "BCA",
            DestinationAccountNumber = "1234567890",
            DestinationAccountHolderName = "Owner NeverFade",
            UpdatedAt = now,
            ProcessingStartedAt =
                status == WithdrawalConstants.StatusProcessing
                    ? now
                    : null,
            ProcessedAt =
                status is WithdrawalConstants.StatusPaid or
                    WithdrawalConstants.StatusRejected
                    ? now
                    : null,
            CancelledAt =
                status == WithdrawalConstants.StatusCancelled
                    ? now
                    : null
        };

        db.WithdrawalRequests.Add(withdrawal);
        db.WithdrawalRoutes.Add(new WithdrawalRoute
        {
            TenantId = targetTenantId,
            WithdrawalRequestId = withdrawal.Id
        });

        if (includeDebit)
        {
            db.PaymentLedgerEntries.Add(new PaymentLedgerEntry
            {
                TenantId = targetTenantId,
                WithdrawalRequestId = withdrawal.Id,
                EntryType = PaymentConstants.LedgerWithdrawalDebit,
                Amount = amount
            });
        }

        await db.SaveChangesAsync();
        return withdrawal.Id;
    }

    private static async Task<OtherTenant> SeedOtherTenantAsync(
        FinanceApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var username = $"finance-owner-{Guid.NewGuid():N}";
        const string password = "FinanceOtherOwner123!";

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            NamaToko = "Finance Tenant B",
            Slug = $"finance-tenant-{tenantId:N}",
            Status = "active"
        });

        using (scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "seed-finance-other-tenant"))
        {
            db.Users.Add(new User
            {
                Id = ownerId,
                TenantId = tenantId,
                Nama = "Finance Owner B",
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "owner",
                Active = true
            });

            await db.SaveChangesAsync();
        }

        return new OtherTenant(
            tenantId,
            ownerId,
            username,
            password);
    }

    private static Task<Guid> GetDemoTenantIdAsync(
        AppDbContext db) =>
        db.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();

    private sealed record OtherTenant(
        Guid TenantId,
        Guid OwnerId,
        string Username,
        string Password);

    private sealed class FinanceApiFactory
        : WebApplicationFactory<Program>
    {
        private const string TenantKey =
            "finance-tenant-test-key-that-is-at-least-32-characters";
        private const string PlatformKey =
            "finance-platform-test-key-that-is-at-least-32-characters";

        private readonly string _databaseName =
            $"finance-api-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            var config = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=test;Username=test;Password=test",
                ["Jwt:Key"] = TenantKey,
                ["Jwt:Issuer"] = "NeverfadePos.Finance.Test",
                ["Jwt:Audience"] = "NeverfadePos.Finance.Test.Client",
                ["PlatformJwt:Key"] = PlatformKey,
                ["PlatformJwt:Issuer"] =
                    "NeverfadePos.Platform.Finance.Test",
                ["PlatformJwt:Audience"] =
                    "NeverfadePos.Platform.Finance.Test.Client",
                ["PlatformBootstrap:Enabled"] = "false"
            };

            foreach (var item in config)
            {
                builder.UseSetting(item.Key, item.Value);
            }

            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(config));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<
                    IDbContextOptionsConfiguration<AppDbContext>>();

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}

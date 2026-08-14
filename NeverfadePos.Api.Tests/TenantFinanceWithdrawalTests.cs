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
    public async Task FinanceSummary_CalculatesIncomeWithdrawnPendingAndAvailable()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        await SeedCreditAsync(factory, 100_000m);
        await SeedWithdrawalAsync(
            factory,
            30_000m,
            WithdrawalConstants.StatusPaid,
            includeDebit: true);
        await SeedWithdrawalAsync(
            factory,
            20_000m,
            WithdrawalConstants.StatusRequested);

        var summary = await owner.GetFromJsonAsync<FinanceSummaryDto>(
            "/api/finance/summary");

        Assert.NotNull(summary);
        Assert.Equal(50_000m, summary.AvailableBalance);
        Assert.Equal(100_000m, summary.TotalSuccessfulNonCashIncome);
        Assert.Equal(30_000m, summary.TotalWithdrawn);
        Assert.Equal(20_000m, summary.PendingWithdrawalAmount);
    }

    [Fact]
    public async Task Owner_CanCreateValidWithdrawalRequest()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        await SeedCreditAsync(factory, 100_000m);

        var response = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 40_000m });

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            await response.Content.ReadAsStringAsync());
        var withdrawal = await response.Content
            .ReadFromJsonAsync<WithdrawalDto>();
        Assert.NotNull(withdrawal);
        Assert.Equal(40_000m, withdrawal.Amount);
        Assert.Equal(
            WithdrawalConstants.StatusRequested,
            withdrawal.Status);

        var listed = await owner.GetFromJsonAsync<List<WithdrawalDto>>(
            "/api/finance/withdrawals");
        Assert.Single(listed!);
        Assert.Equal(withdrawal.Id, listed![0].Id);
    }

    [Fact]
    public async Task Withdrawal_RejectsInsufficientBalance()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        await SeedCreditAsync(factory, 50_000m);

        var response = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 50_001m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(
            "WITHDRAWAL_INSUFFICIENT_BALANCE",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task MultiplePendingWithdrawals_CannotExceedBalance()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        await SeedCreditAsync(factory, 100_000m);

        var first = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 60_000m });
        var second = await owner.PostAsJsonAsync(
            "/api/finance/withdrawals",
            new { amount = 50_000m });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var summary = await owner.GetFromJsonAsync<FinanceSummaryDto>(
            "/api/finance/summary");
        Assert.Equal(40_000m, summary!.AvailableBalance);
        Assert.Equal(60_000m, summary.PendingWithdrawalAmount);
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
            10_000m,
            WithdrawalConstants.StatusRequested,
            tenantId: other.TenantId,
            ownerId: other.OwnerId);

        var tenantAList = await ownerA
            .GetFromJsonAsync<List<WithdrawalDto>>(
                "/api/finance/withdrawals");
        var tenantBList = await ownerB
            .GetFromJsonAsync<List<WithdrawalDto>>(
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
            new { amount = 1m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanMarkPaidAndReject()
    {
        await using var factory = new FinanceApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");
        using var platform = await CreatePlatformClientAsync(factory);
        await SeedCreditAsync(factory, 100_000m);
        var paidCandidate = await CreateWithdrawalAsync(owner, 40_000m);
        var rejectedCandidate = await CreateWithdrawalAsync(owner, 20_000m);

        var platformList = await platform
            .GetFromJsonAsync<List<PlatformWithdrawalDto>>(
                "/api/platform/withdrawals");
        var paidResponse = await platform.PostAsync(
            $"/api/platform/withdrawals/{paidCandidate.Id}/mark-paid",
            null);
        var rejectedResponse = await platform.PostAsync(
            $"/api/platform/withdrawals/{rejectedCandidate.Id}/reject",
            null);

        Assert.Equal(2, platformList!.Count);
        Assert.Equal(HttpStatusCode.OK, paidResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rejectedResponse.StatusCode);
        Assert.Equal(
            WithdrawalConstants.StatusPaid,
            (await paidResponse.Content
                .ReadFromJsonAsync<PlatformWithdrawalDto>())!.Status);
        Assert.Equal(
            WithdrawalConstants.StatusRejected,
            (await rejectedResponse.Content
                .ReadFromJsonAsync<PlatformWithdrawalDto>())!.Status);

        var summary = await owner.GetFromJsonAsync<FinanceSummaryDto>(
            "/api/finance/summary");
        Assert.Equal(60_000m, summary!.AvailableBalance);
        Assert.Equal(40_000m, summary.TotalWithdrawn);
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
        await SeedCreditAsync(factory, 100_000m);
        var withdrawal = await CreateWithdrawalAsync(owner, 30_000m);

        var first = await platform.PostAsync(
            $"/api/platform/withdrawals/{withdrawal.Id}/mark-paid",
            null);
        var duplicate = await platform.PostAsync(
            $"/api/platform/withdrawals/{withdrawal.Id}/mark-paid",
            null);

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
        Assert.Equal(30_000m, debits[0].Amount);
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
            new AuthenticationHeaderValue("Bearer", login!.Token);
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
            new AuthenticationHeaderValue("Bearer", login!.Token);
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

    private static async Task SeedCreditAsync(
        FinanceApiFactory factory,
        decimal amount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await GetDemoTenantIdAsync(db);
        var ownerId = await db.Users
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Role == "owner")
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
        var withdrawal = new WithdrawalRequest
        {
            TenantId = targetTenantId,
            Amount = amount,
            Status = status,
            RequestedByUserId = targetOwnerId,
            ProcessedAt = status == WithdrawalConstants.StatusRequested
                ? null
                : DateTime.UtcNow
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

        return new OtherTenant(tenantId, ownerId, username, password);
    }

    private static Task<Guid> GetDemoTenantIdAsync(AppDbContext db) =>
        db.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();

    private sealed record OtherTenant(
        Guid TenantId,
        Guid OwnerId,
        string Username,
        string Password);

    private sealed class FinanceApiFactory : WebApplicationFactory<Program>
    {
        private const string TenantKey =
            "finance-tenant-test-key-that-is-at-least-32-characters";
        private const string PlatformKey =
            "finance-platform-test-key-that-is-at-least-32-characters";
        private readonly string _databaseName =
            $"finance-api-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
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

            builder.ConfigureAppConfiguration((_, configuration) =>
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

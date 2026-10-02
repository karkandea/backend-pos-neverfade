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
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Controllers;
using NeverfadePos.Api.Services.Observability;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.DTOs.Tenant;
using NeverfadePos.Api.DTOs.Outlet;
using NeverfadePos.Api.DTOs.Laporan;
using NeverfadePos.Api.DTOs.Onboarding;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class TenantContextApiTests
{
    private const string TenantKey =
        "tenant-context-test-key-123456789012345678901234";
    private const string PlatformKey =
        "platform-context-test-key-1234567890123456789012";
    private const string TenantIssuer =
        "NeverfadePos.TenantContext.Test";
    private const string TenantAudience =
        "NeverfadePos.TenantContext.Client";
    private const string PlatformIssuer =
        "NeverfadePos.Platform.TenantContext.Test";
    private const string PlatformAudience =
        "NeverfadePos.Platform.TenantContext.Client";
    private const string TestConnectionString =
        "Host=localhost;Database=test;Username=test;Password=test";

    [Theory]
    [InlineData("owner", "owner123", "owner")]
    [InlineData("admin", "admin123", "admin")]
    [InlineData("kasir", "kasir123", "kasir")]
    public async Task TenantContext_ReturnsServerResolvedBusinessModeForAuthenticatedRole(
        string username,
        string password,
        string expectedRole)
    {
        await using var factory = new TenantContextFactory();
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var loginBody = await login.Content
            .ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginBody);
        Assert.False(string.IsNullOrWhiteSpace(loginBody.Token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginBody.Token);

        var response = await client.GetAsync("/api/tenant/context");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var context = await response.Content
            .ReadFromJsonAsync<TenantContextDto>();
        Assert.NotNull(context);
        Assert.NotEqual(Guid.Empty, context.TenantId);
        Assert.Equal("WARUNG LUMPIA BEEF", context.NamaToko);
        Assert.Equal("general_retail", context.BusinessType);
        Assert.Equal(expectedRole, context.Role);
        Assert.Equal(
            new[]
            {
                "core_pos",
                "inventory",
                "customers",
                "reports",
                "attendance",
                "finance_withdrawal"
            },
            context.Capabilities);
        Assert.DoesNotContain("table_orders", context.Capabilities);
        Assert.DoesNotContain("work_orders", context.Capabilities);
        Assert.DoesNotContain("appointments", context.Capabilities);
    }

    [Fact]
    public async Task TenantContext_RejectsAnonymousRequest()
    {
        await using var factory = new TenantContextFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tenant/context");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OutletTimezone_IsValidatedAndSurvivesLegacyUpdate()
    {
        await using var factory = new TenantContextFactory();
        using var owner = factory.CreateClient();
        var login = (await (await owner.PostAsJsonAsync("/api/auth/login",
            new { username = "owner", password = "owner123" })).Content
            .ReadFromJsonAsync<LoginResponseDto>())!;
        owner.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Token);

        var createdResponse = await owner.PostAsJsonAsync("/api/outlets", new
        {
            code = "WITA-QA", name = "Makassar QA", address = "", phone = "",
            isDefault = false, timeZoneId = "Asia/Makassar"
        });
        Assert.Equal(HttpStatusCode.OK, createdResponse.StatusCode);
        var created = (await createdResponse.Content.ReadFromJsonAsync<OutletDto>())!;
        Assert.Equal("Asia/Makassar", created.TimeZoneId);
        var persisted = (await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!;
        Assert.Equal("Asia/Makassar", persisted.Single(x => x.Id == created.Id).TimeZoneId);

        var legacyUpdate = await owner.PutAsJsonAsync($"/api/outlets/{created.Id}", new
        {
            code = created.Code, name = "Makassar Updated", address = "",
            phone = "", isDefault = false, active = true
        });
        Assert.Equal(HttpStatusCode.OK, legacyUpdate.StatusCode);
        var fetchedAfterLegacy = (await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!;
        Assert.Equal("Asia/Makassar", fetchedAfterLegacy.Single(x => x.Id == created.Id).TimeZoneId);
        Assert.Equal("Asia/Makassar",
            (await legacyUpdate.Content.ReadFromJsonAsync<OutletDto>())!.TimeZoneId);

        var invalid = await owner.PutAsJsonAsync($"/api/outlets/{created.Id}", new
        {
            code = created.Code, name = "Makassar Updated", address = "",
            phone = "", isDefault = false, active = true, timeZoneId = "Invalid/Zone"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var inherited = await owner.PutAsJsonAsync($"/api/outlets/{created.Id}", new
        {
            code = created.Code, name = "Makassar Updated", address = "",
            phone = "", isDefault = false, active = true, timeZoneId = ""
        });
        Assert.Equal(HttpStatusCode.OK, inherited.StatusCode);
        Assert.Null((await inherited.Content.ReadFromJsonAsync<OutletDto>())!.TimeZoneId);
    }

    [Fact]
    public async Task OpsHealth_ReportsStalledJobWithCorrelationAndOwnerOnly()
    {
        await using var factory = new TenantContextFactory();
        using var owner = factory.CreateClient();
        using var cashier = factory.CreateClient();
        var login = (await (await owner.PostAsJsonAsync("/api/auth/login",
            new { username = "owner", password = "owner123" })).Content
            .ReadFromJsonAsync<LoginResponseDto>())!;
        owner.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Token);
        var cashierLogin = (await (await cashier.PostAsJsonAsync("/api/auth/login",
            new { username = "kasir", password = "kasir123" })).Content
            .ReadFromJsonAsync<LoginResponseDto>())!;
        cashier.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cashierLogin.Token);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants.AsNoTracking().Select(x => x.Id).SingleAsync();
            using var trusted = scope.ServiceProvider
                .GetRequiredService<NeverfadePos.Api.Auth.ITrustedTenantExecutionScope>()
                .Begin(tenantId, "OPS_HEALTH_TEST");
            db.Jobs.Add(new Job
            {
                TenantId = tenantId,
                Kind = "export",
                State = "queued",
                UpdatedAt = DateTime.UtcNow.AddMinutes(-30)
            });
            db.TenantAuditEvents.Add(new TenantAuditEvent
            {
                TenantId = tenantId, EventType = "QA_OPS_HEALTH",
                Metadata = "private-metadata-do-not-serve"
            });
            var transaction = new Transaction
            {
                TenantId = tenantId, NoTrx = "OPS-QA-001",
                Kasir = "QA", Status = TransactionStatuses.PendingPayment
            };
            db.Transactions.Add(transaction);
            var payment = new Payment
            {
                TenantId = tenantId, TransactionId = transaction.Id,
                ProviderReferenceId = "ops-qa", Status = PaymentConstants.StatusCreating,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            };
            db.Payments.Add(payment);
            db.PaymentWebhookEvents.Add(new PaymentWebhookEvent
            {
                TenantId = tenantId, PaymentId = payment.Id,
                ProviderEventKey = "ops-qa-event", EventType = "payment.failure",
                ProviderPaymentId = "ops-qa-provider",
                ProcessingStatus = "needs_review"
            });
            await db.SaveChangesAsync();

            var scanner = scope.ServiceProvider.GetRequiredService<OpsAlertScanner>();
            Assert.Equal(3, await scanner.ScanAsync(DateTime.UtcNow));
            Assert.Equal(0, await scanner.ScanAsync(DateTime.UtcNow));
            Assert.Equal(3, await db.OpsAlerts.CountAsync());
        }
        // Simulate process restart: new DI scope and DbContext, same tenant.
        await using (var replayScope = factory.Services.CreateAsyncScope())
        {
            var db = replayScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants.AsNoTracking().Select(x => x.Id).SingleAsync();
            using var trusted = replayScope.ServiceProvider
                .GetRequiredService<NeverfadePos.Api.Auth.ITrustedTenantExecutionScope>()
                .Begin(tenantId, "OPS_RESTART_TEST");
            var scanner = replayScope.ServiceProvider.GetRequiredService<OpsAlertScanner>();
            Assert.Equal(0, await scanner.ScanAsync(DateTime.UtcNow));
            Assert.Equal(3, await db.OpsAlerts.CountAsync());
        }

        var response = await owner.GetAsync("/api/v2/ops/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<OpsHealthDto>>();
        Assert.NotNull(envelope);
        Assert.True(envelope.Data.NeedsAttention);
        Assert.Equal(1, envelope.Data.StalledJobs);
        Assert.Equal(1, envelope.Data.StalledPayments);
        Assert.Equal(1, envelope.Data.UnprocessedWebhooks);
        Assert.False(envelope.Data.OutboxMetricsAvailable);
        Assert.False(envelope.Data.WorkerDispatchAvailable);
        var alertResponse = await owner.GetAsync("/api/v2/ops/alerts?limit=10");
        Assert.Equal(HttpStatusCode.OK, alertResponse.StatusCode);
        var alertList = await alertResponse.Content
            .ReadFromJsonAsync<ApiResponseEnvelope<List<OpsAlertDto>>>();
        Assert.NotNull(alertList);
        Assert.Equal(3, alertList.Data.Count);
        Assert.Equal(3, alertList.Data.Select(x => x.CorrelationId).Distinct().Count());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync("/api/v2/ops/alerts")).StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var ids));
        Assert.Equal(Assert.Single(ids!), envelope.Meta.CorrelationId);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync("/api/v2/ops/health")).StatusCode);
        using var unauthenticated = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await unauthenticated.GetAsync("/api/v2/ops/health")).StatusCode);

        var audit = await owner.GetAsync("/api/v2/ops/audit?limit=10");
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        var body = await audit.Content.ReadAsStringAsync();
        Assert.Contains("QA_OPS_HEALTH", body);
        Assert.DoesNotContain("private-metadata-do-not-serve", body);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync("/api/v2/ops/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.GetAsync("/api/v2/ops/audit?limit=101")).StatusCode);
    }

    [Fact]
    public async Task Reports_FilterAllThreeSurfacesByAssignedOrSelectedOutlet()
    {
        await using var factory = new TenantContextFactory();
        using var owner = factory.CreateClient();
        using var admin = factory.CreateClient();
        var ownerLogin = (await (await owner.PostAsJsonAsync("/api/auth/login",
            new { username = "owner", password = "owner123" })).Content
            .ReadFromJsonAsync<LoginResponseDto>())!;
        var adminLogin = (await (await admin.PostAsJsonAsync("/api/auth/login",
            new { username = "admin", password = "admin123" })).Content
            .ReadFromJsonAsync<LoginResponseDto>())!;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerLogin.Token);
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminLogin.Token);
        var main = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        var branchResponse = await owner.PostAsJsonAsync("/api/outlets", new
        { code = "REPORT-BRANCH", name = "Report branch", address = "", phone = "", isDefault = false });
        Assert.Equal(HttpStatusCode.OK, branchResponse.StatusCode);
        var branch = (await branchResponse.Content.ReadFromJsonAsync<OutletDto>())!;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = (await db.Tenants.AsNoTracking().SingleAsync()).Id;
            using var trusted = scope.ServiceProvider.GetRequiredService<NeverfadePos.Api.Auth.ITrustedTenantExecutionScope>()
                .Begin(tenantId, "REPORT_OUTLET_TEST");
            foreach (var (outlet, amount, name) in new[]
            {
                (main.Id, 100m, "MAIN-REPORT"), (branch.Id, 200m, "BRANCH-REPORT")
            })
            {
                var transaction = new Transaction
                {
                    TenantId = tenantId, OutletId = outlet, NoTrx = $"TRX-{name}",
                    Kasir = "QA", Status = TransactionStatuses.Paid,
                    Total = amount, Subtotal = amount, Dibayar = amount,
                    CreatedAt = DateTime.UtcNow, Tanggal = DateTime.UtcNow
                };
                db.Transactions.Add(transaction);
                db.TransactionItems.Add(new TransactionItem
                {
                    TenantId = tenantId, TransactionId = transaction.Id, ProductId = Guid.NewGuid(),
                    Nama = name, HargaJual = amount, Qty = 1, Subtotal = amount
                });
            }
            await db.SaveChangesAsync();
        }

        // Preserve owner aggregate when no outlet header; admin gets assigned outlets only.
        Assert.Equal(300m, (await owner.GetFromJsonAsync<LaporanSummaryDto>(
            "/api/laporan/summary"))!.Omzet);
        Assert.Equal(100m, (await admin.GetFromJsonAsync<LaporanSummaryDto>(
            "/api/laporan/summary"))!.Omzet);
        var adminProducts = (await admin.GetFromJsonAsync<List<TopProductDto>>(
            "/api/laporan/top-products"))!;
        Assert.Equal("MAIN-REPORT", Assert.Single(adminProducts).Nama);
        var adminChart = (await admin.GetFromJsonAsync<List<LaporanChartDto>>(
            "/api/laporan/chart"))!;
        Assert.Equal(100m, adminChart.Sum(x => x.Total));

        owner.DefaultRequestHeaders.Add("X-Outlet-Id", branch.Id.ToString());
        Assert.Equal(200m, (await owner.GetFromJsonAsync<LaporanSummaryDto>(
            "/api/laporan/summary"))!.Omzet);
        Assert.Equal("BRANCH-REPORT", Assert.Single((await owner.GetFromJsonAsync<List<TopProductDto>>(
            "/api/laporan/top-products"))!).Nama);
        owner.DefaultRequestHeaders.Remove("X-Outlet-Id");
        admin.DefaultRequestHeaders.Add("X-Outlet-Id", branch.Id.ToString());
        foreach (var endpoint in new[] { "/api/laporan/summary", "/api/laporan/chart", "/api/laporan/top-products" })
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync(endpoint)).StatusCode);
        admin.DefaultRequestHeaders.Remove("X-Outlet-Id");
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", "not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/laporan/summary")).StatusCode);
    }

    [Fact]
    public async Task Cashier_DirectMutationAndReportsDenied_ButCatalogAndCustomerSearchRetained()
    {
        await using var factory = new TenantContextFactory();
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "kasir", password = "kasir123" });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.Token);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/products", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/products/{Guid.NewGuid()}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.DeleteAsync($"/api/products/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/stock-history", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/laporan/summary")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/finance/bank-account")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync("/api/finance/bank-account", new
            {
                bankName = "BCA",
                accountNumber = "1234567890",
                accountHolderName = "Kasir Tidak Boleh"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/customers/{Guid.NewGuid()}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.DeleteAsync($"/api/customers/{Guid.NewGuid()}")).StatusCode);

        var productCatalog = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, productCatalog.StatusCode);
        Assert.DoesNotContain(
            "\"hargaModal\"",
            await productCatalog.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/customers?search=test")).StatusCode);
    }

    [Fact]
    public async Task RoleChange_RevokesPreviouslyIssuedJwt()
    {
        await using var factory = new TenantContextFactory();
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "kasir", password = "kasir123" });
        var body = await login.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(body);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.Token);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/tenant/context")).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(x => x.Username == "kasir");
            var trustedScope = scope.ServiceProvider.GetRequiredService<NeverfadePos.Api.Auth.ITrustedTenantExecutionScope>();
            using (trustedScope.Begin(user.TenantId, "TEST_ROLE_REVOKE"))
            {
                user.Role = "admin";
                await db.SaveChangesAsync();
            }
        }
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/tenant/context")).StatusCode);
    }

    [Fact]
    public async Task Cashier_CannotSelectUnassignedOutlet_AndOwnerCanDelegateAndRevoke()
    {
        await using var factory = new TenantContextFactory();
        using var owner = factory.CreateClient();
        using var cashier = factory.CreateClient();
        var ownerLogin = (await (await owner.PostAsJsonAsync("/api/auth/login",
            new { username = "owner", password = "owner123" })).Content
            .ReadFromJsonAsync<LoginResponseDto>())!;
        var cashierLogin = (await (await cashier.PostAsJsonAsync("/api/auth/login",
            new { username = "kasir", password = "kasir123" })).Content
            .ReadFromJsonAsync<LoginResponseDto>())!;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerLogin.Token);
        cashier.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cashierLogin.Token);

        var main = (await (await cashier.GetAsync("/api/outlets")).Content
            .ReadFromJsonAsync<List<OutletDto>>())!;
        Assert.Single(main);
        var branchResponse = await owner.PostAsJsonAsync("/api/outlets", new
        {
            code = "BRANCH2", name = "Cabang Dua", address = "", phone = "", isDefault = false
        });
        Assert.Equal(HttpStatusCode.OK, branchResponse.StatusCode);
        var branch = (await branchResponse.Content.ReadFromJsonAsync<OutletDto>())!;

        var cashierOutlets = (await (await cashier.GetAsync("/api/outlets")).Content
            .ReadFromJsonAsync<List<OutletDto>>())!;
        Assert.Single(cashierOutlets);
        var invalidSale = await cashier.PostAsJsonAsync("/api/transactions", new
        {
            outletId = branch.Id, metodePembayaran = "tunai",
            items = new[] { new { id = Guid.NewGuid(), nama = "Test", hargaJual = 1, qty = 1, subtotal = 1 } },
            subtotal = 1, total = 1, dibayar = 1
        });
        Assert.Equal(HttpStatusCode.Forbidden, invalidSale.StatusCode);
        Assert.Contains("OUTLET_NOT_ASSIGNED", await invalidSale.Content.ReadAsStringAsync());

        var assignment = await owner.PutAsJsonAsync(
            $"/api/users/{cashierLogin.User.Id}/outlets",
            new { outletIds = new[] { main[0].Id, branch.Id } });
        Assert.Equal(HttpStatusCode.OK, assignment.StatusCode);
        Assert.Equal(2, (await (await cashier.GetAsync("/api/outlets")).Content
            .ReadFromJsonAsync<List<OutletDto>>())!.Count);

        var revoke = await owner.PutAsJsonAsync(
            $"/api/users/{cashierLogin.User.Id}/outlets",
            new { outletIds = new[] { main[0].Id } });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Single((await (await cashier.GetAsync("/api/outlets")).Content
            .ReadFromJsonAsync<List<OutletDto>>())!);
        var branchTransactionId = Guid.NewGuid();
        var branchPaymentId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants.Where(x => x.Slug == "warung-lumpia-beef")
                .Select(x => x.Id).SingleAsync();
            using (scope.ServiceProvider.GetRequiredService<NeverfadePos.Api.Auth.ITrustedTenantExecutionScope>()
                .Begin(tenantId, "TEST_OUTLET_READ"))
            {
                db.Transactions.Add(new Transaction
                {
                    Id = branchTransactionId, TenantId = tenantId, OutletId = branch.Id,
                    NoTrx = "TRX-SECOND-OUTLET", Kasir = "QA", KasirId = cashierLogin.User.Id,
                    MetodePembayaran = "QRIS", Status = TransactionStatuses.PendingPayment
                });
                db.Payments.Add(new Payment
                {
                    Id = branchPaymentId, TenantId = tenantId, TransactionId = branchTransactionId,
                    ProviderReferenceId = "qa-second-outlet", Amount = 1000,
                    Status = PaymentConstants.StatusPending
                });
                await db.SaveChangesAsync();
            }
        }
        Assert.DoesNotContain("TRX-SECOND-OUTLET", await
            (await cashier.GetAsync("/api/transactions")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound,
            (await cashier.GetAsync($"/api/transactions/{branchTransactionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync($"/api/transactions/{branchTransactionId}/receipt/whatsapp/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await cashier.GetAsync($"/api/payments/{branchPaymentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await cashier.GetAsync("/api/payments/current")).StatusCode);
        cashier.DefaultRequestHeaders.Add("X-Outlet-Id", branch.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync("/api/transactions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync($"/api/payments/{branchPaymentId}")).StatusCode);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", branch.Id.ToString());
        Assert.Equal(HttpStatusCode.OK,
            (await owner.GetAsync($"/api/transactions/{branchTransactionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.GetAsync($"/api/payments/{branchPaymentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.PostAsJsonAsync("/api/transactions", new
            {
                outletId = branch.Id, metodePembayaran = "tunai",
                items = new[] { new { id = Guid.NewGuid(), nama = "Test", hargaJual = 1, qty = 1, subtotal = 1 } },
                subtotal = 1, total = 1, dibayar = 1
            })).StatusCode);
    }

    [Fact]
    public async Task Onboarding_IsOwnerAdminOnly_AndFollowsPersistedProfile()
    {
        await using var factory = new TenantContextFactory();
        using var owner = factory.CreateClient();
        using var cashier = factory.CreateClient();
        foreach (var (client, username, password) in new[]
        {
            (owner, "owner", "owner123"), (cashier, "kasir", "kasir123")
        })
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
            var response = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", response.Token);
        }
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync("/api/tenant/onboarding")).StatusCode);
        var first = (await owner.GetFromJsonAsync<TenantOnboardingDto>("/api/tenant/onboarding"))!;
        Assert.Equal("live", first.Mode);
        Assert.Equal(3, first.TotalRequired);
        Assert.True(Assert.Single(first.Steps, x => x.Id == "cash_payment").Complete);
        Assert.False(Assert.Single(first.Steps, x => x.Id == "staff_assignment").Required);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
            using (scope.ServiceProvider.GetRequiredService<NeverfadePos.Api.Auth.ITrustedTenantExecutionScope>()
                .Begin(tenantId, "ONBOARDING_TEST"))
            {
                var setting = await db.Settings.SingleAsync();
                setting.Alamat = ""; setting.Telepon = "";
                var outlet = await db.Outlets.SingleAsync(x => x.IsDefault);
                outlet.Address = ""; outlet.Phone = "";
                await db.SaveChangesAsync();
            }
        }
        var incomplete = (await owner.GetFromJsonAsync<TenantOnboardingDto>("/api/tenant/onboarding"))!;
        Assert.False(incomplete.RequiredStepsComplete);
        Assert.False(Assert.Single(incomplete.Steps, x => x.Id == "business_profile").Complete);
        Assert.False(Assert.Single(incomplete.Steps, x => x.Id == "default_outlet").Complete);
    }

    [Theory]
    [InlineData("food_beverage", "restaurant_tables")]
    [InlineData("laundry", "laundry_services")]
    [InlineData("salon_barbershop", "salon_services")]
    public async Task Onboarding_AddsRequirementForEachSpecialCategory(string mode, string expectedStep)
    {
        await using var factory = new TenantContextFactory();
        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "owner", password = "owner123" });
        var auth = (await login.Content.ReadFromJsonAsync<LoginResponseDto>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.Tenants.SingleAsync();
            entity.BusinessType = mode;
            await db.SaveChangesAsync();
        }
        var result = (await client.GetFromJsonAsync<TenantOnboardingDto>("/api/tenant/onboarding"))!;
        Assert.Equal(mode, result.BusinessType);
        Assert.Equal(4, result.TotalRequired);
        var special = Assert.Single(result.Steps, x => x.Id == expectedStep);
        Assert.True(special.Required);
        Assert.StartsWith("/", special.ActionPath);
    }

    private sealed class TenantContextFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName =
            $"tenant-context-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                TestConnectionString);
            builder.UseSetting("Jwt:Key", TenantKey);
            builder.UseSetting("Jwt:Issuer", TenantIssuer);
            builder.UseSetting("Jwt:Audience", TenantAudience);
            builder.UseSetting("PlatformJwt:Key", PlatformKey);
            builder.UseSetting("PlatformJwt:Issuer", PlatformIssuer);
            builder.UseSetting("PlatformJwt:Audience", PlatformAudience);
            builder.UseSetting("Payments:Mode", "Disabled");
            builder.UseSetting("PlatformBootstrap:Enabled", "false");

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        TestConnectionString,
                    ["Jwt:Key"] = TenantKey,
                    ["Jwt:Issuer"] = TenantIssuer,
                    ["Jwt:Audience"] = TenantAudience,
                    ["PlatformJwt:Key"] = PlatformKey,
                    ["PlatformJwt:Issuer"] = PlatformIssuer,
                    ["PlatformJwt:Audience"] = PlatformAudience,
                    ["Payments:Mode"] = "Disabled",
                    ["PlatformBootstrap:Enabled"] = "false"
                }));

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

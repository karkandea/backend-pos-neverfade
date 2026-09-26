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
            (await client.PutAsJsonAsync($"/api/customers/{Guid.NewGuid()}", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.DeleteAsync($"/api/customers/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/products")).StatusCode);
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

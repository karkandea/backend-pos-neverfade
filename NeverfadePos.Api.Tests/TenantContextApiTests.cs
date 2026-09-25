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
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.PostAsJsonAsync("/api/transactions", new
            {
                outletId = branch.Id, metodePembayaran = "tunai",
                items = new[] { new { id = Guid.NewGuid(), nama = "Test", hargaJual = 1, qty = 1, subtotal = 1 } },
                subtotal = 1, total = 1, dibayar = 1
            })).StatusCode);
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

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.DTOs.Product;
using NeverfadePos.Api.DTOs.Restaurant;
using NeverfadePos.Api.DTOs.Laundry;
using NeverfadePos.Api.DTOs.Transaction;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class DemoModeTests
{
    [Fact]
    public async Task PublicDemo_AllowsCashTransaction_ButBlocksMasterMutation()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsync(
            "/api/demo/session",
            content: null);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login.Token));

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Token);

        var products = await client.GetFromJsonAsync<List<ProductDto>>(
            "/api/products");
        Assert.NotNull(products);

        var pants = Assert.Single(products, x => x.Kode == "RTL003");
        Assert.Equal(24, pants.Stok);

        var blockedMutation = await client.PostAsJsonAsync(
            "/api/products",
            new { });
        Assert.Equal(HttpStatusCode.Forbidden, blockedMutation.StatusCode);

        var transaction = new CreateTransactionDto
        {
            Items =
            [
                new CreateTransactionItemDto
                {
                    Id = pants.Id,
                    Nama = pants.Nama,
                    HargaJual = 239000m,
                    Qty = 1,
                    Quantity = 1,
                    Subtotal = 239000m
                }
            ],
            Subtotal = 239000m,
            Disc = 0,
            Tax = 0,
            DiscAmt = 0,
            TaxAmt = 0,
            Total = 239000m,
            MetodePembayaran = "tunai",
            Dibayar = 250000m,
            Kembalian = 11000m
        };

        var checkout = await client.PostAsJsonAsync(
            "/api/transactions",
            transaction);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        var afterCheckout = await client.GetFromJsonAsync<ProductDto>(
            $"/api/products/{pants.Id}");
        Assert.NotNull(afterCheckout);
        Assert.Equal(23, afterCheckout.Stok);
    }

    [Fact]
    public async Task PublicDemo_FoodBeverageSession_ExposesTablesAndKitchenQueue()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsync(
            "/api/demo/session?businessType=food_beverage",
            content: null);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Token);

        var context = await client.GetFromJsonAsync<JsonElement>(
            "/api/tenant/context");
        Assert.Equal(
            "food_beverage",
            context.GetProperty("businessType").GetString());

        var tables = await client.GetFromJsonAsync<List<RestaurantTableDto>>(
            "/api/restaurant/tables");
        Assert.NotNull(tables);
        Assert.Equal(8, tables.Count);
        Assert.Contains(tables, x => x.Status == "occupied");

        var kitchen = await client.GetFromJsonAsync<List<KitchenQueueOrderDto>>(
            "/api/restaurant/kitchen");
        Assert.NotNull(kitchen);
        Assert.NotEmpty(kitchen);
        Assert.Contains(
            kitchen.SelectMany(x => x.Items),
            x => x.KitchenStatus == "queued" ||
                 x.KitchenStatus == "preparing");

        var blockedTableMasterMutation = await client.PostAsJsonAsync(
            "/api/restaurant/tables",
            new
            {
                code = "T99",
                name = "Meja 99",
                capacity = 2,
                active = true,
                sortOrder = 990
            });
        Assert.Equal(
            HttpStatusCode.Forbidden,
            blockedTableMasterMutation.StatusCode);
    }

    [Fact]
    public async Task PublicDemo_LaundrySession_ExposesWorkOrders()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsync(
            "/api/demo/session?businessType=laundry",
            content: null);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Token);

        var context = await client.GetFromJsonAsync<JsonElement>(
            "/api/tenant/context");
        Assert.Equal(
            "laundry",
            context.GetProperty("businessType").GetString());

        var workOrders = await client.GetFromJsonAsync<List<LaundryWorkOrderDto>>(
            "/api/laundry/work-orders");
        Assert.NotNull(workOrders);
        Assert.Equal(2, workOrders.Count);
        Assert.Contains(
            workOrders,
            x => x.Status == "received");
        Assert.Contains(
            workOrders,
            x => x.Status == "in_progress");
    }

    [Fact]
    public async Task PublicDemo_AuthenticatedSession_CanSwitchBusinessType()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsync(
            "/api/demo/session?businessType=laundry",
            content: null);
        var first = await firstResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(first);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", first.Token);

        var switchResponse = await client.PostAsync(
            "/api/demo/session?businessType=food_beverage",
            content: null);

        Assert.True(switchResponse.StatusCode == HttpStatusCode.OK,
            $"Switch returned {switchResponse.StatusCode}: {await switchResponse.Content.ReadAsStringAsync()}");

        var switched = await switchResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(switched);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", switched.Token);

        var context = await client.GetFromJsonAsync<JsonElement>(
            "/api/tenant/context");

        Assert.Equal(
            "food_beverage",
            context.GetProperty("businessType").GetString());
    }

    [Fact]
    public async Task PublicDemo_RecordsAllowlistedAnonymousConversionEvents()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();
        var sessionId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/demo/events", new
        {
            sessionId,
            businessType = "food_beverage",
            eventName = "category_selected"
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recorded = await db.DemoConversionEvents.AsNoTracking()
            .SingleAsync(x => x.SessionId == sessionId);

        Assert.Equal("food_beverage", recorded.BusinessType);
        Assert.Equal("category_selected", recorded.EventName);
        Assert.Null(recorded.Mode);
    }

    [Fact]
    public async Task PublicDemo_RejectsArbitraryTelemetryPayloads()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/demo/events", new
        {
            sessionId = Guid.NewGuid(),
            businessType = "food_beverage",
            eventName = "contact_form_data",
            step = "my-phone-number"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PublicDemo_AllowsEventAfterAuthenticatedSwitch()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();
        var loginResponse = await client.PostAsync(
            "/api/demo/session?businessType=laundry", content: null);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.Token);

        var response = await client.PostAsJsonAsync("/api/demo/events", new
        {
            sessionId = Guid.NewGuid(),
            businessType = "laundry",
            eventName = "demo_started",
            mode = "guided"
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task PublicDemo_VisitorsHaveSeparateStock_AndCookiePreservesOwnTenant()
    {
        await using var factory = new DemoModeFactory();
        using var visitorA = factory.CreateClient();
        using var visitorB = factory.CreateClient();

        var responseA = await visitorA.PostAsync(
            "/api/demo/session?businessType=general_retail", null);
        var responseB = await visitorB.PostAsync(
            "/api/demo/session?businessType=general_retail", null);
        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);

        var loginA = await responseA.Content.ReadFromJsonAsync<LoginResponseDto>();
        var loginB = await responseB.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(loginA);
        Assert.NotNull(loginB);
        var tenantA = new JwtSecurityTokenHandler().ReadJwtToken(loginA.Token)
            .Claims.Single(x => x.Type == "tenant_id").Value;
        var tenantB = new JwtSecurityTokenHandler().ReadJwtToken(loginB.Token)
            .Claims.Single(x => x.Type == "tenant_id").Value;
        Assert.NotEqual(tenantA, tenantB);

        var cookie = Assert.Single(responseA.Headers.GetValues("Set-Cookie"))
            .Split(';')[0];
        Assert.StartsWith("nf_demo_visitor=", cookie);
        visitorA.DefaultRequestHeaders.Add("Cookie", cookie);
        visitorA.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginA.Token);
        visitorB.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginB.Token);

        var productsA = await visitorA.GetFromJsonAsync<List<ProductDto>>("/api/products");
        var productsB = await visitorB.GetFromJsonAsync<List<ProductDto>>("/api/products");
        var waterA = Assert.Single(productsA!, x => x.Kode == "GEN001");
        var waterB = Assert.Single(productsB!, x => x.Kode == "GEN001");
        Assert.NotEqual(waterA.Id, waterB.Id);
        Assert.Equal(80, waterA.Stok);
        Assert.Equal(80, waterB.Stok);

        var checkout = await visitorA.PostAsJsonAsync("/api/transactions", new CreateTransactionDto
        {
            Items = [new CreateTransactionItemDto
            {
                Id = waterA.Id, Nama = waterA.Nama, HargaJual = waterA.HargaJual,
                Qty = 1, Quantity = 1, Subtotal = waterA.HargaJual
            }],
            Subtotal = waterA.HargaJual,
            Total = waterA.HargaJual,
            MetodePembayaran = "tunai",
            Dibayar = waterA.HargaJual
        });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        var afterA = await visitorA.GetFromJsonAsync<ProductDto>($"/api/products/{waterA.Id}");
        var afterB = await visitorB.GetFromJsonAsync<ProductDto>($"/api/products/{waterB.Id}");
        Assert.Equal(79, afterA!.Stok);
        Assert.Equal(80, afterB!.Stok);

        var reenterA = await visitorA.PostAsync(
            "/api/demo/session?businessType=general_retail", null);
        Assert.Equal(HttpStatusCode.OK, reenterA.StatusCode);
        var repeat = await reenterA.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(repeat);
        var repeatTenant = new JwtSecurityTokenHandler().ReadJwtToken(repeat.Token)
            .Claims.Single(x => x.Type == "tenant_id").Value;
        Assert.Equal(tenantA, repeatTenant);
    }

    [Fact]
    public async Task PublicDemo_ExpiredVisitorTokenIsRejected_AndSessionStartsFresh()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();
        var firstResponse = await client.PostAsync(
            "/api/demo/session?businessType=general_retail", null);
        var first = await firstResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(first);
        var priorTenant = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(first.Token)
            .Claims.Single(x => x.Type == "tenant_id").Value);
        var cookie = Assert.Single(firstResponse.Headers.GetValues("Set-Cookie"))
            .Split(';')[0];
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", first.Token);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.DemoVisitorSessions.SingleAsync(x => x.TenantId == priorTenant);
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var expired = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        var renewedResponse = await client.PostAsync(
            "/api/demo/session?businessType=general_retail", null);
        Assert.Equal(HttpStatusCode.OK, renewedResponse.StatusCode);
        var renewed = await renewedResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(renewed);
        var newTenant = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(renewed.Token)
            .Claims.Single(x => x.Type == "tenant_id").Value);
        Assert.NotEqual(priorTenant, newTenant);
    }

    [Fact]
    public async Task PublicDemo_RejectsUnknownBusinessType()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/demo/session?businessType=unknown",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PublicDemo_RejectsDatabaseContainingNonDemoTenant()
    {
        await using var factory = new DemoModeFactory(
            seedForeignTenantBeforeStartup: true);

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var client = factory.CreateClient();
            await client.GetAsync("/api/products");
        });

        Assert.Contains(
            "isolated",
            exception.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DemoModeFactory(
        bool seedForeignTenantBeforeStartup = false)
        : WebApplicationFactory<Program>
    {
        private readonly string _databaseName =
            $"demo-mode-{Guid.NewGuid():N}";
        private readonly InMemoryDatabaseRoot _databaseRoot = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=test;Username=test;Password=test");
            builder.UseSetting(
                "Jwt:Key",
                "demo-mode-tenant-jwt-key-12345678901234567890");
            builder.UseSetting("Jwt:Issuer", "NeverfadePos.Demo.Test");
            builder.UseSetting("Jwt:Audience", "NeverfadePos.Demo.Client");
            builder.UseSetting(
                "PlatformJwt:Key",
                "demo-mode-platform-jwt-key-123456789012345678");
            builder.UseSetting("PlatformJwt:Issuer", "NeverfadePos.Platform.Demo.Test");
            builder.UseSetting("PlatformJwt:Audience", "NeverfadePos.Platform.Demo.Client");
            builder.UseSetting("Payments:Mode", "Disabled");
            builder.UseSetting("PlatformBootstrap:Enabled", "false");
            builder.UseSetting("DemoMode:Enabled", "true");
            builder.UseSetting("DemoMode:ResetIntervalMinutes", "180");

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=test;Username=test;Password=test",
                    ["Jwt:Key"] =
                        "demo-mode-tenant-jwt-key-12345678901234567890",
                    ["Jwt:Issuer"] = "NeverfadePos.Demo.Test",
                    ["Jwt:Audience"] = "NeverfadePos.Demo.Client",
                    ["PlatformJwt:Key"] =
                        "demo-mode-platform-jwt-key-123456789012345678",
                    ["PlatformJwt:Issuer"] = "NeverfadePos.Platform.Demo.Test",
                    ["PlatformJwt:Audience"] = "NeverfadePos.Platform.Demo.Client",
                    ["Payments:Mode"] = "Disabled",
                    ["PlatformBootstrap:Enabled"] = "false",
                    ["DemoMode:Enabled"] = "true",
                    ["DemoMode:ResetIntervalMinutes"] = "180"
                }));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<
                    IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options
                        .UseInMemoryDatabase(
                            _databaseName,
                            _databaseRoot)
                        .ConfigureWarnings(warnings =>
                            warnings.Ignore(
                                InMemoryEventId.TransactionIgnoredWarning)));

                if (seedForeignTenantBeforeStartup)
                {
                    using var provider = services.BuildServiceProvider();
                    using var scope = provider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Tenants.Add(new NeverfadePos.Api.Entities.Tenant
                    {
                        NamaToko = "Foreign Tenant",
                        Slug = "foreign-tenant"
                    });
                    db.SaveChanges();
                }
            });
        }
    }
}

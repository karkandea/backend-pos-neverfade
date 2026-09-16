using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using NeverfadePos.Api.DTOs.Transaction;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class DemoModeTests
{
    private const string DemoPassword =
        "neverfade-public-demo-password-2026";

    [Fact]
    public async Task PublicDemo_AllowsCashTransaction_ButBlocksMasterMutation()
    {
        await using var factory = new DemoModeFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                username = "demo",
                password = DemoPassword
            });

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

        var pants = Assert.Single(
            products.Where(x => x.Kode == "RTL003"));
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
            builder.UseSetting("DemoMode:Password", DemoPassword);
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
                    ["DemoMode:Password"] = DemoPassword,
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

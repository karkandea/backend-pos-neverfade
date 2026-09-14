using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Product;
using NeverfadePos.Api.DTOs.Retail;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Auth;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed partial class AdvancedRetailApiTests
{    private const string TenantKey =
        "advanced-retail-test-key-123456789012345678901234";
    private const string PlatformKey =
        "advanced-retail-platform-key-1234567890123456789012";
    private const string TenantIssuer = "NeverfadePos.AdvancedRetail.Test";
    private const string TenantAudience = "NeverfadePos.AdvancedRetail.Client";
    private const string PlatformIssuer = "NeverfadePos.Platform.AdvancedRetail.Test";
    private const string PlatformAudience = "NeverfadePos.Platform.AdvancedRetail.Client";
    private const string TestConnectionString =
        "Host=localhost;Database=test;Username=test;Password=test";

    [Fact]
    public async Task GeneralRetail_CannotAccessAdvancedRetailCatalog()
    {
        await using var factory = new AdvancedRetailFactory();
        using var owner = await AuthClientAsync(factory, "owner");

        var response = await owner.GetAsync("/api/retail/catalog");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(
            "CAPABILITY_NOT_ENABLED",
            await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Kasir_CanReadCatalog_ButCannotMutateVariants()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        using var kasir = await AuthClientAsync(factory, "kasir");
        var product = await CreateProductAsync(owner, "TSHIRT-ROLE");

        var catalog = await kasir.GetAsync("/api/retail/catalog");
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);

        var mutate = await kasir.PostAsJsonAsync(
            "/api/retail/variants",
            NewVariant(product.Id, "TSHIRT-ROLE-BLK-M", "Black / M", 5));

        Assert.Equal(HttpStatusCode.Forbidden, mutate.StatusCode);
    }

    [Fact]
    public async Task CashCheckout_AppliesAutomaticWholesale_AndDecrementsVariantExactlyOnce()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var setup = await CreatePricedVariantAsync(owner, "TSHIRT-AUTO");
        var response = await PostCashAsync(
            owner,
            setup.Product.Id,
            setup.Variant.Id,
            null,
            6,
            80m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.NotNull(transaction);
        var item = Assert.Single(transaction!.Items);
        Assert.Equal(80m, item.HargaJual);
        Assert.Equal(100m, item.BasePrice);
        Assert.Equal("Grosir", item.PriceLevelName);
        Assert.Equal(setup.Variant.Id, item.ProductVariantId);
        Assert.Equal(setup.Variant.Sku, item.VariantSku);

        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 4, 4);
    }

    [Fact]
    public async Task CashCheckout_AllowsConfiguredManualTierBelowThreshold()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var setup = await CreatePricedVariantAsync(owner, "TSHIRT-MANUAL");
        var response = await PostCashAsync(
            owner,
            setup.Product.Id,
            setup.Variant.Id,
            setup.Level.Id,
            1,
            80m);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var transaction = await response.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.NotNull(transaction);
        var item = Assert.Single(transaction!.Items);
        Assert.Equal(80m, item.HargaJual);
        Assert.Equal("Grosir", item.PriceLevelName);
        Assert.Equal(setup.Level.Id, item.PriceLevelId);
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 9, 9);
    }

    [Fact]
    public async Task CashCheckout_RejectsSpoofedPrice()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var setup = await CreatePricedVariantAsync(owner, "TSHIRT-SPOOF");

        var response = await PostCashAsync(
            owner, setup.Product.Id, setup.Variant.Id, null, 6, 1m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 10, 10);
    }
    [Fact]
    public async Task CashCheckout_RequiresVariant_WhenProductHasActiveVariants()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var setup = await CreatePricedVariantAsync(owner, "TSHIRT-REQ");

        var response = await PostCashAsync(
            owner, setup.Product.Id, null, null, 1, 100m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "PRODUCT_VARIANT_REQUIRED",
            await response.Content.ReadAsStringAsync());
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 10, 10);
    }

    private static async Task EnableFashionRetailAsync(AdvancedRetailFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = await db.Tenants.SingleAsync(x => x.Slug == "warung-lumpia-beef");
        tenant.BusinessType = BusinessTypes.FashionRetail;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
    private static async Task<HttpClient> AuthClientAsync(
        AdvancedRetailFactory factory,
        string username)
    {
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "advanced-retail-test-auth");
        var user = await db.Users.SingleAsync(x => x.Username == username);
        var token = scope.ServiceProvider
            .GetRequiredService<IJwtService>()
            .GenerateToken(user);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<ProductDto> CreateProductAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync(
            "/api/products",
            new CreateProductDto
            {
                Kode = code,
                Barcode = $"BAR-{code}",
                Nama = code,
                Kategori = "Fashion",
                HargaModal = 50m,
                HargaJual = 100m,
                Stok = 0,
                Supplier = "QA",
                Satuan = "pcs",
                Deskripsi = "Advanced retail QA",
                Type = ProductTypes.Goods,
                TracksStock = true,
                QuantityPrecision = 0
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private static CreateProductVariantDto NewVariant(
        Guid productId, string sku, string label, int stock) => new()
    {
        ProductId = productId,
        Sku = sku,
        Barcode = $"BC-{sku}",
        Label = label,
        Option1Name = "Color",
        Option1Value = "Black",
        Option2Name = "Size",
        Option2Value = "M",
        HargaModal = 50m,
        HargaJual = 100m,
        Stok = stock
    };

    private static async Task<(ProductDto Product, ProductVariantDto Variant, PriceLevelDto Level)>
        CreatePricedVariantAsync(HttpClient client, string code)
    {
        var product = await CreateProductAsync(client, code);
        var variantResponse = await client.PostAsJsonAsync(
            "/api/retail/variants",
            NewVariant(product.Id, $"{code}-BLK-M", "Black / M", 10));
        Assert.Equal(HttpStatusCode.OK, variantResponse.StatusCode);
        var variant = (await variantResponse.Content
            .ReadFromJsonAsync<ProductVariantDto>())!;

        var levelResponse = await client.PostAsJsonAsync(
            "/api/retail/price-levels",
            new CreatePriceLevelDto
            {
                Code = $"grosir-{code.ToLowerInvariant()}",
                Name = "Grosir",
                SortOrder = 10
            });
        Assert.Equal(HttpStatusCode.OK, levelResponse.StatusCode);
        var level = (await levelResponse.Content
            .ReadFromJsonAsync<PriceLevelDto>())!;

        var priceResponse = await client.PostAsJsonAsync(
            "/api/retail/prices",
            new CreateProductPriceDto
            {
                ProductId = product.Id,
                ProductVariantId = variant.Id,
                PriceLevelId = level.Id,
                MinQuantity = 6m,
                UnitPrice = 80m
            });
        Assert.Equal(HttpStatusCode.OK, priceResponse.StatusCode);

        return (product, variant, level);
    }

    private static async Task<HttpResponseMessage> PostCashAsync(
        HttpClient client,
        Guid productId,
        Guid? variantId,
        Guid? priceLevelId,
        int quantity,
        decimal unitPrice)
    {
        var subtotal = unitPrice * quantity;
        return await client.PostAsJsonAsync(
            "/api/transactions",
            new
            {
                customerId = (Guid?)null,
                items = new[]
                {
                    new
                    {
                        id = productId,
                        nama = "client-label-ignored",
                        hargaJual = unitPrice,
                        productVariantId = variantId,
                        priceLevelId,
                        qty = quantity,
                        quantity = (decimal)quantity,
                        subtotal
                    }
                },
                subtotal,
                disc = 0m,
                tax = 0m,
                discAmt = 0m,
                taxAmt = 0m,
                total = subtotal,
                metodePembayaran = "tunai",
                dibayar = subtotal,
                kembalian = 0m
            });
    }
    private static async Task AssertStockAsync(
        AdvancedRetailFactory factory,
        Guid productId,
        Guid variantId,
        int expectedProductStock,
        int expectedVariantStock)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();
        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "advanced-retail-stock-assert");

        var product = await db.Products.SingleAsync(x => x.Id == productId);
        var variant = await db.ProductVariants.SingleAsync(x => x.Id == variantId);
        Assert.Equal(expectedProductStock, product.Stok);
        Assert.Equal(expectedVariantStock, variant.Stok);
    }

    private sealed class AdvancedRetailFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName =
            $"advanced-retail-{Guid.NewGuid():N}";
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            var config = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestConnectionString,
                ["Jwt:Key"] = TenantKey,
                ["Jwt:Issuer"] = TenantIssuer,
                ["Jwt:Audience"] = TenantAudience,
                ["PlatformJwt:Key"] = PlatformKey,
                ["PlatformJwt:Issuer"] = PlatformIssuer,
                ["PlatformJwt:Audience"] = PlatformAudience,
                ["Payments:Mode"] = "Disabled",
                ["PlatformBootstrap:Enabled"] = "false"
            };

            foreach (var item in config)
                builder.UseSetting(item.Key, item.Value);

            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(config));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<
                    IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options =>
                    options
                        .UseInMemoryDatabase(_databaseName)
                        .ConfigureWarnings(warnings =>
                            warnings.Ignore(
                                InMemoryEventId.TransactionIgnoredWarning)));
            });
        }
    }
}

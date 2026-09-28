using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Outlet;
using NeverfadePos.Api.DTOs.Sales;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed partial class AdvancedRetailApiTests
{
    [Fact]
    public async Task Quote_ComputesServerWholesaleSnapshot_WithoutSellingOrReservingStock()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-QUOTE-WHOLESALE");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var result = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        {
            outletId = outlet.Id,
            lines = new[] { new { productId = priced.Product.Id, variantId = priced.Variant.Id, quantity = 6m } },
            subtotal = 0m, total = 0m, // malicious client prices are not accepted as an input
        });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var response = (await result.Content.ReadFromJsonAsync<SaleQuoteResponseDto>())!;
        Assert.False(string.IsNullOrWhiteSpace(response.Meta.CorrelationId));
        var quote = response.Data;
        Assert.NotEqual(Guid.Empty, quote.QuoteId);
        Assert.NotEqual(Guid.Empty, quote.QuoteVersion);
        Assert.Equal(outlet.Id, quote.OutletId);
        Assert.Equal(480m, quote.Subtotal);
        Assert.Equal(480m, quote.Total);
        Assert.Equal(0m, quote.Discount);
        Assert.Equal(0m, quote.ServiceCharge);
        Assert.False(quote.StockReserved);
        Assert.Equal("quoted", quote.Status);
        Assert.Equal("Grosir", Assert.Single(quote.Lines).PriceLevelName);
        Assert.Equal(80m, quote.Lines[0].UnitPrice);
        Assert.Equal(6m, quote.Lines[0].Quantity);
        var read = await owner.GetFromJsonAsync<SaleQuoteResponseDto>($"/api/v2/sales/quotes/{quote.QuoteId}");
        Assert.Equal(quote.QuoteVersion, read!.Data.QuoteVersion);
        Assert.Equal(480m, read.Data.Total);
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 10, 10);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "s2-quote-assert");
        Assert.Equal(1, await db.SaleQuotes.CountAsync());
        Assert.Equal(0, await db.Transactions.CountAsync());
    }

    [Fact]
    public async Task Quote_OutletMismatchAndRestrictedOperatorCannotIssue()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        var another = await owner.PostAsJsonAsync("/api/outlets", new
        { code = "S2-Q-B", name = "S2 Branch", address = "", phone = "", isDefault = false });
        Assert.Equal(HttpStatusCode.OK, another.StatusCode);
        var branch = (await another.Content.ReadFromJsonAsync<OutletDto>())!;
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var mismatch = await owner.PostAsJsonAsync("/api/v2/sales/quotes",
            new { outletId = branch.Id, lines = new[] { new { productId = Guid.NewGuid(), quantity = 1m } } });
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Contains("QUOTE_OUTLET_MISMATCH", await mismatch.Content.ReadAsStringAsync());
        using var cashier = await AuthClientAsync(factory, "kasir");
        cashier.DefaultRequestHeaders.Add("X-Outlet-Id", branch.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cashier.GetAsync($"/api/v2/sales/quotes/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Quote_DuplicateLinesCannotExceedCombinedStock()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-QUOTE-REPEAT");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var response = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        {
            outletId = outlet.Id,
            lines = new[]
            {
                new { productId = priced.Product.Id, variantId = priced.Variant.Id, quantity = 6m },
                new { productId = priced.Product.Id, variantId = priced.Variant.Id, quantity = 6m }
            }
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("QUOTE_STOCK_INSUFFICIENT", await response.Content.ReadAsStringAsync());
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 10, 10);
    }

    [Fact]
    public async Task Quote_UnknownDiscountAndInvalidQuantitiesFailClosed()
    {
        await using var factory = new AdvancedRetailFactory();
        using var owner = await AuthClientAsync(factory, "owner");
        var product = await CreateProductAsync(owner, "S2-QUOTE-VALIDATE");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var unsupported = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        { outletId = outlet.Id, discountCode = "FAKE", lines = new[] { new { productId = product.Id, quantity = 1m } } });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupported.StatusCode);
        var fraction = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        { outletId = outlet.Id, lines = new[] { new { productId = product.Id, quantity = 1.5m } } });
        Assert.Equal(HttpStatusCode.BadRequest, fraction.StatusCode);
    }
    [Fact]
    public async Task Quote_ManualDiscountAndConfiguredTaxAreComputedByServerAndSnapshotIsImmutable()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-QUOTE-DISCOUNT");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
            using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
                .Begin(tenantId, "s2-tax-setup");
            var settings = await db.Settings.SingleAsync();
            settings.ShowTax = true;
            settings.DefaultTax = 10m;
            await db.SaveChangesAsync();
        }
        var result = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        {
            outletId = outlet.Id,
            discountPercent = 10m,
            lines = new[] { new { productId = priced.Product.Id, variantId = priced.Variant.Id, quantity = 6m } },
            total = 1m, tax = 0m // client monetary totals are not trusted inputs
        });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var original = (await result.Content.ReadFromJsonAsync<SaleQuoteResponseDto>())!.Data;
        Assert.Equal(480m, original.Subtotal);
        Assert.Equal(48m, original.Discount);
        Assert.Equal(10m, original.DiscountPercent);
        Assert.Equal(10m, original.TaxRatePercent);
        Assert.Equal(43.20m, original.Tax);
        Assert.Equal(475.20m, original.Total);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
            using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
                .Begin(tenantId, "s2-quote-stale-pricing");
            var price = await db.ProductPrices.SingleAsync(x => x.ProductId == priced.Product.Id);
            price.UnitPrice = 90m;
            var quote = await db.SaleQuotes.SingleAsync(x => x.Id == original.QuoteId);
            quote.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        var response = await owner.GetFromJsonAsync<SaleQuoteResponseDto>(
            $"/api/v2/sales/quotes/{original.QuoteId}");
        Assert.Equal(original.QuoteVersion, response!.Data.QuoteVersion);
        Assert.Equal(80m, response.Data.Lines[0].UnitPrice);
        Assert.Equal(475.20m, response.Data.Total);
        Assert.Equal("expired", response.Data.Status);
        Assert.False(response.Data.StockReserved);
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 10, 10);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Quote_DiscountOutsidePolicyIsRejected(int rate)
    {
        await using var factory = new AdvancedRetailFactory();
        using var owner = await AuthClientAsync(factory, "owner");
        var product = await CreateProductAsync(owner, $"S2-QUOTE-DISC-{rate}");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var response = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        {
            outletId = outlet.Id, discountPercent = rate,
            lines = new[] { new { productId = product.Id, quantity = 1m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

}

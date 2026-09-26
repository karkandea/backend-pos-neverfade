using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Outlet;
using NeverfadePos.Api.DTOs.Sales;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed partial class AdvancedRetailApiTests
{
    [Fact]
    public async Task CashCommit_ConsumesQuoteOnce_AndReplaysWithoutSecondSaleOrStockMutation()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-CASH-REPLAY");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var quoteResponse = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        {
            outletId = outlet.Id,
            lines = new[] { new { productId = priced.Product.Id, variantId = priced.Variant.Id,
                quantity = 6m, note = "Test line note" } }
        });
        Assert.Equal(HttpStatusCode.OK, quoteResponse.StatusCode);
        var quote = (await quoteResponse.Content.ReadFromJsonAsync<SaleQuoteResponseDto>())!.Data;
        Assert.Equal("Test line note", Assert.Single(quote.Lines).Note);
        var key = Guid.NewGuid().ToString("N");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var input = new { quoteId = quote.QuoteId, quoteVersion = quote.QuoteVersion,
            amountReceived = quote.Total + 20m };
        var first = await owner.PostAsJsonAsync("/api/v2/sales/cash", input);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = (await first.Content.ReadFromJsonAsync<CashSaleResponseDto>())!;
        Assert.False(original.Meta.Replayed);
        var read = await owner.GetAsync($"/api/v2/sales/cash/idempotency/{key}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("no-store", read.Headers.CacheControl?.ToString());
        var recovered = (await read.Content.ReadFromJsonAsync<CashSaleResponseDto>())!;
        Assert.True(recovered.Meta.Replayed);
        Assert.Equal(original.Data.Id, recovered.Data.Id);
        Assert.Equal(original.Data.NoTrx, recovered.Data.NoTrx);
        Assert.Equal(quote.QuoteId, original.Meta.QuoteId);
        Assert.Equal("paid", original.Data.Status);
        Assert.Equal("tunai", original.Data.MetodePembayaran);
        Assert.Equal(quote.Total, original.Data.Total);
        Assert.Equal(20m, original.Data.Kembalian);
        Assert.Equal("Test line note", Assert.Single(original.Data.Items).Note);
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 4, 4);
        var repeat = await owner.PostAsJsonAsync("/api/v2/sales/cash", input);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        var replay = (await repeat.Content.ReadFromJsonAsync<CashSaleResponseDto>())!;
        Assert.True(replay.Meta.Replayed);
        Assert.Equal(original.Data.Id, replay.Data.Id);
        Assert.Equal(original.Data.NoTrx, replay.Data.NoTrx);
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 4, 4);
        var conflicting = await owner.PostAsJsonAsync("/api/v2/sales/cash", new
        { quoteId = quote.QuoteId, quoteVersion = quote.QuoteVersion,
            amountReceived = quote.Total + 21m });
        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_REUSED", await conflicting.Content.ReadAsStringAsync());
        owner.DefaultRequestHeaders.Remove("Idempotency-Key");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var secondKey = await owner.PostAsJsonAsync("/api/v2/sales/cash", input);
        Assert.Equal(HttpStatusCode.Conflict, secondKey.StatusCode);
        Assert.Contains("QUOTE_ALREADY_CONSUMED", await secondKey.Content.ReadAsStringAsync());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "s2-cash-idempotency-assert");
        Assert.Single(await db.Transactions.ToListAsync());
        Assert.Single(await db.StockHistories.Where(x => x.Tipe == "transaksi").ToListAsync());
        var stored = await db.SaleQuotes.SingleAsync(x => x.Id == quote.QuoteId);
        Assert.Equal("consumed", stored.Status);
        Assert.Equal(original.Data.Id, stored.ConsumedTransactionId);
    }

    [Fact]
    public async Task CashCommitRecovery_UnknownOrInvalidKeyCannotClaimSuccess_AndOtherOutletIsIsolated()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-CASH-STATUS");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var key = Guid.NewGuid().ToString("N");
        var path = $"/api/v2/sales/cash/idempotency/{key}";
        var unknown = await owner.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains("CASH_COMMIT_NOT_CONFIRMED", await unknown.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await owner.GetAsync("/api/v2/sales/cash/idempotency/invalid")).StatusCode);
        var quoteResponse = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        { outletId = outlet.Id, lines = new[] { new { productId = priced.Product.Id,
            variantId = priced.Variant.Id, quantity = 1m } } });
        Assert.Equal(HttpStatusCode.OK, quoteResponse.StatusCode);
        var quote = (await quoteResponse.Content.ReadFromJsonAsync<SaleQuoteResponseDto>())!.Data;
        owner.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var committed = await owner.PostAsJsonAsync("/api/v2/sales/cash", new
        { quoteId = quote.QuoteId, quoteVersion = quote.QuoteVersion, amountReceived = quote.Total });
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var original = (await committed.Content.ReadFromJsonAsync<CashSaleResponseDto>())!;
        var branchResponse = await owner.PostAsJsonAsync("/api/outlets", new
        { code = "S2-CASH-OTHER", name = "Another outlet", address = "QA", phone = "", isDefault = false });
        Assert.Equal(HttpStatusCode.OK, branchResponse.StatusCode);
        var branch = (await branchResponse.Content.ReadFromJsonAsync<OutletDto>())!;
        owner.DefaultRequestHeaders.Remove("X-Outlet-Id");
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", branch.Id.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(path)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-Outlet-Id");
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var recovered = (await owner.GetFromJsonAsync<CashSaleResponseDto>(path))!;
        Assert.Equal(original.Data.Id, recovered.Data.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "s2-read-only-recovery");
        Assert.Single(await db.Transactions.ToListAsync());
        Assert.Single(await db.StockHistories.Where(x => x.Tipe == "transaksi").ToListAsync());
    }

    [Fact]
    public async Task CashCommit_StaleStockAndMissingKeyCannotMutateSale()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-CASH-STOCK");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        async Task<SaleQuoteDto> CreateQuote()
        {
            var result = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
            { outletId = outlet.Id,
                lines = new[] { new { productId = priced.Product.Id,
                    variantId = priced.Variant.Id, quantity = 6m } } });
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            return (await result.Content.ReadFromJsonAsync<SaleQuoteResponseDto>())!.Data;
        }
        var firstQuote = await CreateQuote();
        var staleQuote = await CreateQuote();
        var noKey = await owner.PostAsJsonAsync("/api/v2/sales/cash", new
        { quoteId = firstQuote.QuoteId, quoteVersion = firstQuote.QuoteVersion,
            amountReceived = firstQuote.Total });
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Contains("IDEMPOTENCY_KEY_REQUIRED", await noKey.Content.ReadAsStringAsync());
        owner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var paid = await owner.PostAsJsonAsync("/api/v2/sales/cash", new
        { quoteId = firstQuote.QuoteId, quoteVersion = firstQuote.QuoteVersion,
            amountReceived = firstQuote.Total });
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        var stale = await owner.PostAsJsonAsync("/api/v2/sales/cash", new
        { quoteId = staleQuote.QuoteId, quoteVersion = staleQuote.QuoteVersion,
            amountReceived = staleQuote.Total });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 4, 4);
    }
}

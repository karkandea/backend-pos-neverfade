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
    public async Task PreparedCash_CrossSessionRecoveryUsesSameKeyBeforeAnySale_AndCommitsExactlyOnce()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-CASH-PREPARED");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var quoteResponse = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        {
            outletId = outlet.Id,
            lines = new[] { new { productId = priced.Product.Id,
                variantId = priced.Variant.Id, quantity = 2m } }
        });
        Assert.Equal(HttpStatusCode.OK, quoteResponse.StatusCode);
        var quote = (await quoteResponse.Content.ReadFromJsonAsync<SaleQuoteResponseDto>())!.Data;
        var key = Guid.NewGuid().ToString("N");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var input = new { quoteId = quote.QuoteId,
            quoteVersion = quote.QuoteVersion, amountReceived = quote.Total + 5m };
        var preparedResponse = await owner.PostAsJsonAsync("/api/v2/sales/cash/prepare", input);
        Assert.Equal(HttpStatusCode.OK, preparedResponse.StatusCode);
        var prepared = (await preparedResponse.Content.ReadFromJsonAsync<PreparedCashSaleDto>())!;
        Assert.Equal("prepared", prepared.Status);
        Assert.Equal(key, prepared.IdempotencyKey);
        Assert.Equal(input.amountReceived, prepared.AmountReceived);
        Assert.Null(prepared.TransactionId);
        Assert.Equal(quote.QuoteId, prepared.QuoteId);
        var repeat = await owner.PostAsJsonAsync("/api/v2/sales/cash/prepare", input);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(prepared.PreparedAt,
            (await repeat.Content.ReadFromJsonAsync<PreparedCashSaleDto>())!.PreparedAt);

        // A fresh authenticated client/device can read the persisted attempt.
        using var otherDevice = await AuthClientAsync(factory, "owner");
        otherDevice.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var recovered = (await otherDevice.GetFromJsonAsync<PreparedCashSaleDto>(
            "/api/v2/sales/cash/current"))!;
        Assert.Equal(prepared.IdempotencyKey, recovered.IdempotencyKey);
        Assert.Equal(prepared.AmountReceived, recovered.AmountReceived);
        Assert.Equal(prepared.QuoteVersion, recovered.QuoteVersion);
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 10, 10);

        owner.DefaultRequestHeaders.Remove("Idempotency-Key");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var wrongKey = await owner.PostAsJsonAsync("/api/v2/sales/cash", input);
        Assert.Equal(HttpStatusCode.Conflict, wrongKey.StatusCode);
        owner.DefaultRequestHeaders.Remove("Idempotency-Key");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var paidResponse = await owner.PostAsJsonAsync("/api/v2/sales/cash", input);
        Assert.Equal(HttpStatusCode.OK, paidResponse.StatusCode);
        var paid = (await paidResponse.Content.ReadFromJsonAsync<CashSaleResponseDto>())!;
        Assert.Equal("paid", paid.Data.Status);
        Assert.Equal(5m, paid.Data.Kembalian);
        Assert.Equal(HttpStatusCode.NoContent,
            (await otherDevice.GetAsync("/api/v2/sales/cash/current")).StatusCode);
        var lookup = (await otherDevice.GetFromJsonAsync<CashSaleResponseDto>(
            $"/api/v2/sales/cash/idempotency/{key}"))!;
        Assert.True(lookup.Meta.Replayed);
        Assert.Equal(paid.Data.Id, lookup.Data.Id);
        var duplicate = (await owner.PostAsJsonAsync("/api/v2/sales/cash", input));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(paid.Data.Id,
            (await duplicate.Content.ReadFromJsonAsync<CashSaleResponseDto>())!.Data.Id);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash/abandon", input)).StatusCode);
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 8, 8);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "s2-prepared-cash-verify");
        Assert.Single(await db.Transactions.ToListAsync());
        Assert.Single(await db.StockHistories.Where(x => x.Tipe == "transaksi").ToListAsync());
        var stored = await db.SaleQuotes.SingleAsync(x => x.Id == quote.QuoteId);
        Assert.Equal("consumed", stored.Status);
        Assert.NotNull(stored.PreparedAt);
        Assert.Equal(input.amountReceived, stored.PreparedAmountReceived);
    }

    [Fact]
    public async Task PreparedCash_AbandonBeforeCommitBlocksDelayedPostAndUnlocksNextQuote()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-CASH-ABANDON");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        async Task<SaleQuoteDto> Quote()
        {
            var response = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
            { outletId = outlet.Id, lines = new[] { new { productId = priced.Product.Id,
                variantId = priced.Variant.Id, quantity = 1m } } });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<SaleQuoteResponseDto>())!.Data;
        }
        var first = await Quote();
        var second = await Quote();
        var key = Guid.NewGuid().ToString("N");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var input = new { quoteId = first.QuoteId,
            quoteVersion = first.QuoteVersion, amountReceived = first.Total };
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash/prepare", input)).StatusCode);
        var otherKey = Guid.NewGuid().ToString("N");
        owner.DefaultRequestHeaders.Remove("Idempotency-Key");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", otherKey);
        var otherInput = new { quoteId = second.QuoteId,
            quoteVersion = second.QuoteVersion, amountReceived = second.Total };
        var bypass = await owner.PostAsJsonAsync("/api/v2/sales/cash", otherInput);
        Assert.Equal(HttpStatusCode.Conflict, bypass.StatusCode);
        Assert.Contains("CASH_ATTEMPT_ALREADY_PREPARED", await bypass.Content.ReadAsStringAsync());
        var concurrent = await owner.PostAsJsonAsync("/api/v2/sales/cash/prepare", otherInput);
        Assert.Equal(HttpStatusCode.Conflict, concurrent.StatusCode);
        Assert.Contains("CASH_ATTEMPT_ALREADY_PREPARED", await concurrent.Content.ReadAsStringAsync());
        owner.DefaultRequestHeaders.Remove("Idempotency-Key");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var abandoned = await owner.PostAsJsonAsync("/api/v2/sales/cash/abandon", input);
        Assert.Equal(HttpStatusCode.OK, abandoned.StatusCode);
        Assert.Equal("abandoned",
            (await abandoned.Content.ReadFromJsonAsync<PreparedCashSaleDto>())!.Status);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.GetAsync("/api/v2/sales/cash/current")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash", input)).StatusCode);
        owner.DefaultRequestHeaders.Remove("Idempotency-Key");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", otherKey);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash/prepare", new
            { quoteId = second.QuoteId, quoteVersion = second.QuoteVersion,
                amountReceived = second.Total })).StatusCode);
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 10, 10);
    }

    [Fact]
    public async Task PreparedCash_ClosingUnpersistedQuoteFencesLatePrepareAndCommit()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-CASH-EARLY-CLOSE");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var response = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        { outletId = outlet.Id, lines = new[] { new { productId = priced.Product.Id,
            variantId = priced.Variant.Id, quantity = 1m } } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = (await response.Content.ReadFromJsonAsync<SaleQuoteResponseDto>())!.Data;
        var key = Guid.NewGuid().ToString("N");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var input = new { quoteId = quote.QuoteId, quoteVersion = quote.QuoteVersion,
            amountReceived = quote.Total };
        var closedResponse = await owner.PostAsJsonAsync("/api/v2/sales/cash/abandon", input);
        Assert.Equal(HttpStatusCode.OK, closedResponse.StatusCode);
        Assert.Equal("abandoned", (await closedResponse.Content.ReadFromJsonAsync<PreparedCashSaleDto>())!.Status);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash/prepare", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash", input)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.GetAsync("/api/v2/sales/cash/current")).StatusCode);
        await AssertStockAsync(factory, priced.Product.Id, priced.Variant.Id, 10, 10);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants.Select(x => x.Id).SingleAsync();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "s2-cash-unprepared-fence");
        Assert.Empty(await db.Transactions.ToListAsync());
        Assert.Empty(await db.StockHistories.Where(x => x.Tipe == "transaksi").ToListAsync());
        var stored = await db.SaleQuotes.SingleAsync(x => x.Id == quote.QuoteId);
        Assert.Equal("abandoned", stored.Status);
        Assert.Equal(key, stored.IdempotencyKey);
        Assert.Equal(input.amountReceived, stored.PreparedAmountReceived);
    }

    [Fact]
    public async Task PreparedCash_IsolatedByOutletAndUser_AndRejectsAmountChanges()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var priced = await CreatePricedVariantAsync(owner, "S2-CASH-SECURITY");
        var outlet = Assert.Single((await owner.GetFromJsonAsync<List<OutletDto>>("/api/outlets"))!);
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        var qResponse = await owner.PostAsJsonAsync("/api/v2/sales/quotes", new
        { outletId = outlet.Id, lines = new[] { new { productId = priced.Product.Id,
            variantId = priced.Variant.Id, quantity = 1m } } });
        Assert.Equal(HttpStatusCode.OK, qResponse.StatusCode);
        var quote = (await qResponse.Content.ReadFromJsonAsync<SaleQuoteResponseDto>())!.Data;
        var key = Guid.NewGuid().ToString("N");
        owner.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var input = new { quoteId = quote.QuoteId,
            quoteVersion = quote.QuoteVersion, amountReceived = quote.Total };
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash/prepare", new
            { quoteId = quote.QuoteId, quoteVersion = quote.QuoteVersion,
                amountReceived = quote.Total + 0.001m })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash/prepare", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash/prepare", new
            { quoteId = quote.QuoteId, quoteVersion = quote.QuoteVersion,
                amountReceived = quote.Total + 1m })).StatusCode);
        using var cashier = await AuthClientAsync(factory, "kasir");
        cashier.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        Assert.Equal(HttpStatusCode.NoContent,
            (await cashier.GetAsync("/api/v2/sales/cash/current")).StatusCode);
        var otherOutlet = await owner.PostAsJsonAsync("/api/outlets", new
        { code = "S2-PREP-OTHER", name = "Other outlet", address = "QA", phone = "", isDefault = false });
        Assert.Equal(HttpStatusCode.OK, otherOutlet.StatusCode);
        var branch = (await otherOutlet.Content.ReadFromJsonAsync<OutletDto>())!;
        owner.DefaultRequestHeaders.Remove("X-Outlet-Id");
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", branch.Id.ToString());
        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.GetAsync("/api/v2/sales/cash/current")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await owner.PostAsJsonAsync("/api/v2/sales/cash/abandon", input)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-Outlet-Id");
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", outlet.Id.ToString());
        Assert.Equal(HttpStatusCode.OK,
            (await owner.GetAsync("/api/v2/sales/cash/current")).StatusCode);
    }
}

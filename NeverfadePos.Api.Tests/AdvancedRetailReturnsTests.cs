using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Retail;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed partial class AdvancedRetailApiTests
{
    [Fact]
    public async Task GeneralRetail_CannotAccessReturns()
    {
        await using var factory = new AdvancedRetailFactory();
        using var owner = await AuthClientAsync(factory, "owner");

        var response = await owner.GetAsync($"/api/retail/returns?transactionId={Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("CAPABILITY_NOT_ENABLED", await response.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Return_RestocksAndIsIdempotent()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var setup = await CreatePricedVariantAsync(owner, "TSHIRT-RET");

        var saleResponse = await PostCashAsync(
            owner, setup.Product.Id, setup.Variant.Id, setup.Level.Id, 6, 80m);
        var sale = (await saleResponse.Content.ReadFromJsonAsync<TransactionDto>())!;
        var saleItem = Assert.Single(sale.Items);
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 4, 4);

        var request = NewReturnRequest(sale.Id, saleItem.TransactionItemId, 2, true, "return-restock-1");
        var response = await owner.PostAsJsonAsync("/api/retail/returns", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<RetailReturnDto>())!;

        Assert.Equal(160m, result.RefundAmount);
        Assert.Equal(2, Assert.Single(result.Items).Quantity);
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 6, 6);

        var duplicate = await owner.PostAsJsonAsync("/api/retail/returns", request);
        var duplicateResult = (await duplicate.Content.ReadFromJsonAsync<RetailReturnDto>())!;
        Assert.Equal(result.Id, duplicateResult.Id);
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 6, 6);
    }
    [Fact]
    public async Task Return_RejectsCumulativeOverReturnWithoutExtraStock()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var setup = await CreatePricedVariantAsync(owner, "TSHIRT-OVER");

        var saleResponse = await PostCashAsync(
            owner, setup.Product.Id, setup.Variant.Id, setup.Level.Id, 6, 80m);
        var sale = (await saleResponse.Content.ReadFromJsonAsync<TransactionDto>())!;
        var item = Assert.Single(sale.Items);

        var first = await owner.PostAsJsonAsync(
            "/api/retail/returns",
            NewReturnRequest(sale.Id, item.TransactionItemId, 4, true, "return-over-1"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 8, 8);

        var second = await owner.PostAsJsonAsync(
            "/api/retail/returns",
            NewReturnRequest(sale.Id, item.TransactionItemId, 3, true, "return-over-2"));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("melebihi sisa quantity", await second.Content.ReadAsStringAsync());
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 8, 8);
    }
    [Fact]
    public async Task Return_WithRestockFalse_LeavesStockUnchanged()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var setup = await CreatePricedVariantAsync(owner, "TSHIRT-NORESTOCK");

        var saleResponse = await PostCashAsync(
            owner, setup.Product.Id, setup.Variant.Id, setup.Level.Id, 1, 80m);
        var sale = (await saleResponse.Content.ReadFromJsonAsync<TransactionDto>())!;
        var item = Assert.Single(sale.Items);
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 9, 9);

        var response = await owner.PostAsJsonAsync(
            "/api/retail/returns",
            NewReturnRequest(sale.Id, item.TransactionItemId, 1, false, "return-damaged-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertStockAsync(factory, setup.Product.Id, setup.Variant.Id, 9, 9);
    }
    [Fact]
    public async Task Exchange_RejectsRestockFalse()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var setup = await CreatePricedVariantAsync(owner, "TSHIRT-EXC-NORESTOCK");

        var replacementResponse = await owner.PostAsJsonAsync(
            "/api/retail/variants",
            NewVariant(setup.Product.Id, "TSHIRT-EXC-NORESTOCK-L", "Black / L", 5));
        var replacement = (await replacementResponse.Content.ReadFromJsonAsync<ProductVariantDto>())!;

        var saleResponse = await PostCashAsync(
            owner, setup.Product.Id, setup.Variant.Id, null, 2, 100m);
        var sale = (await saleResponse.Content.ReadFromJsonAsync<TransactionDto>())!;
        var item = Assert.Single(sale.Items);

        var response = await owner.PostAsJsonAsync(
            "/api/retail/returns",
            NewExchangeRequest(sale.Id, item.TransactionItemId, replacement.Id, 1, false, "exchange-no-restock"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("wajib mengembalikan stok", await response.Content.ReadAsStringAsync());
        await AssertExchangeStockAsync(
            factory, setup.Product.Id, setup.Variant.Id, replacement.Id,
            expectedProduct: 13, expectedOriginal: 8, expectedReplacement: 5);
    }

    [Fact]
    public async Task Exchange_RestocksOriginalAndDecrementsSiblingVariant()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var owner = await AuthClientAsync(factory, "owner");
        var setup = await CreatePricedVariantAsync(owner, "TSHIRT-EXC");

        var replacementResponse = await owner.PostAsJsonAsync(
            "/api/retail/variants",
            NewVariant(setup.Product.Id, "TSHIRT-EXC-WHT-L", "White / L", 5));
        var replacement = (await replacementResponse.Content
            .ReadFromJsonAsync<ProductVariantDto>())!;

        var saleResponse = await PostCashAsync(
            owner, setup.Product.Id, setup.Variant.Id, null, 2, 100m);
        var sale = (await saleResponse.Content.ReadFromJsonAsync<TransactionDto>())!;
        var item = Assert.Single(sale.Items);

        var response = await owner.PostAsJsonAsync(
            "/api/retail/returns",
            NewExchangeRequest(sale.Id, item.TransactionItemId, replacement.Id, 1, true, "exchange-size-1"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<RetailReturnDto>())!;
        Assert.Equal(0m, result.RefundAmount);
        Assert.Equal(replacement.Id, Assert.Single(result.Items).ReplacementVariantId);

        await AssertExchangeStockAsync(
            factory, setup.Product.Id, setup.Variant.Id, replacement.Id,
            expectedProduct: 13, expectedOriginal: 9, expectedReplacement: 4);
    }
    [Fact]
    public async Task Kasir_CannotCreateReturn()
    {
        await using var factory = new AdvancedRetailFactory();
        await EnableFashionRetailAsync(factory);
        using var kasir = await AuthClientAsync(factory, "kasir");

        var response = await kasir.PostAsJsonAsync(
            "/api/retail/returns",
            NewReturnRequest(Guid.NewGuid(), Guid.NewGuid(), 1, true, "kasir-denied"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static CreateRetailReturnDto NewReturnRequest(
        Guid transactionId,
        Guid transactionItemId,
        int quantity,
        bool restock,
        string key) => new()
    {
        IdempotencyKey = key,
        TransactionId = transactionId,
        Type = RetailReturnTypes.Return,
        Reason = "Customer return",
        Items =
        [
            new CreateRetailReturnItemDto
            {
                TransactionItemId = transactionItemId,
                Quantity = quantity,
                Restock = restock
            }
        ]
    };
    private static CreateRetailReturnDto NewExchangeRequest(
        Guid transactionId,
        Guid transactionItemId,
        Guid replacementVariantId,
        int quantity,
        bool restock,
        string key) => new()
    {
        IdempotencyKey = key,
        TransactionId = transactionId,
        Type = RetailReturnTypes.Exchange,
        Reason = "Size exchange",
        Items =
        [
            new CreateRetailReturnItemDto
            {
                TransactionItemId = transactionItemId,
                Quantity = quantity,
                Restock = restock,
                ReplacementVariantId = replacementVariantId
            }
        ]
    };

    private static async Task AssertExchangeStockAsync(
        AdvancedRetailFactory factory,
        Guid productId,
        Guid originalVariantId,
        Guid replacementVariantId,
        int expectedProduct,
        int expectedOriginal,
        int expectedReplacement)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();
        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "advanced-retail-exchange-stock-assert");

        var product = await db.Products.SingleAsync(x => x.Id == productId);
        var original = await db.ProductVariants.SingleAsync(x => x.Id == originalVariantId);
        var replacement = await db.ProductVariants.SingleAsync(x => x.Id == replacementVariantId);

        Assert.Equal(expectedProduct, product.Stok);
        Assert.Equal(expectedOriginal, original.Stok);
        Assert.Equal(expectedReplacement, replacement.Stok);
    }
}

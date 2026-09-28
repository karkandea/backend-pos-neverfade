using System.Net;
using System.Net.Http.Json;
using NeverfadePos.Api.DTOs.Outlet;
using NeverfadePos.Api.DTOs.Product;
using NeverfadePos.Api.DTOs.StockHistory;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed partial class AdvancedRetailApiTests
{
    [Fact]
    public async Task OutletStock_Isolated_AndInactiveOutletCannotCheckout()
    {
        await using var factory = new AdvancedRetailFactory();
        using var main = await AuthClientAsync(factory, "owner");

        var createProduct = await main.PostAsJsonAsync("/api/products", new CreateProductDto
        {
            Kode = "OUTLET-STOCK-01",
            Barcode = "OUTLET-STOCK-BC",
            Nama = "Outlet Stock QA",
            Kategori = "QA",
            HargaModal = 40m,
            HargaJual = 100m,
            Stok = 5,
            Supplier = "QA",
            Satuan = "pcs",
            Type = ProductTypes.Goods,
            TracksStock = true,
            QuantityPrecision = 0
        });
        Assert.Equal(HttpStatusCode.OK, createProduct.StatusCode);
        var product = (await createProduct.Content.ReadFromJsonAsync<ProductDto>())!;
        Assert.Equal(5, product.Stok);

        var branchResponse = await main.PostAsJsonAsync("/api/outlets", new CreateOutletDto
        {
            Code = "BRANCH-QA",
            Name = "Branch QA",
            Address = "",
            Phone = "",
            IsDefault = false
        });
        Assert.Equal(HttpStatusCode.OK, branchResponse.StatusCode);
        var branch = (await branchResponse.Content.ReadFromJsonAsync<OutletDto>())!;

        using var branchClient = await AuthClientAsync(factory, "owner");
        branchClient.DefaultRequestHeaders.Add("X-Outlet-Id", branch.Id.ToString());

        var branchProduct = await branchClient.GetFromJsonAsync<ProductDto>(
            $"/api/products/{product.Id}");
        Assert.NotNull(branchProduct);
        Assert.Equal(0, branchProduct!.Stok);

        var blocked = await PostOutletCashAsync(branchClient, branch.Id, product.Id, 1);
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains("PRODUCT_STOCK_INSUFFICIENT", await blocked.Content.ReadAsStringAsync());

        var stockIn = await branchClient.PostAsJsonAsync(
            "/api/stock-history",
            new CreateStockHistoryDto
            {
                ProdukId = product.Id,
                Tipe = "masuk",
                Jumlah = 3,
                Keterangan = "Branch opening stock"
            });
        Assert.Equal(HttpStatusCode.OK, stockIn.StatusCode);

        branchProduct = await branchClient.GetFromJsonAsync<ProductDto>(
            $"/api/products/{product.Id}");
        var mainProduct = await main.GetFromJsonAsync<ProductDto>(
            $"/api/products/{product.Id}");
        Assert.Equal(3, branchProduct!.Stok);
        Assert.Equal(5, mainProduct!.Stok);

        var sale = await PostOutletCashAsync(branchClient, branch.Id, product.Id, 2);
        Assert.Equal(HttpStatusCode.OK, sale.StatusCode);
        var transaction = await sale.Content.ReadFromJsonAsync<TransactionDto>();
        Assert.NotNull(transaction);
        Assert.Equal(branch.Id, transaction!.OutletId);

        branchProduct = await branchClient.GetFromJsonAsync<ProductDto>(
            $"/api/products/{product.Id}");
        mainProduct = await main.GetFromJsonAsync<ProductDto>(
            $"/api/products/{product.Id}");
        Assert.Equal(1, branchProduct!.Stok);
        Assert.Equal(5, mainProduct!.Stok);

        var deactivate = await main.PutAsJsonAsync(
            $"/api/outlets/{branch.Id}",
            new UpdateOutletDto
            {
                Code = branch.Code,
                Name = branch.Name,
                Address = branch.Address,
                Phone = branch.Phone,
                IsDefault = false,
                Active = false
            });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var inactiveSale = await PostOutletCashAsync(branchClient, branch.Id, product.Id, 1);
        Assert.Equal(HttpStatusCode.NotFound, inactiveSale.StatusCode);
    }

    private static Task<HttpResponseMessage> PostOutletCashAsync(
        HttpClient client,
        Guid outletId,
        Guid productId,
        int quantity)
    {
        var subtotal = 100m * quantity;
        return client.PostAsJsonAsync("/api/transactions", new
        {
            outletId,
            customerId = (Guid?)null,
            items = new[]
            {
                new
                {
                    id = productId,
                    nama = "ignored",
                    hargaJual = 100m,
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
}

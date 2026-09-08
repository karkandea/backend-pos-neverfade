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
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.DTOs.Restaurant;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class RestaurantFnbTests
{
    [Fact]
    public async Task GeneralRetail_CannotAccessRestaurantCapabilityEndpoints()
    {
        await using var factory = new RestaurantApiFactory();
        using var owner = await CreateTenantClientAsync(factory, "owner", "owner123");

        var response = await owner.GetAsync("/api/restaurant/tables");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FoodBeverage_TableOrderKitchenFlow_IsStrict()
    {
        await using var factory = new RestaurantApiFactory();
        await EnableFoodBeverageAsync(factory);
        var product = await SeedProductAsync(factory);

        using var owner = await CreateTenantClientAsync(factory, "owner", "owner123");
        using var kasir = await CreateTenantClientAsync(factory, "kasir", "kasir123");

        var table = await CreateTableAsync(owner, "A1", "Meja A1");
        var opened = await OpenOrderAsync(kasir, table.Id);

        var duplicateOpen = await kasir.PostAsJsonAsync(
            "/api/restaurant/orders",
            new { tableId = table.Id });
        var duplicateBody = await duplicateOpen.Content.ReadFromJsonAsync<RestaurantOrderDto>();

        Assert.Equal(HttpStatusCode.OK, duplicateOpen.StatusCode);
        Assert.Equal(opened.Id, duplicateBody!.Id);

        var addResponse = await kasir.PostAsJsonAsync(
            $"/api/restaurant/orders/{opened.Id}/items",
            new { productId = product.Id, qty = 2, note = "Tanpa es" });

        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
        var withItem = await addResponse.Content.ReadFromJsonAsync<RestaurantOrderDto>();
        Assert.NotNull(withItem);
        Assert.Single(withItem!.Items);
        Assert.Equal("Tanpa es", withItem.Items[0].Note);
        Assert.Equal(RestaurantConstants.KitchenDraft, withItem.Items[0].KitchenStatus);

        var itemId = withItem.Items[0].Id;

        var kitchenResponse = await kasir.PostAsync(
            $"/api/restaurant/orders/{opened.Id}/send-to-kitchen",
            null);
        Assert.Equal(HttpStatusCode.OK, kitchenResponse.StatusCode);

        var queue = await kasir.GetFromJsonAsync<List<KitchenQueueOrderDto>>(
            "/api/restaurant/kitchen");
        Assert.NotNull(queue);
        Assert.Single(queue!);
        Assert.Single(queue[0].Items);
        Assert.Equal(RestaurantConstants.KitchenQueued, queue[0].Items[0].KitchenStatus);

        var editSentItem = await kasir.PutAsJsonAsync(
            $"/api/restaurant/orders/{opened.Id}/items/{itemId}",
            new { qty = 3, note = "ubah" });
        Assert.Equal(HttpStatusCode.Conflict, editSentItem.StatusCode);
        Assert.Contains(
            "RESTAURANT_ITEM_ALREADY_SENT",
            await editSentItem.Content.ReadAsStringAsync());

        var preparing = await kasir.PostAsJsonAsync(
            $"/api/restaurant/kitchen/items/{itemId}/status",
            new { status = RestaurantConstants.KitchenPreparing });
        Assert.Equal(HttpStatusCode.OK, preparing.StatusCode);

        var invalidSkip = await kasir.PostAsJsonAsync(
            $"/api/restaurant/kitchen/items/{itemId}/status",
            new { status = RestaurantConstants.KitchenServed });
        Assert.Equal(HttpStatusCode.Conflict, invalidSkip.StatusCode);

        var ready = await kasir.PostAsJsonAsync(
            $"/api/restaurant/kitchen/items/{itemId}/status",
            new { status = RestaurantConstants.KitchenReady });
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        var served = await kasir.PostAsJsonAsync(
            $"/api/restaurant/kitchen/items/{itemId}/status",
            new { status = RestaurantConstants.KitchenServed });
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        var emptyQueue = await kasir.GetFromJsonAsync<List<KitchenQueueOrderDto>>(
            "/api/restaurant/kitchen");
        Assert.Empty(emptyQueue!);

        var tables = await kasir.GetFromJsonAsync<List<RestaurantTableDto>>(
            "/api/restaurant/tables");
        Assert.NotNull(tables);
        var occupiedTable = tables!.Single();
        Assert.Equal("occupied", occupiedTable.Status);
        Assert.Equal(opened.Id, occupiedTable.OpenOrderId);
    }

    [Fact]
    public async Task RestaurantOrder_CanCloseOnlyWithMatchingPaidTransaction()
    {
        await using var factory = new RestaurantApiFactory();
        await EnableFoodBeverageAsync(factory);
        var product = await SeedProductAsync(factory);

        using var owner = await CreateTenantClientAsync(factory, "owner", "owner123");
        using var kasir = await CreateTenantClientAsync(factory, "kasir", "kasir123");

        var table = await CreateTableAsync(owner, "B1", "Meja B1");
        var order = await OpenOrderAsync(kasir, table.Id);

        var add = await kasir.PostAsJsonAsync(
            $"/api/restaurant/orders/{order.Id}/items",
            new { productId = product.Id, qty = 2, note = "Pedas sedang" });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        var pendingTransactionId = await SeedPendingTransactionAsync(factory, product, 2);
        var closePending = await kasir.PostAsJsonAsync(
            $"/api/restaurant/orders/{order.Id}/close",
            new { transactionId = pendingTransactionId });
        Assert.Equal(HttpStatusCode.Conflict, closePending.StatusCode);
        Assert.Contains(
            "RESTAURANT_TRANSACTION_NOT_PAID",
            await closePending.Content.ReadAsStringAsync());

        var paidTransactionId = await SeedPaidTransactionAsync(
            factory,
            product,
            qty: 2);

        var closePaid = await kasir.PostAsJsonAsync(
            $"/api/restaurant/orders/{order.Id}/close",
            new { transactionId = paidTransactionId });
        Assert.Equal(HttpStatusCode.OK, closePaid.StatusCode);

        var closed = await closePaid.Content.ReadFromJsonAsync<RestaurantOrderDto>();
        Assert.Equal(RestaurantConstants.OrderClosed, closed!.Status);
        Assert.Equal(paidTransactionId, closed.TransactionId);

        var repeatClose = await kasir.PostAsJsonAsync(
            $"/api/restaurant/orders/{order.Id}/close",
            new { transactionId = paidTransactionId });
        Assert.Equal(HttpStatusCode.OK, repeatClose.StatusCode);

        var tables = await kasir.GetFromJsonAsync<List<RestaurantTableDto>>(
            "/api/restaurant/tables");
        Assert.Equal("available", tables!.Single().Status);
    }

    [Fact]
    public async Task RestaurantOrder_CloseRejectsMismatchedPaidTransaction()
    {
        await using var factory = new RestaurantApiFactory();
        await EnableFoodBeverageAsync(factory);
        var productA = await SeedProductAsync(factory, "FNB-A", "Es Kopi", 18000m);
        var productB = await SeedProductAsync(factory, "FNB-B", "Teh", 12000m);

        using var owner = await CreateTenantClientAsync(factory, "owner", "owner123");
        using var kasir = await CreateTenantClientAsync(factory, "kasir", "kasir123");

        var table = await CreateTableAsync(owner, "C1", "Meja C1");
        var order = await OpenOrderAsync(kasir, table.Id);

        await kasir.PostAsJsonAsync(
            $"/api/restaurant/orders/{order.Id}/items",
            new { productId = productA.Id, qty = 1, note = "" });

        var paidTransactionId = await SeedPaidTransactionAsync(
            factory,
            productB,
            qty: 1);

        var close = await kasir.PostAsJsonAsync(
            $"/api/restaurant/orders/{order.Id}/close",
            new { transactionId = paidTransactionId });

        Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
        Assert.Contains(
            "RESTAURANT_TRANSACTION_MISMATCH",
            await close.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RestaurantOrder_CancelRequiresAdminAndReason()
    {
        await using var factory = new RestaurantApiFactory();
        await EnableFoodBeverageAsync(factory);

        using var owner = await CreateTenantClientAsync(factory, "owner", "owner123");
        using var kasir = await CreateTenantClientAsync(factory, "kasir", "kasir123");

        var table = await CreateTableAsync(owner, "D1", "Meja D1");
        var order = await OpenOrderAsync(kasir, table.Id);

        var kasirCancel = await kasir.PostAsJsonAsync(
            $"/api/restaurant/orders/{order.Id}/cancel",
            new { reason = "Customer batal." });
        Assert.Equal(HttpStatusCode.Forbidden, kasirCancel.StatusCode);

        var invalidReason = await owner.PostAsJsonAsync(
            $"/api/restaurant/orders/{order.Id}/cancel",
            new { reason = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidReason.StatusCode);

        var cancelledResponse = await owner.PostAsJsonAsync(
            $"/api/restaurant/orders/{order.Id}/cancel",
            new { reason = "Customer pindah lokasi." });
        Assert.Equal(HttpStatusCode.OK, cancelledResponse.StatusCode);

        var cancelled = await cancelledResponse.Content.ReadFromJsonAsync<RestaurantOrderDto>();
        Assert.Equal(RestaurantConstants.OrderCancelled, cancelled!.Status);
        Assert.Equal("Customer pindah lokasi.", cancelled.CancellationReason);
    }

    private static async Task<RestaurantTableDto> CreateTableAsync(
        HttpClient owner,
        string code,
        string name)
    {
        var response = await owner.PostAsJsonAsync(
            "/api/restaurant/tables",
            new
            {
                code,
                name,
                capacity = 4,
                active = true,
                sortOrder = 1
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RestaurantTableDto>())!;
    }

    private static async Task<RestaurantOrderDto> OpenOrderAsync(
        HttpClient client,
        Guid tableId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/restaurant/orders",
            new { tableId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RestaurantOrderDto>())!;
    }

    private static async Task<HttpClient> CreateTenantClientAsync(
        RestaurantApiFactory factory,
        string username,
        string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);

        return client;
    }

    private static async Task EnableFoodBeverageAsync(RestaurantApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = await db.Tenants.SingleAsync(x => x.Slug == "warung-lumpia-beef");
        tenant.BusinessType = BusinessTypes.FoodBeverage;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static async Task<Product> SeedProductAsync(
        RestaurantApiFactory factory,
        string code = "FNB-001",
        string name = "Es Kopi Susu",
        decimal price = 18000m)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "seed-fnb-product");

        var existing = await db.Products.SingleOrDefaultAsync(x => x.Kode == code);
        if (existing is not null)
        {
            return existing;
        }

        var product = new Product
        {
            TenantId = tenantId,
            Kode = code,
            Barcode = $"BAR-{code}",
            Nama = name,
            Kategori = "Minuman",
            HargaModal = price / 2m,
            HargaJual = price,
            Stok = 100,
            Supplier = "QA",
            Satuan = "pcs",
            Deskripsi = "F&B QA"
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static async Task<Guid> SeedPaidTransactionAsync(
        RestaurantApiFactory factory,
        Product product,
        int qty)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();

        var kasirId = await db.Users
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Role == "kasir")
            .Select(x => x.Id)
            .SingleAsync();

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "seed-fnb-paid-transaction");

        var subtotal = product.HargaJual * qty;
        var transaction = new Transaction
        {
            TenantId = tenantId,
            NoTrx = $"TRX-FNB-PAID-{Guid.NewGuid():N}",
            Kasir = "Kasir QA",
            KasirId = kasirId,
            Subtotal = subtotal,
            Total = subtotal,
            MetodePembayaran = "tunai",
            Dibayar = subtotal,
            Kembalian = 0,
            Status = TransactionStatuses.Paid,
            FinalizedAt = DateTime.UtcNow
        };

        db.Transactions.Add(transaction);
        db.TransactionItems.Add(new TransactionItem
        {
            TenantId = tenantId,
            TransactionId = transaction.Id,
            ProductId = product.Id,
            Nama = product.Nama,
            HargaJual = product.HargaJual,
            Qty = qty,
            Subtotal = subtotal
        });

        await db.SaveChangesAsync();
        return transaction.Id;
    }

    private static async Task<Guid> SeedPendingTransactionAsync(
        RestaurantApiFactory factory,
        Product product,
        int qty)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await db.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();

        var kasirId = await db.Users
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.Role == "kasir")
            .Select(x => x.Id)
            .SingleAsync();

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "seed-fnb-pending-transaction");

        var subtotal = product.HargaJual * qty;
        var transaction = new Transaction
        {
            TenantId = tenantId,
            NoTrx = $"TRX-FNB-PENDING-{Guid.NewGuid():N}",
            Kasir = "Kasir QA",
            KasirId = kasirId,
            Subtotal = subtotal,
            Total = subtotal,
            MetodePembayaran = "QRIS",
            Dibayar = 0,
            Kembalian = 0,
            Status = TransactionStatuses.PendingPayment,
            FinalizedAt = null
        };

        db.Transactions.Add(transaction);
        db.TransactionItems.Add(new TransactionItem
        {
            TenantId = tenantId,
            TransactionId = transaction.Id,
            ProductId = product.Id,
            Nama = product.Nama,
            HargaJual = product.HargaJual,
            Qty = qty,
            Subtotal = subtotal
        });

        await db.SaveChangesAsync();
        return transaction.Id;
    }

    private sealed class RestaurantApiFactory : WebApplicationFactory<Program>
    {
        private const string TenantKey =
            "restaurant-tenant-test-key-123456789012345678901234";
        private const string PlatformKey =
            "restaurant-platform-test-key-12345678901234567890";
        private readonly string _databaseName =
            $"restaurant-api-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            var config = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Database=test;Username=test;Password=test",
                ["Jwt:Key"] = TenantKey,
                ["Jwt:Issuer"] = "NeverfadePos.Restaurant.Test",
                ["Jwt:Audience"] = "NeverfadePos.Restaurant.Test.Client",
                ["PlatformJwt:Key"] = PlatformKey,
                ["PlatformJwt:Issuer"] = "NeverfadePos.Platform.Restaurant.Test",
                ["PlatformJwt:Audience"] = "NeverfadePos.Platform.Restaurant.Test.Client",
                ["Payments:Mode"] = "Disabled",
                ["PlatformBootstrap:Enabled"] = "false"
            };

            foreach (var item in config)
            {
                builder.UseSetting(item.Key, item.Value);
            }

            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(config));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}

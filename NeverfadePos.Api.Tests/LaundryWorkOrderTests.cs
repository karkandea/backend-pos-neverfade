using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.DTOs.Laundry;
using NeverfadePos.Api.DTOs.Product;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class LaundryWorkOrderTests
{
    [Fact]
    public async Task GeneralRetail_CannotAccessLaundryEndpoints()
    {
        await using var factory = new LaundryApiFactory();
        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");

        var response = await owner.GetAsync(
            "/api/laundry/work-orders");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);
        Assert.Contains(
            "CAPABILITY_NOT_ENABLED",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TenantA_CannotReadTenantB_LaundryWorkOrder()
    {
        await using var factory = new LaundryApiFactory();
        await EnableLaundryAsync(factory);

        var foreignTenantId = Guid.NewGuid();
        var foreignOrderId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            using var tenantScope = scope.ServiceProvider
                .GetRequiredService<ITrustedTenantExecutionScope>()
                .Begin(
                    foreignTenantId,
                    "laundry-tenant-isolation-seed");

            db.LaundryWorkOrders.Add(
                new LaundryWorkOrder
                {
                    Id = foreignOrderId,
                    TenantId = foreignTenantId,
                    CustomerId = Guid.NewGuid(),
                    CreatedByUserId = Guid.NewGuid(),
                    OrderNumber = "LDR-TENANT-B",
                    Status = LaundryConstants.StatusReceived,
                    PaymentStatus = LaundryConstants.PaymentUnpaid,
                    PromisedAt = DateTime.UtcNow.AddHours(4),
                    UpdatedAt = DateTime.UtcNow
                });

            await db.SaveChangesAsync();
        }

        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");

        var direct = await owner.GetAsync(
            $"/api/laundry/work-orders/{foreignOrderId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            direct.StatusCode);

        var list = await owner.GetFromJsonAsync<
            List<LaundryWorkOrderDto>>(
            "/api/laundry/work-orders");

        Assert.NotNull(list);
        Assert.DoesNotContain(
            list!,
            x => x.Id == foreignOrderId);
    }

    [Fact]
    public async Task ServiceProduct_AcceptsDecimalQuantity_WithoutStockMutation()
    {
        await using var factory = new LaundryApiFactory();
        await EnableLaundryAsync(factory);

        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");

        var createProduct = await owner.PostAsJsonAsync(
            "/api/products",
            new
            {
                kode = "LDR-SVC-001",
                barcode = "",
                nama = "Cuci Kering",
                kategori = "Laundry",
                hargaModal = 0m,
                hargaJual = 10000m,
                stok = 999,
                supplier = "",
                satuan = "kg",
                deskripsi = "Jasa per kilogram",
                type = ProductTypes.Service,
                tracksStock = true,
                quantityPrecision = 2
            });

        Assert.Equal(
            HttpStatusCode.OK,
            createProduct.StatusCode);

        var service = await createProduct.Content
            .ReadFromJsonAsync<ProductDto>();

        Assert.NotNull(service);
        Assert.Equal(ProductTypes.Service, service!.Type);
        Assert.False(service.TracksStock);
        Assert.Equal(2, service.QuantityPrecision);
        Assert.Equal(0, service.Stok);

        var customer = await GetSeedCustomerAsync(factory);
        var subtotal = 25000m;

        var transactionResponse = await owner.PostAsJsonAsync(
            "/api/transactions",
            new
            {
                customerId = customer.Id,
                items = new[]
                {
                    new
                    {
                        id = service.Id,
                        nama = service.Nama,
                        hargaJual = service.HargaJual,
                        qty = 1,
                        quantity = 2.5m,
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

        Assert.Equal(
            HttpStatusCode.OK,
            transactionResponse.StatusCode);

        var transaction = await transactionResponse.Content
            .ReadFromJsonAsync<TransactionDto>();

        Assert.NotNull(transaction);
        Assert.Single(transaction!.Items);
        Assert.Equal(1, transaction.Items[0].Qty);
        Assert.Equal(2.5m, transaction.Items[0].Quantity);
        Assert.Equal(ProductTypes.Service, transaction.Items[0].ProductType);
        Assert.False(transaction.Items[0].TracksStock);
        Assert.Equal("kg", transaction.Items[0].Unit);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await DemoTenantIdAsync(db);

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "verify-service-stock");

        var persisted = await db.Products.SingleAsync(
            x => x.Id == service.Id);
        var histories = await db.StockHistories.CountAsync(
            x => x.ProdukId == service.Id);

        Assert.Equal(0, persisted.Stok);
        Assert.Equal(0, histories);
    }

    [Fact]
    public async Task Goods_FractionalQuantity_IsRejected_AndStockRemainsIntact()
    {
        await using var factory = new LaundryApiFactory();

        var goods = await SeedProductAsync(
            factory,
            "LDR-GOODS-001",
            ProductTypes.Goods,
            tracksStock: true,
            quantityPrecision: 0,
            stock: 10,
            unit: "pcs",
            price: 5000m);

        using var owner = await CreateTenantClientAsync(
            factory,
            "owner",
            "owner123");

        var subtotal = 7500m;
        var response = await owner.PostAsJsonAsync(
            "/api/transactions",
            new
            {
                customerId = (Guid?)null,
                items = new[]
                {
                    new
                    {
                        id = goods.Id,
                        nama = goods.Nama,
                        hargaJual = goods.HargaJual,
                        qty = 1,
                        quantity = 1.5m,
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

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
        Assert.Contains(
            "GOODS_QUANTITY_MUST_BE_INTEGER",
            await response.Content.ReadAsStringAsync());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = await DemoTenantIdAsync(db);

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(tenantId, "verify-goods-stock");

        var persisted = await db.Products.SingleAsync(
            x => x.Id == goods.Id);

        Assert.Equal(10, persisted.Stok);
    }

    [Fact]
    public async Task Laundry_WorkOrderLifecycle_RequiresMatchingPaidTransaction()
    {
        await using var factory = new LaundryApiFactory();
        await EnableLaundryAsync(factory);

        var service = await SeedProductAsync(
            factory,
            "LDR-SVC-002",
            ProductTypes.Service,
            tracksStock: false,
            quantityPrecision: 2,
            stock: 0,
            unit: "kg",
            price: 12000m);

        var customer = await GetSeedCustomerAsync(factory);

        using var kasir = await CreateTenantClientAsync(
            factory,
            "kasir",
            "kasir123");

        var create = await kasir.PostAsJsonAsync(
            "/api/laundry/work-orders",
            new
            {
                customerId = customer.Id,
                promisedAt = DateTime.UtcNow.AddHours(6),
                notes = "Pisahkan pakaian putih.",
                items = new[]
                {
                    new
                    {
                        productId = service.Id,
                        quantity = 2.5m
                    }
                }
            });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var order = await create.Content
            .ReadFromJsonAsync<LaundryWorkOrderDto>();

        Assert.NotNull(order);
        Assert.Equal(LaundryConstants.StatusReceived, order!.Status);
        Assert.Equal(LaundryConstants.PaymentUnpaid, order.PaymentStatus);
        Assert.Equal(30000m, order.Total);
        Assert.Single(order.Items);
        Assert.Equal(2.5m, order.Items[0].Quantity);
        Assert.Equal("kg", order.Items[0].Unit);
        Assert.Single(order.StatusHistory);
        Assert.Equal(
            LaundryConstants.StatusReceived,
            order.StatusHistory[0].ToStatus);

        var inProgress = await kasir.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/status",
            new { status = LaundryConstants.StatusInProgress, reason = "" });
        Assert.True(
            inProgress.StatusCode == HttpStatusCode.OK,
            await inProgress.Content.ReadAsStringAsync());

        var ready = await kasir.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/status",
            new { status = LaundryConstants.StatusReady, reason = "" });
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        var completeUnpaid = await kasir.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/status",
            new { status = LaundryConstants.StatusCompleted, reason = "" });
        Assert.Equal(HttpStatusCode.Conflict, completeUnpaid.StatusCode);
        Assert.Contains(
            "LAUNDRY_PAYMENT_REQUIRED",
            await completeUnpaid.Content.ReadAsStringAsync());

        var wrongTransaction = await CreateCashTransactionAsync(
            kasir,
            customer.Id,
            service,
            quantity: 1m);

        var mismatch = await kasir.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/complete-payment",
            new { transactionId = wrongTransaction.Id });
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Contains(
            "LAUNDRY_TRANSACTION_TOTAL_MISMATCH",
            await mismatch.Content.ReadAsStringAsync());

        var paidTransaction = await CreateCashTransactionAsync(
            kasir,
            customer.Id,
            service,
            quantity: 2.5m);

        var paidResponse = await kasir.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/complete-payment",
            new { transactionId = paidTransaction.Id });
        Assert.Equal(HttpStatusCode.OK, paidResponse.StatusCode);

        var paid = await paidResponse.Content
            .ReadFromJsonAsync<LaundryWorkOrderDto>();
        Assert.NotNull(paid);
        Assert.Equal(LaundryConstants.PaymentPaid, paid!.PaymentStatus);
        Assert.Equal(paidTransaction.Id, paid.TransactionId);

        var repeat = await kasir.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/complete-payment",
            new { transactionId = paidTransaction.Id });
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);

        var complete = await kasir.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/status",
            new { status = LaundryConstants.StatusCompleted, reason = "" });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var completed = await complete.Content
            .ReadFromJsonAsync<LaundryWorkOrderDto>();
        Assert.Equal(LaundryConstants.StatusCompleted, completed!.Status);
        Assert.NotNull(completed.CompletedAt);

        var invalidAfterCompleted = await kasir.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/status",
            new { status = LaundryConstants.StatusReady, reason = "" });
        Assert.Equal(HttpStatusCode.Conflict, invalidAfterCompleted.StatusCode);
        Assert.Contains(
            "LAUNDRY_INVALID_TRANSITION",
            await invalidAfterCompleted.Content.ReadAsStringAsync());
    }

    private static async Task<TransactionDto> CreateCashTransactionAsync(
        HttpClient client,
        Guid customerId,
        Product product,
        decimal quantity)
    {
        var subtotal = decimal.Round(
            product.HargaJual * quantity,
            2,
            MidpointRounding.AwayFromZero);

        var response = await client.PostAsJsonAsync(
            "/api/transactions",
            new
            {
                customerId,
                items = new[]
                {
                    new
                    {
                        id = product.Id,
                        nama = product.Nama,
                        hargaJual = product.HargaJual,
                        qty = product.Type == ProductTypes.Goods
                            ? checked((int)quantity)
                            : 1,
                        quantity,
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content
            .ReadFromJsonAsync<TransactionDto>())!;
    }

    [Fact]
    public async Task LaundryOperator_CanOnlyAdvanceOwnOutletWorkWithoutMoneyFields()
    {
        await using var factory = new LaundryApiFactory();
        await EnableLaundryAsync(factory);
        var service = await SeedProductAsync(factory, "LDR-S1-OPERATOR",
            ProductTypes.Service, tracksStock: false, quantityPrecision: 2,
            stock: 0, unit: "kg", price: 15000m);
        var customer = await GetSeedCustomerAsync(factory);
        using var owner = await CreateTenantClientAsync(factory, "owner", "owner123");
        var created = await owner.PostAsJsonAsync("/api/laundry/work-orders", new
        {
            customerId = customer.Id, promisedAt = DateTime.UtcNow.AddHours(6),
            notes = "Pisahkan warna", items = new[] { new { productId = service.Id, quantity = 2m } }
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var order = (await created.Content.ReadFromJsonAsync<LaundryWorkOrderDto>())!;
        var staffResponse = await owner.PostAsJsonAsync("/api/users", new
        { nama = "Laundry QA Operator", username = "qa.laundry.operator", password = "LaundryQa123!", role = "laundry_operator" });
        Assert.Equal(HttpStatusCode.OK, staffResponse.StatusCode);
        var staff = (await staffResponse.Content.ReadFromJsonAsync<NeverfadePos.Api.DTOs.User.UserDto>())!;
        using var operatorClient = await CreateTenantClientAsync(factory, "qa.laundry.operator", "LaundryQa123!");
        var context = await operatorClient.GetFromJsonAsync<NeverfadePos.Api.DTOs.Tenant.TenantContextDto>(
            "/api/tenant/context");
        Assert.Equal("laundry_operator", context!.Role);
        Assert.Contains("laundry.work.operate", context.EffectivePermissions);
        Assert.DoesNotContain("pos.sell", context.EffectivePermissions);
        Assert.DoesNotContain("finance.read", context.EffectivePermissions);
        var queue = await operatorClient.GetAsync("/api/laundry/operator");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        var raw = await queue.Content.ReadAsStringAsync();
        foreach (var financialField in new[] { "unitPrice", "subtotal", "total", "paymentStatus", "transactionId", "customerPhone" })
            Assert.DoesNotContain($"\"{financialField}\"", raw, StringComparison.OrdinalIgnoreCase);
        var tickets = await queue.Content.ReadFromJsonAsync<List<LaundryOperatorOrderDto>>();
        var ticket = Assert.Single(tickets!);
        Assert.Equal(order.Id, ticket.Id);
        Assert.Equal("Pisahkan warna", ticket.Notes);
        Assert.Equal(2m, Assert.Single(ticket.Items).Quantity);
        foreach (var target in new[] { LaundryConstants.StatusCompleted, LaundryConstants.StatusCancelled })
        {
            var denied = await operatorClient.PostAsJsonAsync(
                $"/api/laundry/operator/{order.Id}/status", new { status = target });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        var progress = await operatorClient.PostAsJsonAsync(
            $"/api/laundry/operator/{order.Id}/status", new { status = LaundryConstants.StatusInProgress });
        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
        Assert.Equal(LaundryConstants.StatusInProgress,
            (await progress.Content.ReadFromJsonAsync<LaundryOperatorOrderDto>())!.Status);
        var ready = await operatorClient.PostAsJsonAsync(
            $"/api/laundry/operator/{order.Id}/status", new { status = LaundryConstants.StatusReady });
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(LaundryConstants.StatusReady,
            (await ready.Content.ReadFromJsonAsync<LaundryOperatorOrderDto>())!.Status);
        foreach (var path in new[] { "/api/products", "/api/transactions", "/api/laundry/work-orders",
                     "/api/restaurant/kitchen", "/api/users", "/api/laporan/summary" })
            Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PostAsJsonAsync(
            "/api/laundry/work-orders", new { customerId = customer.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/complete-payment", new { transactionId = Guid.NewGuid() })).StatusCode);
        var branch = await owner.PostAsJsonAsync("/api/outlets", new
        { code = "LAUNDRY-OPS-B", name = "Laundry other branch", address = "", phone = "", isDefault = false });
        Assert.Equal(HttpStatusCode.OK, branch.StatusCode);
        var branchId = (await branch.Content.ReadFromJsonAsync<NeverfadePos.Api.DTOs.Outlet.OutletDto>())!.Id;
        operatorClient.DefaultRequestHeaders.Add("X-Outlet-Id", branchId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorClient.GetAsync("/api/laundry/operator")).StatusCode);
        operatorClient.DefaultRequestHeaders.Remove("X-Outlet-Id");
        var revoke = await owner.PutAsJsonAsync($"/api/users/{staff.Id}", new
        { nama = staff.Nama, username = staff.Username, role = "kasir", active = true, password = "" });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await operatorClient.GetAsync(
            "/api/tenant/context")).StatusCode);
    }

    [Fact]
    public async Task Laundry_OutletWorkOrders_AreIsolatedBySelectionAndAssignment()
    {
        await using var factory = new LaundryApiFactory();
        await EnableLaundryAsync(factory);
        var service = await SeedProductAsync(factory, "LDR-S1-OUTLET",
            ProductTypes.Service, tracksStock: false, quantityPrecision: 2,
            stock: 0, unit: "kg", price: 10000m);
        var customer = await GetSeedCustomerAsync(factory);
        using var owner = await CreateTenantClientAsync(factory, "owner", "owner123");
        using var cashier = await CreateTenantClientAsync(factory, "kasir", "kasir123");
        var response = await owner.PostAsJsonAsync("/api/outlets", new
        {
            code = "LDR-BRANCH", name = "Laundry Branch", address = "", phone = "", isDefault = false
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var branchId = (await response.Content.ReadFromJsonAsync<NeverfadePos.Api.DTOs.Outlet.OutletDto>())!.Id;
        owner.DefaultRequestHeaders.Add("X-Outlet-Id", branchId.ToString());
        var create = await owner.PostAsJsonAsync("/api/laundry/work-orders", new
        {
            customerId = customer.Id, promisedAt = DateTime.UtcNow.AddHours(5), notes = "",
            items = new[] { new { productId = service.Id, quantity = 2m } }
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var order = (await create.Content.ReadFromJsonAsync<LaundryWorkOrderDto>())!;
        Assert.Single((await owner.GetFromJsonAsync<List<LaundryWorkOrderDto>>(
            "/api/laundry/work-orders"))!);
        owner.DefaultRequestHeaders.Remove("X-Outlet-Id");
        Assert.Empty((await owner.GetFromJsonAsync<List<LaundryWorkOrderDto>>(
            "/api/laundry/work-orders"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(
            $"/api/laundry/work-orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cashier.GetAsync(
            $"/api/laundry/work-orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cashier.PostAsJsonAsync(
            $"/api/laundry/work-orders/{order.Id}/status",
            new { status = LaundryConstants.StatusInProgress, reason = "" })).StatusCode);
        cashier.DefaultRequestHeaders.Add("X-Outlet-Id", branchId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync(
            "/api/laundry/work-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cashier.GetAsync(
            $"/api/laundry/work-orders/{order.Id}")).StatusCode);
    }

    private static async Task<HttpClient> CreateTenantClientAsync(
        LaundryApiFactory factory,
        string username,
        string password)
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = await response.Content
            .ReadFromJsonAsync<LoginResponseDto>();

        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                login!.Token);

        return client;
    }

    private static async Task EnableLaundryAsync(
        LaundryApiFactory factory)
    {
        await using var scope =
            factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var tenant = await db.Tenants
            .SingleAsync(
                x => x.Slug == "warung-lumpia-beef");

        tenant.BusinessType =
            BusinessTypes.Laundry;
        tenant.UpdatedAt =
            DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    private static async Task<Customer> GetSeedCustomerAsync(
        LaundryApiFactory factory)
    {
        await using var scope =
            factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var tenantId = await DemoTenantIdAsync(db);

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(
                tenantId,
                "laundry-seed-customer");

        return await db.Customers
            .OrderBy(x => x.Nama)
            .FirstAsync();
    }

    private static async Task<Product> SeedProductAsync(
        LaundryApiFactory factory,
        string code,
        string type,
        bool tracksStock,
        int quantityPrecision,
        int stock,
        string unit,
        decimal price)
    {
        await using var scope =
            factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var tenantId = await DemoTenantIdAsync(db);

        using var tenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>()
            .Begin(
                tenantId,
                "laundry-seed-product");

        var product = new Product
        {
            TenantId = tenantId,
            Kode = code,
            Barcode = $"BAR-{code}",
            Nama = code,
            Kategori = "Laundry",
            HargaModal = 0m,
            HargaJual = price,
            Stok = stock,
            Supplier = "QA",
            Satuan = unit,
            Deskripsi = "Laundry QA",
            Type = type,
            TracksStock = tracksStock,
            QuantityPrecision = quantityPrecision
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return product;
    }

    private static async Task<Guid> DemoTenantIdAsync(
        AppDbContext db) =>
        await db.Tenants
            .Where(x => x.Slug == "warung-lumpia-beef")
            .Select(x => x.Id)
            .SingleAsync();

    private sealed class LaundryApiFactory
        : WebApplicationFactory<Program>
    {
        private const string TenantKey =
            "laundry-tenant-test-key-1234567890123456789012345";
        private const string PlatformKey =
            "laundry-platform-test-key-12345678901234567890123";

        private readonly string _databaseName =
            $"laundry-api-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            var config =
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Database=test;Username=test;Password=test",
                    ["Jwt:Key"] = TenantKey,
                    ["Jwt:Issuer"] = "NeverfadePos.Laundry.Test",
                    ["Jwt:Audience"] = "NeverfadePos.Laundry.Test.Client",
                    ["PlatformJwt:Key"] = PlatformKey,
                    ["PlatformJwt:Issuer"] = "NeverfadePos.Platform.Laundry.Test",
                    ["PlatformJwt:Audience"] = "NeverfadePos.Platform.Laundry.Test.Client",
                    ["Payments:Mode"] = "Disabled",
                    ["PlatformBootstrap:Enabled"] = "false"
                };

            foreach (var item in config)
            {
                builder.UseSetting(
                    item.Key,
                    item.Value);
            }

            builder.ConfigureAppConfiguration(
                (_, configuration) =>
                    configuration.AddInMemoryCollection(
                        config));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<
                    DbContextOptions<AppDbContext>>();
                services.RemoveAll<
                    IDbContextOptionsConfiguration<AppDbContext>>();

                services.AddDbContext<AppDbContext>(
                    options =>
                        options
                            .UseInMemoryDatabase(
                                _databaseName)
                            .ConfigureWarnings(warnings =>
                                warnings.Ignore(
                                    InMemoryEventId.TransactionIgnoredWarning)));
            });
        }
    }
}

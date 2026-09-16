using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.DemoMode;

internal static class DemoResetScheduler
{
    private static int _started;

    public static void Start(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled") ||
            Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        var intervalMinutes =
            configuration.GetValue<int?>("DemoMode:ResetIntervalMinutes") ?? 180;
        var interval = TimeSpan.FromMinutes(intervalMinutes);
        var logger = loggerFactory.CreateLogger("DemoResetScheduler");

        _ = Task.Run(
            () => RunAsync(
                scopeFactory,
                interval,
                logger,
                applicationLifetime.ApplicationStopping),
            applicationLifetime.ApplicationStopping);
    }

    private static async Task RunAsync(
        IServiceScopeFactory scopeFactory,
        TimeSpan interval,
        ILogger logger,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ResetAsync(scopeFactory, stoppingToken);
                logger.LogInformation(
                    "NeverFade public demo state reset completed.");
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "NeverFade public demo reset failed; the next scheduled reset will retry.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    internal static async Task ResetAsync(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trustedTenantScope = scope.ServiceProvider
            .GetRequiredService<ITrustedTenantExecutionScope>();

        var tenantIds = await db.Tenants
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (tenantIds.Count != 1 ||
            tenantIds[0] != DemoModeDefaults.TenantId)
        {
            throw new InvalidOperationException(
                "Demo reset aborted because the database is not an isolated NeverFade demo database.");
        }

        using var tenantScope = trustedTenantScope.Begin(
            DemoModeDefaults.TenantId,
            "public-demo-periodic-reset");

        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        var items = await db.TransactionItems
            .ToListAsync(cancellationToken);
        db.TransactionItems.RemoveRange(items);
        await db.SaveChangesAsync(cancellationToken);

        var stockHistory = await db.StockHistories
            .ToListAsync(cancellationToken);
        db.StockHistories.RemoveRange(stockHistory);
        await db.SaveChangesAsync(cancellationToken);

        var transactions = await db.Transactions
            .ToListAsync(cancellationToken);
        db.Transactions.RemoveRange(transactions);
        await db.SaveChangesAsync(cancellationToken);

        await RestoreMasterCountersAsync(db, cancellationToken);
        await SeedHistoryAsync(db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task RestoreMasterCountersAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var productStocks = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["RTL001"] = 42,
            ["RTL002"] = 28,
            ["RTL003"] = 24,
            ["RTL004"] = 50,
            ["RTL005"] = 36
        };

        var products = await db.Products.ToListAsync(cancellationToken);
        foreach (var product in products)
        {
            if (!productStocks.TryGetValue(product.Kode, out var stock))
            {
                throw new InvalidOperationException(
                    $"Unexpected demo product '{product.Kode}' detected during reset.");
            }

            product.Stok = stock;
        }

        var variantStocks = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["RTL001-BLK-M"] = 14,
            ["RTL001-BLK-L"] = 12,
            ["RTL002-NAT-M"] = 9,
            ["RTL002-NAT-L"] = 8
        };

        var variants = await db.ProductVariants.ToListAsync(cancellationToken);
        foreach (var variant in variants)
        {
            if (!variantStocks.TryGetValue(variant.Sku, out var stock))
            {
                throw new InvalidOperationException(
                    $"Unexpected demo variant '{variant.Sku}' detected during reset.");
            }

            variant.Stok = stock;
        }

        var customers = await db.Customers.ToListAsync(cancellationToken);
        foreach (var customer in customers)
        {
            switch (customer.Hp)
            {
                case "081200000101":
                    customer.Poin = 220;
                    customer.TotalTransaksi = 8;
                    break;
                case "081200000102":
                    customer.Poin = 140;
                    customer.TotalTransaksi = 5;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected demo customer '{customer.Hp}' detected during reset.");
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedHistoryAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var cashier = await db.Users.SingleAsync(
            x => x.Username == DemoModeDefaults.Username,
            cancellationToken);
        var customers = await db.Customers
            .ToDictionaryAsync(x => x.Hp, cancellationToken);
        var products = await db.Products
            .ToDictionaryAsync(x => x.Kode, cancellationToken);

        AddHistory(
            db, cashier, customers["081200000101"], products["RTL001"],
            2, 129000m, 0, "DEMO-0003");
        AddHistory(
            db, cashier, customers["081200000102"], products["RTL002"],
            1, 189000m, 1, "DEMO-0002");
        AddHistory(
            db, cashier, customers["081200000101"], products["RTL004"],
            3, 89000m, 2, "DEMO-0001");
    }

    private static void AddHistory(
        AppDbContext db,
        User cashier,
        Customer customer,
        Product product,
        int quantity,
        decimal unitPrice,
        int daysAgo,
        string number)
    {
        var total = unitPrice * quantity;
        var date = DateTime.UtcNow.Date.AddHours(4).AddDays(-daysAgo);

        var entity = new Transaction
        {
            TenantId = DemoModeDefaults.TenantId,
            CreatedAt = date,
            Tanggal = date,
            NoTrx = number,
            Kasir = cashier.Nama,
            KasirId = cashier.Id,
            CustomerId = customer.Id,
            CustomerNama = customer.Nama,
            Subtotal = total,
            Total = total,
            MetodePembayaran = "cash",
            Dibayar = total,
            Kembalian = 0,
            Status = TransactionStatuses.Paid,
            FinalizedAt = date
        };

        db.Transactions.Add(entity);
        db.TransactionItems.Add(new TransactionItem
        {
            TenantId = DemoModeDefaults.TenantId,
            CreatedAt = date,
            TransactionId = entity.Id,
            ProductId = product.Id,
            Nama = product.Nama,
            HargaJual = unitPrice,
            BasePrice = product.HargaJual,
            Qty = quantity,
            Quantity = quantity,
            Unit = product.Satuan,
            Subtotal = total
        });
    }
}

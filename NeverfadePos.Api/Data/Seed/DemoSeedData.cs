using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DemoMode;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Seed;

internal static class DemoSeedData
{
    private sealed record ProductSeed(
        string Code,
        string Name,
        string Category,
        decimal Cost,
        decimal Price,
        int Stock,
        string Unit,
        string Type = ProductTypes.Goods,
        bool TracksStock = true,
        int QuantityPrecision = 0);

    public static async Task InitializeAsync(
        AppDbContext db,
        ITrustedTenantExecutionScope trustedTenantScope)
    {
        var existingTenantIds = await db.Tenants
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync();

        if (existingTenantIds.Count > 0)
        {
            if (!await DemoTenantIntegrity.IsIsolatedAsync(db))
            {
                throw new InvalidOperationException(
                    "DemoMode requires an isolated database containing only the NeverFade demo tenants.");
            }

            return;
        }

        if (await db.DemoVisitorSessions.AnyAsync())
            throw new InvalidOperationException("DemoMode requires an isolated, empty demo database before initial seed.");

        foreach (var profile in DemoModeDefaults.Profiles)
        {
            await SeedTenantAsync(
                db,
                trustedTenantScope,
                profile);
        }
    }

    internal static async Task ResetMutableStateAsync(
        AppDbContext db,
        DemoProfile profile,
        CancellationToken cancellationToken = default)
    {
        await RemoveMutableDataAsync(db, cancellationToken);

        var baselineStocks = GetProductSeeds(profile.BusinessType)
            .ToDictionary(x => x.Code, x => x.Stock, StringComparer.Ordinal);

        var products = await db.Products
            .ToListAsync(cancellationToken);

        foreach (var product in products)
        {
            if (!baselineStocks.TryGetValue(product.Kode, out var stock))
            {
                throw new InvalidOperationException(
                    $"Unexpected demo product '{product.Kode}' detected during reset.");
            }

            product.Stok = stock;
        }

        var variants = await db.ProductVariants
            .ToListAsync(cancellationToken);

        var variantStocks = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["RTL001-BLK-M"] = 14,
            ["RTL001-BLK-L"] = 12,
            ["RTL002-NAT-M"] = 9,
            ["RTL002-NAT-L"] = 8
        };

        foreach (var variant in variants)
        {
            if (!variantStocks.TryGetValue(variant.Sku, out var stock))
            {
                throw new InvalidOperationException(
                    $"Unexpected demo variant '{variant.Sku}' detected during reset.");
            }

            variant.Stok = stock;
        }

        var customers = await db.Customers
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (customers.Count != 2)
        {
            throw new InvalidOperationException(
                $"Expected exactly two demo customers for '{profile.Key}'.");
        }

        customers[0].Poin = 220;
        customers[0].TotalTransaksi = 8;
        customers[1].Poin = 140;
        customers[1].TotalTransaksi = 5;

        await db.SaveChangesAsync(cancellationToken);

        var user = await db.Users.SingleAsync(
            x => x.Username == profile.Username,
            cancellationToken);

        await SeedMutableDemoDataAsync(
            db,
            profile,
            user,
            customers[0],
            customers[1],
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static async Task RemoveMutableDataAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var laundryHistory = await db.LaundryWorkOrderStatusHistory
            .ToListAsync(cancellationToken);
        db.LaundryWorkOrderStatusHistory.RemoveRange(laundryHistory);

        var laundryItems = await db.LaundryWorkOrderItems
            .ToListAsync(cancellationToken);
        db.LaundryWorkOrderItems.RemoveRange(laundryItems);

        var laundryOrders = await db.LaundryWorkOrders
            .ToListAsync(cancellationToken);
        db.LaundryWorkOrders.RemoveRange(laundryOrders);

        var restaurantItems = await db.RestaurantOrderItems
            .ToListAsync(cancellationToken);
        db.RestaurantOrderItems.RemoveRange(restaurantItems);

        var restaurantOrders = await db.RestaurantOrders
            .ToListAsync(cancellationToken);
        db.RestaurantOrders.RemoveRange(restaurantOrders);

        var transactionItems = await db.TransactionItems
            .ToListAsync(cancellationToken);
        db.TransactionItems.RemoveRange(transactionItems);

        var stockHistory = await db.StockHistories
            .ToListAsync(cancellationToken);
        db.StockHistories.RemoveRange(stockHistory);

        var transactions = await db.Transactions
            .ToListAsync(cancellationToken);
        db.Transactions.RemoveRange(transactions);

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static async Task SeedTenantAsync(
        AppDbContext db,
        ITrustedTenantExecutionScope trustedTenantScope,
        DemoProfile profile)
    {
        var tenant = new Tenant
        {
            Id = profile.TenantId,
            NamaToko = profile.StoreName,
            Slug = profile.TenantSlug,
            BusinessType = profile.BusinessType,
            Status = "active"
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        using var tenantScope = trustedTenantScope.Begin(
            profile.TenantId,
            $"public-demo-seed:{profile.Key}");

        var user = new User
        {
            TenantId = profile.TenantId,
            Nama = "Demo Owner",
            Username = profile.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                Guid.NewGuid().ToString("N")),
            Role = "owner",
            Active = true
        };

        var customer1 = new Customer
        {
            TenantId = profile.TenantId,
            Nama = "Ayu Lestari",
            Hp = "081200000101",
            Email = "ayu@example.test",
            Alamat = "Denpasar",
            Poin = 220,
            TotalTransaksi = 8
        };

        var customer2 = new Customer
        {
            TenantId = profile.TenantId,
            Nama = "Raka Putra",
            Hp = "081200000102",
            Email = "raka@example.test",
            Alamat = "Badung",
            Poin = 140,
            TotalTransaksi = 5
        };

        var products = GetProductSeeds(profile.BusinessType)
            .Select(seed => CreateProduct(profile.TenantId, seed))
            .ToList();

        db.Users.Add(user);
        db.Customers.AddRange(customer1, customer2);
        db.Products.AddRange(products);

        db.Settings.Add(new Settings
        {
            TenantId = profile.TenantId,
            NamaToko = profile.StoreName,
            Alamat = GetAddress(profile.BusinessType),
            Telepon = "081200000000",
            Email = $"demo-{profile.Key.Replace('_', '-')}@neverfade.example",
            Website = "",
            HeaderStruk = "Terima kasih sudah mencoba NeverFade POS!",
            FooterStruk = "Ini adalah transaksi simulasi pada lingkungan demo.",
            ShowTax = profile.BusinessType == BusinessTypes.FoodBeverage,
            ShowPoint = true,
            DefaultTax = profile.BusinessType == BusinessTypes.FoodBeverage
                ? 10
                : 0,
            MinStok = 5,
            PoinRate = 1
        });

        db.Karyawans.AddRange(
            new Karyawan
            {
                TenantId = profile.TenantId,
                Nama = "Dina Pratiwi",
                Jabatan = "Kasir",
                Gaji = 3800000,
                Status = "aktif",
                TanggalMasuk = DateOnly.FromDateTime(
                    DateTime.Today.AddMonths(-8))
            },
            new Karyawan
            {
                TenantId = profile.TenantId,
                Nama = "Fajar Mahendra",
                Jabatan = GetCrewRole(profile.BusinessType),
                Gaji = 3600000,
                Status = "aktif",
                TanggalMasuk = DateOnly.FromDateTime(
                    DateTime.Today.AddMonths(-5))
            });

        await db.SaveChangesAsync();

        if (profile.BusinessType == BusinessTypes.FashionRetail)
        {
            await SeedFashionMasterDataAsync(
                db,
                profile,
                products,
                cancellationToken: default);
        }

        if (profile.BusinessType == BusinessTypes.FoodBeverage)
        {
            SeedRestaurantTables(db, profile);
            await db.SaveChangesAsync();
        }

        await SeedMutableDemoDataAsync(
            db,
            profile,
            user,
            customer1,
            customer2,
            cancellationToken: default);

        await db.SaveChangesAsync();
    }

    private static async Task SeedFashionMasterDataAsync(
        AppDbContext db,
        DemoProfile profile,
        IReadOnlyList<Product> products,
        CancellationToken cancellationToken)
    {
        var byCode = products.ToDictionary(x => x.Kode, StringComparer.Ordinal);
        var tshirt = byCode["RTL001"];
        var shirt = byCode["RTL002"];

        var retailLevel = new PriceLevel
        {
            TenantId = profile.TenantId,
            Code = "SATUAN",
            Name = "Satuan",
            SortOrder = 10,
            Active = true
        };

        var wholesaleLevel = new PriceLevel
        {
            TenantId = profile.TenantId,
            Code = "GROSIR",
            Name = "Grosir",
            SortOrder = 20,
            Active = true
        };

        db.PriceLevels.AddRange(retailLevel, wholesaleLevel);
        await db.SaveChangesAsync(cancellationToken);

        db.ProductVariants.AddRange(
            Variant(profile.TenantId, tshirt, "RTL001-BLK-M", "Black / M", "Color", "Black", "Size", "M", 14),
            Variant(profile.TenantId, tshirt, "RTL001-BLK-L", "Black / L", "Color", "Black", "Size", "L", 12),
            Variant(profile.TenantId, shirt, "RTL002-NAT-M", "Natural / M", "Color", "Natural", "Size", "M", 9),
            Variant(profile.TenantId, shirt, "RTL002-NAT-L", "Natural / L", "Color", "Natural", "Size", "L", 8));

        db.ProductPrices.AddRange(
            Price(profile.TenantId, byCode["RTL001"], retailLevel, 1, 129000m),
            Price(profile.TenantId, byCode["RTL001"], wholesaleLevel, 6, 105000m),
            Price(profile.TenantId, byCode["RTL002"], retailLevel, 1, 189000m),
            Price(profile.TenantId, byCode["RTL002"], wholesaleLevel, 6, 158000m),
            Price(profile.TenantId, byCode["RTL003"], retailLevel, 1, 239000m),
            Price(profile.TenantId, byCode["RTL003"], wholesaleLevel, 6, 199000m),
            Price(profile.TenantId, byCode["RTL004"], retailLevel, 1, 89000m),
            Price(profile.TenantId, byCode["RTL004"], wholesaleLevel, 12, 69000m),
            Price(profile.TenantId, byCode["RTL005"], retailLevel, 1, 99000m),
            Price(profile.TenantId, byCode["RTL005"], wholesaleLevel, 12, 79000m));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedMutableDemoDataAsync(
        AppDbContext db,
        DemoProfile profile,
        User user,
        Customer customer1,
        Customer customer2,
        CancellationToken cancellationToken)
    {
        var products = await db.Products
            .ToDictionaryAsync(x => x.Kode, cancellationToken);

        var history = GetProductSeeds(profile.BusinessType);

        AddHistoryTransaction(
            db,
            profile.TenantId,
            user,
            customer1,
            products[history[0].Code],
            quantity: 2,
            unitPrice: history[0].Price,
            daysAgo: 0,
            number: "DEMO-0003");

        AddHistoryTransaction(
            db,
            profile.TenantId,
            user,
            customer2,
            products[history[1].Code],
            quantity: 1,
            unitPrice: history[1].Price,
            daysAgo: 1,
            number: "DEMO-0002");

        AddHistoryTransaction(
            db,
            profile.TenantId,
            user,
            customer1,
            products[history[2].Code],
            quantity: 1,
            unitPrice: history[2].Price,
            daysAgo: 2,
            number: "DEMO-0001");

        if (profile.BusinessType == BusinessTypes.FoodBeverage)
        {
            await SeedRestaurantOpenOrderAsync(
                db,
                profile,
                user,
                products,
                cancellationToken);
        }

        if (profile.BusinessType == BusinessTypes.Laundry)
        {
            SeedLaundryWorkOrders(
                db,
                profile,
                user,
                customer1,
                customer2,
                products);
        }
    }

    private static void SeedRestaurantTables(
        AppDbContext db,
        DemoProfile profile)
    {
        for (var i = 1; i <= 8; i++)
        {
            db.RestaurantTables.Add(new RestaurantTable
            {
                TenantId = profile.TenantId,
                Code = $"T{i:00}",
                Name = $"Meja {i}",
                Capacity = i <= 4 ? 2 : 4,
                SortOrder = i * 10,
                Active = true
            });
        }
    }

    private static async Task SeedRestaurantOpenOrderAsync(
        AppDbContext db,
        DemoProfile profile,
        User user,
        IReadOnlyDictionary<string, Product> products,
        CancellationToken cancellationToken)
    {
        var table = await db.RestaurantTables
            .OrderBy(x => x.SortOrder)
            .Skip(1)
            .FirstAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var order = new RestaurantOrder
        {
            TenantId = profile.TenantId,
            TableId = table.Id,
            OpenedByUserId = user.Id,
            OrderNumber = "DEMO-FNB-001",
            Status = RestaurantConstants.OrderOpen,
            CreatedAt = now.AddMinutes(-18),
            UpdatedAt = now.AddMinutes(-6)
        };

        db.RestaurantOrders.Add(order);

        db.RestaurantOrderItems.AddRange(
            new RestaurantOrderItem
            {
                TenantId = profile.TenantId,
                RestaurantOrderId = order.Id,
                ProductId = products["FNB002"].Id,
                Nama = products["FNB002"].Nama,
                HargaJual = products["FNB002"].HargaJual,
                Qty = 2,
                Note = "Less ice",
                KitchenStatus = RestaurantConstants.KitchenPreparing,
                CreatedAt = now.AddMinutes(-17),
                UpdatedAt = now.AddMinutes(-7),
                QueuedAt = now.AddMinutes(-15),
                PreparingAt = now.AddMinutes(-7)
            },
            new RestaurantOrderItem
            {
                TenantId = profile.TenantId,
                RestaurantOrderId = order.Id,
                ProductId = products["FNB004"].Id,
                Nama = products["FNB004"].Nama,
                HargaJual = products["FNB004"].HargaJual,
                Qty = 1,
                Note = "",
                KitchenStatus = RestaurantConstants.KitchenQueued,
                CreatedAt = now.AddMinutes(-16),
                UpdatedAt = now.AddMinutes(-15),
                QueuedAt = now.AddMinutes(-15)
            });
    }

    private static void SeedLaundryWorkOrders(
        AppDbContext db,
        DemoProfile profile,
        User user,
        Customer customer1,
        Customer customer2,
        IReadOnlyDictionary<string, Product> products)
    {
        var now = DateTime.UtcNow;

        var first = new LaundryWorkOrder
        {
            TenantId = profile.TenantId,
            CustomerId = customer1.Id,
            CreatedByUserId = user.Id,
            OrderNumber = "DEMO-LDR-001",
            Status = LaundryConstants.StatusReceived,
            PaymentStatus = LaundryConstants.PaymentUnpaid,
            Notes = "Pisahkan pakaian putih.",
            PromisedAt = now.AddDays(2),
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-2)
        };

        var second = new LaundryWorkOrder
        {
            TenantId = profile.TenantId,
            CustomerId = customer2.Id,
            CreatedByUserId = user.Id,
            OrderNumber = "DEMO-LDR-002",
            Status = LaundryConstants.StatusInProgress,
            PaymentStatus = LaundryConstants.PaymentUnpaid,
            Notes = "Gunakan pewangi lembut.",
            PromisedAt = now.AddDays(1),
            CreatedAt = now.AddHours(-6),
            UpdatedAt = now.AddHours(-1)
        };

        db.LaundryWorkOrders.AddRange(first, second);

        AddLaundryItem(db, profile.TenantId, first, products["LDR001"], 4.5m);
        AddLaundryItem(db, profile.TenantId, second, products["LDR002"], 3.2m);

        db.LaundryWorkOrderStatusHistory.Add(new LaundryWorkOrderStatusHistory
        {
            TenantId = profile.TenantId,
            LaundryWorkOrderId = second.Id,
            ActorUserId = user.Id,
            FromStatus = LaundryConstants.StatusReceived,
            ToStatus = LaundryConstants.StatusInProgress,
            ActorName = user.Nama,
            Reason = "Mulai proses pencucian",
            CreatedAt = now.AddHours(-1)
        });
    }

    private static void AddLaundryItem(
        AppDbContext db,
        Guid tenantId,
        LaundryWorkOrder order,
        Product product,
        decimal quantity)
    {
        db.LaundryWorkOrderItems.Add(new LaundryWorkOrderItem
        {
            TenantId = tenantId,
            LaundryWorkOrderId = order.Id,
            ProductId = product.Id,
            Nama = product.Nama,
            ProductType = product.Type,
            Unit = product.Satuan,
            Quantity = quantity,
            QuantityPrecision = product.QuantityPrecision,
            UnitPrice = product.HargaJual,
            Subtotal = decimal.Round(
                product.HargaJual * quantity,
                2,
                MidpointRounding.AwayFromZero)
        });
    }

    private static Product CreateProduct(
        Guid tenantId,
        ProductSeed seed) =>
        new()
        {
            TenantId = tenantId,
            Kode = seed.Code,
            Nama = seed.Name,
            Kategori = seed.Category,
            HargaModal = seed.Cost,
            HargaJual = seed.Price,
            Stok = seed.Stock,
            Supplier = "NeverFade Demo Supplier",
            Satuan = seed.Unit,
            Deskripsi = "Data contoh untuk mencoba fitur NeverFade POS.",
            Type = seed.Type,
            TracksStock = seed.TracksStock,
            QuantityPrecision = seed.QuantityPrecision
        };

    private static ProductVariant Variant(
        Guid tenantId,
        Product product,
        string sku,
        string label,
        string option1Name,
        string option1Value,
        string option2Name,
        string option2Value,
        int stock) =>
        new()
        {
            TenantId = tenantId,
            ProductId = product.Id,
            Sku = sku,
            Label = label,
            Option1Name = option1Name,
            Option1Value = option1Value,
            Option2Name = option2Name,
            Option2Value = option2Value,
            Stok = stock,
            Active = true
        };

    private static ProductPrice Price(
        Guid tenantId,
        Product product,
        PriceLevel level,
        decimal minimumQuantity,
        decimal unitPrice) =>
        new()
        {
            TenantId = tenantId,
            ProductId = product.Id,
            PriceLevelId = level.Id,
            MinQuantity = minimumQuantity,
            UnitPrice = unitPrice
        };

    private static void AddHistoryTransaction(
        AppDbContext db,
        Guid tenantId,
        User cashier,
        Customer customer,
        Product product,
        int quantity,
        decimal unitPrice,
        int daysAgo,
        string number)
    {
        var total = unitPrice * quantity;
        var date = DateTime.UtcNow.Date
            .AddHours(4)
            .AddDays(-daysAgo);

        var transaction = new Transaction
        {
            TenantId = tenantId,
            CreatedAt = date,
            NoTrx = number,
            Tanggal = date,
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

        db.Transactions.Add(transaction);
        db.TransactionItems.Add(new TransactionItem
        {
            TenantId = tenantId,
            CreatedAt = date,
            TransactionId = transaction.Id,
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

    private static IReadOnlyList<ProductSeed> GetProductSeeds(
        string businessType) =>
        businessType switch
        {
            BusinessTypes.GeneralRetail =>
            [
                new("GEN001", "Air Mineral 600ml", "Minuman", 2200m, 5000m, 80, "pcs"),
                new("GEN002", "Mi Instan", "Makanan", 2500m, 4000m, 65, "pcs"),
                new("GEN003", "Sabun Cair", "Rumah Tangga", 12000m, 18000m, 30, "pcs"),
                new("GEN004", "Kopi Sachet", "Minuman", 1800m, 3000m, 100, "pcs"),
                new("GEN005", "Tissue Pack", "Rumah Tangga", 8000m, 12000m, 45, "pcs")
            ],
            BusinessTypes.FashionRetail =>
            [
                new("RTL001", "Oversized Essential Tee", "T-Shirt", 65000m, 129000m, 42, "pcs"),
                new("RTL002", "Linen Relaxed Shirt", "Shirt", 95000m, 189000m, 28, "pcs"),
                new("RTL003", "Everyday Cargo Pants", "Pants", 120000m, 239000m, 24, "pcs"),
                new("RTL004", "Canvas Daily Tote", "Accessories", 38000m, 89000m, 50, "pcs"),
                new("RTL005", "Classic Logo Cap", "Accessories", 42000m, 99000m, 36, "pcs")
            ],
            BusinessTypes.FoodBeverage =>
            [
                new("FNB001", "Americano", "Coffee", 8000m, 20000m, 100, "cup"),
                new("FNB002", "Cafe Latte", "Coffee", 10000m, 28000m, 100, "cup"),
                new("FNB003", "Nasi Goreng Kampung", "Main Course", 16000m, 35000m, 60, "plate"),
                new("FNB004", "Butter Croissant", "Pastry", 10000m, 25000m, 40, "pcs"),
                new("FNB005", "Iced Tea", "Non Coffee", 3000m, 12000m, 120, "glass")
            ],
            BusinessTypes.Laundry =>
            [
                new("LDR001", "Cuci Kering Kiloan", "Laundry", 0m, 7000m, 0, "kg", ProductTypes.Service, false, 2),
                new("LDR002", "Cuci Setrika Kiloan", "Laundry", 0m, 10000m, 0, "kg", ProductTypes.Service, false, 2),
                new("LDR003", "Bed Cover", "Laundry", 0m, 25000m, 0, "pcs", ProductTypes.Service, false, 0),
                new("LDR004", "Cuci Sepatu", "Laundry", 0m, 35000m, 0, "pasang", ProductTypes.Service, false, 0),
                new("LDR005", "Express 6 Jam", "Laundry", 0m, 16000m, 0, "kg", ProductTypes.Service, false, 2)
            ],
            BusinessTypes.SalonBarbershop =>
            [
                new("SAL001", "Haircut", "Hair", 0m, 75000m, 0, "service", ProductTypes.Service, false, 0),
                new("SAL002", "Hair Wash", "Hair", 0m, 35000m, 0, "service", ProductTypes.Service, false, 0),
                new("SAL003", "Creambath", "Treatment", 0m, 95000m, 0, "service", ProductTypes.Service, false, 0),
                new("SAL004", "Beard Trim", "Barber", 0m, 40000m, 0, "service", ProductTypes.Service, false, 0),
                new("SAL005", "Hair Coloring", "Treatment", 0m, 250000m, 0, "service", ProductTypes.Service, false, 0)
            ],
            _ => throw new ArgumentException(
                "Unsupported demo business type.",
                nameof(businessType))
        };

    private static string GetAddress(string businessType) =>
        businessType switch
        {
            BusinessTypes.GeneralRetail => "Jl. Demo Retail No. 1",
            BusinessTypes.FashionRetail => "Jl. Demo Fashion No. 2",
            BusinessTypes.FoodBeverage => "Jl. Demo Cafe No. 3",
            BusinessTypes.Laundry => "Jl. Demo Laundry No. 4",
            BusinessTypes.SalonBarbershop => "Jl. Demo Salon No. 5",
            _ => "Jl. Demo NeverFade"
        };

    private static string GetCrewRole(string businessType) =>
        businessType switch
        {
            BusinessTypes.FoodBeverage => "Barista",
            BusinessTypes.Laundry => "Laundry Crew",
            BusinessTypes.SalonBarbershop => "Stylist",
            _ => "Store Crew"
        };
}

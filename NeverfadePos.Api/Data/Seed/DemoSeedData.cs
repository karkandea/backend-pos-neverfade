using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DemoMode;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Seed;

internal static class DemoSeedData
{
    public static async Task InitializeAsync(
        AppDbContext db,
        ITrustedTenantExecutionScope trustedTenantScope)
    {
        var existingTenants = await db.Tenants
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync();

        if (existingTenants.Count > 0)
        {
            if (existingTenants.Count != 1 ||
                existingTenants[0] != DemoModeDefaults.TenantId)
            {
                throw new InvalidOperationException(
                    "DemoMode requires an isolated database containing only the NeverFade demo tenant.");
            }

            return;
        }

        var tenant = new Tenant
        {
            Id = DemoModeDefaults.TenantId,
            NamaToko = "NeverFade Fashion Demo",
            Slug = DemoModeDefaults.TenantSlug,
            BusinessType = BusinessTypes.FashionRetail,
            Status = "active"
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        using var tenantScope = trustedTenantScope.Begin(
            tenant.Id,
            "public-demo-seed");

        var demoUser = new User
        {
            TenantId = tenant.Id,
            Nama = "Demo Owner",
            Username = DemoModeDefaults.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(
                Guid.NewGuid().ToString("N")),
            Role = "owner",
            Active = true
        };

        var customer1 = new Customer
        {
            TenantId = tenant.Id,
            Nama = "Ayu Lestari",
            Hp = "081200000101",
            Email = "ayu@example.test",
            Alamat = "Denpasar",
            Poin = 220,
            TotalTransaksi = 8
        };

        var customer2 = new Customer
        {
            TenantId = tenant.Id,
            Nama = "Raka Putra",
            Hp = "081200000102",
            Email = "raka@example.test",
            Alamat = "Badung",
            Poin = 140,
            TotalTransaksi = 5
        };

        var tshirt = Product(
            tenant.Id,
            "RTL001",
            "Oversized Essential Tee",
            "T-Shirt",
            65000m,
            129000m,
            42,
            "pcs");

        var shirt = Product(
            tenant.Id,
            "RTL002",
            "Linen Relaxed Shirt",
            "Shirt",
            95000m,
            189000m,
            28,
            "pcs");

        var pants = Product(
            tenant.Id,
            "RTL003",
            "Everyday Cargo Pants",
            "Pants",
            120000m,
            239000m,
            24,
            "pcs");

        var tote = Product(
            tenant.Id,
            "RTL004",
            "Canvas Daily Tote",
            "Accessories",
            38000m,
            89000m,
            50,
            "pcs");

        var cap = Product(
            tenant.Id,
            "RTL005",
            "Classic Logo Cap",
            "Accessories",
            42000m,
            99000m,
            36,
            "pcs");

        var retailLevel = new PriceLevel
        {
            TenantId = tenant.Id,
            Code = "SATUAN",
            Name = "Satuan",
            SortOrder = 10,
            Active = true
        };

        var wholesaleLevel = new PriceLevel
        {
            TenantId = tenant.Id,
            Code = "GROSIR",
            Name = "Grosir",
            SortOrder = 20,
            Active = true
        };

        db.Users.Add(demoUser);
        db.Customers.AddRange(customer1, customer2);
        db.Products.AddRange(tshirt, shirt, pants, tote, cap);
        db.PriceLevels.AddRange(retailLevel, wholesaleLevel);

        db.Settings.Add(new Settings
        {
            TenantId = tenant.Id,
            NamaToko = tenant.NamaToko,
            Alamat = "Jl. Demo Retail No. 1",
            Telepon = "081200000000",
            Email = "demo@neverfade.example",
            Website = "",
            HeaderStruk = "Terima kasih sudah mencoba NeverFade POS!",
            FooterStruk = "Ini adalah transaksi simulasi pada lingkungan demo.",
            ShowTax = false,
            ShowPoint = true,
            DefaultTax = 0,
            MinStok = 5,
            PoinRate = 1
        });

        db.Karyawans.AddRange(
            new Karyawan
            {
                TenantId = tenant.Id,
                Nama = "Dina Pratiwi",
                Jabatan = "Kasir",
                Gaji = 3800000,
                Status = "aktif",
                TanggalMasuk = DateOnly.FromDateTime(DateTime.Today.AddMonths(-8))
            },
            new Karyawan
            {
                TenantId = tenant.Id,
                Nama = "Fajar Mahendra",
                Jabatan = "Store Crew",
                Gaji = 3600000,
                Status = "aktif",
                TanggalMasuk = DateOnly.FromDateTime(DateTime.Today.AddMonths(-5))
            });

        await db.SaveChangesAsync();

        var teeM = Variant(tenant.Id, tshirt, "RTL001-BLK-M", "Black / M", "Color", "Black", "Size", "M", 14);
        var teeL = Variant(tenant.Id, tshirt, "RTL001-BLK-L", "Black / L", "Color", "Black", "Size", "L", 12);
        var shirtM = Variant(tenant.Id, shirt, "RTL002-NAT-M", "Natural / M", "Color", "Natural", "Size", "M", 9);
        var shirtL = Variant(tenant.Id, shirt, "RTL002-NAT-L", "Natural / L", "Color", "Natural", "Size", "L", 8);

        db.ProductVariants.AddRange(teeM, teeL, shirtM, shirtL);
        await db.SaveChangesAsync();

        db.ProductPrices.AddRange(
            Price(tenant.Id, tshirt, null, retailLevel, 1, 129000m),
            Price(tenant.Id, tshirt, null, wholesaleLevel, 6, 105000m),
            Price(tenant.Id, shirt, null, retailLevel, 1, 189000m),
            Price(tenant.Id, shirt, null, wholesaleLevel, 6, 158000m),
            Price(tenant.Id, pants, null, retailLevel, 1, 239000m),
            Price(tenant.Id, pants, null, wholesaleLevel, 6, 199000m),
            Price(tenant.Id, tote, null, retailLevel, 1, 89000m),
            Price(tenant.Id, tote, null, wholesaleLevel, 12, 69000m),
            Price(tenant.Id, cap, null, retailLevel, 1, 99000m),
            Price(tenant.Id, cap, null, wholesaleLevel, 12, 79000m));

        AddHistoryTransaction(
            db,
            tenant.Id,
            demoUser,
            customer1,
            tshirt,
            quantity: 2,
            unitPrice: 129000m,
            daysAgo: 0,
            number: "DEMO-0003");

        AddHistoryTransaction(
            db,
            tenant.Id,
            demoUser,
            customer2,
            shirt,
            quantity: 1,
            unitPrice: 189000m,
            daysAgo: 1,
            number: "DEMO-0002");

        AddHistoryTransaction(
            db,
            tenant.Id,
            demoUser,
            customer1,
            tote,
            quantity: 3,
            unitPrice: 89000m,
            daysAgo: 2,
            number: "DEMO-0001");

        await db.SaveChangesAsync();
    }

    private static Product Product(
        Guid tenantId,
        string code,
        string name,
        string category,
        decimal cost,
        decimal price,
        int stock,
        string unit) =>
        new()
        {
            TenantId = tenantId,
            Kode = code,
            Nama = name,
            Kategori = category,
            HargaModal = cost,
            HargaJual = price,
            Stok = stock,
            Supplier = "NeverFade Demo Supplier",
            Satuan = unit,
            Deskripsi = "Data contoh untuk mencoba fitur NeverFade POS."
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
        ProductVariant? variant,
        PriceLevel level,
        decimal minimumQuantity,
        decimal unitPrice) =>
        new()
        {
            TenantId = tenantId,
            ProductId = product.Id,
            ProductVariantId = variant?.Id,
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
}

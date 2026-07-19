using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Seed;

public static class SeedData
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        await using var scope =
            services.CreateAsyncScope();

        var db =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var logger =
            scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("SeedData");

        if (await db.Tenants.AnyAsync())
        {
            return;
        }

        if (environment.IsDevelopment())
        {
            await SeedDemoAsync(db);

            logger.LogInformation(
                "Development demo data created.");

            return;
        }

        var bootstrapEnabled =
            configuration.GetValue<bool>(
                "Bootstrap:Enabled");

        if (!bootstrapEnabled)
        {
            logger.LogWarning(
                "Database is empty and production bootstrap is disabled.");

            return;
        }

        await SeedProductionBootstrapAsync(
            db,
            configuration);

        logger.LogInformation(
            "Production owner bootstrap completed.");
    }

    private static async Task SeedProductionBootstrapAsync(
        AppDbContext db,
        IConfiguration configuration)
    {
        var tenantName = Require(
            configuration,
            "Bootstrap:TenantName");

        var tenantSlug = Require(
            configuration,
            "Bootstrap:TenantSlug");

        var ownerName = Require(
            configuration,
            "Bootstrap:OwnerName");

        var ownerUsername = Require(
            configuration,
            "Bootstrap:OwnerUsername");

        var ownerPassword = Require(
            configuration,
            "Bootstrap:OwnerPassword");

        if (ownerPassword.Length < 12)
        {
            throw new InvalidOperationException(
                "Bootstrap:OwnerPassword must contain at least 12 characters.");
        }

        if (ownerPassword is
            "owner123" or
            "admin123" or
            "kasir123")
        {
            throw new InvalidOperationException(
                "Bootstrap owner password cannot use a demo password.");
        }

        var tenant = new Tenant
        {
            NamaToko = tenantName,
            Slug = tenantSlug
        };

        db.Tenants.Add(tenant);

        await db.SaveChangesAsync();

        db.Users.Add(new User
        {
            TenantId = tenant.Id,
            Nama = ownerName,
            Username = ownerUsername,
            PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(
                    ownerPassword),
            Role = "owner",
            Active = true
        });

        db.Settings.Add(new Settings
        {
            TenantId = tenant.Id,
            NamaToko = tenantName,
            Alamat = "",
            Telepon = "",
            Email = "",
            Website = "",
            HeaderStruk =
                "Terima kasih telah berkunjung!",
            FooterStruk =
                "Barang yang sudah dibeli tidak dapat dikembalikan.",
            ShowTax = false,
            ShowPoint = true,
            DefaultTax = 0,
            MinStok = 5,
            PoinRate = 1
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDemoAsync(
        AppDbContext db)
    {
        var tenant = new Tenant
        {
            NamaToko = "WARUNG LUMPIA BEEF",
            Slug = "warung-lumpia-beef"
        };

        db.Tenants.Add(tenant);

        await db.SaveChangesAsync();

        db.Users.AddRange(
            new User
            {
                TenantId = tenant.Id,
                Nama = "Administrator",
                Username = "owner",
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        "owner123"),
                Role = "owner",
                Active = true
            },
            new User
            {
                TenantId = tenant.Id,
                Nama = "Admin Toko",
                Username = "admin",
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        "admin123"),
                Role = "admin",
                Active = true
            },
            new User
            {
                TenantId = tenant.Id,
                Nama = "Kasir Utama",
                Username = "kasir",
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword(
                        "kasir123"),
                Role = "kasir",
                Active = true
            });

        db.Settings.Add(new Settings
        {
            TenantId = tenant.Id,
            NamaToko = tenant.NamaToko,
            Alamat =
                "Jl. Kuliner No. 1, Kota Anda",
            Telepon = "081234567890",
            Email = "info@lumpiabeef.id",
            Website = "",
            HeaderStruk =
                "Terima kasih telah berkunjung!",
            FooterStruk =
                "Barang yang sudah dibeli tidak dapat dikembalikan.",
            ShowTax = false,
            ShowPoint = true,
            DefaultTax = 0,
            MinStok = 5,
            PoinRate = 1
        });

        db.Products.AddRange(
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD001",
                Nama = "Lumpia Beef Original",
                Kategori = "Lumpia",
                HargaModal = 12000,
                HargaJual = 18000,
                Stok = 100,
                Supplier = "Internal",
                Satuan = "pcs"
            },
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD002",
                Nama = "Lumpia Beef Pedas",
                Kategori = "Lumpia",
                HargaModal = 12500,
                HargaJual = 19000,
                Stok = 100,
                Supplier = "Internal",
                Satuan = "pcs"
            },
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD003",
                Nama = "Lumpia Ubi Ungu",
                Kategori = "Lumpia",
                HargaModal = 9000,
                HargaJual = 15000,
                Stok = 80,
                Supplier = "Internal",
                Satuan = "pcs"
            },
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD004",
                Nama = "Burger Beef Klasik",
                Kategori = "Burger",
                HargaModal = 18000,
                HargaJual = 30000,
                Stok = 60,
                Supplier = "Internal",
                Satuan = "pcs"
            },
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD005",
                Nama = "Burger Beef Double",
                Kategori = "Burger",
                HargaModal = 25000,
                HargaJual = 42000,
                Stok = 50,
                Supplier = "Internal",
                Satuan = "pcs"
            },
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD006",
                Nama = "Burger Crispy Chicken",
                Kategori = "Burger",
                HargaModal = 17000,
                HargaJual = 28000,
                Stok = 60,
                Supplier = "Internal",
                Satuan = "pcs"
            },
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD007",
                Nama = "Paket Hemat 3 Lumpia",
                Kategori = "Paket",
                HargaModal = 30000,
                HargaJual = 45000,
                Stok = 40,
                Supplier = "Internal",
                Satuan = "paket"
            },
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD008",
                Nama = "Combo Burger + Lumpia",
                Kategori = "Paket",
                HargaModal = 32000,
                HargaJual = 50000,
                Stok = 40,
                Supplier = "Internal",
                Satuan = "paket"
            },
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD009",
                Nama = "Es Teh Manis",
                Kategori = "Minuman",
                HargaModal = 2500,
                HargaJual = 7000,
                Stok = 200,
                Supplier = "Internal",
                Satuan = "gelas"
            },
            new Product
            {
                TenantId = tenant.Id,
                Kode = "PRD010",
                Nama = "Es Jeruk Peras",
                Kategori = "Minuman",
                HargaModal = 4000,
                HargaJual = 10000,
                Stok = 150,
                Supplier = "Internal",
                Satuan = "gelas"
            });

        db.Customers.AddRange(
            new Customer
            {
                TenantId = tenant.Id,
                Nama = "Budi Santoso",
                Hp = "081111111111",
                Poin = 150,
                TotalTransaksi = 12
            },
            new Customer
            {
                TenantId = tenant.Id,
                Nama = "Siti Rahma",
                Hp = "082222222222",
                Poin = 80,
                TotalTransaksi = 8
            },
            new Customer
            {
                TenantId = tenant.Id,
                Nama = "Ahmad Fauzi",
                Hp = "083333333333",
                Poin = 200,
                TotalTransaksi = 20
            });

        db.Karyawans.AddRange(
            new Karyawan
            {
                TenantId = tenant.Id,
                Nama = "Dewi Safitri",
                Jabatan = "Kasir",
                Gaji = 3500000,
                Status = "aktif",
                TanggalMasuk =
                    DateOnly.FromDateTime(
                        DateTime.Today.AddMonths(-12))
            },
            new Karyawan
            {
                TenantId = tenant.Id,
                Nama = "Budi Santoso",
                Jabatan = "Staff Gudang",
                Gaji = 3400000,
                Status = "aktif",
                TanggalMasuk =
                    DateOnly.FromDateTime(
                        DateTime.Today.AddMonths(-10))
            },
            new Karyawan
            {
                TenantId = tenant.Id,
                Nama = "Sari Indah",
                Jabatan = "Kasir",
                Gaji = 3500000,
                Status = "aktif",
                TanggalMasuk =
                    DateOnly.FromDateTime(
                        DateTime.Today.AddMonths(-8))
            },
            new Karyawan
            {
                TenantId = tenant.Id,
                Nama = "Rizki Pratama",
                Jabatan = "Supervisor",
                Gaji = 5000000,
                Status = "aktif",
                TanggalMasuk =
                    DateOnly.FromDateTime(
                        DateTime.Today.AddMonths(-18))
            });

        await db.SaveChangesAsync();
    }

    private static string Require(
        IConfiguration configuration,
        string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} missing.");
        }

        return value.Trim();
    }
}

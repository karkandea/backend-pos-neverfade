using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Data.Seed;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.Tenants.AnyAsync())
            return;

        var tenant = new Tenant
        {
            NamaToko = "WARUNG LUMPIA BEEF",
            Slug = "warung-lumpia-beef"
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var owner = new User
        {
            TenantId = tenant.Id,
            Nama = "Administrator",
            Username = "owner",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("owner123"),
            Role = "owner",
            Active = true
        };

        var admin = new User
        {
            TenantId = tenant.Id,
            Nama = "Admin Toko",
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Role = "admin",
            Active = true
        };

        var kasir = new User
        {
            TenantId = tenant.Id,
            Nama = "Kasir Utama",
            Username = "kasir",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("kasir123"),
            Role = "kasir",
            Active = true
        };

        db.Users.AddRange(owner, admin, kasir);

        db.Settings.Add(new Settings
        {
            TenantId = tenant.Id,
            NamaToko = tenant.NamaToko,
            Alamat = "Jl. Kuliner No. 1, Kota Anda",
            Telepon = "081234567890",
            Email = "info@lumpiabeef.id",
            Website = "",
            HeaderStruk = "Terima kasih telah berkunjung!",
            FooterStruk = "Barang yang sudah dibeli tidak dapat dikembalikan.",
            ShowTax = false,
            ShowPoint = true,
            DefaultTax = 0,
            MinStok = 5,
            PoinRate = 1
        });

        db.Products.AddRange(
            new Product { TenantId = tenant.Id, Kode = "PRD001", Nama = "Lumpia Beef Original", Kategori = "Lumpia", HargaModal = 12000, HargaJual = 18000, Stok = 100, Supplier = "Internal", Satuan = "pcs" },
            new Product { TenantId = tenant.Id, Kode = "PRD002", Nama = "Lumpia Beef Pedas", Kategori = "Lumpia", HargaModal = 12500, HargaJual = 19000, Stok = 100, Supplier = "Internal", Satuan = "pcs" },
            new Product { TenantId = tenant.Id, Kode = "PRD003", Nama = "Lumpia Ubi Ungu", Kategori = "Lumpia", HargaModal = 9000, HargaJual = 15000, Stok = 80, Supplier = "Internal", Satuan = "pcs" },
            new Product { TenantId = tenant.Id, Kode = "PRD004", Nama = "Burger Beef Klasik", Kategori = "Burger", HargaModal = 18000, HargaJual = 30000, Stok = 60, Supplier = "Internal", Satuan = "pcs" },
            new Product { TenantId = tenant.Id, Kode = "PRD005", Nama = "Burger Beef Double", Kategori = "Burger", HargaModal = 25000, HargaJual = 42000, Stok = 50, Supplier = "Internal", Satuan = "pcs" },
            new Product { TenantId = tenant.Id, Kode = "PRD006", Nama = "Burger Crispy Chicken", Kategori = "Burger", HargaModal = 17000, HargaJual = 28000, Stok = 60, Supplier = "Internal", Satuan = "pcs" },
            new Product { TenantId = tenant.Id, Kode = "PRD007", Nama = "Paket Hemat 3 Lumpia", Kategori = "Paket", HargaModal = 30000, HargaJual = 45000, Stok = 40, Supplier = "Internal", Satuan = "paket" },
            new Product { TenantId = tenant.Id, Kode = "PRD008", Nama = "Combo Burger + Lumpia", Kategori = "Paket", HargaModal = 32000, HargaJual = 50000, Stok = 40, Supplier = "Internal", Satuan = "paket" },
            new Product { TenantId = tenant.Id, Kode = "PRD009", Nama = "Es Teh Manis", Kategori = "Minuman", HargaModal = 2500, HargaJual = 7000, Stok = 200, Supplier = "Internal", Satuan = "gelas" },
            new Product { TenantId = tenant.Id, Kode = "PRD010", Nama = "Es Jeruk Peras", Kategori = "Minuman", HargaModal = 4000, HargaJual = 10000, Stok = 150, Supplier = "Internal", Satuan = "gelas" }
        );

        db.Customers.AddRange(
            new Customer { TenantId = tenant.Id, Nama = "Budi Santoso", Hp = "081111111111", Poin = 150, TotalTransaksi = 12 },
            new Customer { TenantId = tenant.Id, Nama = "Siti Rahma", Hp = "082222222222", Poin = 80, TotalTransaksi = 8 },
            new Customer { TenantId = tenant.Id, Nama = "Ahmad Fauzi", Hp = "083333333333", Poin = 200, TotalTransaksi = 20 }
        );

        db.Karyawans.AddRange(
            new Karyawan { TenantId = tenant.Id, Nama = "Dewi Safitri", Jabatan = "Kasir", Gaji = 3500000, Status = "aktif", TanggalMasuk = DateOnly.FromDateTime(DateTime.Today.AddMonths(-12)) },
            new Karyawan { TenantId = tenant.Id, Nama = "Budi Santoso", Jabatan = "Staff Gudang", Gaji = 3400000, Status = "aktif", TanggalMasuk = DateOnly.FromDateTime(DateTime.Today.AddMonths(-10)) },
            new Karyawan { TenantId = tenant.Id, Nama = "Sari Indah", Jabatan = "Kasir", Gaji = 3500000, Status = "aktif", TanggalMasuk = DateOnly.FromDateTime(DateTime.Today.AddMonths(-8)) },
            new Karyawan { TenantId = tenant.Id, Nama = "Rizki Pratama", Jabatan = "Supervisor", Gaji = 5000000, Status = "aktif", TanggalMasuk = DateOnly.FromDateTime(DateTime.Today.AddMonths(-18)) }
        );

        await db.SaveChangesAsync();
    }
}

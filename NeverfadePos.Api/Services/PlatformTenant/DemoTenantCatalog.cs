using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.PlatformTenant;

/// <summary>
/// Minimal opt-in, tenant-owned sample catalog for demo provisioning only.
/// No payment, order, transaction, or live merchant data is copied or seeded.
/// </summary>
internal static class DemoTenantCatalog
{
    public static void Add(AppDbContext db, Guid tenantId, Guid outletId, string businessType)
    {
        var entries = businessType switch
        {
            BusinessTypes.GeneralRetail => new[]
            {
                ("DEMO-GR-01", "Air Mineral 600ml", "Minuman", 3000m, 5000m, "pcs", false, 0),
                ("DEMO-GR-02", "Roti Cokelat", "Makanan", 4500m, 7000m, "pcs", false, 0)
            },
            BusinessTypes.FashionRetail => new[]
            {
                ("DEMO-FA-01", "Kaos Polos", "Pakaian", 35000m, 70000m, "pcs", false, 0),
                ("DEMO-FA-02", "Tote Bag", "Aksesori", 18000m, 39000m, "pcs", false, 0)
            },
            BusinessTypes.FoodBeverage => new[]
            {
                ("DEMO-FB-01", "Kopi Susu", "Minuman", 8000m, 18000m, "gelas", false, 0),
                ("DEMO-FB-02", "Nasi Goreng", "Makanan", 13000m, 28000m, "porsi", false, 0)
            },
            BusinessTypes.Laundry => new[]
            {
                ("DEMO-LD-01", "Cuci Kering", "Layanan", 0m, 7000m, "kg", true, 2),
                ("DEMO-LD-02", "Setrika", "Layanan", 0m, 5000m, "kg", true, 2)
            },
            BusinessTypes.SalonBarbershop => new[]
            {
                ("DEMO-SB-01", "Potong Rambut", "Layanan", 0m, 45000m, "layanan", true, 0),
                ("DEMO-SB-02", "Keramas", "Layanan", 0m, 25000m, "layanan", true, 0)
            },
            _ => throw new InvalidOperationException("Unsupported demo business type.")
        };

        foreach (var (code, name, category, cost, price, unit, service, precision) in entries)
        {
            db.Products.Add(new NeverfadePos.Api.Entities.Product
            {
                TenantId = tenantId, Kode = code, Barcode = string.Empty,
                Nama = name, Kategori = category, HargaModal = cost,
                HargaJual = price, Stok = service ? 0 : 25,
                Supplier = string.Empty, Satuan = unit,
                Deskripsi = "Contoh untuk tenant demo; bukan transaksi nyata.",
                Type = service ? ProductTypes.Service : ProductTypes.Goods,
                TracksStock = !service, QuantityPrecision = precision
            });
        }

        if (businessType == BusinessTypes.FoodBeverage)
        {
            db.RestaurantTables.AddRange(
                new RestaurantTable
                {
                    TenantId = tenantId, OutletId = outletId, Code = "A1",
                    Name = "Meja A1", Capacity = 4, SortOrder = 1
                },
                new RestaurantTable
                {
                    TenantId = tenantId, OutletId = outletId, Code = "A2",
                    Name = "Meja A2", Capacity = 4, SortOrder = 2
                });
        }
    }
}

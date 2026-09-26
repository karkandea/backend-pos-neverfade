using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Onboarding;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Onboarding;

public interface ITenantOnboardingService
{
    Task<TenantOnboardingDto> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only, persisted-state-derived setup checklist. Never claims a merchant is
/// certified for release, never copies demo sample data into a live tenant.
/// </summary>
public sealed class TenantOnboardingService(AppDbContext db, CurrentUser currentUser)
    : ITenantOnboardingService
{
    public async Task<TenantOnboardingDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId;
        if (tenantId is null || tenantId == Guid.Empty ||
            currentUser.Role is not ("owner" or "admin"))
            throw new TenantApiException(StatusCodes.Status403Forbidden,
                "ONBOARDING_ACCESS_DENIED", "Hanya owner atau admin yang dapat melihat setup bisnis.");

        var tenant = await db.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == tenantId.Value, cancellationToken)
            ?? throw new TenantApiException(StatusCodes.Status404NotFound,
                "TENANT_NOT_FOUND", "Tenant tidak ditemukan.");
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var outlets = await db.Outlets.AsNoTracking()
            .Where(x => x.Active)
            .Select(x => new { x.Id, x.IsDefault, x.Name, x.Address, x.Phone })
            .ToListAsync(cancellationToken);
        var products = await db.Products.AsNoTracking()
            .Select(x => new { x.Id, x.Type, x.Nama, x.HargaJual })
            .ToListAsync(cancellationToken);
        var defaultOutletIds = outlets.Where(x => x.IsDefault).Select(x => x.Id).ToArray();
        var hasStaff = await db.Users.AsNoTracking()
            .Where(x => x.Active && x.Role != "owner")
            .AnyAsync(x => db.UserOutletAssignments.Any(a => a.UserId == x.Id &&
                db.Outlets.Any(o => o.Id == a.OutletId && o.Active)), cancellationToken);

        var steps = new List<TenantOnboardingStepDto>();
        void Add(string id, string title, string description, string actionPath, bool required, bool complete) =>
            steps.Add(new TenantOnboardingStepDto
            {
                Id = id, Title = title, Description = description,
                ActionPath = actionPath, Required = required, Complete = complete
            });

        Add("business_profile", "Identitas usaha", "Isi nama, alamat dan nomor telepon usaha pada pengaturan.",
            "/pengaturan", true, settings is not null &&
            !string.IsNullOrWhiteSpace(settings.NamaToko) &&
            !string.IsNullOrWhiteSpace(settings.Alamat) &&
            !string.IsNullOrWhiteSpace(settings.Telepon));
        Add("default_outlet", "Outlet utama", "Lengkapi nama, alamat dan nomor telepon outlet utama.",
            "/pengaturan", true, outlets.Any(x => x.IsDefault &&
                !string.IsNullOrWhiteSpace(x.Name) &&
                !string.IsNullOrWhiteSpace(x.Address) &&
                !string.IsNullOrWhiteSpace(x.Phone)));
        Add("catalog", "Katalog produk atau jasa", "Tambahkan setidaknya satu produk atau jasa dengan nama dan harga valid.",
            "/produk", true, products.Any(x => !string.IsNullOrWhiteSpace(x.Nama) && x.HargaJual > 0));

        switch (tenant.BusinessType)
        {
            case BusinessTypes.FoodBeverage:
                var tableReady = defaultOutletIds.Length > 0 &&
                    await db.RestaurantTables.AsNoTracking().AnyAsync(
                        x => defaultOutletIds.Contains(x.OutletId) && x.Active, cancellationToken);
                Add("restaurant_tables", "Meja restoran", "Tambahkan minimal satu meja aktif untuk outlet utama.",
                    "/meja", true, tableReady);
                break;
            case BusinessTypes.Laundry:
                Add("laundry_services", "Layanan laundry", "Tambahkan layanan berbasis jasa, misalnya cuci dan setrika.",
                    "/produk", true, products.Any(x => x.Type == ProductTypes.Service && x.HargaJual > 0));
                break;
            case BusinessTypes.SalonBarbershop:
                Add("salon_services", "Daftar layanan", "Tambahkan layanan potong rambut atau perawatan dengan harga.",
                    "/produk", true, products.Any(x => x.Type == ProductTypes.Service && x.HargaJual > 0));
                break;
        }

        Add("cash_payment", "Pembayaran tunai", "Pembayaran tunai tersedia; QRIS perlu konfigurasi dan verifikasi terpisah.",
            "/kasir", false, true);
        Add("staff_assignment", "Petugas outlet", "Opsional untuk usaha yang dijalankan sendiri: buat akun petugas dan tugaskan outlet.",
            "/pengguna", false, hasStaff);
        if (tenant.Mode == "demo")
            Add("demo_notice", "Lingkungan demo", "Data contoh khusus tenant demo; bukan data merchant atau sertifikasi rilis.",
                "/produk", false, true);

        var required = steps.Where(x => x.Required).ToArray();
        return new TenantOnboardingDto
        {
            TenantId = tenant.Id,
            Mode = tenant.Mode,
            BusinessType = tenant.BusinessType,
            CompletedRequired = required.Count(x => x.Complete),
            TotalRequired = required.Length,
            RequiredStepsComplete = required.All(x => x.Complete),
            Steps = steps
        };
    }
}

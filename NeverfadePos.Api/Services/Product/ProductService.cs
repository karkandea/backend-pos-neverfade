using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Product;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Stock;
using Npgsql;
using ProductEntity = NeverfadePos.Api.Entities.Product;

namespace NeverfadePos.Api.Services.Product;

public sealed class ProductService(
    AppDbContext db,
    CurrentUser currentUser,
    IStockBalanceService stockBalances)
    : IProductService
{
    public async Task<List<ProductDto>> GetAllAsync(
        string? search,
        string? kategori,
        CancellationToken cancellationToken = default)
    {
        var query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Nama.Contains(search) ||
                x.Kode.Contains(search) ||
                x.Barcode.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(kategori))
            query = query.Where(x => x.Kategori == kategori);

        var products = await query.OrderBy(x => x.Nama).ToListAsync(cancellationToken);
        var stocks = await stockBalances.GetProductAvailableUnitsAsync(
            products.Select(x => x.Id).ToArray(), cancellationToken);
        return products.Select(x => MapToDto(x, stocks.GetValueOrDefault(x.Id))).ToList();
    }

    public async Task<ProductDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Product tidak ditemukan.");
        var stock = await stockBalances.GetProductAvailableUnitsAsync(id, cancellationToken);
        return MapToDto(product, stock);
    }

    public async Task<ProductDto> CreateAsync(
        CreateProductDto request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue)
            throw new UnauthorizedAccessException();

        if (await db.Products.AnyAsync(x => x.Kode == request.Kode, cancellationToken))
            throw new InvalidOperationException("Kode produk sudah digunakan.");

        var profile = NormalizeProfile(
            request.Type, request.TracksStock, request.QuantityPrecision, request.Stok);

        var entity = new ProductEntity
        {
            TenantId = currentUser.TenantId.Value,
            Kode = request.Kode,
            Barcode = request.Barcode,
            Nama = request.Nama,
            Kategori = request.Kategori,
            HargaModal = request.HargaModal,
            HargaJual = request.HargaJual,
            Stok = 0,
            Supplier = request.Supplier,
            Satuan = request.Satuan,
            Deskripsi = request.Deskripsi,
            Type = profile.Type,
            TracksStock = profile.TracksStock,
            QuantityPrecision = profile.QuantityPrecision
        };
        db.Products.Add(entity);

        if (entity.TracksStock && profile.Stok > 0)
        {
            await stockBalances.SetAsync(
                entity, null, profile.Stok, "Stok awal produk",
                currentUser.Username ?? string.Empty, cancellationToken);
        }
        else
        {
            entity.Stok = profile.Stok;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ProductDto> UpdateAsync(
        Guid id,
        UpdateProductDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Products.FirstOrDefaultAsync(
            x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Product tidak ditemukan.");

        if (await db.Products.AnyAsync(
            x => x.Id != id && x.Kode == request.Kode, cancellationToken))
            throw new InvalidOperationException("Kode produk sudah digunakan.");

        var profile = NormalizeProfile(
            request.Type, request.TracksStock, request.QuantityPrecision, request.Stok);
        var currentOutletStock = await stockBalances.GetProductAvailableUnitsAsync(
            entity.Id, cancellationToken);
        var hasVariants = await db.ProductVariants.AnyAsync(
            x => x.ProductId == entity.Id, cancellationToken);

        if (hasVariants &&
            (request.Stok != currentOutletStock ||
             profile.Type != entity.Type ||
             profile.TracksStock != entity.TracksStock))
        {
            throw new TenantApiException(
                StatusCodes.Status409Conflict,
                "VARIANT_PRODUCT_STOCK_MANAGED_SEPARATELY",
                "Stok dan tipe produk bervarian harus dikelola melalui varian.");
        }

        if (entity.Type != profile.Type && entity.Stok != 0)
        {
            throw new TenantApiException(
                StatusCodes.Status409Conflict,
                "PRODUCT_TYPE_CHANGE_REQUIRES_ZERO_STOCK",
                "Tipe produk hanya dapat diubah setelah stok menjadi 0.");
        }

        entity.Kode = request.Kode;
        entity.Barcode = request.Barcode;
        entity.Nama = request.Nama;
        entity.Kategori = request.Kategori;
        entity.HargaModal = request.HargaModal;
        entity.HargaJual = request.HargaJual;
        entity.Supplier = request.Supplier;
        entity.Satuan = request.Satuan;
        entity.Deskripsi = request.Deskripsi;
        entity.Type = profile.Type;
        entity.TracksStock = profile.TracksStock;
        entity.QuantityPrecision = profile.QuantityPrecision;

        if (!hasVariants && entity.TracksStock)
        {
            await stockBalances.SetAsync(
                entity, null, profile.Stok, "Penyesuaian dari edit produk",
                currentUser.Username ?? string.Empty, cancellationToken);
        }
        else if (!entity.TracksStock)
        {
            entity.Stok = profile.Stok;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Products.FirstOrDefaultAsync(
            x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Product tidak ditemukan.");

        db.Products.Remove(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pg &&
                  pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new ConflictException(
                "Produk tidak dapat dihapus karena memiliki riwayat stok atau transaksi.");
        }
    }

    private static ProductProfile NormalizeProfile(
        string? type, bool tracksStock, int quantityPrecision, int stock)
    {
        var normalizedType = type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ProductTypes.IsValid(normalizedType))
            throw new TenantApiException(StatusCodes.Status400BadRequest,
                "PRODUCT_TYPE_INVALID", "Tipe produk harus goods atau service.");

        if (quantityPrecision is < 0 or > ProductQuantityRules.MaxPrecision)
            throw new TenantApiException(StatusCodes.Status400BadRequest,
                "PRODUCT_QUANTITY_PRECISION_INVALID",
                "Presisi jumlah produk harus antara 0 sampai 3.");

        if (normalizedType == ProductTypes.Goods)
        {
            if (quantityPrecision != 0)
                throw new TenantApiException(StatusCodes.Status400BadRequest,
                    "GOODS_QUANTITY_PRECISION_INVALID",
                    "Barang harus menggunakan jumlah bilangan bulat.");
            return new ProductProfile(normalizedType, tracksStock, 0, stock);
        }

        return new ProductProfile(ProductTypes.Service, false, quantityPrecision, 0);
    }

    private static ProductDto MapToDto(ProductEntity x, int stock) => new()
    {
        Id = x.Id,
        Kode = x.Kode,
        Barcode = x.Barcode,
        Nama = x.Nama,
        Kategori = x.Kategori,
        HargaModal = x.HargaModal,
        HargaJual = x.HargaJual,
        Stok = stock,
        Supplier = x.Supplier,
        Satuan = x.Satuan,
        Deskripsi = x.Deskripsi,
        Type = x.Type,
        TracksStock = x.TracksStock,
        QuantityPrecision = x.QuantityPrecision,
        CreatedAt = x.CreatedAt
    };

    private sealed record ProductProfile(
        string Type, bool TracksStock, int QuantityPrecision, int Stok);
}

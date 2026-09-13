using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Retail;
using NeverfadePos.Api.Entities;
using ProductEntity = NeverfadePos.Api.Entities.Product;

namespace NeverfadePos.Api.Services.Retail;

public sealed class RetailCatalogService(
    AppDbContext db,
    CurrentUser currentUser)
    : IRetailCatalogService
{
    private Guid TenantId => currentUser.TenantId
        ?? throw new UnauthorizedAccessException();

    public async Task<RetailCatalogDto> GetCatalogAsync(
        string? search,
        string? kategori,
        CancellationToken cancellationToken = default)
    {
        var query = db.Products
            .AsNoTracking()
            .Include(x => x.Variants)
            .Include(x => x.Prices)
                .ThenInclude(x => x.PriceLevel)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Nama.Contains(term) ||
                x.Kode.Contains(term) ||
                x.Barcode.Contains(term) ||
                x.Variants.Any(v => v.Sku.Contains(term) || v.Barcode.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(kategori))
        {
            query = query.Where(x => x.Kategori == kategori);
        }

        var products = await query
            .OrderBy(x => x.Nama)
            .ToListAsync(cancellationToken);

        var levels = await GetPriceLevelsAsync(cancellationToken);

        return new RetailCatalogDto
        {
            PriceLevels = levels,
            Products = products.Select(MapCatalogProduct).ToList()
        };
    }

    public async Task<List<ProductVariantDto>> GetVariantsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await RequireProductAsync(productId, cancellationToken);
        return await db.ProductVariants
            .AsNoTracking()
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.Label)
            .Select(x => MapVariant(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductVariantDto> CreateVariantAsync(
        CreateProductVariantDto request,
        CancellationToken cancellationToken = default)
    {
        var product = await RequireProductAsync(request.ProductId, cancellationToken);
        EnsureVariantProduct(product);

        var existingCount = await db.ProductVariants
            .CountAsync(x => x.ProductId == product.Id, cancellationToken);

        if (existingCount == 0 && product.Stok != 0)
        {
            throw Conflict(
                "VARIANT_INITIALIZATION_REQUIRES_ZERO_PARENT_STOCK",
                "Stok produk harus 0 sebelum stok dipisahkan menjadi varian.");
        }

        var normalized = NormalizeVariant(request);
        await EnsureVariantUniqueAsync(null, normalized.Sku, normalized.Barcode, cancellationToken);

        var entity = new ProductVariant
        {
            TenantId = TenantId,
            ProductId = product.Id,
            Sku = normalized.Sku,
            Barcode = normalized.Barcode,
            Label = normalized.Label,
            Option1Name = normalized.Option1Name,
            Option1Value = normalized.Option1Value,
            Option2Name = normalized.Option2Name,
            Option2Value = normalized.Option2Value,
            Option3Name = normalized.Option3Name,
            Option3Value = normalized.Option3Value,
            HargaModal = request.HargaModal,
            HargaJual = request.HargaJual,
            Stok = request.Stok,
            Active = true
        };

        product.Stok += entity.Stok;
        db.ProductVariants.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return MapVariant(entity);
    }

    public async Task<ProductVariantDto> UpdateVariantAsync(
        Guid id,
        UpdateProductVariantDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ProductVariants
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("VARIANT_NOT_FOUND", "Varian produk tidak ditemukan.");

        if (request.ProductId != Guid.Empty && request.ProductId != entity.ProductId)
        {
            throw Conflict("VARIANT_PRODUCT_IMMUTABLE", "Varian tidak dapat dipindahkan ke produk lain.");
        }

        var product = entity.Product
            ?? throw new InvalidOperationException("Produk varian tidak tersedia.");
        EnsureVariantProduct(product);

        var normalized = NormalizeVariant(request);
        await EnsureVariantUniqueAsync(entity.Id, normalized.Sku, normalized.Barcode, cancellationToken);

        var stockDelta = request.Stok - entity.Stok;
        if (product.Stok + stockDelta < 0)
        {
            throw Conflict("VARIANT_STOCK_INVALID", "Stok agregat produk tidak boleh negatif.");
        }

        product.Stok += stockDelta;
        entity.Sku = normalized.Sku;
        entity.Barcode = normalized.Barcode;
        entity.Label = normalized.Label;
        entity.Option1Name = normalized.Option1Name;
        entity.Option1Value = normalized.Option1Value;
        entity.Option2Name = normalized.Option2Name;
        entity.Option2Value = normalized.Option2Value;
        entity.Option3Name = normalized.Option3Name;
        entity.Option3Value = normalized.Option3Value;
        entity.HargaModal = request.HargaModal;
        entity.HargaJual = request.HargaJual;
        entity.Stok = request.Stok;
        entity.Active = request.Active;

        await db.SaveChangesAsync(cancellationToken);
        return MapVariant(entity);
    }

    public async Task DeleteVariantAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ProductVariants
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("VARIANT_NOT_FOUND", "Varian produk tidak ditemukan.");

        if (entity.Stok != 0)
        {
            throw Conflict("VARIANT_DELETE_REQUIRES_ZERO_STOCK", "Varian hanya dapat dihapus setelah stok menjadi 0.");
        }

        db.ProductVariants.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<PriceLevelDto>> GetPriceLevelsAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.PriceLevels
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => MapLevel(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<PriceLevelDto> CreatePriceLevelAsync(
        CreatePriceLevelDto request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code);
        var name = RequireText(request.Name, "Nama level harga wajib diisi.");
        await EnsureLevelCodeUniqueAsync(null, code, cancellationToken);

        var entity = new PriceLevel
        {
            TenantId = TenantId,
            Code = code,
            Name = name,
            SortOrder = request.SortOrder,
            Active = true
        };
        db.PriceLevels.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return MapLevel(entity);
    }

    public async Task<PriceLevelDto> UpdatePriceLevelAsync(
        Guid id,
        UpdatePriceLevelDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PriceLevels
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("PRICE_LEVEL_NOT_FOUND", "Level harga tidak ditemukan.");

        var code = NormalizeCode(request.Code);
        await EnsureLevelCodeUniqueAsync(entity.Id, code, cancellationToken);
        entity.Code = code;
        entity.Name = RequireText(request.Name, "Nama level harga wajib diisi.");
        entity.SortOrder = request.SortOrder;
        entity.Active = request.Active;
        await db.SaveChangesAsync(cancellationToken);
        return MapLevel(entity);
    }

    public async Task DeletePriceLevelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.PriceLevels
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("PRICE_LEVEL_NOT_FOUND", "Level harga tidak ditemukan.");

        if (await db.ProductPrices.AnyAsync(x => x.PriceLevelId == id, cancellationToken))
        {
            throw Conflict("PRICE_LEVEL_IN_USE", "Level harga masih digunakan. Nonaktifkan level atau hapus aturan harganya terlebih dahulu.");
        }

        db.PriceLevels.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ProductPriceDto>> GetPricesAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        await RequireProductAsync(productId, cancellationToken);
        return await db.ProductPrices
            .AsNoTracking()
            .Include(x => x.PriceLevel)
            .Where(x => x.ProductId == productId)
            .OrderBy(x => x.MinQuantity)
            .ThenBy(x => x.PriceLevel!.SortOrder)
            .Select(x => MapPrice(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductPriceDto> CreatePriceAsync(
        CreateProductPriceDto request,
        CancellationToken cancellationToken = default)
    {
        var context = await ValidatePriceRequestAsync(null, request, cancellationToken);
        var entity = new ProductPrice
        {
            TenantId = TenantId,
            ProductId = context.Product.Id,
            ProductVariantId = context.Variant?.Id,
            PriceLevelId = context.Level.Id,
            MinQuantity = request.MinQuantity,
            UnitPrice = Money(request.UnitPrice)
        };
        db.ProductPrices.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        entity.PriceLevel = context.Level;
        return MapPrice(entity);
    }

    public async Task<ProductPriceDto> UpdatePriceAsync(
        Guid id,
        UpdateProductPriceDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ProductPrices
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("PRODUCT_PRICE_NOT_FOUND", "Aturan harga tidak ditemukan.");

        var context = await ValidatePriceRequestAsync(entity.Id, request, cancellationToken);
        entity.ProductId = context.Product.Id;
        entity.ProductVariantId = context.Variant?.Id;
        entity.PriceLevelId = context.Level.Id;
        entity.MinQuantity = request.MinQuantity;
        entity.UnitPrice = Money(request.UnitPrice);
        await db.SaveChangesAsync(cancellationToken);
        entity.PriceLevel = context.Level;
        return MapPrice(entity);
    }

    public async Task DeletePriceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ProductPrices
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("PRODUCT_PRICE_NOT_FOUND", "Aturan harga tidak ditemukan.");
        db.ProductPrices.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProductEntity> RequireProductAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw NotFound("PRODUCT_NOT_FOUND", "Produk tidak ditemukan.");
    }

    private static void EnsureVariantProduct(ProductEntity product)
    {
        if (product.Type != ProductTypes.Goods || !product.TracksStock)
        {
            throw new TenantApiException(
                StatusCodes.Status400BadRequest,
                "VARIANT_REQUIRES_TRACKED_GOODS",
                "Varian stok hanya didukung untuk barang yang melacak stok.");
        }
    }

    private async Task EnsureVariantUniqueAsync(
        Guid? exceptId,
        string sku,
        string barcode,
        CancellationToken cancellationToken)
    {
        if (await db.ProductVariants.AnyAsync(
                x => (!exceptId.HasValue || x.Id != exceptId.Value) && x.Sku == sku,
                cancellationToken))
            throw Conflict("VARIANT_SKU_DUPLICATE", "SKU varian sudah digunakan.");

        if (!string.IsNullOrEmpty(barcode) && await db.ProductVariants.AnyAsync(
                x => (!exceptId.HasValue || x.Id != exceptId.Value) && x.Barcode == barcode,
                cancellationToken))
            throw Conflict("VARIANT_BARCODE_DUPLICATE", "Barcode varian sudah digunakan.");
    }

    private async Task EnsureLevelCodeUniqueAsync(Guid? exceptId, string code, CancellationToken cancellationToken)
    {
        if (await db.PriceLevels.AnyAsync(
                x => (!exceptId.HasValue || x.Id != exceptId.Value) && x.Code == code,
                cancellationToken))
            throw Conflict("PRICE_LEVEL_CODE_DUPLICATE", "Kode level harga sudah digunakan.");
    }

    private async Task<PriceRequestContext> ValidatePriceRequestAsync(
        Guid? exceptId,
        CreateProductPriceDto request,
        CancellationToken cancellationToken)
    {
        var product = await RequireProductAsync(request.ProductId, cancellationToken);
        ProductQuantityRules.Validate(product, request.MinQuantity, enforceStock: false);

        var level = await db.PriceLevels
            .FirstOrDefaultAsync(x => x.Id == request.PriceLevelId, cancellationToken)
            ?? throw NotFound("PRICE_LEVEL_NOT_FOUND", "Level harga tidak ditemukan.");

        if (!level.Active)
            throw Conflict("PRICE_LEVEL_INACTIVE", "Level harga sedang nonaktif.");

        ProductVariant? variant = null;
        if (request.ProductVariantId.HasValue)
        {
            variant = await db.ProductVariants.FirstOrDefaultAsync(
                x => x.Id == request.ProductVariantId.Value,
                cancellationToken)
                ?? throw NotFound("VARIANT_NOT_FOUND", "Varian produk tidak ditemukan.");
            if (variant.ProductId != product.Id)
                throw Conflict("VARIANT_PRODUCT_MISMATCH", "Varian tidak berasal dari produk yang dipilih.");
        }

        if (await db.ProductPrices.AnyAsync(
                x => (!exceptId.HasValue || x.Id != exceptId.Value) &&
                     x.ProductId == product.Id &&
                     x.ProductVariantId == request.ProductVariantId &&
                     x.PriceLevelId == level.Id,
                cancellationToken))
            throw Conflict("PRODUCT_PRICE_DUPLICATE", "Aturan level harga untuk item ini sudah ada.");

        return new PriceRequestContext(product, variant, level);
    }

    private static RetailCatalogProductDto MapCatalogProduct(ProductEntity product) => new()
    {
        Id = product.Id,
        Kode = product.Kode,
        Barcode = product.Barcode,
        Nama = product.Nama,
        Kategori = product.Kategori,
        HargaModal = product.HargaModal,
        HargaJual = product.HargaJual,
        Stok = product.Stok,
        Supplier = product.Supplier,
        Satuan = product.Satuan,
        Deskripsi = product.Deskripsi,
        Type = product.Type,
        TracksStock = product.TracksStock,
        QuantityPrecision = product.QuantityPrecision,
        Variants = product.Variants.OrderBy(x => x.Label).Select(MapVariant).ToList(),
        Prices = product.Prices
            .Where(x => x.PriceLevel is not null)
            .OrderBy(x => x.MinQuantity)
            .ThenBy(x => x.PriceLevel!.SortOrder)
            .Select(MapPrice)
            .ToList()
    };

    private static ProductVariantDto MapVariant(ProductVariant x) => new()
    {
        Id = x.Id,
        ProductId = x.ProductId,
        Sku = x.Sku,
        Barcode = x.Barcode,
        Label = x.Label,
        Option1Name = x.Option1Name,
        Option1Value = x.Option1Value,
        Option2Name = x.Option2Name,
        Option2Value = x.Option2Value,
        Option3Name = x.Option3Name,
        Option3Value = x.Option3Value,
        HargaModal = x.HargaModal,
        HargaJual = x.HargaJual,
        Stok = x.Stok,
        Active = x.Active
    };

    private static PriceLevelDto MapLevel(PriceLevel x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        SortOrder = x.SortOrder,
        Active = x.Active
    };

    private static ProductPriceDto MapPrice(ProductPrice x) => new()
    {
        Id = x.Id,
        ProductId = x.ProductId,
        ProductVariantId = x.ProductVariantId,
        PriceLevelId = x.PriceLevelId,
        PriceLevelCode = x.PriceLevel?.Code ?? string.Empty,
        PriceLevelName = x.PriceLevel?.Name ?? string.Empty,
        PriceLevelSortOrder = x.PriceLevel?.SortOrder ?? 0,
        MinQuantity = x.MinQuantity,
        UnitPrice = x.UnitPrice
    };

    private static NormalizedVariant NormalizeVariant(CreateProductVariantDto request) => new(
        RequireText(request.Sku, "SKU varian wajib diisi.").ToUpperInvariant(),
        request.Barcode?.Trim() ?? string.Empty,
        RequireText(request.Label, "Label varian wajib diisi."),
        request.Option1Name?.Trim() ?? string.Empty,
        request.Option1Value?.Trim() ?? string.Empty,
        request.Option2Name?.Trim() ?? string.Empty,
        request.Option2Value?.Trim() ?? string.Empty,
        request.Option3Name?.Trim() ?? string.Empty,
        request.Option3Value?.Trim() ?? string.Empty);

    private static string NormalizeCode(string value)
    {
        var code = RequireText(value, "Kode level harga wajib diisi.")
            .Trim()
            .ToLowerInvariant()
            .Replace(' ', '_');
        if (code.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-')))
            throw new TenantApiException(StatusCodes.Status400BadRequest, "PRICE_LEVEL_CODE_INVALID", "Kode level harga hanya boleh berisi huruf, angka, '-' atau '_'.");
        return code;
    }

    private static string RequireText(string? value, string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new TenantApiException(StatusCodes.Status400BadRequest, "RETAIL_FIELD_REQUIRED", message);
        return normalized;
    }

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static TenantApiException Conflict(string code, string message) => new(StatusCodes.Status409Conflict, code, message);
    private static TenantApiException NotFound(string code, string message) => new(StatusCodes.Status404NotFound, code, message);

    private sealed record PriceRequestContext(ProductEntity Product, ProductVariant? Variant, PriceLevel Level);
    private sealed record NormalizedVariant(
        string Sku,
        string Barcode,
        string Label,
        string Option1Name,
        string Option1Value,
        string Option2Name,
        string Option2Value,
        string Option3Name,
        string Option3Value);
}

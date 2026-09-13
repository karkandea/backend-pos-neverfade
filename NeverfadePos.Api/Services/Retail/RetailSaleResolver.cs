using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;
using ProductEntity = NeverfadePos.Api.Entities.Product;

namespace NeverfadePos.Api.Services.Retail;

public sealed record RetailSaleItemResolution(
    ProductEntity Product,
    ProductVariant? Variant,
    int LegacyQty,
    decimal Quantity,
    decimal BasePrice,
    decimal UnitPrice,
    Guid? PriceLevelId,
    string PriceLevelName,
    decimal Subtotal);

public interface IRetailSaleResolver
{
    Task<RetailSaleItemResolution> ResolveAsync(
        Guid productId,
        int legacyQty,
        decimal? quantity,
        Guid? productVariantId,
        Guid? requestedPriceLevelId,
        bool enforceStock,
        CancellationToken cancellationToken = default);
}

public sealed class RetailSaleResolver(AppDbContext db) : IRetailSaleResolver
{
    public async Task<RetailSaleItemResolution> ResolveAsync(
        Guid productId,
        int legacyQty,
        decimal? quantity,
        Guid? productVariantId,
        Guid? requestedPriceLevelId,
        bool enforceStock,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new KeyNotFoundException($"Product {productId} tidak ditemukan.");

        var resolvedQuantity = ProductQuantityRules.Resolve(
            product, legacyQty, quantity, enforceStock: false);
        var resolvedLegacyQty = product.Type == ProductTypes.Goods
            ? ProductQuantityRules.ToStockUnits(product, resolvedQuantity)
            : 1;

        ProductVariant? variant = null;
        var hasActiveVariants = await db.ProductVariants
            .AnyAsync(x => x.ProductId == product.Id && x.Active, cancellationToken);

        if (hasActiveVariants && !productVariantId.HasValue)
        {
            throw Invalid(
                "PRODUCT_VARIANT_REQUIRED",
                $"Pilih varian untuk produk {product.Nama}.");
        }

        if (productVariantId.HasValue)
        {
            if (product.Type != ProductTypes.Goods || !product.TracksStock)
            {
                throw Invalid(
                    "PRODUCT_VARIANT_NOT_SUPPORTED",
                    "Varian hanya dapat digunakan untuk barang yang melacak stok.");
            }

            variant = await db.ProductVariants
                .FirstOrDefaultAsync(
                    x => x.Id == productVariantId.Value && x.ProductId == product.Id,
                    cancellationToken)
                ?? throw Invalid(
                    "PRODUCT_VARIANT_INVALID",
                    "Varian tidak sesuai dengan produk yang dipilih.");

            if (!variant.Active)
            {
                throw Invalid("PRODUCT_VARIANT_INACTIVE", "Varian produk sedang nonaktif.");
            }

            if (enforceStock && variant.Stok < resolvedLegacyQty)
            {
                throw Invalid(
                    "PRODUCT_VARIANT_STOCK_INSUFFICIENT",
                    $"Stok varian {product.Nama} {variant.Label} tidak mencukupi.");
            }
        }
        else if (enforceStock)
        {
            ProductQuantityRules.Validate(product, resolvedQuantity, enforceStock: true);
        }

        if (enforceStock && product.TracksStock && product.Stok < resolvedLegacyQty)
        {
            throw Invalid(
                "PRODUCT_STOCK_INSUFFICIENT",
                $"Stok produk {product.Nama} tidak mencukupi.");
        }

        var basePrice = Money(variant?.HargaJual ?? product.HargaJual);
        var variantId = variant?.Id;
        var candidates = await db.ProductPrices
            .AsNoTracking()
            .Include(x => x.PriceLevel)
            .Where(x =>
                x.ProductId == product.Id &&
                x.PriceLevel != null &&
                x.PriceLevel.Active &&
                (x.ProductVariantId == null || x.ProductVariantId == variantId))
            .Select(x => new RetailPriceCandidate(
                x.PriceLevelId,
                x.PriceLevel!.Name,
                x.PriceLevel.SortOrder,
                x.ProductVariantId,
                x.MinQuantity,
                x.UnitPrice))
            .ToListAsync(cancellationToken);

        RetailPriceResolution price;
        if (requestedPriceLevelId.HasValue)
        {
            if (!RetailPricingRules.TryResolveManual(
                    basePrice,
                    variant?.Id,
                    requestedPriceLevelId.Value,
                    candidates,
                    out price))
            {
                throw Invalid(
                    "PRICE_LEVEL_NOT_AVAILABLE",
                    "Level harga yang dipilih tidak tersedia untuk item ini.");
            }
        }
        else
        {
            price = RetailPricingRules.ResolveAutomatic(
                basePrice,
                resolvedQuantity,
                variant?.Id,
                candidates);
        }

        var unitPrice = Money(price.UnitPrice);
        var subtotal = Money(unitPrice * resolvedQuantity);
        return new RetailSaleItemResolution(
            product,
            variant,
            resolvedLegacyQty,
            resolvedQuantity,
            basePrice,
            unitPrice,
            price.PriceLevelId,
            price.PriceLevelName,
            subtotal);
    }

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static TenantApiException Invalid(string code, string message) =>
        new(StatusCodes.Status400BadRequest, code, message);
}

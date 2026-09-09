using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Common;

public static class ProductQuantityRules
{
    public const int MaxPrecision = 3;

    public static decimal Resolve(
        Product product,
        int legacyQty,
        decimal? quantity,
        bool enforceStock = true)
    {
        var resolved = quantity ?? legacyQty;
        Validate(product, resolved, enforceStock);
        return resolved;
    }

    public static void Validate(
        Product product,
        decimal quantity,
        bool enforceStock = true)
    {
        if (quantity <= 0m)
        {
            throw Invalid(
                "PRODUCT_QUANTITY_INVALID",
                $"Jumlah {product.Nama} harus lebih dari 0.");
        }

        if (product.QuantityPrecision is < 0 or > MaxPrecision)
        {
            throw Invalid(
                "PRODUCT_QUANTITY_PRECISION_INVALID",
                $"Presisi jumlah {product.Nama} tidak valid.");
        }

        if (product.Type == ProductTypes.Goods &&
            quantity != decimal.Truncate(quantity))
        {
            throw Invalid(
                "GOODS_QUANTITY_MUST_BE_INTEGER",
                $"Jumlah barang {product.Nama} harus bilangan bulat.");
        }

        if (decimal.Round(
                quantity,
                product.QuantityPrecision,
                MidpointRounding.ToZero) != quantity)
        {
            throw Invalid(
                "PRODUCT_QUANTITY_PRECISION_EXCEEDED",
                $"Jumlah {product.Nama} maksimal {product.QuantityPrecision} angka desimal.");
        }

        if (!product.TracksStock || !enforceStock)
        {
            return;
        }

        var stockUnits = ToStockUnits(product, quantity);

        if (product.Stok < stockUnits)
        {
            throw Invalid(
                "PRODUCT_STOCK_INSUFFICIENT",
                $"Stok produk {product.Nama} tidak mencukupi.");
        }
    }

    public static int ToStockUnits(
        Product product,
        decimal quantity)
    {
        if (quantity != decimal.Truncate(quantity))
        {
            throw Invalid(
                "STOCK_QUANTITY_MUST_BE_INTEGER",
                $"Jumlah stok {product.Nama} harus bilangan bulat.");
        }

        return checked((int)quantity);
    }

    private static TenantApiException Invalid(
        string code,
        string message) =>
        new(
            StatusCodes.Status400BadRequest,
            code,
            message);
}

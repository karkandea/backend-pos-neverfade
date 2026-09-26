using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Sales;
using NeverfadePos.Api.Entities;
using ProductEntity = NeverfadePos.Api.Entities.Product;
using NeverfadePos.Api.Services.Retail;

namespace NeverfadePos.Api.Services.Sales;

public interface ISaleQuoteService
{
    Task<SaleQuoteDto> CreateAsync(CreateSaleQuoteRequestDto request, Guid outletId,
        CancellationToken cancellationToken = default);
    Task<SaleQuoteDto> GetAsync(Guid quoteId, Guid outletId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// S2 slice 1: immutable server-computed quote. Sale commit/stock reservation and
/// payment finality are intentionally separate, pending idempotent S2 integration.
/// </summary>
public sealed class SaleQuoteService(
    AppDbContext db, CurrentUser currentUser, IRetailSaleResolver pricing)
    : ISaleQuoteService
{
    private static readonly JsonSerializerOptions SnapshotOptions =
        new(JsonSerializerDefaults.Web);
    private const int QuoteLifetimeMinutes = 5;

    public async Task<SaleQuoteDto> CreateAsync(CreateSaleQuoteRequestDto request,
        Guid outletId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();
        if (request.Lines is not { Count: > 0 and <= 100 })
            throw Invalid("QUOTE_LINES_INVALID", "Quote membutuhkan 1–100 item.");
        if (request.Lines.Any(x => x.ProductId == Guid.Empty))
            throw Invalid("QUOTE_PRODUCT_INVALID", "Produk harus dipilih.");
        if (request.DiscountPercent is < 0m or > 100m)
            throw Invalid("QUOTE_DISCOUNT_INVALID", "Diskon harus antara 0 dan 100 persen.");
        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
            throw Invalid("DISCOUNT_CODE_NOT_SUPPORTED",
                "Kode diskon belum didukung pada versi quote ini.");
        if (request.CustomerId.HasValue && !await db.Customers.AsNoTracking()
            .AnyAsync(x => x.Id == request.CustomerId.Value, cancellationToken))
            throw new TenantApiException(404, "QUOTE_CUSTOMER_NOT_FOUND", "Pelanggan tidak ditemukan.");

        var lines = new List<SaleQuoteLineDto>(request.Lines.Count);
        var stockChecks = new List<(ProductEntity Product, ProductVariant? Variant, decimal Quantity)>();
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0 || line.Quantity > 1_000_000m)
                throw Invalid("QUOTE_QUANTITY_INVALID", "Jumlah item tidak valid.");
            var resolved = await pricing.ResolveAsync(
                line.ProductId, legacyQty: 1, quantity: line.Quantity,
                productVariantId: line.VariantId,
                requestedPriceLevelId: line.PriceLevelId,
                enforceStock: true, cancellationToken: cancellationToken);
            stockChecks.Add((resolved.Product, resolved.Variant, resolved.Quantity));
            lines.Add(new SaleQuoteLineDto
            {
                ProductId = resolved.Product.Id,
                VariantId = resolved.Variant?.Id,
                PriceLevelId = resolved.PriceLevelId,
                ProductName = resolved.Product.Nama,
                Unit = resolved.Product.Satuan,
                PriceLevelName = resolved.PriceLevelName,
                Quantity = resolved.Quantity,
                UnitPrice = Money(resolved.UnitPrice),
                Subtotal = Money(resolved.Subtotal)
            });
        }

        // A repeated product in separate lines cannot bypass the available-stock check.
        // This is only a quote-time observation; stock remains unreserved until sale commit.
        foreach (var grouped in stockChecks.GroupBy(x => x.Product.Id))
        {
            var product = grouped.First().Product;
            if (!product.TracksStock) continue;
            if (grouped.Sum(x => x.Quantity) > product.Stok)
                throw Invalid("QUOTE_STOCK_INSUFFICIENT", "Stok gabungan item tidak mencukupi.");
            foreach (var variantGroup in grouped.Where(x => x.Variant is not null)
                .GroupBy(x => x.Variant!.Id))
            {
                if (variantGroup.Sum(x => x.Quantity) > variantGroup.First().Variant!.Stok)
                    throw Invalid("QUOTE_VARIANT_STOCK_INSUFFICIENT", "Stok varian gabungan tidak mencukupi.");
            }
        }

        var subtotal = Money(lines.Sum(x => x.Subtotal));
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var rate = settings?.ShowTax == true ? settings.DefaultTax : 0m;
        if (rate is < 0m or > 100m)
            throw Invalid("QUOTE_TAX_CONFIGURATION_INVALID", "Konfigurasi pajak tidak valid.");
        var discount = Money(subtotal * request.DiscountPercent / 100m);
        var taxable = Money(subtotal - discount);
        var tax = Money(taxable * rate / 100m);
        var total = Money(taxable + tax);
        var quote = new SaleQuote
        {
            TenantId = currentUser.TenantId.Value,
            OutletId = outletId,
            CreatedByUserId = currentUser.UserId.Value,
            ExpiresAt = DateTime.UtcNow.AddMinutes(QuoteLifetimeMinutes),
            Total = total
        };
        var dto = new SaleQuoteDto
        {
            QuoteId = quote.Id, QuoteVersion = quote.QuoteVersion,
            OutletId = outletId, CustomerId = request.CustomerId,
            ExpiresAt = quote.ExpiresAt, Status = "quoted", StockReserved = false,
            Lines = lines, Subtotal = subtotal, Discount = discount,
            DiscountPercent = request.DiscountPercent, TaxRatePercent = rate,
            Tax = tax, ServiceCharge = 0m, Total = total,
            Warnings = ["Quote belum mengunci stok; harga dan ketersediaan diperiksa ulang saat checkout."]
        };
        quote.SnapshotJson = JsonSerializer.Serialize(dto, SnapshotOptions);
        db.SaleQuotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);
        return dto;
    }

    public async Task<SaleQuoteDto> GetAsync(Guid quoteId, Guid outletId,
        CancellationToken cancellationToken = default)
    {
        var quote = await db.SaleQuotes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == quoteId && x.OutletId == outletId, cancellationToken)
            ?? throw new TenantApiException(404, "QUOTE_NOT_FOUND", "Quote tidak ditemukan.");
        var dto = JsonSerializer.Deserialize<SaleQuoteDto>(quote.SnapshotJson, SnapshotOptions)
            ?? throw new InvalidOperationException("Stored quote snapshot invalid.");
        dto.Status = quote.Status == "consumed" ? "consumed" :
            quote.ExpiresAt <= DateTime.UtcNow ? "expired" : "quoted";
        dto.StockReserved = false;
        return dto;
    }

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static TenantApiException Invalid(string code, string message) =>
        new(StatusCodes.Status422UnprocessableEntity, code, message);
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Sales;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Retail;
using NeverfadePos.Api.Services.Transaction;

namespace NeverfadePos.Api.Services.Sales;

public sealed record CashCommitResult(TransactionDto Transaction, bool Replayed);

public interface IQuoteCashSaleService
{
    Task<CashCommitResult> CommitAsync(CommitCashSaleRequestDto request,
        string? idempotencyKey, Guid outletId,
        CancellationToken cancellationToken = default);
    Task<CashCommitResult> FindCommittedAsync(string? idempotencyKey,
        Guid outletId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Quote -> cash sale. PostgreSQL tenant lock plus a unique idempotency key
/// serialize competing cash commits before any stock read or sale number allocation.
/// This is deliberately cash-only; QRIS finality is a separate S2 gate.
/// </summary>
public sealed class QuoteCashSaleService(
    AppDbContext db,
    CurrentUser currentUser,
    IRetailSaleResolver resolver,
    ITransactionService transactions) : IQuoteCashSaleService
{
    private static readonly JsonSerializerOptions SnapshotOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<CashCommitResult> CommitAsync(CommitCashSaleRequestDto request,
        string? idempotencyKey, Guid outletId,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();
        if (request.QuoteId == Guid.Empty || request.QuoteVersion == Guid.Empty)
            throw Invalid(400, "QUOTE_ID_REQUIRED", "Quote dan versi wajib dipilih.");
        ValidateKey(idempotencyKey);
        var key = idempotencyKey!;
        var hash = Fingerprint(request);
        var tenantId = currentUser.TenantId.Value;

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await TenantStockLock.AcquireAsync(db, tenantId, cancellationToken);

        var existing = await db.SaleQuotes.FirstOrDefaultAsync(
            x => x.OutletId == outletId && x.IdempotencyKey == key,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.Id != request.QuoteId ||
                existing.QuoteVersion != request.QuoteVersion ||
                existing.IdempotencyRequestHash != hash ||
                existing.Status != "consumed" || !existing.ConsumedTransactionId.HasValue)
                throw Invalid(409, "IDEMPOTENCY_KEY_REUSED",
                    "Kunci transaksi sudah digunakan untuk permintaan yang berbeda.");
            var prior = await transactions.GetByIdAsync(
                existing.ConsumedTransactionId.Value, outletId, cancellationToken);
            return new CashCommitResult(prior, true);
        }

        var quote = await db.SaleQuotes.FirstOrDefaultAsync(
            x => x.Id == request.QuoteId && x.OutletId == outletId,
            cancellationToken)
            ?? throw Invalid(404, "QUOTE_NOT_FOUND", "Quote tidak ditemukan di outlet ini.");
        if (quote.QuoteVersion != request.QuoteVersion)
            throw Invalid(409, "QUOTE_VERSION_MISMATCH", "Versi quote tidak sesuai.");
        if (quote.Status != "quoted")
            throw Invalid(409, "QUOTE_ALREADY_CONSUMED", "Quote sudah dipakai untuk transaksi lain.");
        if (quote.ExpiresAt <= DateTime.UtcNow)
            throw Invalid(409, "QUOTE_EXPIRED", "Quote kedaluwarsa. Periksa ulang harga dan stok.");

        var snapshot = JsonSerializer.Deserialize<SaleQuoteDto>(quote.SnapshotJson, SnapshotOptions)
            ?? throw new InvalidOperationException("Persisted quote snapshot invalid.");
        if (snapshot.QuoteId != quote.Id || snapshot.QuoteVersion != quote.QuoteVersion ||
            snapshot.OutletId != outletId || snapshot.Total != quote.Total ||
            snapshot.Lines.Count is < 1 or > 100)
            throw new InvalidOperationException("Persisted quote snapshot identity invalid.");
        if (request.AmountReceived < snapshot.Total)
            throw Invalid(422, "CASH_AMOUNT_INSUFFICIENT",
                "Uang diterima kurang dari total transaksi.");

        // Reprice after the transaction-scoped lock. Quotes are not reservations.
        var priced = new List<RetailSaleItemResolution>(snapshot.Lines.Count);
        foreach (var line in snapshot.Lines)
        {
            var current = await resolver.ResolveAsync(line.ProductId, 1,
                line.Quantity, line.VariantId, line.RequestedPriceLevelId,
                enforceStock: true, cancellationToken);
            if (current.Product.Id != line.ProductId ||
                current.Variant?.Id != line.VariantId ||
                current.PriceLevelId != line.PriceLevelId ||
                current.Quantity != line.Quantity ||
                Money(current.UnitPrice) != line.UnitPrice ||
                Money(current.Subtotal) != line.Subtotal)
                throw Invalid(409, "QUOTE_STALE",
                    "Harga atau pilihan item berubah. Buat quote baru.");
            priced.Add(current);
        }
        foreach (var byProduct in priced.Where(x => x.Product.TracksStock)
            .GroupBy(x => x.Product.Id))
        {
            if (byProduct.Sum(x => ProductQuantityRules.ToStockUnits(x.Product, x.Quantity)) >
                byProduct.First().Product.Stok)
                throw Invalid(409, "QUOTE_STALE_STOCK", "Stok gabungan tidak mencukupi.");
            foreach (var variant in byProduct.Where(x => x.Variant is not null)
                .GroupBy(x => x.Variant!.Id))
                if (variant.Sum(x => ProductQuantityRules.ToStockUnits(x.Product, x.Quantity)) >
                    variant.First().Variant!.Stok)
                    throw Invalid(409, "QUOTE_STALE_STOCK", "Stok varian gabungan tidak mencukupi.");
        }
        var currentTaxRate = await db.Settings.AsNoTracking()
            .Select(x => x.ShowTax ? x.DefaultTax : 0m)
            .FirstOrDefaultAsync(cancellationToken);
        if (currentTaxRate != snapshot.TaxRatePercent)
            throw Invalid(409, "QUOTE_STALE_TAX", "Konfigurasi pajak berubah. Buat quote baru.");
        var subtotal = Money(priced.Sum(x => x.Subtotal));
        var discount = Money(subtotal * snapshot.DiscountPercent / 100m);
        var tax = Money(Money(subtotal - discount) * snapshot.TaxRatePercent / 100m);
        if (subtotal != snapshot.Subtotal || discount != snapshot.Discount ||
            tax != snapshot.Tax || Money(subtotal - discount + tax) != snapshot.Total)
            throw Invalid(409, "QUOTE_STALE", "Total quote tidak lagi sesuai harga server.");

        var cash = new CreateTransactionDto
        {
            OutletId = outletId,
            CustomerId = snapshot.CustomerId,
            Items = snapshot.Lines.Select(line => new CreateTransactionItemDto
            {
                Id = line.ProductId,
                ProductVariantId = line.VariantId,
                PriceLevelId = line.RequestedPriceLevelId,
                Nama = line.ProductName,
                HargaJual = line.UnitPrice,
                Qty = 1,
                Quantity = line.Quantity,
                Subtotal = line.Subtotal,
                Note = line.Note
            }).ToList(),
            Subtotal = snapshot.Subtotal,
            Disc = snapshot.DiscountPercent,
            Tax = snapshot.TaxRatePercent,
            DiscAmt = snapshot.Discount,
            TaxAmt = snapshot.Tax,
            Total = snapshot.Total,
            MetodePembayaran = "tunai",
            Dibayar = Money(request.AmountReceived),
            Kembalian = Money(request.AmountReceived - snapshot.Total)
        };
        var result = await transactions.CreateAsync(cash, cancellationToken);
        quote.Status = "consumed";
        quote.IdempotencyKey = key;
        quote.IdempotencyRequestHash = hash;
        quote.ConsumedTransactionId = result.Id;
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new CashCommitResult(result, false);
    }

    /// <summary>
    /// Read-only recovery of a completed cash write by its original key. A 404
    /// cannot prove that an in-flight request is safe to replace: the client
    /// must retain and replay the SAME original key after an unknown response.
    /// </summary>
    public async Task<CashCommitResult> FindCommittedAsync(string? idempotencyKey,
        Guid outletId, CancellationToken cancellationToken = default)
    {
        ValidateKey(idempotencyKey);
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();
        var sale = await db.SaleQuotes.AsNoTracking().SingleOrDefaultAsync(
            x => x.OutletId == outletId && x.IdempotencyKey == idempotencyKey &&
                 x.Status == "consumed" && x.ConsumedTransactionId.HasValue,
            cancellationToken);
        if (sale?.ConsumedTransactionId is not { } transactionId)
            throw Invalid(404, "CASH_COMMIT_NOT_CONFIRMED",
                "Transaksi belum terkonfirmasi. Jangan gunakan kunci pembayaran baru; periksa kembali attempt sebelumnya.");
        var prior = await transactions.GetByIdAsync(transactionId, outletId,
            cancellationToken);
        return new CashCommitResult(prior, true);
    }

    private static void ValidateKey(string? key)
    {
        if (key is null || key.Length is < 16 or > 128 ||
            key.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('-' or '_')))
            throw Invalid(400, "IDEMPOTENCY_KEY_REQUIRED",
                "Gunakan Idempotency-Key unik (16–128 karakter ASCII, huruf/angka/-/_). ");
    }

    private static string Fingerprint(CommitCashSaleRequestDto request)
    {
        var canonical = $"{request.QuoteId:N}|{request.QuoteVersion:N}|{request.AmountReceived.ToString("G29", CultureInfo.InvariantCulture)}|tunai";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static TenantApiException Invalid(int status, string code, string message) =>
        new(status, code, message);
}

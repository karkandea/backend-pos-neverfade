using System.Data;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Retail;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Retail;

public sealed class RetailReturnService(
    AppDbContext db,
    CurrentUser currentUser)
    : IRetailReturnService
{
    public async Task<List<RetailReturnDto>> GetByTransactionAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await db.RetailReturns
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.TransactionId == transactionId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => MapDto(x))
            .ToListAsync(cancellationToken);
    }
    public async Task<RetailReturnDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.RetailReturns
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null
            ? throw new KeyNotFoundException("Retur/tukar tidak ditemukan.")
            : MapDto(entity);
    }

    public async Task<RetailReturnDto> CreateAsync(
        CreateRetailReturnDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureIdentity();
        ValidateRequest(request);

        var key = request.IdempotencyKey.Trim();
        var existing = await db.RetailReturns
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);

        if (existing is not null)
            return MapDto(existing);
        await using var trx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        existing = await db.RetailReturns
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);

        if (existing is not null)
        {
            await trx.CommitAsync(cancellationToken);
            return MapDto(existing);
        }

        var transaction = await db.Transactions
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == request.TransactionId, cancellationToken)
            ?? throw new KeyNotFoundException("Transaksi asal tidak ditemukan.");

        if (transaction.Status != TransactionStatuses.Paid)
            throw new InvalidOperationException("Hanya transaksi paid yang dapat diretur atau ditukar.");

        var requestedIds = request.Items.Select(x => x.TransactionItemId).ToHashSet();
        var sourceItems = transaction.Items
            .Where(x => requestedIds.Contains(x.Id))
            .ToDictionary(x => x.Id);

        if (sourceItems.Count != request.Items.Count)
            throw new InvalidOperationException("Item retur harus berasal dari transaksi yang dipilih.");
        var returnNumber = BuildReturnNumber(request.Type);
        var entity = new RetailReturn
        {
            TenantId = currentUser.TenantId!.Value,
            ReturnNumber = returnNumber,
            IdempotencyKey = key,
            TransactionId = transaction.Id,
            TransactionNumber = transaction.NoTrx,
            Type = request.Type.Trim(),
            Reason = request.Reason.Trim(),
            Notes = request.Notes.Trim(),
            CreatedByUserId = currentUser.UserId!.Value,
            CreatedByName = currentUser.Nama ?? currentUser.Username ?? ""
        };

        db.RetailReturns.Add(entity);

        decimal refundTotal = 0m;
        foreach (var requested in request.Items)
        {
            var source = sourceItems[requested.TransactionItemId];
            await ProcessItemAsync(
                entity,
                transaction,
                source,
                requested,
                cancellationToken);

            if (request.Type == RetailReturnTypes.Return)
                refundTotal += CalculateRefund(transaction, source, requested.Quantity);
        }

        entity.RefundAmount = decimal.Round(refundTotal, 2, MidpointRounding.AwayFromZero);
        if (request.Type == RetailReturnTypes.Return)
        {
            var previousRefund = await db.RetailReturns
                .Where(x =>
                    x.TransactionId == transaction.Id &&
                    x.Type == RetailReturnTypes.Return)
                .SumAsync(x => (decimal?)x.RefundAmount, cancellationToken) ?? 0m;

            if (previousRefund + entity.RefundAmount > transaction.Total + 0.01m)
                throw new InvalidOperationException("Total refund tidak boleh melebihi total transaksi asal.");
        }

        await db.SaveChangesAsync(cancellationToken);
        await trx.CommitAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    private async Task ProcessItemAsync(
        RetailReturn parent,
        NeverfadePos.Api.Entities.Transaction transaction,
        TransactionItem source,
        CreateRetailReturnItemDto request,
        CancellationToken cancellationToken)
    {
        if (source.ProductType != ProductTypes.Goods)
            throw new InvalidOperationException("Phase 3E2 hanya mendukung retur/tukar barang.");

        if (source.Quantity != decimal.Truncate(source.Quantity))
            throw new InvalidOperationException("Quantity transaksi asal bukan unit barang bulat.");
        var soldQuantity = checked((int)source.Quantity);
        var alreadyReturned = await db.RetailReturnItems
            .Where(x => x.TransactionItemId == source.Id)
            .SumAsync(x => (int?)x.Quantity, cancellationToken) ?? 0;

        if (alreadyReturned + request.Quantity > soldQuantity)
            throw new InvalidOperationException("Quantity retur/tukar melebihi sisa quantity transaksi asal.");

        var product = await db.Products
            .SingleOrDefaultAsync(x => x.Id == source.ProductId, cancellationToken)
            ?? throw new InvalidOperationException("Produk asal sudah tidak tersedia.");

        ProductVariant? originalVariant = null;
        if (source.ProductVariantId.HasValue)
        {
            originalVariant = await db.ProductVariants
                .SingleOrDefaultAsync(x => x.Id == source.ProductVariantId.Value, cancellationToken);
        }

        if (request.Restock && source.TracksStock &&
            source.ProductVariantId.HasValue && originalVariant is null)
        {
            throw new InvalidOperationException(
                "Varian asal sudah tidak tersedia. Gunakan restock=false untuk mencatat retur tanpa menambah stok.");
        }

        ProductVariant? replacement = null;
        if (parent.Type == RetailReturnTypes.Exchange)
        {
            replacement = await ValidateReplacementAsync(source, request, cancellationToken);
        }
        if (request.Restock && source.TracksStock)
        {
            if (originalVariant is not null)
                originalVariant.Stok += request.Quantity;

            product.Stok += request.Quantity;
            AddStockHistory(
                product,
                originalVariant,
                request.Quantity,
                product.Stok,
                parent.Type == RetailReturnTypes.Exchange ? "exchange_return" : "return",
                parent.ReturnNumber);
        }

        if (replacement is not null && source.TracksStock)
        {
            replacement.Stok -= request.Quantity;
            product.Stok -= request.Quantity;
            AddStockHistory(
                product,
                replacement,
                -request.Quantity,
                product.Stok,
                "exchange_out",
                parent.ReturnNumber);
        }

        var itemRefund = parent.Type == RetailReturnTypes.Return
            ? CalculateRefund(transaction, source, request.Quantity)
            : 0m;

        db.RetailReturnItems.Add(new RetailReturnItem
        {
            TenantId = currentUser.TenantId!.Value,
            RetailReturnId = parent.Id,
            TransactionItemId = source.Id,
            ProductId = source.ProductId,
            ProductName = source.Nama,
            OriginalVariantId = source.ProductVariantId,
            OriginalVariantSku = source.VariantSku,
            OriginalVariantLabel = source.VariantLabel,
            Quantity = request.Quantity,
            OriginalUnitPrice = source.HargaJual,
            RefundAmount = itemRefund,
            Restock = request.Restock,
            ReplacementVariantId = replacement?.Id,
            ReplacementVariantSku = replacement?.Sku ?? string.Empty,
            ReplacementVariantLabel = replacement?.Label ?? string.Empty
        });
    }

    private async Task<ProductVariant> ValidateReplacementAsync(
        TransactionItem source,
        CreateRetailReturnItemDto request,
        CancellationToken cancellationToken)
    {
        if (!source.TracksStock)
            throw new InvalidOperationException("Exchange hanya didukung untuk barang dengan stock tracking.");

        if (!source.ProductVariantId.HasValue || !request.ReplacementVariantId.HasValue)
            throw new InvalidOperationException("Exchange membutuhkan varian asal dan varian pengganti.");

        if (source.ProductVariantId == request.ReplacementVariantId)
            throw new InvalidOperationException("Varian pengganti harus berbeda dari varian asal.");

        var replacement = await db.ProductVariants
            .SingleOrDefaultAsync(x => x.Id == request.ReplacementVariantId.Value, cancellationToken)
            ?? throw new InvalidOperationException("Varian pengganti tidak ditemukan.");
        if (!replacement.Active)
            throw new InvalidOperationException("Varian pengganti tidak aktif.");

        if (replacement.ProductId != source.ProductId)
            throw new InvalidOperationException("Exchange hanya boleh ke varian lain dari produk yang sama.");

        if (replacement.Stok < request.Quantity)
            throw new InvalidOperationException("Stok varian pengganti tidak mencukupi.");

        return replacement;
    }

    private void AddStockHistory(
        NeverfadePos.Api.Entities.Product product,
        ProductVariant? variant,
        int delta,
        int endingStock,
        string type,
        string returnNumber)
    {
        db.StockHistories.Add(new NeverfadePos.Api.Entities.StockHistory
        {
            TenantId = currentUser.TenantId!.Value,
            ProdukId = product.Id,
            ProdukNama = product.Nama,
            ProductVariantId = variant?.Id,
            VariantSku = variant?.Sku ?? string.Empty,
            VariantLabel = variant?.Label ?? string.Empty,
            Tipe = type,
            Jumlah = delta,
            StokAkhir = endingStock,
            Keterangan = $"Retur/Tukar {returnNumber}",
            User = currentUser.Username ?? ""
        });
    }
    private static decimal CalculateRefund(
        NeverfadePos.Api.Entities.Transaction transaction,
        TransactionItem item,
        int quantity)
    {
        if (transaction.Subtotal <= 0m || item.Quantity <= 0m)
            return 0m;

        var lineShare = item.Subtotal / transaction.Subtotal;
        var historicalLineNet = transaction.Total * lineShare;
        var quantityShare = quantity / item.Quantity;

        return decimal.Round(
            historicalLineNet * quantityShare,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static string BuildReturnNumber(string type)
    {
        var prefix = type == RetailReturnTypes.Exchange ? "EXC" : "RET";
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{suffix}";
    }

    private void EnsureIdentity()
    {
        if (!currentUser.TenantId.HasValue || !currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();
    }
    private static void ValidateRequest(CreateRetailReturnDto request)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ArgumentException("Idempotency key wajib diisi.");

        if (!RetailReturnTypes.IsValid(request.Type?.Trim()))
            throw new ArgumentException("Tipe retur/tukar tidak valid.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Alasan retur/tukar wajib diisi.");

        if (request.Items.Count == 0)
            throw new ArgumentException("Minimal satu item harus dipilih.");

        if (request.Items.Select(x => x.TransactionItemId).Distinct().Count() != request.Items.Count)
            throw new ArgumentException("Item transaksi tidak boleh duplikat dalam satu request.");

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException("Quantity retur/tukar harus lebih dari nol.");

            if (request.Type == RetailReturnTypes.Return && item.ReplacementVariantId.HasValue)
                throw new ArgumentException("Return tidak menerima varian pengganti.");

            if (request.Type == RetailReturnTypes.Exchange && !item.ReplacementVariantId.HasValue)
                throw new ArgumentException("Exchange wajib memilih varian pengganti.");

            if (request.Type == RetailReturnTypes.Exchange && !item.Restock)
                throw new ArgumentException("Exchange wajib mengembalikan stok varian asal.");
        }
    }
    private static RetailReturnDto MapDto(RetailReturn entity)
    {
        return new RetailReturnDto
        {
            Id = entity.Id,
            ReturnNumber = entity.ReturnNumber,
            IdempotencyKey = entity.IdempotencyKey,
            TransactionId = entity.TransactionId,
            TransactionNumber = entity.TransactionNumber,
            Type = entity.Type,
            Reason = entity.Reason,
            Notes = entity.Notes,
            RefundAmount = entity.RefundAmount,
            CreatedByUserId = entity.CreatedByUserId,
            CreatedByName = entity.CreatedByName,
            CreatedAt = entity.CreatedAt,
            Items = entity.Items
                .OrderBy(x => x.CreatedAt)
                .Select(MapItemDto)
                .ToList()
        };
    }

    private static RetailReturnItemDto MapItemDto(RetailReturnItem item)
    {
        return new RetailReturnItemDto
        {
            Id = item.Id,
            TransactionItemId = item.TransactionItemId,
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            OriginalVariantId = item.OriginalVariantId,
            OriginalVariantSku = item.OriginalVariantSku,
            OriginalVariantLabel = item.OriginalVariantLabel,
            Quantity = item.Quantity,
            OriginalUnitPrice = item.OriginalUnitPrice,
            RefundAmount = item.RefundAmount,
            Restock = item.Restock,
            ReplacementVariantId = item.ReplacementVariantId,
            ReplacementVariantSku = item.ReplacementVariantSku,
            ReplacementVariantLabel = item.ReplacementVariantLabel
        };
    }
}

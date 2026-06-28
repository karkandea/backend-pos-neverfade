using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Transaction;

namespace NeverfadePos.Api.Services.Transaction;

public sealed class TransactionService(
    AppDbContext db,
    CurrentUser currentUser)
    : ITransactionService
{
    public async Task<List<TransactionDto>> GetAllAsync(
        string? search,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = db.Transactions
            .AsNoTracking()
            .Include(x => x.Items)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.NoTrx.Contains(search) ||
                x.CustomerNama.Contains(search) ||
                x.Kasir.Contains(search));
        }

        if (startDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.Date.AddDays(1);

            query = query.Where(x => x.CreatedAt < end);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<TransactionDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var transaction = await db.Transactions
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        return transaction is null
            ? throw new KeyNotFoundException("Transaction tidak ditemukan.")
            : MapToDto(transaction);
    }

    private async Task<string> GenerateNoTrxAsync(
        CancellationToken cancellationToken)
    {
        var prefix = $"TRX-{DateTime.UtcNow:yyyyMMdd}";

        var lastNo = await db.Transactions
            .Where(x => x.NoTrx.StartsWith(prefix))
            .OrderByDescending(x => x.NoTrx)
            .Select(x => x.NoTrx)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;

        if (!string.IsNullOrWhiteSpace(lastNo))
        {
            var number = lastNo.Split('-').Last();

            if (int.TryParse(number, out var current))
            {
                next = current + 1;
            }
        }

        return $"{prefix}-{next:0000}";
    }

    public async Task<TransactionDto> CreateAsync(
        CreateTransactionDto request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue ||
            !currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException();
        }

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Item transaksi tidak boleh kosong.");
        }

        await using var trx =
            await db.Database.BeginTransactionAsync(cancellationToken);

        var noTrx = await GenerateNoTrxAsync(cancellationToken);

        var customer = request.CustomerId.HasValue
            ? await db.Customers.FirstOrDefaultAsync(
                x => x.Id == request.CustomerId.Value,
                cancellationToken)
            : null;

        var entity = new NeverfadePos.Api.Entities.Transaction
        {
            TenantId = currentUser.TenantId.Value,
            NoTrx = noTrx,
            Kasir = currentUser.Nama ?? "",
            KasirId = currentUser.UserId.Value,
            CustomerId = customer?.Id,
            CustomerNama = customer?.Nama ?? "",
            Subtotal = request.Subtotal,
            Disc = request.Disc,
            Tax = request.Tax,
            DiscAmt = request.DiscAmt,
            TaxAmt = request.TaxAmt,
            Total = request.Total,
            MetodePembayaran = request.MetodePembayaran,
            Dibayar = request.Dibayar,
            Kembalian = request.Kembalian
        };

        db.Transactions.Add(entity);

        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in request.Items)
        {
            var product = await db.Products
                .FirstOrDefaultAsync(
                    x => x.Id == item.Id,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Product {item.Id} tidak ditemukan.");

            if (product.Stok < item.Qty)
            {
                throw new InvalidOperationException(
                    $"Stok produk {product.Nama} tidak mencukupi.");
            }

            product.Stok -= item.Qty;

            db.TransactionItems.Add(
                new NeverfadePos.Api.Entities.TransactionItem
                {
                    TenantId = currentUser.TenantId.Value,
                    TransactionId = entity.Id,
                    ProductId = product.Id,
                    Nama = product.Nama,
                    HargaJual = item.HargaJual,
                    Qty = item.Qty,
                    Subtotal = item.Subtotal
                });

            db.StockHistories.Add(
                new NeverfadePos.Api.Entities.StockHistory
                {
                    TenantId = currentUser.TenantId.Value,
                    ProdukId = product.Id,
                    ProdukNama = product.Nama,
                    Tipe = "transaksi",
                    Jumlah = -item.Qty,
                    StokAkhir = product.Stok,
                    Keterangan = $"Transaksi {noTrx}",
                    User = currentUser.Username ?? ""
                });
        }

        if (customer is not null)
        {
            var settings = await db.Settings
                .FirstAsync(cancellationToken);

            customer.Poin +=
                (int)Math.Floor(request.Total / 1000m)
                * settings.PoinRate;

            customer.TotalTransaksi++;
        }

        await db.SaveChangesAsync(cancellationToken);

        await trx.CommitAsync(cancellationToken);

        return await GetByIdAsync(
            entity.Id,
            cancellationToken);
    }

    private static System.Linq.Expressions.Expression<
        Func<NeverfadePos.Api.Entities.Transaction, TransactionDto>>
        MapToDto()
    {
        return x => new TransactionDto
        {
            Id = x.Id,
            NoTrx = x.NoTrx,
            Tanggal = x.CreatedAt,
            Kasir = x.Kasir,
            CustomerId = x.CustomerId,
            CustomerNama = x.CustomerNama,
            Items = x.Items
                .Select(i => new TransactionItemDto
                {
                    Id = i.ProductId,
                    Nama = i.Nama,
                    HargaJual = i.HargaJual,
                    Qty = i.Qty,
                    Subtotal = i.Subtotal
                })
                .ToList(),
            Subtotal = x.Subtotal,
            Disc = x.Disc,
            Tax = x.Tax,
            DiscAmt = x.DiscAmt,
            TaxAmt = x.TaxAmt,
            Total = x.Total,
            MetodePembayaran = x.MetodePembayaran,
            Dibayar = x.Dibayar,
            Kembalian = x.Kembalian
        };
    }

    private static TransactionDto MapToDto(
        NeverfadePos.Api.Entities.Transaction x)
    {
        return new TransactionDto
        {
            Id = x.Id,
            NoTrx = x.NoTrx,
            Tanggal = x.CreatedAt,
            Kasir = x.Kasir,
            CustomerId = x.CustomerId,
            CustomerNama = x.CustomerNama,
            Items = x.Items
                .Select(i => new TransactionItemDto
                {
                    Id = i.ProductId,
                    Nama = i.Nama,
                    HargaJual = i.HargaJual,
                    Qty = i.Qty,
                    Subtotal = i.Subtotal
                })
                .ToList(),
            Subtotal = x.Subtotal,
            Disc = x.Disc,
            Tax = x.Tax,
            DiscAmt = x.DiscAmt,
            TaxAmt = x.TaxAmt,
            Total = x.Total,
            MetodePembayaran = x.MetodePembayaran,
            Dibayar = x.Dibayar,
            Kembalian = x.Kembalian
        };
    }
}

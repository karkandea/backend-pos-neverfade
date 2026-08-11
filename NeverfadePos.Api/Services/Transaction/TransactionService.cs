using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;

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
            query = query.Where(
                x => x.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            var end = endDate.Value.Date.AddDays(1);

            query = query.Where(
                x => x.CreatedAt < end);
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
            ? throw new KeyNotFoundException(
                "Transaction tidak ditemukan.")
            : MapToDto(transaction);
    }

    private async Task<string> GenerateNoTrxAsync(
        CancellationToken cancellationToken)
    {
        var prefix =
            $"TRX-{DateTime.UtcNow:yyyyMMdd}";

        var lastNo = await db.Transactions
            .Where(x => x.NoTrx.StartsWith(prefix))
            .OrderByDescending(x => x.NoTrx)
            .Select(x => x.NoTrx)
            .FirstOrDefaultAsync(cancellationToken);

        var next = 1;

        if (!string.IsNullOrWhiteSpace(lastNo))
        {
            var number =
                lastNo.Split('-').Last();

            if (int.TryParse(
                number,
                out var current))
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

        if (!string.Equals(
            request.MetodePembayaran,
            "tunai",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Transaksi non-tunai harus dibuat melalui payment endpoint.");
        }

        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException(
                "Item transaksi tidak boleh kosong.");
        }

        if (request.Disc < 0 ||
            request.Disc > 100)
        {
            throw new InvalidOperationException(
                "Diskon harus berada antara 0 sampai 100 persen.");
        }

        if (request.Tax < 0 ||
            request.Tax > 100)
        {
            throw new InvalidOperationException(
                "Pajak harus berada antara 0 sampai 100 persen.");
        }

        await using var trx =
            await db.Database.BeginTransactionAsync(
                cancellationToken);

        NeverfadePos.Api.Entities.Customer?
            customer = null;

        if (request.CustomerId.HasValue)
        {
            customer = await db.Customers
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                        request.CustomerId.Value,
                    cancellationToken);

            if (customer is null)
            {
                throw new KeyNotFoundException(
                    "Customer tidak ditemukan.");
            }
        }

        var resolvedItems =
            new List<ResolvedTransactionItem>();

        foreach (var item in request.Items)
        {
            var product = await db.Products
                .FirstOrDefaultAsync(
                    x => x.Id == item.Id,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Product {item.Id} tidak ditemukan.");

            if (item.Qty <= 0)
            {
                throw new InvalidOperationException(
                    $"Qty produk {product.Nama} harus lebih dari 0.");
            }

            if (product.Stok < item.Qty)
            {
                throw new InvalidOperationException(
                    $"Stok produk {product.Nama} tidak mencukupi.");
            }

            var itemSubtotal =
                Money(product.HargaJual * item.Qty);

            ValidateMoney(
                "harga jual produk",
                item.HargaJual,
                product.HargaJual);

            ValidateMoney(
                "subtotal item",
                item.Subtotal,
                itemSubtotal);

            resolvedItems.Add(
                new ResolvedTransactionItem(
                    product,
                    item.Qty,
                    Money(product.HargaJual),
                    itemSubtotal));
        }

        var subtotal =
            Money(
                resolvedItems.Sum(
                    x => x.Subtotal));

        var discAmt =
            Money(
                subtotal *
                request.Disc /
                100m);

        var afterDiscount =
            Money(subtotal - discAmt);

        var taxAmt =
            Money(
                afterDiscount *
                request.Tax /
                100m);

        var total =
            Money(
                afterDiscount +
                taxAmt);

        ValidateMoney(
            "subtotal transaksi",
            request.Subtotal,
            subtotal);

        ValidateMoney(
            "nilai diskon",
            request.DiscAmt,
            discAmt);

        ValidateMoney(
            "nilai pajak",
            request.TaxAmt,
            taxAmt);

        ValidateMoney(
            "total transaksi",
            request.Total,
            total);

        var dibayar =
            Money(request.Dibayar);

        if (dibayar < total)
        {
            throw new InvalidOperationException(
                "Jumlah pembayaran kurang dari total transaksi.");
        }

        var isTunai =
            string.Equals(
                request.MetodePembayaran,
                "tunai",
                StringComparison.OrdinalIgnoreCase);

        var kembalian =
            isTunai
                ? Money(dibayar - total)
                : 0m;

        ValidateMoney(
            "kembalian",
            request.Kembalian,
            kembalian);

        var noTrx =
            await GenerateNoTrxAsync(
                cancellationToken);

        var entity =
            new NeverfadePos.Api.Entities.Transaction
            {
                TenantId =
                    currentUser.TenantId.Value,

                NoTrx = noTrx,

                Kasir =
                    currentUser.Nama ?? "",

                KasirId =
                    currentUser.UserId.Value,

                CustomerId =
                    customer?.Id,

                CustomerNama =
                    customer?.Nama ?? "",

                Subtotal =
                    subtotal,

                Disc =
                    request.Disc,

                Tax =
                    request.Tax,

                DiscAmt =
                    discAmt,

                TaxAmt =
                    taxAmt,

                Total =
                    total,

                MetodePembayaran =
                    request.MetodePembayaran,

                Dibayar =
                    dibayar,

                Kembalian =
                    kembalian,

                Status =
                    TransactionStatuses.Paid,

                FinalizedAt =
                    DateTime.UtcNow
            };

        db.Transactions.Add(entity);

        await db.SaveChangesAsync(
            cancellationToken);

        foreach (var item in resolvedItems)
        {
            item.Product.Stok -=
                item.Qty;

            db.TransactionItems.Add(
                new NeverfadePos.Api.Entities.TransactionItem
                {
                    TenantId =
                        currentUser.TenantId.Value,

                    TransactionId =
                        entity.Id,

                    ProductId =
                        item.Product.Id,

                    Nama =
                        item.Product.Nama,

                    HargaJual =
                        item.HargaJual,

                    Qty =
                        item.Qty,

                    Subtotal =
                        item.Subtotal
                });

            db.StockHistories.Add(
                new NeverfadePos.Api.Entities.StockHistory
                {
                    TenantId =
                        currentUser.TenantId.Value,

                    ProdukId =
                        item.Product.Id,

                    ProdukNama =
                        item.Product.Nama,

                    Tipe =
                        "transaksi",

                    Jumlah =
                        -item.Qty,

                    StokAkhir =
                        item.Product.Stok,

                    Keterangan =
                        $"Transaksi {noTrx}",

                    User =
                        currentUser.Username ?? ""
                });
        }

        if (customer is not null)
        {
            var settings =
                await db.Settings
                    .FirstAsync(
                        cancellationToken);

            customer.Poin +=
                (int)Math.Floor(
                    total / 1000m) *
                settings.PoinRate;

            customer.TotalTransaksi++;
        }

        await db.SaveChangesAsync(
            cancellationToken);

        await trx.CommitAsync(
            cancellationToken);

        return await GetByIdAsync(
            entity.Id,
            cancellationToken);
    }

    private static decimal Money(
        decimal value)
    {
        return decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static void ValidateMoney(
        string field,
        decimal clientValue,
        decimal serverValue)
    {
        if (Money(clientValue) !=
            Money(serverValue))
        {
            throw new InvalidOperationException(
                $"Nilai {field} tidak sesuai data server.");
        }
    }

    private sealed record
        ResolvedTransactionItem(
            NeverfadePos.Api.Entities.Product Product,
            int Qty,
            decimal HargaJual,
            decimal Subtotal);

    private static System.Linq.Expressions.Expression<
        Func<
            NeverfadePos.Api.Entities.Transaction,
            TransactionDto>>
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
                .Select(i =>
                    new TransactionItemDto
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

            MetodePembayaran =
                x.MetodePembayaran,

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
                .Select(i =>
                    new TransactionItemDto
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

            MetodePembayaran =
                x.MetodePembayaran,

            Dibayar = x.Dibayar,
            Kembalian = x.Kembalian
        };
    }
}

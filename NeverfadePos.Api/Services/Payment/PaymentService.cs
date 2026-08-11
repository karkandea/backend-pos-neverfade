using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Payments.Xendit;

namespace NeverfadePos.Api.Services.Payment;

internal sealed class PaymentService(
    AppDbContext db,
    CurrentUser currentUser,
    ITrustedTenantExecutionScope trustedTenantScope,
    IXenditPaymentProvider xendit,
    IOptions<XenditOptions> xenditOptions)
    : IPaymentService
{
    public async Task<QrisPaymentDto> CreateQrisAsync(
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
            "QRIS",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "PAYMENT_METHOD_NOT_SUPPORTED",
                "Endpoint ini hanya menerima metode pembayaran QRIS.");
        }

        var draft = await ResolveDraftAsync(
            request,
            cancellationToken);

        var tenantId = currentUser.TenantId.Value;
        var paymentId = Guid.NewGuid();
        var referenceId = $"nf-{paymentId:N}";
        var noTrx = await GenerateNoTrxAsync(cancellationToken);

        var transaction = new NeverfadePos.Api.Entities.Transaction
        {
            TenantId = tenantId,
            NoTrx = noTrx,
            Kasir = currentUser.Nama ?? string.Empty,
            KasirId = currentUser.UserId.Value,
            CustomerId = draft.Customer?.Id,
            CustomerNama = draft.Customer?.Nama ?? string.Empty,
            Subtotal = draft.Subtotal,
            Disc = request.Disc,
            Tax = request.Tax,
            DiscAmt = draft.DiscAmt,
            TaxAmt = draft.TaxAmt,
            Total = draft.Total,
            MetodePembayaran = "QRIS",
            Dibayar = 0m,
            Kembalian = 0m,
            Status = TransactionStatuses.PendingPayment
        };

        var payment = new NeverfadePos.Api.Entities.Payment
        {
            Id = paymentId,
            TenantId = tenantId,
            TransactionId = transaction.Id,
            Provider = PaymentConstants.Provider,
            ProviderReferenceId = referenceId,
            Method = PaymentConstants.MethodQris,
            Currency = PaymentConstants.CurrencyIdr,
            Amount = draft.Total,
            Status = PaymentConstants.StatusCreating
        };

        await using (var localTransaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null)
        {
            db.Transactions.Add(transaction);
            db.TransactionItems.AddRange(draft.Items.Select(item =>
                new TransactionItem
                {
                    TenantId = tenantId,
                    TransactionId = transaction.Id,
                    ProductId = item.Product.Id,
                    Nama = item.Product.Nama,
                    HargaJual = item.HargaJual,
                    Qty = item.Qty,
                    Subtotal = item.Subtotal
                }));
            db.Payments.Add(payment);

            await db.SaveChangesAsync(cancellationToken);
            if (localTransaction is not null)
            {
                await localTransaction.CommitAsync(cancellationToken);
            }
        }

        try
        {
            var providerResult = await xendit.CreateQrisAsync(
                referenceId,
                draft.Total,
                $"NeverFade POS {noTrx}",
                cancellationToken);

            if (!string.Equals(
                    providerResult.ReferenceId,
                    referenceId,
                    StringComparison.Ordinal) ||
                Money(providerResult.RequestAmount) != draft.Total)
            {
                throw new XenditProviderException(
                    StatusCodes.Status502BadGateway,
                    "Xendit payment response tidak sesuai request NeverFade.");
            }

            payment.ProviderPaymentRequestId =
                providerResult.PaymentRequestId;
            payment.Status = PaymentConstants.StatusPending;
            payment.UpdatedAt = DateTime.UtcNow;

            db.PaymentRoutes.Add(new PaymentRoute
            {
                TenantId = tenantId,
                PaymentId = payment.Id,
                Provider = PaymentConstants.Provider,
                ProviderPaymentRequestId =
                    providerResult.PaymentRequestId
            });

            await db.SaveChangesAsync(cancellationToken);

            return new QrisPaymentDto
            {
                Id = payment.Id,
                TransactionId = transaction.Id,
                ProviderPaymentRequestId =
                    providerResult.PaymentRequestId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Status = payment.Status,
                QrString = providerResult.QrString,
                ExpiresAt = providerResult.ExpiresAt
            };
        }
        catch
        {
            payment.Status = PaymentConstants.StatusFailed;
            payment.UpdatedAt = DateTime.UtcNow;
            transaction.Status = TransactionStatuses.Failed;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PaymentStatusDto> GetStatusAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments
            .AsNoTracking()
            .Where(x => x.Id == paymentId)
            .Select(x => new PaymentStatusDto
            {
                Id = x.Id,
                TransactionId = x.TransactionId,
                Status = x.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        return payment ?? throw new KeyNotFoundException(
            "Payment tidak ditemukan.");
    }

    public async Task ProcessXenditWebhookAsync(
        string? callbackToken,
        XenditPaymentWebhookDto webhook,
        CancellationToken cancellationToken = default)
    {
        VerifyCallbackToken(callbackToken);
        ValidateWebhookShape(webhook);

        var route = await db.PaymentRoutes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Provider == PaymentConstants.Provider &&
                    x.ProviderPaymentRequestId ==
                        webhook.Data.PaymentRequestId,
                cancellationToken)
            ?? throw new PaymentApiException(
                StatusCodes.Status404NotFound,
                "PAYMENT_ROUTE_NOT_FOUND",
                "Payment route tidak ditemukan.");

        using var tenantScope = trustedTenantScope.Begin(
            route.TenantId,
            $"xendit-webhook:{webhook.Event}");

        var eventKey = $"{webhook.Event}:{webhook.Data.PaymentId}";
        var duplicate = await db.PaymentWebhookEvents
            .AsNoTracking()
            .AnyAsync(
                x => x.ProviderEventKey == eventKey,
                cancellationToken);

        if (duplicate)
        {
            return;
        }

        var payment = await db.Payments
            .Include(x => x.Transaction)
                .ThenInclude(x => x!.Items)
            .SingleAsync(
                x =>
                    x.Id == route.PaymentId &&
                    x.ProviderPaymentRequestId ==
                        route.ProviderPaymentRequestId,
                cancellationToken);

        ValidateWebhookMatchesPayment(webhook, payment);

        await using var databaseTransaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (webhook.Event == "payment.capture")
        {
            await ApplySuccessfulPaymentAsync(
                payment,
                webhook,
                cancellationToken);
        }
        else
        {
            payment.Status = PaymentConstants.StatusFailed;
            payment.FailureCode = webhook.Data.FailureCode;
            payment.ProviderPaymentId = webhook.Data.PaymentId;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.Transaction!.Status = TransactionStatuses.Failed;
        }

        db.PaymentWebhookEvents.Add(new PaymentWebhookEvent
        {
            TenantId = route.TenantId,
            PaymentId = payment.Id,
            ProviderEventKey = eventKey,
            EventType = webhook.Event,
            ProviderPaymentId = webhook.Data.PaymentId,
            ProcessingStatus = "processed"
        });

        await db.SaveChangesAsync(cancellationToken);
        if (databaseTransaction is not null)
        {
            await databaseTransaction.CommitAsync(cancellationToken);
        }
    }

    private async Task ApplySuccessfulPaymentAsync(
        NeverfadePos.Api.Entities.Payment payment,
        XenditPaymentWebhookDto webhook,
        CancellationToken cancellationToken)
    {
        if (payment.Status == PaymentConstants.StatusPaid)
        {
            return;
        }

        var transaction = payment.Transaction!;

        foreach (var item in transaction.Items)
        {
            var product = await db.Products.SingleAsync(
                x => x.Id == item.ProductId,
                cancellationToken);

            if (product.Stok < item.Qty)
            {
                throw new PaymentApiException(
                    StatusCodes.Status409Conflict,
                    "PAYMENT_STOCK_CONFLICT",
                    $"Stok produk {product.Nama} tidak mencukupi untuk finalisasi payment.");
            }

            product.Stok -= item.Qty;
            db.StockHistories.Add(new NeverfadePos.Api.Entities.StockHistory
            {
                TenantId = transaction.TenantId,
                ProdukId = product.Id,
                ProdukNama = product.Nama,
                Tipe = "transaksi",
                Jumlah = -item.Qty,
                StokAkhir = product.Stok,
                Keterangan = $"Transaksi {transaction.NoTrx}",
                User = transaction.Kasir
            });
        }

        if (transaction.CustomerId.HasValue)
        {
            var customer = await db.Customers.SingleAsync(
                x => x.Id == transaction.CustomerId.Value,
                cancellationToken);
            var settings = await db.Settings.SingleAsync(cancellationToken);

            customer.Poin +=
                (int)Math.Floor(transaction.Total / 1000m) *
                settings.PoinRate;
            customer.TotalTransaksi++;
        }

        var paidAt = DateTime.UtcNow;
        transaction.Status = TransactionStatuses.Paid;
        transaction.Dibayar = transaction.Total;
        transaction.Kembalian = 0m;
        transaction.FinalizedAt = paidAt;

        payment.Status = PaymentConstants.StatusPaid;
        payment.ProviderPaymentId = webhook.Data.PaymentId;
        payment.PaidAt = paidAt;
        payment.UpdatedAt = paidAt;

        db.PaymentLedgerEntries.Add(new PaymentLedgerEntry
        {
            TenantId = payment.TenantId,
            PaymentId = payment.Id,
            TransactionId = transaction.Id,
            EntryType = PaymentConstants.LedgerPaymentCredit,
            Amount = payment.Amount,
            Currency = payment.Currency,
            ProviderReference = webhook.Data.PaymentId
        });
    }

    private async Task<TransactionDraft> ResolveDraftAsync(
        CreateTransactionDto request,
        CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException(
                "Item transaksi tidak boleh kosong.");
        }

        if (request.Disc is < 0 or > 100 ||
            request.Tax is < 0 or > 100)
        {
            throw new InvalidOperationException(
                "Diskon dan pajak harus berada antara 0 sampai 100 persen.");
        }

        NeverfadePos.Api.Entities.Customer? customer = null;
        if (request.CustomerId.HasValue)
        {
            customer = await db.Customers.SingleOrDefaultAsync(
                x => x.Id == request.CustomerId.Value,
                cancellationToken)
                ?? throw new KeyNotFoundException(
                    "Customer tidak ditemukan.");
        }

        var items = new List<DraftItem>();
        foreach (var item in request.Items)
        {
            var product = await db.Products.SingleOrDefaultAsync(
                x => x.Id == item.Id,
                cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Product {item.Id} tidak ditemukan.");

            if (item.Qty <= 0 || product.Stok < item.Qty)
            {
                throw new InvalidOperationException(
                    $"Qty atau stok produk {product.Nama} tidak valid.");
            }

            var itemSubtotal = Money(product.HargaJual * item.Qty);
            ValidateMoney("harga jual produk", item.HargaJual, product.HargaJual);
            ValidateMoney("subtotal item", item.Subtotal, itemSubtotal);
            items.Add(new DraftItem(
                product,
                item.Qty,
                Money(product.HargaJual),
                itemSubtotal));
        }

        var subtotal = Money(items.Sum(x => x.Subtotal));
        var discAmt = Money(subtotal * request.Disc / 100m);
        var afterDiscount = Money(subtotal - discAmt);
        var taxAmt = Money(afterDiscount * request.Tax / 100m);
        var total = Money(afterDiscount + taxAmt);

        ValidateMoney("subtotal transaksi", request.Subtotal, subtotal);
        ValidateMoney("nilai diskon", request.DiscAmt, discAmt);
        ValidateMoney("nilai pajak", request.TaxAmt, taxAmt);
        ValidateMoney("total transaksi", request.Total, total);

        return new TransactionDraft(
            customer,
            items,
            subtotal,
            discAmt,
            taxAmt,
            total);
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

        if (!string.IsNullOrWhiteSpace(lastNo) &&
            int.TryParse(lastNo.Split('-').Last(), out var current))
        {
            next = current + 1;
        }

        return $"{prefix}-{next:0000}";
    }

    private void VerifyCallbackToken(string? callbackToken)
    {
        var expected = xenditOptions.Value.WebhookCallbackToken;
        if (string.IsNullOrWhiteSpace(expected))
        {
            throw new InvalidOperationException(
                "Xendit:WebhookCallbackToken is required for webhook processing.");
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(callbackToken ?? string.Empty);
        var valid = expectedBytes.Length == actualBytes.Length &&
            CryptographicOperations.FixedTimeEquals(
                expectedBytes,
                actualBytes);

        if (!valid)
        {
            throw new PaymentApiException(
                StatusCodes.Status401Unauthorized,
                "XENDIT_WEBHOOK_UNAUTHORIZED",
                "Xendit webhook token tidak valid.");
        }
    }

    private static void ValidateWebhookShape(
        XenditPaymentWebhookDto webhook)
    {
        var validCapture =
            webhook.Event == "payment.capture" &&
            webhook.Data.Status == "SUCCEEDED";
        var validFailure =
            webhook.Event == "payment.failure" &&
            webhook.Data.Status == "FAILED";

        if ((!validCapture && !validFailure) ||
            string.IsNullOrWhiteSpace(webhook.Data.PaymentId) ||
            string.IsNullOrWhiteSpace(webhook.Data.PaymentRequestId))
        {
            throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "XENDIT_WEBHOOK_INVALID",
                "Xendit webhook payment tidak valid.");
        }
    }

    private static void ValidateWebhookMatchesPayment(
        XenditPaymentWebhookDto webhook,
        NeverfadePos.Api.Entities.Payment payment)
    {
        if (!string.Equals(
                webhook.Data.ReferenceId,
                payment.ProviderReferenceId,
                StringComparison.Ordinal) ||
            Money(webhook.Data.RequestAmount) != payment.Amount ||
            webhook.Data.ChannelCode != "QRIS" ||
            webhook.Data.Currency != PaymentConstants.CurrencyIdr)
        {
            throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "XENDIT_WEBHOOK_MISMATCH",
                "Xendit webhook tidak sesuai dengan payment NeverFade.");
        }
    }

    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static void ValidateMoney(
        string field,
        decimal clientValue,
        decimal serverValue)
    {
        if (Money(clientValue) != Money(serverValue))
        {
            throw new InvalidOperationException(
                $"Nilai {field} tidak sesuai data server.");
        }
    }

    private sealed record DraftItem(
        NeverfadePos.Api.Entities.Product Product,
        int Qty,
        decimal HargaJual,
        decimal Subtotal);

    private sealed record TransactionDraft(
        NeverfadePos.Api.Entities.Customer? Customer,
        List<DraftItem> Items,
        decimal Subtotal,
        decimal DiscAmt,
        decimal TaxAmt,
        decimal Total);
}

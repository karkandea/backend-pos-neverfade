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
using NeverfadePos.Api.Payments;
using NeverfadePos.Api.Services.Retail;
using NeverfadePos.Api.Services.Outlet;
using NeverfadePos.Api.Services.Sales;

namespace NeverfadePos.Api.Services.Payment;

internal sealed class PaymentService(
    AppDbContext db,
    CurrentUser currentUser,
    ITrustedTenantExecutionScope trustedTenantScope,
    IPaymentModeGate paymentModeGate,
    IRetailSaleResolver retailSaleResolver,
    IOutletExecutionContext outletContext,
    IXenditPaymentProvider xendit,
    IOptions<XenditOptions> xenditOptions)
    : IPaymentService
{
    public PaymentCapabilitiesDto GetCapabilities()
    {
        var tenantId = currentUser.TenantId ??
            throw new UnauthorizedAccessException();

        if (db.Tenants.AsNoTracking().Any(x => x.Id == tenantId && x.Mode == "demo"))
            return new PaymentCapabilitiesDto { QrisEnabled = false, Mode = "disabled", IsSandbox = false };
        return paymentModeGate.GetCapabilities(tenantId);
    }

    public async Task<QrisPaymentDto> CreateQrisAsync(
        CreateTransactionDto request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue ||
            !currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException();
        }

        if (await db.Tenants.AsNoTracking().AnyAsync(
            x => x.Id == currentUser.TenantId.Value && x.Mode == "demo", cancellationToken))
            throw new PaymentApiException(StatusCodes.Status403Forbidden,
                "PAYMENT_DEMO_TENANT_FORBIDDEN", "Tenant demo tidak dapat membuat pembayaran provider.");

        paymentModeGate.EnsureQrisAllowed(
            currentUser.TenantId.Value);

        var outletId = outletContext.OutletId
            ?? throw new InvalidOperationException("QRIS requires a resolved outlet.");
        var existingPayment = await db.Payments
            .Where(x => x.Transaction != null && x.Transaction.OutletId == outletId &&
                (x.Status == PaymentConstants.StatusCreating ||
                 x.Status == PaymentConstants.StatusPending))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingPayment is not null)
        {
            if (existingPayment.Status == PaymentConstants.StatusCreating ||
                existingPayment.Status == PaymentConstants.StatusPending)
            {
                throw new PaymentApiException(
                    StatusCodes.Status409Conflict,
                    "PAYMENT_ALREADY_PENDING",
                    "Masih ada pembayaran QRIS yang belum selesai.");
            }
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
                    ProductVariantId = item.Variant?.Id,
                    VariantSku = item.Variant?.Sku ?? string.Empty,
                    VariantLabel = item.Variant?.Label ?? string.Empty,
                    BasePrice = item.BasePrice,
                    PriceLevelId = item.PriceLevelId,
                    PriceLevelName = item.PriceLevelName,
                    Qty = item.Qty,
                    Quantity = item.Quantity,
                    ProductType = item.Product.Type,
                    TracksStock = item.Product.TracksStock,
                    QuantityPrecision = item.Product.QuantityPrecision,
                    Unit = item.Product.Satuan,
                    Note = item.Note,
                    Subtotal = item.Subtotal
                }));
            db.Payments.Add(payment);

            await db.SaveChangesAsync(cancellationToken);
            if (localTransaction is not null)
            {
                await localTransaction.CommitAsync(cancellationToken);
            }
        }

        // A provider POST may be accepted even if the response is lost. A failed
        // local payment would permit a second charge, so keep the original
        // immutable reference pending until a verified provider event resolves it.
        var providerRequestStarted = false;
        try
        {
            var expiryMinutes = xenditOptions.Value.QrisExpiryMinutes;
            if (expiryMinutes is < 2 or > 30)
            {
                throw new InvalidOperationException(
                    "Xendit:QrisExpiryMinutes must be between 2 and 30.");
            }

            providerRequestStarted = true;
            var providerResult = await xendit.CreateQrisAsync(
                referenceId,
                draft.Total,
                $"NeverFade POS {noTrx}",
                DateTime.UtcNow.AddMinutes(expiryMinutes),
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

            // An authenticated webhook may have arrived before the provider POST
            // returned. Serialize registration and reload the terminal state;
            // never overwrite a paid/failed outcome with pending.
            await using var registrationTransaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(cancellationToken)
                : null;
            await TenantStockLock.AcquireAsync(db, tenantId, cancellationToken);
            await db.Entry(payment).ReloadAsync(cancellationToken);
            if (payment.ProviderPaymentRequestId is { } existingRequestId &&
                existingRequestId != providerResult.PaymentRequestId)
                throw new PaymentApiException(StatusCodes.Status409Conflict,
                    "PAYMENT_PROVIDER_REQUEST_CONFLICT",
                    "Provider request tidak sesuai dengan attempt yang tersimpan.");

            if (payment.Status is not (PaymentConstants.StatusPaid or PaymentConstants.StatusFailed))
            {
                payment.ProviderPaymentRequestId = providerResult.PaymentRequestId;
                payment.QrString = providerResult.QrString;
                payment.ExpiresAt = providerResult.ExpiresAt;
                payment.Status = PaymentConstants.StatusPending;
                payment.UpdatedAt = DateTime.UtcNow;
            }
            if (!await db.PaymentRoutes.AsNoTracking().AnyAsync(
                    x => x.PaymentId == payment.Id, cancellationToken))
                db.PaymentRoutes.Add(new PaymentRoute
                {
                    TenantId = tenantId,
                    PaymentId = payment.Id,
                    Provider = PaymentConstants.Provider,
                    ProviderPaymentRequestId = providerResult.PaymentRequestId
                });
            await db.SaveChangesAsync(cancellationToken);
            if (registrationTransaction is not null)
                await registrationTransaction.CommitAsync(cancellationToken);

            return new QrisPaymentDto
            {
                Id = payment.Id,
                TransactionId = transaction.Id,
                ProviderPaymentRequestId =
                    providerResult.PaymentRequestId,
                ProviderReferenceId = payment.ProviderReferenceId,
                Amount = payment.Amount,
                Currency = payment.Currency,
                Status = payment.Status,
                QrString = payment.QrString,
                ExpiresAt = providerResult.ExpiresAt
            };
        }
        catch (Exception ex) when (providerRequestStarted &&
            ex is not OperationCanceledException)
        {
            // In the ambiguous case the persisted state is still 'creating' or
            // was atomically saved as 'pending'. Neither is safe to retry as a new
            // charge. Do NOT mark sale failed or clear the outstanding attempt.
            throw new PaymentApiException(StatusCodes.Status503ServiceUnavailable,
                "PAYMENT_CREATION_UNCERTAIN",
                "Kepastian pembuatan QRIS belum diterima. Periksa pembayaran sebelumnya; jangan buat tagihan baru.");
        }
        catch
        {
            // Known local preflight failure before provider request was sent.
            // A client cancellation after dispatch is also ambiguous and must
            // not finalize a sale as failed.
            if (providerRequestStarted) throw;
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
            .SingleOrDefaultAsync(
                x => x.Id == paymentId,
                cancellationToken);

        if (payment is null)
        {
            throw new KeyNotFoundException("Payment tidak ditemukan.");
        }

        return MapStatus(payment);
    }

    public async Task<PaymentStatusDto?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var outletId = outletContext.OutletId
            ?? throw new InvalidOperationException("Current payment requires a resolved outlet.");
        var payment = await db.Payments
            .Where(x => x.Transaction != null && x.Transaction.OutletId == outletId &&
                (x.Status == PaymentConstants.StatusCreating ||
                 x.Status == PaymentConstants.StatusPending))
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (payment is null)
        {
            return null;
        }

        return MapStatus(payment);
    }

    public async Task<PaymentStatusDto> CancelAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await db.Payments
            .Include(x => x.Transaction)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment tidak ditemukan.");

        if (payment.Status == PaymentConstants.StatusPaid)
        {
            throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "PAYMENT_ALREADY_PAID",
                "Pembayaran sudah berhasil dan tidak dapat dibatalkan.");
        }

        if (payment.Status == PaymentConstants.StatusFailed)
        {
            return MapStatus(payment);
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderPaymentRequestId))
        {
            throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "PAYMENT_NOT_CANCELLABLE",
                "Payment request belum siap dibatalkan. Periksa status kembali.");
        }

        await xendit.CancelPaymentRequestAsync(
            payment.ProviderPaymentRequestId,
            cancellationToken);

        // The provider cancellation request can race a webhook. Re-read after
        // acquiring the same stock/payment lock before changing terminal state.
        await using var cancellationTransaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await TenantStockLock.AcquireAsync(db, payment.TenantId, cancellationToken);
        await db.Entry(payment).ReloadAsync(cancellationToken);
        if (payment.Status == PaymentConstants.StatusPaid)
            throw new PaymentApiException(StatusCodes.Status409Conflict,
                "PAYMENT_ALREADY_PAID",
                "Pembayaran telah dikonfirmasi berhasil dan tidak dapat dibatalkan.");
        if (payment.Status == PaymentConstants.StatusFailed)
            return MapStatus(payment);

        payment.Status = PaymentConstants.StatusFailed;
        payment.FailureCode = "PAYMENT_REQUEST_CANCELED";
        payment.UpdatedAt = DateTime.UtcNow;
        if (payment.Transaction is not null)
        {
            await db.Entry(payment.Transaction).ReloadAsync(cancellationToken);
            if (payment.Transaction.Status == TransactionStatuses.PendingPayment)
                payment.Transaction.Status = TransactionStatuses.Failed;
        }
        await db.SaveChangesAsync(cancellationToken);
        if (cancellationTransaction is not null)
            await cancellationTransaction.CommitAsync(cancellationToken);

        return MapStatus(payment);
    }

    public async Task ProcessXenditWebhookAsync(
        string? callbackToken,
        XenditPaymentWebhookDto webhook,
        CancellationToken cancellationToken = default)
    {
        VerifyCallbackToken(callbackToken);
        ValidateWebhookShape(webhook);

        var route = await db.PaymentRoutes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Provider == PaymentConstants.Provider &&
                x.ProviderPaymentRequestId == webhook.Data.PaymentRequestId,
                cancellationToken);

        // A provider can accept create while its HTTP response is lost. Its
        // authenticated callback still carries the immutable globally unique
        // NeverFade reference; recover that original payment, never create a
        // second draft. The tenant scope is established from the DB record,
        // not from a client-supplied tenant header or payment amount.
        var unregistered = route is null
            ? await db.Payments.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Provider == PaymentConstants.Provider &&
                    x.ProviderReferenceId == webhook.Data.ReferenceId)
                .Select(x => new { x.Id, x.TenantId, x.ProviderPaymentRequestId })
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        if (route is null && unregistered is null)
            throw new PaymentApiException(StatusCodes.Status404NotFound,
                "PAYMENT_ROUTE_NOT_FOUND", "Payment route tidak ditemukan.");
        if (unregistered?.ProviderPaymentRequestId is { } previousRequestId &&
            previousRequestId != webhook.Data.PaymentRequestId)
            throw new PaymentApiException(StatusCodes.Status409Conflict,
                "XENDIT_WEBHOOK_REQUEST_MISMATCH", "Provider request tidak sesuai referensi payment.");

        var paymentId = route?.PaymentId ?? unregistered!.Id;
        var tenantId = route?.TenantId ?? unregistered!.TenantId;
        using var tenantScope = trustedTenantScope.Begin(tenantId,
            $"xendit-webhook:{webhook.Event}");
        await using var databaseTransaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        await TenantStockLock.AcquireAsync(db, tenantId, cancellationToken);

        var eventKey = $"{webhook.Event}:{webhook.Data.PaymentId}";
        if (await db.PaymentWebhookEvents.AsNoTracking().AnyAsync(
                x => x.ProviderEventKey == eventKey, cancellationToken)) return;

        var payment = await db.Payments.Include(x => x.Transaction)
            .ThenInclude(x => x!.Items)
            .SingleAsync(x => x.Id == paymentId, cancellationToken);
        if (payment.ProviderPaymentRequestId is { } storedRequestId &&
            storedRequestId != webhook.Data.PaymentRequestId)
            throw new PaymentApiException(StatusCodes.Status409Conflict,
                "XENDIT_WEBHOOK_REQUEST_MISMATCH", "Provider request tidak sesuai referensi payment.");
        ValidateWebhookMatchesPayment(webhook, payment);

        if (route is null)
        {
            payment.ProviderPaymentRequestId = webhook.Data.PaymentRequestId;
            db.PaymentRoutes.Add(new PaymentRoute
            {
                TenantId = tenantId,
                PaymentId = payment.Id,
                Provider = PaymentConstants.Provider,
                ProviderPaymentRequestId = webhook.Data.PaymentRequestId
            });
        }

        if (webhook.Event == "payment.capture")
        {
            await ApplySuccessfulPaymentAsync(
                payment,
                webhook,
                cancellationToken);
        }
        else if (payment.Status != PaymentConstants.StatusPaid &&
                 payment.Status != PaymentConstants.StatusFailed)
        {
            payment.Status = PaymentConstants.StatusFailed;
            payment.FailureCode = webhook.Data.FailureCode;
            payment.ProviderPaymentId = webhook.Data.PaymentId;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.Transaction!.Status = TransactionStatuses.Failed;
        }

        db.PaymentWebhookEvents.Add(new PaymentWebhookEvent
        {
            TenantId = tenantId,
            PaymentId = payment.Id,
            ProviderEventKey = eventKey,
            EventType = webhook.Event,
            ProviderPaymentId = webhook.Data.PaymentId,
            ProcessingStatus = webhook.Event == "payment.failure" &&
                payment.Status == PaymentConstants.StatusPaid
                ? "ignored_paid_terminal" : "processed"
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
            if (!item.TracksStock)
            {
                continue;
            }

            var product = await db.Products.SingleAsync(
                x => x.Id == item.ProductId,
                cancellationToken);

            var quantity =
                item.Quantity > 0m
                    ? item.Quantity
                    : item.Qty;

            int stockUnits;
            try
            {
                stockUnits = ProductQuantityRules.ToStockUnits(
                    product,
                    quantity);
            }
            catch (TenantApiException exception)
            {
                throw new PaymentApiException(
                    exception.StatusCode,
                    exception.Code,
                    exception.Message);
            }

            ProductVariant? variant = null;
            if (item.ProductVariantId.HasValue)
            {
                variant = await db.ProductVariants.SingleOrDefaultAsync(
                    x => x.Id == item.ProductVariantId.Value && x.ProductId == product.Id,
                    cancellationToken)
                    ?? throw new PaymentApiException(
                        StatusCodes.Status409Conflict,
                        "PAYMENT_VARIANT_CONFLICT",
                        "Varian transaksi tidak lagi tersedia.");

                if (variant.Stok < stockUnits)
                {
                    throw new PaymentApiException(
                        StatusCodes.Status409Conflict,
                        "PAYMENT_VARIANT_STOCK_CONFLICT",
                        $"Stok varian {product.Nama} {item.VariantLabel} tidak mencukupi untuk finalisasi payment.");
                }
            }

            if (product.Stok < stockUnits)
            {
                throw new PaymentApiException(
                    StatusCodes.Status409Conflict,
                    "PAYMENT_STOCK_CONFLICT",
                    $"Stok produk {product.Nama} tidak mencukupi untuk finalisasi payment.");
            }

            if (variant is not null)
            {
                variant.Stok -= stockUnits;
            }

            product.Stok -= stockUnits;
            db.StockHistories.Add(new NeverfadePos.Api.Entities.StockHistory
            {
                TenantId = transaction.TenantId,
                ProdukId = product.Id,
                ProdukNama = product.Nama,
                ProductVariantId = item.ProductVariantId,
                VariantSku = item.VariantSku,
                VariantLabel = item.VariantLabel,
                Tipe = "transaksi",
                Jumlah = -stockUnits,
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
        payment.FailureCode = null;
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

    private static PaymentStatusDto MapStatus(
        NeverfadePos.Api.Entities.Payment payment) => new()
    {
        Id = payment.Id,
        TransactionId = payment.TransactionId,
        Status = payment.Status == PaymentConstants.StatusFailed &&
            IsExpiredFailure(payment.FailureCode)
            ? PaymentConstants.StatusExpired
            : payment.Status,
        Amount = payment.Amount,
        Currency = payment.Currency,
        ProviderPaymentRequestId =
            payment.ProviderPaymentRequestId ?? string.Empty,
        ProviderReferenceId = payment.ProviderReferenceId,
        QrString = payment.QrString,
        ExpiresAt = payment.ExpiresAt,
        FailureCode = payment.FailureCode,
        UpdatedAt = payment.UpdatedAt
    };

    private static bool IsExpiredFailure(string? failureCode) =>
        string.Equals(
            failureCode,
            "PAYMENT_REQUEST_EXPIRED",
            StringComparison.OrdinalIgnoreCase);

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
            var resolved = await retailSaleResolver.ResolveAsync(
                item.Id,
                item.Qty,
                item.Quantity,
                item.ProductVariantId,
                item.PriceLevelId,
                enforceStock: true,
                cancellationToken);

            ValidateMoney("harga jual produk", item.HargaJual, resolved.UnitPrice);
            ValidateMoney("subtotal item", item.Subtotal, resolved.Subtotal);
            items.Add(new DraftItem(
                resolved.Product,
                resolved.Variant,
                resolved.LegacyQty,
                resolved.Quantity,
                resolved.BasePrice,
                resolved.UnitPrice,
                resolved.PriceLevelId,
                resolved.PriceLevelName,
                resolved.Subtotal,
                item.Note?.Trim() ?? string.Empty));
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
        ProductVariant? Variant,
        int Qty,
        decimal Quantity,
        decimal BasePrice,
        decimal HargaJual,
        Guid? PriceLevelId,
        string PriceLevelName,
        decimal Subtotal,
        string Note);

    private sealed record TransactionDraft(
        NeverfadePos.Api.Entities.Customer? Customer,
        List<DraftItem> Items,
        decimal Subtotal,
        decimal DiscAmt,
        decimal TaxAmt,
        decimal Total);
}

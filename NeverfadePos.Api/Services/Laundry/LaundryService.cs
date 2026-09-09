using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Laundry;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Laundry;

public sealed class LaundryService(
    AppDbContext db,
    CurrentUser currentUser)
    : ILaundryService
{
    public async Task<IReadOnlyList<LaundryWorkOrderDto>> GetAllAsync(
        string? status,
        CancellationToken cancellationToken = default)
    {
        RequireUser();

        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? null
            : status.Trim().ToLowerInvariant();

        if (normalizedStatus is not null &&
            !LaundryConstants.Statuses.Contains(normalizedStatus))
        {
            throw BadRequest(
                "LAUNDRY_STATUS_INVALID",
                "Status pesanan laundry tidak valid.");
        }

        var query = db.LaundryWorkOrders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items)
            .Include(x => x.StatusHistory)
            .AsQueryable();

        if (normalizedStatus is not null)
        {
            query = query.Where(x => x.Status == normalizedStatus);
        }

        return await query
            .OrderBy(x =>
                x.Status == LaundryConstants.StatusReady ? 0 :
                x.Status == LaundryConstants.StatusInProgress ? 1 :
                x.Status == LaundryConstants.StatusReceived ? 2 :
                x.Status == LaundryConstants.StatusCompleted ? 3 : 4)
            .ThenBy(x => x.PromisedAt)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<LaundryWorkOrderDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        RequireUser();

        var order = await LoadAsync(
            id,
            tracking: false,
            cancellationToken);

        return Map(order);
    }

    public async Task<LaundryWorkOrderDto> CreateAsync(
        CreateLaundryWorkOrderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireUser();

        if (request.Items.Count == 0)
        {
            throw BadRequest(
                "LAUNDRY_ITEMS_REQUIRED",
                "Pesanan laundry harus memiliki minimal satu item.");
        }

        if (request.CustomerId == Guid.Empty)
        {
            throw BadRequest(
                "LAUNDRY_CUSTOMER_REQUIRED",
                "Pelanggan wajib dipilih.");
        }

        var customer = await db.Customers
            .SingleOrDefaultAsync(
                x => x.Id == request.CustomerId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Pelanggan tidak ditemukan.");

        var promisedAt = NormalizeUtc(request.PromisedAt);
        if (promisedAt == DateTime.MinValue)
        {
            throw BadRequest(
                "LAUNDRY_ETA_REQUIRED",
                "Estimasi selesai wajib diisi.");
        }

        var groupedRequests = request.Items
            .GroupBy(x => x.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(x => x.Quantity)
            })
            .ToList();

        if (groupedRequests.Any(x => x.ProductId == Guid.Empty))
        {
            throw BadRequest(
                "LAUNDRY_PRODUCT_INVALID",
                "Item layanan tidak valid.");
        }

        var productIds = groupedRequests
            .Select(x => x.ProductId)
            .ToList();

        var products = await db.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (products.Count != productIds.Count)
        {
            throw new KeyNotFoundException(
                "Salah satu produk atau layanan tidak ditemukan.");
        }

        var order = new LaundryWorkOrder
        {
            TenantId = tenantId,
            CustomerId = customer.Id,
            CreatedByUserId = userId,
            OrderNumber = GenerateOrderNumber(DateTime.UtcNow),
            Status = LaundryConstants.StatusReceived,
            PaymentStatus = LaundryConstants.PaymentUnpaid,
            Notes = CleanOptional(request.Notes, 1000),
            PromisedAt = promisedAt,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var requested in groupedRequests)
        {
            var product = products[requested.ProductId];

            ProductQuantityRules.Validate(
                product,
                requested.Quantity,
                enforceStock: true);

            var subtotal = Money(
                product.HargaJual * requested.Quantity);

            order.Items.Add(new LaundryWorkOrderItem
            {
                TenantId = tenantId,
                ProductId = product.Id,
                Nama = product.Nama,
                ProductType = product.Type,
                Unit = product.Satuan,
                Quantity = requested.Quantity,
                QuantityPrecision = product.QuantityPrecision,
                UnitPrice = Money(product.HargaJual),
                Subtotal = subtotal
            });
        }

        order.StatusHistory.Add(new LaundryWorkOrderStatusHistory
        {
            TenantId = tenantId,
            ActorUserId = userId,
            FromStatus = string.Empty,
            ToStatus = LaundryConstants.StatusReceived,
            ActorName = currentUser.Nama ?? currentUser.Username ?? "Pengguna",
            Reason = "Pesanan diterima."
        });

        db.LaundryWorkOrders.Add(order);
        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = userId,
            EventType = "LAUNDRY_WORK_ORDER_CREATED",
            Metadata = JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                customerId = customer.Id,
                itemCount = order.Items.Count,
                promisedAt = order.PromisedAt
            })
        });

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(order.Id, cancellationToken);
    }

    public async Task<LaundryWorkOrderDto> UpdateStatusAsync(
        Guid id,
        UpdateLaundryWorkOrderStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireUser();
        var order = await LoadAsync(
            id,
            tracking: true,
            cancellationToken);

        var next = request.Status?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!LaundryConstants.Statuses.Contains(next))
        {
            throw BadRequest(
                "LAUNDRY_STATUS_INVALID",
                "Status pesanan laundry tidak valid.");
        }

        if (next == order.Status)
        {
            return Map(order);
        }

        EnsureTransition(order, next);

        var reason = CleanOptional(request.Reason, 500);
        if (next == LaundryConstants.StatusCancelled &&
            reason.Length < 3)
        {
            throw BadRequest(
                "LAUNDRY_CANCELLATION_REASON_REQUIRED",
                "Alasan pembatalan wajib diisi.");
        }

        if (next == LaundryConstants.StatusCompleted &&
            order.PaymentStatus != LaundryConstants.PaymentPaid)
        {
            throw Conflict(
                "LAUNDRY_PAYMENT_REQUIRED",
                "Pesanan hanya dapat diselesaikan setelah pembayaran berhasil.");
        }

        if (next == LaundryConstants.StatusCancelled &&
            order.PaymentStatus == LaundryConstants.PaymentPaid)
        {
            throw Conflict(
                "LAUNDRY_PAID_CANNOT_CANCEL",
                "Pesanan yang sudah dibayar tidak dapat dibatalkan tanpa proses refund.");
        }

        var previous = order.Status;
        var now = DateTime.UtcNow;

        order.Status = next;
        order.UpdatedAt = now;

        if (next == LaundryConstants.StatusCompleted)
        {
            order.CompletedAt = now;
        }
        else if (next == LaundryConstants.StatusCancelled)
        {
            order.CancellationReason = reason;
            order.CancelledAt = now;
        }

        db.LaundryWorkOrderStatusHistory.Add(
            new LaundryWorkOrderStatusHistory
            {
                TenantId = tenantId,
                LaundryWorkOrderId = order.Id,
                ActorUserId = userId,
                FromStatus = previous,
                ToStatus = next,
                ActorName = currentUser.Nama ?? currentUser.Username ?? "Pengguna",
                Reason = reason
            });

        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = userId,
            EventType = "LAUNDRY_WORK_ORDER_STATUS_CHANGED",
            Metadata = JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                fromStatus = previous,
                toStatus = next,
                reason
            })
        });

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(
            order.Id,
            cancellationToken);
    }

    public async Task<LaundryWorkOrderDto> CompletePaymentAsync(
        Guid id,
        CompleteLaundryPaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireUser();
        var order = await LoadAsync(
            id,
            tracking: true,
            cancellationToken);

        if (request.TransactionId == Guid.Empty)
        {
            throw BadRequest(
                "LAUNDRY_TRANSACTION_REQUIRED",
                "Transaksi pembayaran wajib dipilih.");
        }

        if (order.Status == LaundryConstants.StatusCancelled)
        {
            throw Conflict(
                "LAUNDRY_ORDER_CANCELLED",
                "Pesanan yang sudah dibatalkan tidak dapat menerima pembayaran.");
        }

        if (order.PaymentStatus == LaundryConstants.PaymentPaid)
        {
            if (order.TransactionId == request.TransactionId)
            {
                return await GetByIdAsync(
                    order.Id,
                    cancellationToken);
            }

            throw Conflict(
                "LAUNDRY_ALREADY_PAID",
                "Pesanan sudah dibayar dengan transaksi lain.");
        }

        if (order.Status != LaundryConstants.StatusReady)
        {
            throw Conflict(
                "LAUNDRY_NOT_READY_FOR_PAYMENT",
                "Pembayaran laundry hanya dapat diselesaikan saat pesanan siap diambil.");
        }

        var transaction = await db.Transactions
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(
                x => x.Id == request.TransactionId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Transaksi tidak ditemukan.");

        if (transaction.Status != TransactionStatuses.Paid ||
            !transaction.FinalizedAt.HasValue)
        {
            throw Conflict(
                "LAUNDRY_TRANSACTION_NOT_PAID",
                "Pesanan hanya dapat ditautkan ke transaksi yang sudah dibayar.");
        }

        if (transaction.CustomerId != order.CustomerId)
        {
            throw Conflict(
                "LAUNDRY_TRANSACTION_CUSTOMER_MISMATCH",
                "Pelanggan transaksi tidak sesuai dengan pesanan laundry.");
        }

        if (
            transaction.Disc != 0m ||
            transaction.Tax != 0m ||
            Money(transaction.Total) !=
                Money(order.Items.Sum(x => x.Subtotal)))
        {
            throw Conflict(
                "LAUNDRY_TRANSACTION_TOTAL_MISMATCH",
                "Total transaksi tidak sesuai dengan pesanan laundry.");
        }

        var orderGroups = order.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Quantity = group.Sum(x => x.Quantity),
                    Subtotal = Money(group.Sum(x => x.Subtotal))
                });

        var transactionGroups = transaction.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Quantity = group.Sum(TransactionQuantity),
                    Subtotal = Money(group.Sum(x => x.Subtotal))
                });

        var itemsMatch =
            orderGroups.Count == transactionGroups.Count &&
            orderGroups.All(pair =>
                transactionGroups.TryGetValue(pair.Key, out var transactionItem) &&
                transactionItem.Quantity == pair.Value.Quantity &&
                transactionItem.Subtotal == pair.Value.Subtotal);

        if (!itemsMatch)
        {
            throw Conflict(
                "LAUNDRY_TRANSACTION_MISMATCH",
                "Item transaksi tidak sesuai dengan pesanan laundry.");
        }

        var transactionUsed = await db.LaundryWorkOrders
            .AnyAsync(
                x =>
                    x.Id != order.Id &&
                    x.TransactionId == transaction.Id,
                cancellationToken);

        if (transactionUsed)
        {
            throw Conflict(
                "LAUNDRY_TRANSACTION_ALREADY_LINKED",
                "Transaksi sudah terhubung ke pesanan laundry lain.");
        }

        var now = DateTime.UtcNow;
        order.TransactionId = transaction.Id;
        order.PaymentStatus = LaundryConstants.PaymentPaid;
        order.PaidAt = now;
        order.UpdatedAt = now;

        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = userId,
            EventType = "LAUNDRY_WORK_ORDER_PAID",
            Metadata = JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                transactionId = transaction.Id,
                transactionNo = transaction.NoTrx
            })
        });

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(
            order.Id,
            cancellationToken);
    }

    private async Task<LaundryWorkOrder> LoadAsync(
        Guid id,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<LaundryWorkOrder> query = db.LaundryWorkOrders;

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        if (tracking)
        {
            query = query
                .Include(x => x.Items);
        }
        else
        {
            query = query
                .Include(x => x.Customer)
                .Include(x => x.Items)
                .Include(x => x.StatusHistory);
        }

        var order = await query
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        return order ??
            throw new KeyNotFoundException(
                "Pesanan laundry tidak ditemukan.");
    }

    private static void EnsureTransition(
        LaundryWorkOrder order,
        string next)
    {
        var valid = order.Status switch
        {
            LaundryConstants.StatusReceived =>
                next is
                    LaundryConstants.StatusInProgress or
                    LaundryConstants.StatusCancelled,

            LaundryConstants.StatusInProgress =>
                next is
                    LaundryConstants.StatusReady or
                    LaundryConstants.StatusCancelled,

            LaundryConstants.StatusReady =>
                next == LaundryConstants.StatusCompleted,

            _ => false
        };

        if (!valid)
        {
            throw Conflict(
                "LAUNDRY_INVALID_TRANSITION",
                $"Status {order.Status} tidak dapat diubah menjadi {next}.");
        }
    }

    private (Guid TenantId, Guid UserId) RequireUser()
    {
        if (!currentUser.TenantId.HasValue ||
            !currentUser.UserId.HasValue ||
            currentUser.Role is not ("owner" or "admin" or "kasir"))
        {
            throw new UnauthorizedAccessException();
        }

        return (
            currentUser.TenantId.Value,
            currentUser.UserId.Value);
    }

    private static LaundryWorkOrderDto Map(
        LaundryWorkOrder order) =>
        new()
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            CustomerName = order.Customer?.Nama ?? string.Empty,
            CustomerPhone = order.Customer?.Hp ?? string.Empty,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            TransactionId = order.TransactionId,
            Total = Money(order.Items.Sum(x => x.Subtotal)),
            Notes = order.Notes,
            CancellationReason = order.CancellationReason,
            ReceivedAt = order.CreatedAt,
            PromisedAt = order.PromisedAt,
            UpdatedAt = order.UpdatedAt,
            PaidAt = order.PaidAt,
            CompletedAt = order.CompletedAt,
            CancelledAt = order.CancelledAt,
            Items = order.Items
                .OrderBy(x => x.CreatedAt)
                .Select(x => new LaundryWorkOrderItemDto
                {
                    Id = x.Id,
                    ProductId = x.ProductId,
                    Nama = x.Nama,
                    ProductType = x.ProductType,
                    Unit = x.Unit,
                    Quantity = x.Quantity,
                    QuantityPrecision = x.QuantityPrecision,
                    UnitPrice = x.UnitPrice,
                    Subtotal = x.Subtotal
                })
                .ToList(),
            StatusHistory = order.StatusHistory
                .OrderBy(x => x.CreatedAt)
                .Select(x => new LaundryWorkOrderStatusHistoryDto
                {
                    Id = x.Id,
                    FromStatus = x.FromStatus,
                    ToStatus = x.ToStatus,
                    ActorName = x.ActorName,
                    Reason = x.Reason,
                    At = x.CreatedAt
                })
                .ToList()
        };

    private static decimal TransactionQuantity(
        TransactionItem item) =>
        item.Quantity > 0m
            ? item.Quantity
            : item.Qty;

    private static string CleanOptional(
        string? value,
        int max)
    {
        var cleaned = value?.Trim() ?? string.Empty;

        if (cleaned.Length > max ||
            cleaned.Any(char.IsControl))
        {
            throw BadRequest(
                "VALIDATION_ERROR",
                "Teks yang dimasukkan tidak valid.");
        }

        return cleaned;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        if (value == default)
        {
            return DateTime.MinValue;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string GenerateOrderNumber(DateTime now) =>
        $"LDR-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..32]
            .ToUpperInvariant();

    private static decimal Money(decimal value) =>
        decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);

    private static TenantApiException BadRequest(
        string code,
        string message) =>
        new(
            StatusCodes.Status400BadRequest,
            code,
            message);

    private static TenantApiException Conflict(
        string code,
        string message) =>
        new(
            StatusCodes.Status409Conflict,
            code,
            message);
}

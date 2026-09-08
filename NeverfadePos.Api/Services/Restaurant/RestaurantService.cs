using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Restaurant;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Restaurant;

public sealed class RestaurantService(
    AppDbContext db,
    CurrentUser currentUser)
    : IRestaurantService
{
    public async Task<IReadOnlyList<RestaurantTableDto>> GetTablesAsync(
        CancellationToken cancellationToken = default)
    {
        RequireUser();

        var tables = await db.RestaurantTables
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var openOrders = await db.RestaurantOrders
            .AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.Status == RestaurantConstants.OrderOpen)
            .ToListAsync(cancellationToken);

        var orderByTable = openOrders
            .ToDictionary(x => x.TableId);

        return tables
            .Select(table => MapTable(
                table,
                orderByTable.GetValueOrDefault(table.Id)))
            .ToList();
    }

    public async Task<RestaurantTableDto> CreateTableAsync(
        UpsertRestaurantTableRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, _) = RequireAdmin();
        var input = NormalizeTable(request);

        if (await db.RestaurantTables.AnyAsync(
            x => x.Code == input.Code,
            cancellationToken))
        {
            throw Conflict(
                "RESTAURANT_TABLE_CODE_EXISTS",
                "Kode meja sudah digunakan.");
        }

        var table = new RestaurantTable
        {
            TenantId = tenantId,
            Code = input.Code,
            Name = input.Name,
            Capacity = input.Capacity,
            Active = input.Active,
            SortOrder = input.SortOrder,
            UpdatedAt = DateTime.UtcNow
        };

        db.RestaurantTables.Add(table);
        await db.SaveChangesAsync(cancellationToken);

        return MapTable(table, null);
    }

    public async Task<RestaurantTableDto> UpdateTableAsync(
        Guid tableId,
        UpsertRestaurantTableRequestDto request,
        CancellationToken cancellationToken = default)
    {
        RequireAdmin();
        var input = NormalizeTable(request);

        var table = await db.RestaurantTables
            .SingleOrDefaultAsync(x => x.Id == tableId, cancellationToken)
            ?? throw new KeyNotFoundException("Meja tidak ditemukan.");

        if (!input.Active)
        {
            var hasOpenOrder = await db.RestaurantOrders.AnyAsync(
                x =>
                    x.TableId == tableId &&
                    x.Status == RestaurantConstants.OrderOpen,
                cancellationToken);

            if (hasOpenOrder)
            {
                throw Conflict(
                    "RESTAURANT_TABLE_OCCUPIED",
                    "Meja dengan pesanan aktif tidak dapat dinonaktifkan.");
            }
        }

        if (await db.RestaurantTables.AnyAsync(
            x => x.Id != tableId && x.Code == input.Code,
            cancellationToken))
        {
            throw Conflict(
                "RESTAURANT_TABLE_CODE_EXISTS",
                "Kode meja sudah digunakan.");
        }

        table.Code = input.Code;
        table.Name = input.Name;
        table.Capacity = input.Capacity;
        table.Active = input.Active;
        table.SortOrder = input.SortOrder;
        table.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return MapTable(table, null);
    }

    public async Task<IReadOnlyList<RestaurantOrderDto>> GetOpenOrdersAsync(
        CancellationToken cancellationToken = default)
    {
        RequireUser();

        var orders = await db.RestaurantOrders
            .AsNoTracking()
            .Include(x => x.Table)
            .Include(x => x.Items)
            .Where(x => x.Status == RestaurantConstants.OrderOpen)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(MapOrder).ToList();
    }

    public async Task<RestaurantOrderDto> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        RequireUser();

        var order = await LoadOrderAsync(
            orderId,
            tracking: false,
            cancellationToken);

        return MapOrder(order);
    }

    public async Task<RestaurantOrderDto> OpenOrderAsync(
        OpenRestaurantOrderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireUser();

        var table = await db.RestaurantTables
            .SingleOrDefaultAsync(
                x => x.Id == request.TableId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Meja tidak ditemukan.");

        if (!table.Active)
        {
            throw Conflict(
                "RESTAURANT_TABLE_INACTIVE",
                "Meja sedang tidak aktif.");
        }

        var existing = await db.RestaurantOrders
            .Include(x => x.Table)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(
                x =>
                    x.TableId == table.Id &&
                    x.Status == RestaurantConstants.OrderOpen,
                cancellationToken);

        if (existing is not null)
        {
            return MapOrder(existing);
        }

        var now = DateTime.UtcNow;
        var order = new RestaurantOrder
        {
            TenantId = tenantId,
            TableId = table.Id,
            OpenedByUserId = userId,
            OrderNumber = GenerateOrderNumber(now),
            Status = RestaurantConstants.OrderOpen,
            UpdatedAt = now,
            Table = table
        };

        db.RestaurantOrders.Add(order);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrent = await db.RestaurantOrders
                .AsNoTracking()
                .Include(x => x.Table)
                .Include(x => x.Items)
                .SingleOrDefaultAsync(
                    x =>
                        x.TableId == table.Id &&
                        x.Status == RestaurantConstants.OrderOpen,
                    cancellationToken);

            if (concurrent is not null)
            {
                return MapOrder(concurrent);
            }

            throw;
        }

        return MapOrder(order);
    }

    public async Task<RestaurantOrderDto> AddItemAsync(
        Guid orderId,
        AddRestaurantOrderItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, _) = RequireUser();

        var orderExists = await db.RestaurantOrders
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id == orderId &&
                    x.Status == RestaurantConstants.OrderOpen,
                cancellationToken);

        if (!orderExists)
        {
            throw new KeyNotFoundException(
                "Pesanan meja aktif tidak ditemukan.");
        }

        var product = await db.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == request.ProductId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Produk tidak ditemukan.");

        if (request.Qty <= 0)
        {
            throw new TenantApiException(
                StatusCodes.Status400BadRequest,
                "RESTAURANT_ITEM_QTY_INVALID",
                "Jumlah item harus lebih dari 0.");
        }

        if (request.Qty > product.Stok)
        {
            throw Conflict(
                "RESTAURANT_STOCK_INSUFFICIENT",
                $"Stok {product.Nama} tidak mencukupi.");
        }

        var note = CleanNote(request.Note);
        var draft = await db.RestaurantOrderItems
            .SingleOrDefaultAsync(
                x =>
                    x.RestaurantOrderId == orderId &&
                    x.ProductId == product.Id &&
                    x.KitchenStatus == RestaurantConstants.KitchenDraft &&
                    (x.Note ?? string.Empty) == note,
                cancellationToken);

        if (draft is not null)
        {
            var nextQty = draft.Qty + request.Qty;

            if (nextQty > product.Stok)
            {
                throw Conflict(
                    "RESTAURANT_STOCK_INSUFFICIENT",
                    $"Stok {product.Nama} tidak mencukupi.");
            }

            draft.Qty = nextQty;
            draft.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.RestaurantOrderItems.Add(new RestaurantOrderItem
            {
                TenantId = tenantId,
                RestaurantOrderId = orderId,
                ProductId = product.Id,
                Nama = product.Nama,
                HargaJual = Money(product.HargaJual),
                Qty = request.Qty,
                Note = note,
                KitchenStatus = RestaurantConstants.KitchenDraft,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return await GetOrderAsync(
            orderId,
            cancellationToken);
    }

    public async Task<RestaurantOrderDto> UpdateDraftItemAsync(
        Guid orderId,
        Guid itemId,
        UpdateRestaurantOrderItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        RequireUser();
        var order = await LoadOpenOrderAsync(orderId, cancellationToken);

        var item = order.Items.SingleOrDefault(x => x.Id == itemId)
            ?? throw new KeyNotFoundException("Item pesanan tidak ditemukan.");

        EnsureDraft(item);

        var product = await db.Products
            .SingleOrDefaultAsync(
                x => x.Id == item.ProductId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Produk tidak ditemukan.");

        if (request.Qty <= 0 || request.Qty > product.Stok)
        {
            throw Conflict(
                "RESTAURANT_STOCK_INSUFFICIENT",
                $"Stok {product.Nama} tidak mencukupi.");
        }

        item.Qty = request.Qty;
        item.Note = CleanNote(request.Note);
        item.UpdatedAt = DateTime.UtcNow;
        order.UpdatedAt = item.UpdatedAt;

        await db.SaveChangesAsync(cancellationToken);
        return MapOrder(order);
    }

    public async Task<RestaurantOrderDto> RemoveDraftItemAsync(
        Guid orderId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        RequireUser();
        var order = await LoadOpenOrderAsync(orderId, cancellationToken);

        var item = order.Items.SingleOrDefault(x => x.Id == itemId)
            ?? throw new KeyNotFoundException("Item pesanan tidak ditemukan.");

        EnsureDraft(item);

        db.RestaurantOrderItems.Remove(item);
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return MapOrder(order);
    }

    public async Task<RestaurantOrderDto> SendToKitchenAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        RequireUser();
        var order = await LoadOpenOrderAsync(orderId, cancellationToken);
        var draftItems = order.Items
            .Where(x => x.KitchenStatus == RestaurantConstants.KitchenDraft)
            .ToList();

        if (draftItems.Count == 0)
        {
            return MapOrder(order);
        }

        var now = DateTime.UtcNow;

        foreach (var item in draftItems)
        {
            item.KitchenStatus = RestaurantConstants.KitchenQueued;
            item.QueuedAt = now;
            item.UpdatedAt = now;
        }

        order.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return MapOrder(order);
    }

    public async Task<RestaurantOrderDto> CancelOrderAsync(
        Guid orderId,
        CancelRestaurantOrderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireAdmin();
        var order = await LoadOpenOrderAsync(orderId, cancellationToken);
        var reason = RequiredText(
            request.Reason,
            3,
            500,
            "Alasan pembatalan wajib diisi.");

        var now = DateTime.UtcNow;
        order.Status = RestaurantConstants.OrderCancelled;
        order.CancellationReason = reason;
        order.CancelledAt = now;
        order.UpdatedAt = now;

        foreach (var item in order.Items.Where(
            x => x.KitchenStatus != RestaurantConstants.KitchenServed))
        {
            item.KitchenStatus = RestaurantConstants.KitchenCancelled;
            item.UpdatedAt = now;
        }

        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = userId,
            EventType = "RESTAURANT_ORDER_CANCELLED",
            Metadata = JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                tableId = order.TableId,
                reason
            })
        });

        await db.SaveChangesAsync(cancellationToken);
        return MapOrder(order);
    }

    public async Task<RestaurantOrderDto> CloseOrderAsync(
        Guid orderId,
        CloseRestaurantOrderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var (tenantId, userId) = RequireUser();

        var order = await LoadOrderAsync(
            orderId,
            tracking: true,
            cancellationToken);

        if (order.Status == RestaurantConstants.OrderClosed)
        {
            if (order.TransactionId == request.TransactionId)
            {
                return MapOrder(order);
            }

            throw Conflict(
                "RESTAURANT_ORDER_ALREADY_CLOSED",
                "Pesanan sudah ditutup dengan transaksi lain.");
        }

        if (order.Status != RestaurantConstants.OrderOpen)
        {
            throw Conflict(
                "RESTAURANT_ORDER_NOT_OPEN",
                "Pesanan sudah tidak aktif.");
        }

        var transaction = await db.Transactions
            .AsNoTracking()
            .Include(x => x.Items)
            .SingleOrDefaultAsync(
                x => x.Id == request.TransactionId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Transaksi tidak ditemukan.");

        if (transaction.Status != TransactionStatuses.Paid ||
            !transaction.FinalizedAt.HasValue)
        {
            throw Conflict(
                "RESTAURANT_TRANSACTION_NOT_PAID",
                "Pesanan hanya dapat ditutup dengan transaksi yang sudah dibayar.");
        }

        var activeItems = order.Items
            .Where(x => x.KitchenStatus != RestaurantConstants.KitchenCancelled)
            .ToList();

        if (activeItems.Count == 0)
        {
            throw Conflict(
                "RESTAURANT_ORDER_EMPTY",
                "Pesanan tidak memiliki item yang dapat dibayar.");
        }

        var orderGroups = activeItems
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Qty = group.Sum(x => x.Qty),
                    Subtotal = Money(group.Sum(x => x.HargaJual * x.Qty))
                });

        var transactionGroups = transaction.Items
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Qty = group.Sum(x => x.Qty),
                    Subtotal = Money(group.Sum(x => x.Subtotal))
                });

        var itemsMatch =
            orderGroups.Count == transactionGroups.Count &&
            orderGroups.All(pair =>
                transactionGroups.TryGetValue(pair.Key, out var transactionItem) &&
                transactionItem.Qty == pair.Value.Qty &&
                transactionItem.Subtotal == pair.Value.Subtotal);

        if (!itemsMatch ||
            Money(transaction.Subtotal) !=
            Money(activeItems.Sum(x => x.HargaJual * x.Qty)))
        {
            throw Conflict(
                "RESTAURANT_TRANSACTION_MISMATCH",
                "Item transaksi tidak sesuai dengan pesanan meja.");
        }

        var transactionUsed = await db.RestaurantOrders
            .AnyAsync(
                x =>
                    x.Id != order.Id &&
                    x.TransactionId == transaction.Id,
                cancellationToken);

        if (transactionUsed)
        {
            throw Conflict(
                "RESTAURANT_TRANSACTION_ALREADY_LINKED",
                "Transaksi sudah terhubung ke pesanan lain.");
        }

        var now = DateTime.UtcNow;
        order.TransactionId = transaction.Id;
        order.Status = RestaurantConstants.OrderClosed;
        order.ClosedAt = now;
        order.UpdatedAt = now;

        db.TenantAuditEvents.Add(new TenantAuditEvent
        {
            TenantId = tenantId,
            ActorUserId = userId,
            EventType = "RESTAURANT_ORDER_CLOSED",
            Metadata = JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                transactionId = transaction.Id,
                transactionNo = transaction.NoTrx
            })
        });

        await db.SaveChangesAsync(cancellationToken);
        return MapOrder(order);
    }

    public async Task<IReadOnlyList<KitchenQueueOrderDto>> GetKitchenQueueAsync(
        CancellationToken cancellationToken = default)
    {
        RequireUser();

        var orders = await db.RestaurantOrders
            .AsNoTracking()
            .Include(x => x.Table)
            .Include(x => x.Items)
            .Where(x => x.Status == RestaurantConstants.OrderOpen)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var visible = new HashSet<string>(StringComparer.Ordinal)
        {
            RestaurantConstants.KitchenQueued,
            RestaurantConstants.KitchenPreparing,
            RestaurantConstants.KitchenReady
        };

        return orders
            .Select(order => new KitchenQueueOrderDto
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                TableId = order.TableId,
                TableCode = order.Table?.Code ?? string.Empty,
                TableName = order.Table?.Name ?? string.Empty,
                OpenedAt = order.CreatedAt,
                Items = order.Items
                    .Where(item => visible.Contains(item.KitchenStatus))
                    .OrderBy(item => item.QueuedAt ?? item.CreatedAt)
                    .Select(MapItem)
                    .ToList()
            })
            .Where(order => order.Items.Count > 0)
            .ToList();
    }

    public async Task<RestaurantOrderItemDto> UpdateKitchenStatusAsync(
        Guid itemId,
        UpdateKitchenStatusRequestDto request,
        CancellationToken cancellationToken = default)
    {
        RequireUser();

        var item = await db.RestaurantOrderItems
            .Include(x => x.RestaurantOrder)
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Item dapur tidak ditemukan.");

        if (item.RestaurantOrder?.Status != RestaurantConstants.OrderOpen)
        {
            throw Conflict(
                "RESTAURANT_ORDER_NOT_OPEN",
                "Pesanan sudah tidak aktif.");
        }

        var requested = (request.Status ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        if (requested == item.KitchenStatus)
        {
            return MapItem(item);
        }

        var allowedNext = item.KitchenStatus switch
        {
            RestaurantConstants.KitchenQueued =>
                RestaurantConstants.KitchenPreparing,
            RestaurantConstants.KitchenPreparing =>
                RestaurantConstants.KitchenReady,
            RestaurantConstants.KitchenReady =>
                RestaurantConstants.KitchenServed,
            _ => string.Empty
        };

        if (!string.Equals(requested, allowedNext, StringComparison.Ordinal))
        {
            throw Conflict(
                "RESTAURANT_KITCHEN_INVALID_TRANSITION",
                "Perubahan status dapur tidak valid.");
        }

        var now = DateTime.UtcNow;
        item.KitchenStatus = requested;
        item.UpdatedAt = now;

        if (requested == RestaurantConstants.KitchenPreparing)
        {
            item.PreparingAt = now;
        }
        else if (requested == RestaurantConstants.KitchenReady)
        {
            item.ReadyAt = now;
        }
        else if (requested == RestaurantConstants.KitchenServed)
        {
            item.ServedAt = now;
        }

        if (item.RestaurantOrder is not null)
        {
            item.RestaurantOrder.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return MapItem(item);
    }

    private async Task<RestaurantOrder> LoadOpenOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(
            orderId,
            tracking: true,
            cancellationToken);

        if (order.Status != RestaurantConstants.OrderOpen)
        {
            throw Conflict(
                "RESTAURANT_ORDER_NOT_OPEN",
                "Pesanan sudah tidak aktif.");
        }

        return order;
    }

    private async Task<RestaurantOrder> LoadOrderAsync(
        Guid orderId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<RestaurantOrder> query = db.RestaurantOrders;

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var order = await query
            .Include(x => x.Table)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        return order
            ?? throw new KeyNotFoundException("Pesanan meja tidak ditemukan.");
    }

    private (Guid TenantId, Guid UserId) RequireUser()
    {
        if (!currentUser.TenantId.HasValue ||
            !currentUser.UserId.HasValue ||
            currentUser.Role is not ("owner" or "admin" or "kasir"))
        {
            throw new UnauthorizedAccessException();
        }

        return (currentUser.TenantId.Value, currentUser.UserId.Value);
    }

    private (Guid TenantId, Guid UserId) RequireAdmin()
    {
        var identity = RequireUser();

        if (currentUser.Role is not ("owner" or "admin"))
        {
            throw new UnauthorizedAccessException();
        }

        return identity;
    }

    private static void EnsureDraft(RestaurantOrderItem item)
    {
        if (item.KitchenStatus != RestaurantConstants.KitchenDraft)
        {
            throw Conflict(
                "RESTAURANT_ITEM_ALREADY_SENT",
                "Item yang sudah dikirim ke dapur tidak dapat diubah.");
        }
    }

    private static RestaurantTableDto MapTable(
        RestaurantTable table,
        RestaurantOrder? openOrder)
    {
        var activeItems = openOrder?.Items
            .Where(x => x.KitchenStatus != RestaurantConstants.KitchenCancelled)
            .ToList() ?? [];

        return new RestaurantTableDto
        {
            Id = table.Id,
            Code = table.Code,
            Name = table.Name,
            Capacity = table.Capacity,
            Active = table.Active,
            SortOrder = table.SortOrder,
            Status = openOrder is null ? "available" : "occupied",
            OpenOrderId = openOrder?.Id,
            OpenOrderNumber = openOrder?.OrderNumber,
            OpenOrderSubtotal = Money(
                activeItems.Sum(x => x.HargaJual * x.Qty)),
            KitchenPendingItems = activeItems.Count(x =>
                x.KitchenStatus is
                    RestaurantConstants.KitchenQueued or
                    RestaurantConstants.KitchenPreparing or
                    RestaurantConstants.KitchenReady)
        };
    }

    private static RestaurantOrderDto MapOrder(RestaurantOrder order)
    {
        var items = order.Items
            .OrderBy(x => x.CreatedAt)
            .Select(MapItem)
            .ToList();

        return new RestaurantOrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            TableId = order.TableId,
            TableCode = order.Table?.Code ?? string.Empty,
            TableName = order.Table?.Name ?? string.Empty,
            Status = order.Status,
            TransactionId = order.TransactionId,
            CancellationReason = order.CancellationReason,
            OpenedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            ClosedAt = order.ClosedAt,
            CancelledAt = order.CancelledAt,
            Subtotal = Money(items
                .Where(x => x.KitchenStatus != RestaurantConstants.KitchenCancelled)
                .Sum(x => x.Subtotal)),
            Items = items
        };
    }

    private static RestaurantOrderItemDto MapItem(RestaurantOrderItem item) =>
        new()
        {
            Id = item.Id,
            ProductId = item.ProductId,
            Nama = item.Nama,
            HargaJual = Money(item.HargaJual),
            Qty = item.Qty,
            Subtotal = Money(item.HargaJual * item.Qty),
            Note = item.Note ?? string.Empty,
            KitchenStatus = item.KitchenStatus,
            CreatedAt = item.CreatedAt,
            QueuedAt = item.QueuedAt,
            PreparingAt = item.PreparingAt,
            ReadyAt = item.ReadyAt,
            ServedAt = item.ServedAt
        };

    private static TableInput NormalizeTable(
        UpsertRestaurantTableRequestDto request)
    {
        var code = RequiredText(
            request.Code,
            1,
            30,
            "Kode meja wajib diisi.")
            .ToUpperInvariant();

        var name = RequiredText(
            request.Name,
            1,
            100,
            "Nama meja wajib diisi.");

        if (request.Capacity is < 1 or > 100)
        {
            throw new TenantApiException(
                StatusCodes.Status400BadRequest,
                "RESTAURANT_TABLE_CAPACITY_INVALID",
                "Kapasitas meja harus antara 1 sampai 100.");
        }

        return new TableInput(
            code,
            name,
            request.Capacity,
            request.Active,
            Math.Max(0, request.SortOrder));
    }

    private static string CleanNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return RequiredText(
            value,
            1,
            500,
            "Catatan item tidak valid.");
    }

    private static string RequiredText(
        string? value,
        int min,
        int max,
        string message)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (normalized.Length < min ||
            normalized.Length > max ||
            normalized.Any(char.IsControl))
        {
            throw new TenantApiException(
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                message);
        }

        return normalized;
    }

    private static string GenerateOrderNumber(DateTime now) =>
        $"FNB-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..32]
            .ToUpperInvariant();

    private static decimal Money(decimal value) =>
        decimal.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);

    private static TenantApiException Conflict(
        string code,
        string message) =>
        new(
            StatusCodes.Status409Conflict,
            code,
            message);

    private sealed record TableInput(
        string Code,
        string Name,
        int Capacity,
        bool Active,
        int SortOrder);
}

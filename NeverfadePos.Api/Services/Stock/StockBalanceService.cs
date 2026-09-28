using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Outlet;
using ProductEntity = NeverfadePos.Api.Entities.Product;
using StockHistoryEntity = NeverfadePos.Api.Entities.StockHistory;

namespace NeverfadePos.Api.Services.Stock;

public interface IStockBalanceService
{
    Task<int> GetProductAvailableUnitsAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<int> GetVariantAvailableUnitsAsync(Guid productId, Guid variantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetProductAvailableUnitsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetVariantAvailableUnitsAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default);
    Task SetAsync(ProductEntity product, ProductVariant? variant, int desiredUnits,
        string reason, string user, CancellationToken cancellationToken = default);
    Task AdjustAsync(ProductEntity product, ProductVariant? variant, int delta,
        string movementType, string reason, string user,
        CancellationToken cancellationToken = default);
}

public sealed class StockBalanceService(
    AppDbContext db,
    CurrentUser currentUser,
    IOutletExecutionContext outletContext) : IStockBalanceService
{
    private Guid TenantId => currentUser.TenantId ?? throw new UnauthorizedAccessException();
    private Guid OutletId => outletContext.OutletId
        ?? throw new InvalidOperationException("Stock operation requires an active outlet scope.");

    public async Task<int> GetProductAvailableUnitsAsync(
        Guid productId, CancellationToken cancellationToken = default)
    {
        var values = await GetProductAvailableUnitsAsync([productId], cancellationToken);
        return values.TryGetValue(productId, out var value) ? value : 0;
    }

    public async Task<int> GetVariantAvailableUnitsAsync(
        Guid productId, Guid variantId, CancellationToken cancellationToken = default)
    {
        var values = await GetVariantAvailableUnitsAsync([variantId], cancellationToken);
        if (values.TryGetValue(variantId, out var value))
            return value;

        return await LegacyFallbackAsync(productId, variantId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetProductAvailableUnitsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0) return new Dictionary<Guid, int>();

        var balances = await db.StockBalances.AsNoTracking()
            .Where(x => x.OutletId == OutletId && productIds.Contains(x.ProductId))
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Available = x.Sum(y => y.Available) })
            .ToListAsync(cancellationToken);
        var result = balances.ToDictionary(x => x.ProductId, x => Units(x.Available));

        if (await IsDefaultOutletAsync(cancellationToken))
        {
            var missing = productIds.Where(x => !result.ContainsKey(x)).ToArray();
            if (missing.Length > 0)
            {
                var anyBalance = await db.StockBalances.AsNoTracking()
                    .Where(x => missing.Contains(x.ProductId))
                    .Select(x => x.ProductId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                var legacyIds = missing.Except(anyBalance).ToArray();
                if (legacyIds.Length > 0)
                {
                    var legacy = await db.Products.AsNoTracking()
                        .Where(x => legacyIds.Contains(x.Id))
                        .Select(x => new { x.Id, x.Stok })
                        .ToListAsync(cancellationToken);
                    foreach (var item in legacy) result[item.Id] = item.Stok;
                }
            }
        }

        foreach (var id in productIds) result.TryAdd(id, 0);
        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetVariantAvailableUnitsAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken = default)
    {
        if (variantIds.Count == 0) return new Dictionary<Guid, int>();

        var balances = await db.StockBalances.AsNoTracking()
            .Where(x => x.OutletId == OutletId &&
                x.ProductVariantId.HasValue && variantIds.Contains(x.ProductVariantId.Value))
            .GroupBy(x => x.ProductVariantId!.Value)
            .Select(x => new { VariantId = x.Key, Available = x.Sum(y => y.Available) })
            .ToListAsync(cancellationToken);
        var result = balances.ToDictionary(x => x.VariantId, x => Units(x.Available));

        if (await IsDefaultOutletAsync(cancellationToken))
        {
            var missing = variantIds.Where(x => !result.ContainsKey(x)).ToArray();
            if (missing.Length > 0)
            {
                var anyBalance = await db.StockBalances.AsNoTracking()
                    .Where(x => x.ProductVariantId.HasValue &&
                        missing.Contains(x.ProductVariantId.Value))
                    .Select(x => x.ProductVariantId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                var legacyIds = missing.Except(anyBalance).ToArray();
                if (legacyIds.Length > 0)
                {
                    var legacy = await db.ProductVariants.AsNoTracking()
                        .Where(x => legacyIds.Contains(x.Id))
                        .Select(x => new { x.Id, x.Stok })
                        .ToListAsync(cancellationToken);
                    foreach (var item in legacy) result[item.Id] = item.Stok;
                }
            }
        }

        foreach (var id in variantIds) result.TryAdd(id, 0);
        return result;
    }

    public async Task SetAsync(
        ProductEntity product,
        ProductVariant? variant,
        int desiredUnits,
        string reason,
        string user,
        CancellationToken cancellationToken = default)
    {
        if (desiredUnits < 0)
            throw Invalid("OUTLET_STOCK_INVALID", "Stok outlet tidak boleh negatif.");

        var balance = await GetOrCreateAsync(product, variant, cancellationToken);
        var current = Units(balance.OnHand);
        await AdjustCoreAsync(product, variant, balance, desiredUnits - current,
            "penyesuaian", reason, user, cancellationToken);
    }

    public async Task AdjustAsync(
        ProductEntity product,
        ProductVariant? variant,
        int delta,
        string movementType,
        string reason,
        string user,
        CancellationToken cancellationToken = default)
    {
        var balance = await GetOrCreateAsync(product, variant, cancellationToken);
        await AdjustCoreAsync(product, variant, balance, delta,
            movementType, reason, user, cancellationToken);
    }

    private Task AdjustCoreAsync(
        ProductEntity product,
        ProductVariant? variant,
        StockBalance balance,
        int delta,
        string movementType,
        string reason,
        string user,
        CancellationToken cancellationToken)
    {
        if (delta == 0) return Task.CompletedTask;

        var nextOnHand = balance.OnHand + delta;
        var nextAvailable = nextOnHand - balance.Reserved - balance.Quarantine;
        if (nextOnHand < 0 || nextAvailable < 0)
        {
            throw Invalid(
                "OUTLET_STOCK_INSUFFICIENT",
                $"Stok {product.Nama} di outlet aktif tidak mencukupi.");
        }

        if (product.Stok + delta < 0 ||
            variant is not null && variant.Stok + delta < 0)
        {
            throw new InvalidOperationException("Legacy aggregate stock projection would become negative.");
        }

        balance.OnHand = nextOnHand;
        balance.Available = nextAvailable;
        balance.AverageCost = product.HargaModal;
        balance.AsOf = DateTime.UtcNow;
        balance.Version++;

        product.Stok += delta;
        if (variant is not null) variant.Stok += delta;

        db.StockHistories.Add(new StockHistoryEntity
        {
            TenantId = TenantId,
            OutletId = OutletId,
            ProdukId = product.Id,
            ProdukNama = product.Nama,
            ProductVariantId = variant?.Id,
            VariantSku = variant?.Sku ?? string.Empty,
            VariantLabel = variant?.Label ?? string.Empty,
            Tipe = movementType,
            Jumlah = delta,
            StokAkhir = Units(nextAvailable),
            Keterangan = reason,
            User = user
        });

        return Task.CompletedTask;
    }

    private async Task<StockBalance> GetOrCreateAsync(
        ProductEntity product,
        ProductVariant? variant,
        CancellationToken cancellationToken)
    {
        var variantId = variant?.Id;
        var balance = await db.StockBalances.FirstOrDefaultAsync(
            x => x.OutletId == OutletId &&
                 x.ProductId == product.Id &&
                 x.ProductVariantId == variantId &&
                 x.LotId == null,
            cancellationToken);
        if (balance is not null) return balance;

        decimal seed = 0;
        if (await IsDefaultOutletAsync(cancellationToken))
        {
            var hasAnyBalance = await db.StockBalances.AsNoTracking().AnyAsync(
                x => x.ProductId == product.Id &&
                    x.ProductVariantId == variantId &&
                    x.LotId == null,
                cancellationToken);
            if (!hasAnyBalance)
                seed = variant?.Stok ?? product.Stok;
        }

        balance = new StockBalance
        {
            TenantId = TenantId,
            OutletId = OutletId,
            ProductId = product.Id,
            ProductVariantId = variantId,
            OnHand = seed,
            Available = seed,
            Reserved = 0,
            Quarantine = 0,
            AverageCost = product.HargaModal,
            AsOf = DateTime.UtcNow,
            Version = 1
        };
        db.StockBalances.Add(balance);
        return balance;
    }

    private async Task<int> LegacyFallbackAsync(
        Guid productId, Guid variantId, CancellationToken cancellationToken)
    {
        if (!await IsDefaultOutletAsync(cancellationToken)) return 0;
        if (await db.StockBalances.AsNoTracking().AnyAsync(
            x => x.ProductVariantId == variantId, cancellationToken)) return 0;

        return await db.ProductVariants.AsNoTracking()
            .Where(x => x.Id == variantId && x.ProductId == productId)
            .Select(x => x.Stok)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<bool> IsDefaultOutletAsync(CancellationToken cancellationToken) =>
        db.Outlets.AsNoTracking().AnyAsync(
            x => x.Id == OutletId && x.Active && x.IsDefault,
            cancellationToken);

    private static int Units(decimal value)
    {
        if (value != decimal.Truncate(value))
            throw new InvalidOperationException("Current tracked-goods API requires whole stock units.");
        return checked((int)value);
    }

    private static TenantApiException Invalid(string code, string message) =>
        new(StatusCodes.Status409Conflict, code, message);
}

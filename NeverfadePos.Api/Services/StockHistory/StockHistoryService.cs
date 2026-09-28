using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.StockHistory;
using NeverfadePos.Api.Services.Outlet;
using NeverfadePos.Api.Services.Stock;

namespace NeverfadePos.Api.Services.StockHistory;

public sealed class StockHistoryService(
    AppDbContext db,
    CurrentUser currentUser,
    IOutletExecutionContext outletContext,
    IStockBalanceService stockBalances)
    : IStockHistoryService
{
    public async Task<List<StockHistoryDto>> GetAllAsync(
        Guid? produkId,
        CancellationToken cancellationToken = default)
    {
        var outletId = outletContext.OutletId
            ?? throw new InvalidOperationException("Stock history requires an outlet scope.");
        var isDefault = await db.Outlets.AsNoTracking()
            .AnyAsync(x => x.Id == outletId && x.Active && x.IsDefault, cancellationToken);
        var query = db.StockHistories.AsNoTracking()
            .Where(x => x.OutletId == outletId || (isDefault && x.OutletId == null));

        if (produkId.HasValue)
            query = query.Where(x => x.ProdukId == produkId.Value);

        return await query.OrderByDescending(x => x.CreatedAt)
            .Select(x => new StockHistoryDto
            {
                Id = x.Id,
                OutletId = x.OutletId,
                ProdukId = x.ProdukId,
                ProdukNama = x.ProdukNama,
                Tipe = x.Tipe,
                Jumlah = x.Jumlah,
                StokAkhir = x.StokAkhir,
                Keterangan = x.Keterangan,
                User = x.User,
                Tanggal = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<StockHistoryDto> CreateAsync(
        CreateStockHistoryDto request,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(
            x => x.Id == request.ProdukId, cancellationToken)
            ?? throw new KeyNotFoundException("Product tidak ditemukan.");

        if (await db.ProductVariants.AnyAsync(
            x => x.ProductId == product.Id && x.Active, cancellationToken))
            throw new InvalidOperationException(
                "Stok produk bervarian harus disesuaikan melalui varian.");

        var oldStock = await stockBalances.GetProductAvailableUnitsAsync(
            product.Id, cancellationToken);
        var stockType = request.Tipe.Trim().ToLowerInvariant();
        var delta = request.Jumlah;
        switch (stockType)
        {
            case "masuk":
                break;
            case "keluar":
                delta = -request.Jumlah;
                break;
            case "penyesuaian":
                if (!request.StokFinal.HasValue)
                    throw new InvalidOperationException("stokFinal wajib diisi.");
                delta = request.StokFinal.Value - oldStock;
                break;
            default:
                throw new InvalidOperationException("Tipe stock history tidak valid.");
        }

        await stockBalances.AdjustAsync(
            product, null, delta, stockType, request.Keterangan,
            currentUser.Username ?? string.Empty, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await db.StockHistories.AsNoTracking()
            .Where(x => x.ProdukId == product.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new StockHistoryDto
            {
                Id = x.Id,
                OutletId = x.OutletId,
                ProdukId = x.ProdukId,
                ProdukNama = x.ProdukNama,
                Tipe = x.Tipe,
                Jumlah = x.Jumlah,
                StokAkhir = x.StokAkhir,
                Keterangan = x.Keterangan,
                User = x.User,
                Tanggal = x.CreatedAt
            })
            .FirstAsync(cancellationToken);
    }
}

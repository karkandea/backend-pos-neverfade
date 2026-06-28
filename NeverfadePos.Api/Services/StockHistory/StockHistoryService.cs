using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.StockHistory;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.StockHistory;

public sealed class StockHistoryService(
    AppDbContext db,
    CurrentUser currentUser)
    : IStockHistoryService
{
    public async Task<List<StockHistoryDto>> GetAllAsync(
        Guid? produkId,
        CancellationToken cancellationToken = default)
    {
        var query = db.StockHistories.AsNoTracking();

        if (produkId.HasValue)
        {
            query = query.Where(x => x.ProdukId == produkId.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<StockHistoryDto> CreateAsync(
        CreateStockHistoryDto request,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .FirstOrDefaultAsync(
                x => x.Id == request.ProdukId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Product tidak ditemukan.");

        var stokLama = product.Stok;
        var jumlah = request.Jumlah;
        var stokAkhir = stokLama;

        switch (request.Tipe.ToLowerInvariant())
        {
            case "masuk":
                stokAkhir = stokLama + jumlah;
                break;

            case "keluar":
                stokAkhir = stokLama - jumlah;

                if (stokAkhir < 0)
                    throw new InvalidOperationException("Stok tidak boleh negatif.");

                break;

            case "penyesuaian":
                if (!request.StokFinal.HasValue)
                    throw new InvalidOperationException("stokFinal wajib diisi.");

                stokAkhir = request.StokFinal.Value;
                jumlah = stokAkhir - stokLama;
                break;

            default:
                throw new InvalidOperationException("Tipe stock history tidak valid.");
        }

        product.Stok = stokAkhir;

        var entity = new NeverfadePos.Api.Entities.StockHistory
        {
            TenantId = currentUser.TenantId
                ?? throw new UnauthorizedAccessException(),

            ProdukId = product.Id,
            ProdukNama = product.Nama,
            Tipe = request.Tipe,
            Jumlah = jumlah,
            StokAkhir = stokAkhir,
            Keterangan = request.Keterangan,
            User = currentUser.Username ?? string.Empty
        };

        db.StockHistories.Add(entity);

        await db.SaveChangesAsync(cancellationToken);

        return await db.StockHistories
            .AsNoTracking()
            .Where(x => x.Id == entity.Id)
            .Select(MapToDto())
            .FirstAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<
        Func<NeverfadePos.Api.Entities.StockHistory, StockHistoryDto>>
        MapToDto()
    {
        return x => new StockHistoryDto
        {
            Id = x.Id,
            ProdukId = x.ProdukId,
            ProdukNama = x.ProdukNama,
            Tipe = x.Tipe,
            Jumlah = x.Jumlah,
            StokAkhir = x.StokAkhir,
            Keterangan = x.Keterangan,
            User = x.User,
            Tanggal = x.CreatedAt
        };
    }
}

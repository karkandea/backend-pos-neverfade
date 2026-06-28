using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Product;
using NeverfadePos.Api.Entities;
using ProductEntity = NeverfadePos.Api.Entities.Product;

namespace NeverfadePos.Api.Services.Product;

public sealed class ProductService(
    AppDbContext db,
    CurrentUser currentUser)
    : IProductService
{
    public async Task<List<ProductDto>> GetAllAsync(
        string? search,
        string? kategori,
        CancellationToken cancellationToken = default)
    {
        var query = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Nama.Contains(search) ||
                x.Kode.Contains(search) ||
                x.Barcode.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(kategori))
        {
            query = query.Where(x => x.Kategori == kategori);
        }

        return await query
            .OrderBy(x => x.Nama)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync(cancellationToken);

        return product ?? throw new KeyNotFoundException("Product tidak ditemukan.");
    }

    public async Task<ProductDto> CreateAsync(
        CreateProductDto request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue)
            throw new UnauthorizedAccessException();

        if (await db.Products.AnyAsync(
                x => x.Kode == request.Kode,
                cancellationToken))
            throw new InvalidOperationException("Kode produk sudah digunakan.");

        var entity = new ProductEntity
        {
            TenantId = currentUser.TenantId.Value,
            Kode = request.Kode,
            Barcode = request.Barcode,
            Nama = request.Nama,
            Kategori = request.Kategori,
            HargaModal = request.HargaModal,
            HargaJual = request.HargaJual,
            Stok = request.Stok,
            Supplier = request.Supplier,
            Satuan = request.Satuan,
            Deskripsi = request.Deskripsi
        };

        db.Products.Add(entity);

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ProductDto> UpdateAsync(
        Guid id,
        UpdateProductDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Products
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Product tidak ditemukan.");

        if (await db.Products.AnyAsync(
                x => x.Id != id && x.Kode == request.Kode,
                cancellationToken))
            throw new InvalidOperationException("Kode produk sudah digunakan.");

        entity.Kode = request.Kode;
        entity.Barcode = request.Barcode;
        entity.Nama = request.Nama;
        entity.Kategori = request.Kategori;
        entity.HargaModal = request.HargaModal;
        entity.HargaJual = request.HargaJual;
        entity.Stok = request.Stok;
        entity.Supplier = request.Supplier;
        entity.Satuan = request.Satuan;
        entity.Deskripsi = request.Deskripsi;

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Products
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Product tidak ditemukan.");

        db.Products.Remove(entity);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<ProductEntity, ProductDto>> MapToDto()
    {
        return x => new ProductDto
        {
            Id = x.Id,
            Kode = x.Kode,
            Barcode = x.Barcode,
            Nama = x.Nama,
            Kategori = x.Kategori,
            HargaModal = x.HargaModal,
            HargaJual = x.HargaJual,
            Stok = x.Stok,
            Supplier = x.Supplier,
            Satuan = x.Satuan,
            Deskripsi = x.Deskripsi,
            CreatedAt = x.CreatedAt
        };
    }
}

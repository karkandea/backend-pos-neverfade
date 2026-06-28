using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Customer;
using CustomerEntity = NeverfadePos.Api.Entities.Customer;

namespace NeverfadePos.Api.Services.Customer;

public sealed class CustomerService(
    AppDbContext db,
    CurrentUser currentUser)
    : ICustomerService
{
    public async Task<List<CustomerDto>> GetAllAsync(
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Nama.Contains(search) ||
                x.Hp.Contains(search));
        }

        return await query
            .OrderBy(x => x.Nama)
            .Select(MapToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(MapToDto())
            .FirstOrDefaultAsync(cancellationToken);

        return customer
            ?? throw new KeyNotFoundException("Customer tidak ditemukan.");
    }

    public async Task<CustomerDto> CreateAsync(
        CreateCustomerDto request,
        CancellationToken cancellationToken = default)
    {
        if (!currentUser.TenantId.HasValue)
            throw new UnauthorizedAccessException();

        var entity = new CustomerEntity
        {
            TenantId = currentUser.TenantId.Value,
            Nama = request.Nama,
            Hp = request.Hp,
            Email = request.Email,
            Alamat = request.Alamat,
            Poin = 0,
            TotalTransaksi = 0
        };

        db.Customers.Add(entity);

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<CustomerDto> UpdateAsync(
        Guid id,
        UpdateCustomerDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Customers
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Customer tidak ditemukan.");

        entity.Nama = request.Nama;
        entity.Hp = request.Hp;
        entity.Email = request.Email;
        entity.Alamat = request.Alamat;

        await db.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Customers
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Customer tidak ditemukan.");

        db.Customers.Remove(entity);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<CustomerEntity, CustomerDto>> MapToDto()
    {
        return x => new CustomerDto
        {
            Id = x.Id,
            Nama = x.Nama,
            Hp = x.Hp,
            Email = x.Email,
            Alamat = x.Alamat,
            Poin = x.Poin,
            TotalTransaksi = x.TotalTransaksi,
            CreatedAt = x.CreatedAt
        };
    }
}

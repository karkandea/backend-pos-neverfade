using NeverfadePos.Api.DTOs.Product;

namespace NeverfadePos.Api.Services.Product;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync(
        string? search,
        string? kategori,
        CancellationToken cancellationToken = default);

    Task<ProductDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ProductDto> CreateAsync(
        CreateProductDto request,
        CancellationToken cancellationToken = default);

    Task<ProductDto> UpdateAsync(
        Guid id,
        UpdateProductDto request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

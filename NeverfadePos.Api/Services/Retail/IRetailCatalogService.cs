using NeverfadePos.Api.DTOs.Retail;

namespace NeverfadePos.Api.Services.Retail;

public interface IRetailCatalogService
{
    Task<RetailCatalogDto> GetCatalogAsync(string? search, string? kategori, CancellationToken cancellationToken = default);
    Task<List<ProductVariantDto>> GetVariantsAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductVariantDto> CreateVariantAsync(CreateProductVariantDto request, CancellationToken cancellationToken = default);
    Task<ProductVariantDto> UpdateVariantAsync(Guid id, UpdateProductVariantDto request, CancellationToken cancellationToken = default);
    Task DeleteVariantAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<PriceLevelDto>> GetPriceLevelsAsync(CancellationToken cancellationToken = default);
    Task<PriceLevelDto> CreatePriceLevelAsync(CreatePriceLevelDto request, CancellationToken cancellationToken = default);
    Task<PriceLevelDto> UpdatePriceLevelAsync(Guid id, UpdatePriceLevelDto request, CancellationToken cancellationToken = default);
    Task DeletePriceLevelAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<ProductPriceDto>> GetPricesAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductPriceDto> CreatePriceAsync(CreateProductPriceDto request, CancellationToken cancellationToken = default);
    Task<ProductPriceDto> UpdatePriceAsync(Guid id, UpdateProductPriceDto request, CancellationToken cancellationToken = default);
    Task DeletePriceAsync(Guid id, CancellationToken cancellationToken = default);
}

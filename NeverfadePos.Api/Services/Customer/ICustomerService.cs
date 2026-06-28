using NeverfadePos.Api.DTOs.Customer;

namespace NeverfadePos.Api.Services.Customer;

public interface ICustomerService
{
    Task<List<CustomerDto>> GetAllAsync(
        string? search,
        CancellationToken cancellationToken = default);

    Task<CustomerDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CustomerDto> CreateAsync(
        CreateCustomerDto request,
        CancellationToken cancellationToken = default);

    Task<CustomerDto> UpdateAsync(
        Guid id,
        UpdateCustomerDto request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

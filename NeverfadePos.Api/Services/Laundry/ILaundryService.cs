using NeverfadePos.Api.DTOs.Laundry;

namespace NeverfadePos.Api.Services.Laundry;

public interface ILaundryService
{
    Task<IReadOnlyList<LaundryWorkOrderDto>> GetAllAsync(
        string? status,
        CancellationToken cancellationToken = default);

    Task<LaundryWorkOrderDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<LaundryWorkOrderDto> CreateAsync(
        CreateLaundryWorkOrderRequestDto request,
        CancellationToken cancellationToken = default);

    Task<LaundryWorkOrderDto> UpdateStatusAsync(
        Guid id,
        UpdateLaundryWorkOrderStatusRequestDto request,
        CancellationToken cancellationToken = default);

    Task<LaundryWorkOrderDto> CompletePaymentAsync(
        Guid id,
        CompleteLaundryPaymentRequestDto request,
        CancellationToken cancellationToken = default);
}

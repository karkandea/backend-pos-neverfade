using NeverfadePos.Api.DTOs.Restaurant;

namespace NeverfadePos.Api.Services.Restaurant;

public interface IRestaurantService
{
    Task<IReadOnlyList<RestaurantTableDto>> GetTablesAsync(
        CancellationToken cancellationToken = default);

    Task<RestaurantTableDto> CreateTableAsync(
        UpsertRestaurantTableRequestDto request,
        CancellationToken cancellationToken = default);

    Task<RestaurantTableDto> UpdateTableAsync(
        Guid tableId,
        UpsertRestaurantTableRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RestaurantOrderDto>> GetOpenOrdersAsync(
        CancellationToken cancellationToken = default);

    Task<RestaurantOrderDto> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<RestaurantOrderDto> OpenOrderAsync(
        OpenRestaurantOrderRequestDto request,
        CancellationToken cancellationToken = default);

    Task<RestaurantOrderDto> AddItemAsync(
        Guid orderId,
        AddRestaurantOrderItemRequestDto request,
        CancellationToken cancellationToken = default);

    Task<RestaurantOrderDto> UpdateDraftItemAsync(
        Guid orderId,
        Guid itemId,
        UpdateRestaurantOrderItemRequestDto request,
        CancellationToken cancellationToken = default);

    Task<RestaurantOrderDto> RemoveDraftItemAsync(
        Guid orderId,
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<RestaurantOrderDto> SendToKitchenAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<RestaurantOrderDto> CancelOrderAsync(
        Guid orderId,
        CancelRestaurantOrderRequestDto request,
        CancellationToken cancellationToken = default);

    Task<RestaurantOrderDto> CloseOrderAsync(
        Guid orderId,
        CloseRestaurantOrderRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KitchenQueueOrderDto>> GetKitchenQueueAsync(
        CancellationToken cancellationToken = default);

    Task<RestaurantOrderItemDto> UpdateKitchenStatusAsync(
        Guid itemId,
        UpdateKitchenStatusRequestDto request,
        CancellationToken cancellationToken = default);
}

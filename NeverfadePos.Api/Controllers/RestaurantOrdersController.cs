using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Restaurant;
using NeverfadePos.Api.Services.Restaurant;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/restaurant/orders")]
[RequireCapability(TenantCapabilities.TableOrders)]
public sealed class RestaurantOrdersController(
    IRestaurantService restaurantService)
    : ControllerBase
{
    [HttpGet("open")]
    public async Task<ActionResult<IReadOnlyList<RestaurantOrderDto>>> GetOpen(
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.GetOpenOrdersAsync(cancellationToken));

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<RestaurantOrderDto>> GetById(
        Guid orderId,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.GetOrderAsync(orderId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RestaurantOrderDto>> Open(
        OpenRestaurantOrderRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.OpenOrderAsync(
            request,
            cancellationToken));

    [HttpPost("{orderId:guid}/items")]
    public async Task<ActionResult<RestaurantOrderDto>> AddItem(
        Guid orderId,
        AddRestaurantOrderItemRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.AddItemAsync(
            orderId,
            request,
            cancellationToken));

    [HttpPut("{orderId:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<RestaurantOrderDto>> UpdateItem(
        Guid orderId,
        Guid itemId,
        UpdateRestaurantOrderItemRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.UpdateDraftItemAsync(
            orderId,
            itemId,
            request,
            cancellationToken));

    [HttpDelete("{orderId:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<RestaurantOrderDto>> RemoveItem(
        Guid orderId,
        Guid itemId,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.RemoveDraftItemAsync(
            orderId,
            itemId,
            cancellationToken));

    [HttpPost("{orderId:guid}/send-to-kitchen")]
    public async Task<ActionResult<RestaurantOrderDto>> SendToKitchen(
        Guid orderId,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.SendToKitchenAsync(
            orderId,
            cancellationToken));

    [HttpPost("{orderId:guid}/cancel")]
    [Authorize(Roles = "owner,admin")]
    public async Task<ActionResult<RestaurantOrderDto>> Cancel(
        Guid orderId,
        CancelRestaurantOrderRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.CancelOrderAsync(
            orderId,
            request,
            cancellationToken));

    [HttpPost("{orderId:guid}/close")]
    public async Task<ActionResult<RestaurantOrderDto>> Close(
        Guid orderId,
        CloseRestaurantOrderRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.CloseOrderAsync(
            orderId,
            request,
            cancellationToken));
}

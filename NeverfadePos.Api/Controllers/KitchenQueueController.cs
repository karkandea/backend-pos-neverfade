using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Restaurant;
using NeverfadePos.Api.Services.Restaurant;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/restaurant/kitchen")]
[RequireCapability(TenantCapabilities.KitchenQueue)]
public sealed class KitchenQueueController(
    IRestaurantService restaurantService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KitchenQueueOrderDto>>> GetQueue(
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.GetKitchenQueueAsync(cancellationToken));

    [HttpPost("items/{itemId:guid}/status")]
    public async Task<ActionResult<RestaurantOrderItemDto>> UpdateStatus(
        Guid itemId,
        UpdateKitchenStatusRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.UpdateKitchenStatusAsync(
            itemId,
            request,
            cancellationToken));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Restaurant;
using NeverfadePos.Api.Services.Restaurant;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "dapur")]
[RequireOutletScope]
[RequireCapability(TenantCapabilities.KitchenQueue)]
[Route("api/restaurant/kitchen/operator")]
public sealed class KitchenOperatorController(IRestaurantService restaurantService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KitchenOperatorOrderDto>>> GetQueue(
        CancellationToken cancellationToken)
    {
        var queue = await restaurantService.GetKitchenQueueAsync(cancellationToken);
        return Ok(queue.Select(KitchenOperatorOrderDto.From).ToList());
    }

    [HttpPost("items/{itemId:guid}/status")]
    public async Task<ActionResult<KitchenOperatorItemDto>> UpdateStatus(
        Guid itemId, UpdateKitchenStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var item = await restaurantService.UpdateKitchenStatusAsync(itemId, request, cancellationToken);
        return Ok(KitchenOperatorItemDto.From(item));
    }
}

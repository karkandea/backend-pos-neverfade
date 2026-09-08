using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Restaurant;
using NeverfadePos.Api.Services.Restaurant;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/restaurant/tables")]
[RequireCapability(TenantCapabilities.TableOrders)]
public sealed class RestaurantTablesController(
    IRestaurantService restaurantService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RestaurantTableDto>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.GetTablesAsync(cancellationToken));

    [HttpPost]
    [Authorize(Roles = "owner,admin")]
    public async Task<ActionResult<RestaurantTableDto>> Create(
        UpsertRestaurantTableRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.CreateTableAsync(
            request,
            cancellationToken));

    [HttpPut("{tableId:guid}")]
    [Authorize(Roles = "owner,admin")]
    public async Task<ActionResult<RestaurantTableDto>> Update(
        Guid tableId,
        UpsertRestaurantTableRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await restaurantService.UpdateTableAsync(
            tableId,
            request,
            cancellationToken));
}

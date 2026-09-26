using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Laundry;
using NeverfadePos.Api.Services.Laundry;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "laundry_operator")]
[RequireOutletScope]
[RequireCapability(TenantCapabilities.WorkOrders)]
[Route("api/laundry/operator")]
public sealed class LaundryOperatorController(ILaundryService laundryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LaundryOperatorOrderDto>>> GetQueue(
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var orders = await laundryService.GetAllAsync(status, cancellationToken);
        return Ok(orders.Select(LaundryOperatorOrderDto.From).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LaundryOperatorOrderDto>> GetById(
        Guid id, CancellationToken cancellationToken) =>
        Ok(LaundryOperatorOrderDto.From(await laundryService.GetByIdAsync(id, cancellationToken)));

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<LaundryOperatorOrderDto>> UpdateStatus(
        Guid id, UpdateLaundryWorkOrderStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var order = await laundryService.UpdateStatusAsync(id, request, cancellationToken);
        return Ok(LaundryOperatorOrderDto.From(order));
    }
}

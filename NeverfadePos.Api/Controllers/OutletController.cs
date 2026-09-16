using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Outlet;
using NeverfadePos.Api.Services.Outlet;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/outlets")]
public sealed class OutletController(
    IOutletService outletService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<OutletDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await outletService.GetAllAsync(
            cancellationToken));
    }

    [Authorize(Roles = "owner,admin")]
    [HttpPost]
    public async Task<ActionResult<OutletDto>> Create(
        CreateOutletDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await outletService.CreateAsync(
            request,
            cancellationToken));
    }

    [Authorize(Roles = "owner,admin")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OutletDto>> Update(
        Guid id,
        UpdateOutletDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await outletService.UpdateAsync(
            id,
            request,
            cancellationToken));
    }
}

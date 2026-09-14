using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Retail;
using NeverfadePos.Api.Services.Retail;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "owner,admin")]
[RequireCapability(TenantCapabilities.ReturnsExchanges)]
[Route("api/retail/returns")]
public sealed class RetailReturnsController(IRetailReturnService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RetailReturnDto>>> GetByTransaction(
        [FromQuery] Guid transactionId,
        CancellationToken cancellationToken)
    {
        if (transactionId == Guid.Empty)
            return BadRequest(new { message = "transactionId wajib diisi." });

        return Ok(await service.GetByTransactionAsync(transactionId, cancellationToken));
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RetailReturnDto>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<RetailReturnDto>> Create(
        CreateRetailReturnDto request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return Ok(result);
    }
}

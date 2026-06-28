using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.StockHistory;
using NeverfadePos.Api.Services.StockHistory;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/stock-history")]
public sealed class StockHistoryController(
    IStockHistoryService stockHistoryService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<StockHistoryDto>>> GetAll(
        [FromQuery] Guid? produkId,
        CancellationToken cancellationToken)
    {
        return Ok(await stockHistoryService.GetAllAsync(
            produkId,
            cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<StockHistoryDto>> Create(
        CreateStockHistoryDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await stockHistoryService.CreateAsync(
            request,
            cancellationToken));
    }
}

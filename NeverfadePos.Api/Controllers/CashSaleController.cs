using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Sales;
using NeverfadePos.Api.Services.Outlet;
using NeverfadePos.Api.Services.Sales;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "owner,admin,kasir")]
[RequireCapability(TenantCapabilities.CorePos)]
[RequireOutletScope]
[Route("api/v2/sales/cash")]
public sealed class CashSaleController(
    IQuoteCashSaleService cash,
    IOutletExecutionContext outletContext) : ControllerBase
{
    [HttpGet("idempotency/{idempotencyKey}")]
    public async Task<ActionResult<CashSaleResponseDto>> FindCommitted(
        string idempotencyKey, CancellationToken cancellationToken)
    {
        var result = await cash.FindCommittedAsync(idempotencyKey,
            outletContext.OutletId!.Value, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new CashSaleResponseDto
        {
            Data = result.Transaction,
            Meta = new CashSaleMetaDto
            {
                CorrelationId = HttpContext.TraceIdentifier,
                Replayed = true
            }
        });
    }

    [HttpPost]
    public async Task<ActionResult<CashSaleResponseDto>> Commit(
        CommitCashSaleRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await cash.CommitAsync(request, idempotencyKey,
            outletContext.OutletId!.Value, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new CashSaleResponseDto
        {
            Data = result.Transaction,
            Meta = new CashSaleMetaDto
            {
                CorrelationId = HttpContext.TraceIdentifier,
                Replayed = result.Replayed,
                QuoteId = request.QuoteId,
                QuoteVersion = request.QuoteVersion
            }
        });
    }
}

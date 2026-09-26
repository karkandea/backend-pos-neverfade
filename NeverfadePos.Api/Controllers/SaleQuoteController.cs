using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.DTOs.Sales;
using NeverfadePos.Api.Services.Outlet;
using NeverfadePos.Api.Services.Sales;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "owner,admin,kasir")]
[RequireCapability(TenantCapabilities.CorePos)]
[RequireOutletScope]
[Route("api/v2/sales/quotes")]
public sealed class SaleQuoteController(
    ISaleQuoteService quotes,
    IOutletExecutionContext outletContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SaleQuoteResponseDto>> Create(
        CreateSaleQuoteRequestDto request, CancellationToken cancellationToken)
    {
        var selected = outletContext.OutletId;
        if (!selected.HasValue || request.OutletId != selected.Value)
            throw new TenantApiException(409, "QUOTE_OUTLET_MISMATCH",
                "Outlet quote harus sesuai outlet yang dipilih.");
        Response.Headers.CacheControl = "no-store";
        return Ok(new SaleQuoteResponseDto
        {
            Data = await quotes.CreateAsync(request, selected.Value, cancellationToken),
            Meta = new SaleQuoteMetaDto { CorrelationId = HttpContext.TraceIdentifier }
        });
    }

    [HttpGet("{quoteId:guid}")]
    public async Task<ActionResult<SaleQuoteResponseDto>> Get(
        Guid quoteId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new SaleQuoteResponseDto
        {
            Data = await quotes.GetAsync(quoteId, outletContext.OutletId!.Value, cancellationToken),
            Meta = new SaleQuoteMetaDto { CorrelationId = HttpContext.TraceIdentifier }
        });
    }
}

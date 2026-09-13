using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Retail;
using NeverfadePos.Api.Services.Retail;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[RequireCapability(TenantCapabilities.MultiPricing)]
[Route("api/retail")]
public sealed class RetailPricingController(IRetailCatalogService retailService) : ControllerBase
{
    [HttpGet("price-levels")]
    public async Task<ActionResult<List<PriceLevelDto>>> GetLevels(CancellationToken cancellationToken) =>
        Ok(await retailService.GetPriceLevelsAsync(cancellationToken));

    [HttpPost("price-levels")]
    public async Task<ActionResult<PriceLevelDto>> CreateLevel(CreatePriceLevelDto request, CancellationToken cancellationToken) =>
        Ok(await retailService.CreatePriceLevelAsync(request, cancellationToken));

    [HttpPut("price-levels/{id:guid}")]
    public async Task<ActionResult<PriceLevelDto>> UpdateLevel(Guid id, UpdatePriceLevelDto request, CancellationToken cancellationToken) =>
        Ok(await retailService.UpdatePriceLevelAsync(id, request, cancellationToken));

    [HttpDelete("price-levels/{id:guid}")]
    public async Task<IActionResult> DeleteLevel(Guid id, CancellationToken cancellationToken)
    {
        await retailService.DeletePriceLevelAsync(id, cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpGet("prices")]
    public async Task<ActionResult<List<ProductPriceDto>>> GetPrices([FromQuery] Guid productId, CancellationToken cancellationToken) =>
        Ok(await retailService.GetPricesAsync(productId, cancellationToken));

    [HttpPost("prices")]
    public async Task<ActionResult<ProductPriceDto>> CreatePrice(CreateProductPriceDto request, CancellationToken cancellationToken) =>
        Ok(await retailService.CreatePriceAsync(request, cancellationToken));

    [HttpPut("prices/{id:guid}")]
    public async Task<ActionResult<ProductPriceDto>> UpdatePrice(Guid id, UpdateProductPriceDto request, CancellationToken cancellationToken) =>
        Ok(await retailService.UpdatePriceAsync(id, request, cancellationToken));

    [HttpDelete("prices/{id:guid}")]
    public async Task<IActionResult> DeletePrice(Guid id, CancellationToken cancellationToken)
    {
        await retailService.DeletePriceAsync(id, cancellationToken);
        return Ok(new { ok = true });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Retail;
using NeverfadePos.Api.Services.Retail;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[RequireCapability(TenantCapabilities.ProductVariants)]
[RequireCapability(TenantCapabilities.MultiPricing)]
[Route("api/retail/catalog")]
public sealed class RetailCatalogController(IRetailCatalogService retailService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RetailCatalogDto>> Get(
        [FromQuery] string? search,
        [FromQuery] string? kategori,
        CancellationToken cancellationToken) =>
        Ok(await retailService.GetCatalogAsync(search, kategori, cancellationToken));
}

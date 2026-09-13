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
[Route("api/retail/variants")]
public sealed class RetailVariantsController(IRetailCatalogService retailService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductVariantDto>>> GetAll(
        [FromQuery] Guid productId,
        CancellationToken cancellationToken) =>
        Ok(await retailService.GetVariantsAsync(productId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ProductVariantDto>> Create(
        CreateProductVariantDto request,
        CancellationToken cancellationToken) =>
        Ok(await retailService.CreateVariantAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductVariantDto>> Update(
        Guid id,
        UpdateProductVariantDto request,
        CancellationToken cancellationToken) =>
        Ok(await retailService.UpdateVariantAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await retailService.DeleteVariantAsync(id, cancellationToken);
        return Ok(new { ok = true });
    }
}

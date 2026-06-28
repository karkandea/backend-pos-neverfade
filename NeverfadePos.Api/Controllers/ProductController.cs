using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Product;
using NeverfadePos.Api.Services.Product;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/products")]
public sealed class ProductController(
    IProductService productService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? kategori,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.GetAllAsync(
            search,
            kategori,
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.GetByIdAsync(
            id,
            cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(
        CreateProductDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.CreateAsync(
            request,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductDto>> Update(
        Guid id,
        UpdateProductDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await productService.UpdateAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await productService.DeleteAsync(
            id,
            cancellationToken);

        return Ok(new { ok = true });
    }
}

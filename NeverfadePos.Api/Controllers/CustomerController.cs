using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Customer;
using NeverfadePos.Api.Services.Customer;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customers")]
public sealed class CustomerController(
    ICustomerService customerService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> GetAll(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        return Ok(await customerService.GetAllAsync(
            search,
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await customerService.GetByIdAsync(
            id,
            cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(
        CreateCustomerDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await customerService.CreateAsync(
            request,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Update(
        Guid id,
        UpdateCustomerDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await customerService.UpdateAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await customerService.DeleteAsync(
            id,
            cancellationToken);

        return Ok(new { ok = true });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Laundry;
using NeverfadePos.Api.Services.Laundry;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/laundry/work-orders")]
[RequireCapability(TenantCapabilities.WorkOrders)]
public sealed class LaundryWorkOrdersController(
    ILaundryService laundryService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LaundryWorkOrderDto>>> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await laundryService.GetAllAsync(
            status,
            cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LaundryWorkOrderDto>> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await laundryService.GetByIdAsync(
            id,
            cancellationToken));

    [HttpPost]
    public async Task<ActionResult<LaundryWorkOrderDto>> Create(
        CreateLaundryWorkOrderRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await laundryService.CreateAsync(
            request,
            cancellationToken));

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<LaundryWorkOrderDto>> UpdateStatus(
        Guid id,
        UpdateLaundryWorkOrderStatusRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await laundryService.UpdateStatusAsync(
            id,
            request,
            cancellationToken));

    [HttpPost("{id:guid}/complete-payment")]
    public async Task<ActionResult<LaundryWorkOrderDto>> CompletePayment(
        Guid id,
        CompleteLaundryPaymentRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await laundryService.CompletePaymentAsync(
            id,
            request,
            cancellationToken));
}

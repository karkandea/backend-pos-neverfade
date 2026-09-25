using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Services.Outlet;
using NeverfadePos.Api.Services.Transaction;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionController(
    ITransactionService transactionService,
    IOutletService outletService,
    IOutletExecutionScope outletExecutionScope)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TransactionDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromHeader(Name = "X-Outlet-Id")] Guid? selectedOutletId,
        CancellationToken cancellationToken)
    {
        var outlet = await outletService.ResolveAsync(selectedOutletId, cancellationToken);
        return Ok(await transactionService.GetAllAsync(
            search, startDate, endDate, outlet.Id, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetById(
        Guid id,
        [FromHeader(Name = "X-Outlet-Id")] Guid? selectedOutletId,
        CancellationToken cancellationToken)
    {
        var outlet = await outletService.ResolveAsync(selectedOutletId, cancellationToken);
        return Ok(await transactionService.GetByIdAsync(
            id, outlet.Id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(
        CreateTransactionDto request,
        CancellationToken cancellationToken)
    {
        var outlet = await outletService.ResolveAsync(
            request.OutletId,
            cancellationToken);

        using var outletScope = outletExecutionScope.Begin(
            outlet.Id);

        return Ok(await transactionService.CreateAsync(
            request,
            cancellationToken));
    }
}

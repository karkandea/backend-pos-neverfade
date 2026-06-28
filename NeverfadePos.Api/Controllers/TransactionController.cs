using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Services.Transaction;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionController(
    ITransactionService transactionService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TransactionDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        return Ok(await transactionService.GetAllAsync(
            search,
            startDate,
            endDate,
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await transactionService.GetByIdAsync(
            id,
            cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create(
        CreateTransactionDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await transactionService.CreateAsync(
            request,
            cancellationToken));
    }
}

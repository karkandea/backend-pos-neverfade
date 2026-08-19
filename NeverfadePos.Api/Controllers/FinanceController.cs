using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Finance;
using NeverfadePos.Api.Services.Finance;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Route("api/finance")]
[Authorize(Roles = "owner")]
public sealed class FinanceController(
    ITenantFinanceService financeService)
    : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<FinanceSummaryDto>> GetSummary(
        CancellationToken cancellationToken) =>
        Ok(await financeService.GetSummaryAsync(cancellationToken));

    [HttpGet("withdrawals")]
    public async Task<ActionResult<IReadOnlyList<WithdrawalDto>>> GetWithdrawals(
        CancellationToken cancellationToken) =>
        Ok(await financeService.GetWithdrawalsAsync(cancellationToken));

    [HttpGet("movements")]
    public async Task<ActionResult<IReadOnlyList<FinanceMovementDto>>> GetMovements(
        CancellationToken cancellationToken) =>
        Ok(await financeService.GetMovementsAsync(cancellationToken));

    [HttpPost("withdrawals")]
    public async Task<ActionResult<WithdrawalDto>> CreateWithdrawal(
        CreateWithdrawalRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await financeService.CreateWithdrawalAsync(
            request,
            cancellationToken));
}

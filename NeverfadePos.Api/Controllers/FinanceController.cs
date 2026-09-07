using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Finance;
using NeverfadePos.Api.Services.Finance;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Route("api/finance")]
[Authorize(Roles = "owner")]
[RequireCapability(TenantCapabilities.FinanceWithdrawal)]
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

    [HttpGet("withdrawal-settings")]
    public async Task<ActionResult<WithdrawalSettingsDto>> GetWithdrawalSettings(
        CancellationToken cancellationToken) =>
        Ok(await financeService.GetWithdrawalSettingsAsync(cancellationToken));

    [HttpGet("bank-account")]
    public async Task<ActionResult<WithdrawalBankAccountDto?>> GetBankAccount(
        CancellationToken cancellationToken) =>
        Ok(await financeService.GetBankAccountAsync(cancellationToken));

    [HttpPut("bank-account")]
    public async Task<ActionResult<WithdrawalBankAccountDto>> PutBankAccount(
        UpdateWithdrawalBankAccountRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await financeService.PutBankAccountAsync(
            request,
            cancellationToken));

    [HttpPost("withdrawals")]
    public async Task<ActionResult<WithdrawalDto>> CreateWithdrawal(
        CreateWithdrawalRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await financeService.CreateWithdrawalAsync(
            request,
            cancellationToken));

    [HttpPost("withdrawals/{withdrawalId:guid}/cancel")]
    public async Task<ActionResult<WithdrawalDto>> CancelWithdrawal(
        Guid withdrawalId,
        CancellationToken cancellationToken) =>
        Ok(await financeService.CancelWithdrawalAsync(
            withdrawalId,
            cancellationToken));
}

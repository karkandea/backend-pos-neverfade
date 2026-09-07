using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.DTOs.Finance;
using NeverfadePos.Api.Services.Finance;

namespace NeverfadePos.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/withdrawals")]
[Authorize(
    AuthenticationSchemes = PlatformAuthConstants.AuthenticationScheme,
    Policy = PlatformAuthConstants.AuthorizationPolicy)]
public sealed class PlatformWithdrawalController(
    IPlatformWithdrawalService withdrawalService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlatformWithdrawalDto>>> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await withdrawalService.GetAllAsync(
            status,
            cancellationToken));

    [HttpGet("bank-accounts")]
    public async Task<ActionResult<IReadOnlyList<PlatformWithdrawalBankAccountDto>>>
        GetBankAccounts(
            [FromQuery] string? status,
            CancellationToken cancellationToken) =>
        Ok(await withdrawalService.GetBankAccountsAsync(
            status,
            cancellationToken));

    [HttpPost("bank-accounts/{tenantId:guid}/review")]
    public async Task<ActionResult<PlatformWithdrawalBankAccountDto>>
        ReviewBankAccount(
            Guid tenantId,
            VerifyWithdrawalBankAccountRequestDto request,
            CancellationToken cancellationToken) =>
        Ok(await withdrawalService.ReviewBankAccountAsync(
            tenantId,
            request,
            cancellationToken));

    [HttpPost("{withdrawalId:guid}/start-processing")]
    public async Task<ActionResult<PlatformWithdrawalDto>> StartProcessing(
        Guid withdrawalId,
        CancellationToken cancellationToken) =>
        Ok(await withdrawalService.StartProcessingAsync(
            withdrawalId,
            cancellationToken));

    [HttpPost("{withdrawalId:guid}/mark-paid")]
    public async Task<ActionResult<PlatformWithdrawalDto>> MarkPaid(
        Guid withdrawalId,
        MarkWithdrawalPaidRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await withdrawalService.MarkPaidAsync(
            withdrawalId,
            request,
            cancellationToken));

    [HttpPost("{withdrawalId:guid}/reject")]
    public async Task<ActionResult<PlatformWithdrawalDto>> Reject(
        Guid withdrawalId,
        RejectWithdrawalRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await withdrawalService.RejectAsync(
            withdrawalId,
            request,
            cancellationToken));
}

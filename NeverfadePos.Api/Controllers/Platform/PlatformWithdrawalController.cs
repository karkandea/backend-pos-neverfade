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
        CancellationToken cancellationToken) =>
        Ok(await withdrawalService.GetAllAsync(cancellationToken));

    [HttpPost("{withdrawalId:guid}/mark-paid")]
    public async Task<ActionResult<PlatformWithdrawalDto>> MarkPaid(
        Guid withdrawalId,
        CancellationToken cancellationToken) =>
        Ok(await withdrawalService.MarkPaidAsync(
            withdrawalId,
            cancellationToken));

    [HttpPost("{withdrawalId:guid}/reject")]
    public async Task<ActionResult<PlatformWithdrawalDto>> Reject(
        Guid withdrawalId,
        CancellationToken cancellationToken) =>
        Ok(await withdrawalService.RejectAsync(
            withdrawalId,
            cancellationToken));
}

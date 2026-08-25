using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Services.Payment;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
public sealed class PaymentController(
    IPaymentService paymentService,
    ISandboxQrisQaService sandboxQrisQaService)
    : ControllerBase
{
    [HttpGet("capabilities")]
    public ActionResult<PaymentCapabilitiesDto> GetCapabilities()
    {
        return Ok(paymentService.GetCapabilities());
    }

    [HttpPost("qris")]
    public async Task<ActionResult<QrisPaymentDto>> CreateQris(
        CreateTransactionDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await paymentService.CreateQrisAsync(
            request,
            cancellationToken));
    }

    [HttpPost("qa/simulate-scan")]
    [Authorize(Roles = "owner,admin")]
    public async Task<ActionResult<PaymentStatusDto>> SimulateSandboxScan(
        SandboxQrisScanRequest request,
        CancellationToken cancellationToken)
    {
        return Accepted(await sandboxQrisQaService.SimulateScannedQrisAsync(
            request.QrString,
            cancellationToken));
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<ActionResult<PaymentStatusDto>> GetStatus(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        return Ok(await paymentService.GetStatusAsync(
            paymentId,
            cancellationToken));
    }

    [HttpPost("{paymentId:guid}/cancel")]
    public async Task<ActionResult<PaymentStatusDto>> Cancel(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        return Ok(await paymentService.CancelAsync(paymentId, cancellationToken));
    }

    [HttpGet("current")]
    public async Task<ActionResult<PaymentStatusDto>> GetCurrent(
        CancellationToken cancellationToken)
    {
        var payment = await paymentService.GetCurrentAsync(
            cancellationToken);

        return payment is null ? NoContent() : Ok(payment);
    }
}

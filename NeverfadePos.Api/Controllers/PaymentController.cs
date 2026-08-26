using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Payment;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
public sealed class PaymentController(
    IPaymentService paymentService,
    ISandboxQrisQaService sandboxQrisQaService,
    AppDbContext db)
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
        await ReconcileExpiredPaymentsAsync(null, cancellationToken);

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
        await ReconcileExpiredPaymentsAsync(paymentId, cancellationToken);

        return Ok(await paymentService.GetStatusAsync(
            paymentId,
            cancellationToken));
    }

    [HttpPost("{paymentId:guid}/cancel")]
    public async Task<ActionResult<PaymentStatusDto>> Cancel(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        await ReconcileExpiredPaymentsAsync(paymentId, cancellationToken);

        return Ok(await paymentService.CancelAsync(paymentId, cancellationToken));
    }

    [HttpGet("current")]
    public async Task<ActionResult<PaymentStatusDto>> GetCurrent(
        CancellationToken cancellationToken)
    {
        await ReconcileExpiredPaymentsAsync(null, cancellationToken);

        var payment = await paymentService.GetCurrentAsync(
            cancellationToken);

        return payment is null ? NoContent() : Ok(payment);
    }

    private async Task ReconcileExpiredPaymentsAsync(
        Guid? paymentId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = db.Payments
            .Include(x => x.Transaction)
            .Where(x =>
                (x.Status == PaymentConstants.StatusCreating ||
                 x.Status == PaymentConstants.StatusPending) &&
                x.ExpiresAt.HasValue &&
                x.ExpiresAt.Value <= now);

        if (paymentId.HasValue)
        {
            query = query.Where(x => x.Id == paymentId.Value);
        }

        var expiredPayments = await query.ToListAsync(cancellationToken);
        if (expiredPayments.Count == 0)
        {
            return;
        }

        foreach (var payment in expiredPayments)
        {
            payment.Status = PaymentConstants.StatusFailed;
            payment.FailureCode = "PAYMENT_REQUEST_EXPIRED";
            payment.UpdatedAt = now;

            if (payment.Transaction is not null &&
                payment.Transaction.Status == TransactionStatuses.PendingPayment)
            {
                payment.Transaction.Status = TransactionStatuses.Failed;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

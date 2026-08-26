using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Payments.Xendit;
using NeverfadePos.Api.Services.Payment;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
public sealed class PaymentController(
    IPaymentService paymentService,
    ISandboxQrisQaService sandboxQrisQaService,
    IXenditPaymentProvider xendit,
    AppDbContext db,
    ILogger<PaymentController> logger)
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
        await ReconcileExpiredPaymentsAsync(
            null,
            false,
            cancellationToken);

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
        await ReconcileExpiredPaymentsAsync(
            paymentId,
            false,
            cancellationToken);

        return Ok(await paymentService.GetStatusAsync(
            paymentId,
            cancellationToken));
    }

    [HttpPost("{paymentId:guid}/cancel")]
    public async Task<ActionResult<PaymentStatusDto>> Cancel(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        await ReconcileExpiredPaymentsAsync(
            paymentId,
            true,
            cancellationToken);

        return Ok(await paymentService.CancelAsync(paymentId, cancellationToken));
    }

    [HttpGet("current")]
    public async Task<ActionResult<PaymentStatusDto>> GetCurrent(
        CancellationToken cancellationToken)
    {
        await ReconcileExpiredPaymentsAsync(
            null,
            false,
            cancellationToken);

        var payment = await paymentService.GetCurrentAsync(
            cancellationToken);

        return payment is null ? NoContent() : Ok(payment);
    }

    private async Task ReconcileExpiredPaymentsAsync(
        Guid? paymentId,
        bool forceProviderCheck,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = db.Payments
            .Include(x => x.Transaction)
            .Where(x =>
                x.Status == PaymentConstants.StatusPending &&
                !string.IsNullOrEmpty(x.ProviderPaymentRequestId));

        if (paymentId.HasValue)
        {
            query = query.Where(x => x.Id == paymentId.Value);
        }

        if (!forceProviderCheck)
        {
            query = query.Where(x =>
                x.ExpiresAt.HasValue &&
                x.ExpiresAt.Value <= now);
        }

        var candidates = await query.ToListAsync(cancellationToken);
        var changed = false;

        foreach (var payment in candidates)
        {
            string providerStatus;
            try
            {
                providerStatus = await xendit.GetPaymentRequestStatusAsync(
                    payment.ProviderPaymentRequestId!,
                    cancellationToken);
            }
            catch (XenditProviderException ex)
            {
                logger.LogWarning(
                    ex,
                    "Unable to reconcile Xendit payment {PaymentId} request {PaymentRequestId}",
                    payment.Id,
                    payment.ProviderPaymentRequestId);
                continue;
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(
                    ex,
                    "Network error while reconciling Xendit payment {PaymentId} request {PaymentRequestId}",
                    payment.Id,
                    payment.ProviderPaymentRequestId);
                continue;
            }
            catch (TaskCanceledException ex)
                when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Timeout while reconciling Xendit payment {PaymentId} request {PaymentRequestId}",
                    payment.Id,
                    payment.ProviderPaymentRequestId);
                continue;
            }

            if (!string.Equals(
                providerStatus,
                "EXPIRED",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            payment.Status = PaymentConstants.StatusFailed;
            payment.FailureCode = "PAYMENT_REQUEST_EXPIRED";
            payment.UpdatedAt = now;

            if (payment.Transaction is not null &&
                payment.Transaction.Status == TransactionStatuses.PendingPayment)
            {
                payment.Transaction.Status = TransactionStatuses.Failed;
            }

            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

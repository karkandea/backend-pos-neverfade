using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Payments.Xendit;
using NeverfadePos.Api.Services.Outlet;
using NeverfadePos.Api.Services.Payment;
using NeverfadePos.Api.Services.Sales;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
public sealed class PaymentController(
    IPaymentService paymentService,
    ISandboxQrisQaService sandboxQrisQaService,
    IXenditPaymentProvider xendit,
    IOutletService outletService,
    IOutletExecutionScope outletExecutionScope,
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
        var outlet = await outletService.ResolveAsync(
            request.OutletId, cancellationToken);
        using var outletScope = outletExecutionScope.Begin(outlet.Id);
        await ReconcileExpiredPaymentsAsync(null, false, outlet.Id, cancellationToken);

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
        [FromHeader(Name = "X-Outlet-Id")] Guid? selectedOutletId,
        CancellationToken cancellationToken)
    {
        var outletId = await RequirePaymentOutletAsync(paymentId, selectedOutletId, cancellationToken);
        await ReconcileExpiredPaymentsAsync(paymentId, false, outletId, cancellationToken);

        return Ok(await paymentService.GetStatusAsync(
            paymentId,
            cancellationToken));
    }

    [HttpPost("{paymentId:guid}/cancel")]
    public async Task<ActionResult<PaymentStatusDto>> Cancel(
        Guid paymentId,
        [FromHeader(Name = "X-Outlet-Id")] Guid? selectedOutletId,
        CancellationToken cancellationToken)
    {
        var outletId = await RequirePaymentOutletAsync(paymentId, selectedOutletId, cancellationToken);
        await ReconcileExpiredPaymentsAsync(paymentId, true, outletId, cancellationToken);

        return Ok(await paymentService.CancelAsync(paymentId, cancellationToken));
    }

    [HttpGet("current")]
    public async Task<ActionResult<PaymentStatusDto>> GetCurrent(
        [FromHeader(Name = "X-Outlet-Id")] Guid? selectedOutletId,
        CancellationToken cancellationToken)
    {
        var outlet = await outletService.ResolveAsync(selectedOutletId, cancellationToken);
        using var outletScope = outletExecutionScope.Begin(outlet.Id);
        await ReconcileExpiredPaymentsAsync(null, false, outlet.Id, cancellationToken);

        var payment = await paymentService.GetCurrentAsync(
            cancellationToken);

        return payment is null ? NoContent() : Ok(payment);
    }

    private async Task<Guid> RequirePaymentOutletAsync(
        Guid paymentId, Guid? selectedOutletId, CancellationToken cancellationToken)
    {
        var outlet = await outletService.ResolveAsync(selectedOutletId, cancellationToken);
        var payment = await db.Payments.AsNoTracking()
            .Where(x => x.Id == paymentId)
            .Select(x => new { OutletId = x.Transaction!.OutletId })
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is null || payment.OutletId != outlet.Id)
            throw new KeyNotFoundException("Payment tidak ditemukan di outlet ini.");
        return outlet.Id;
    }

    private async Task ReconcileExpiredPaymentsAsync(
        Guid? paymentId,
        bool forceProviderCheck,
        Guid outletId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = db.Payments
            .Include(x => x.Transaction)
            .Where(x => x.Transaction != null && x.Transaction.OutletId == outletId &&
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

        // Fetch immutable provider references without tracking; external status
        // lookups are intentionally outside the database transaction/stock lock.
        var candidates = await query.AsNoTracking()
            .Select(x => new { x.Id, x.TenantId, x.ProviderPaymentRequestId })
            .ToListAsync(cancellationToken);

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

            // A paid webhook may have finalized while the provider was queried.
            // All cash and webhook finalizers serialize on this same tenant lock.
            await using var expiryTransaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(cancellationToken)
                : null;
            await TenantStockLock.AcquireAsync(db, payment.TenantId, cancellationToken);
            var fresh = await db.Payments.Include(x => x.Transaction)
                .SingleOrDefaultAsync(x => x.Id == payment.Id &&
                    x.Status == PaymentConstants.StatusPending &&
                    x.ProviderPaymentRequestId == payment.ProviderPaymentRequestId &&
                    x.Transaction != null && x.Transaction.OutletId == outletId,
                    cancellationToken);
            if (fresh is null || fresh.Transaction?.Status == TransactionStatuses.Paid)
                continue;
            if (!forceProviderCheck &&
                (!fresh.ExpiresAt.HasValue || fresh.ExpiresAt.Value > now))
                continue;

            fresh.Status = PaymentConstants.StatusFailed;
            fresh.FailureCode = "PAYMENT_REQUEST_EXPIRED";
            fresh.UpdatedAt = DateTime.UtcNow;
            if (fresh.Transaction?.Status == TransactionStatuses.PendingPayment)
                fresh.Transaction.Status = TransactionStatuses.Failed;
            await db.SaveChangesAsync(cancellationToken);
            if (expiryTransaction is not null)
                await expiryTransaction.CommitAsync(cancellationToken);
        }
    }
}

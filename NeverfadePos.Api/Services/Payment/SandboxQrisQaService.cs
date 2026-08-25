using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Payments;
using NeverfadePos.Api.Payments.Xendit;

namespace NeverfadePos.Api.Services.Payment;

internal sealed class SandboxQrisQaService(
    AppDbContext db,
    CurrentUser currentUser,
    IPaymentModeGate paymentModeGate,
    IXenditSandboxSimulator sandboxSimulator)
    : ISandboxQrisQaService
{
    public async Task<PaymentStatusDto> SimulateScannedQrisAsync(
        string qrString,
        CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId ??
            throw new UnauthorizedAccessException();

        var capabilities = paymentModeGate.GetCapabilities(tenantId);
        if (!capabilities.IsSandbox || !capabilities.QrisEnabled)
        {
            throw new PaymentApiException(
                StatusCodes.Status404NotFound,
                "QA_SANDBOX_NOT_AVAILABLE",
                "QA Scanner hanya tersedia untuk tenant QRIS Sandbox.");
        }

        var normalizedQrString = qrString.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQrString) ||
            normalizedQrString.Length > 4096)
        {
            throw new PaymentApiException(
                StatusCodes.Status400BadRequest,
                "QA_QRIS_SCAN_INVALID",
                "QRIS yang dipindai tidak valid.");
        }

        var payment = await db.Payments
            .SingleOrDefaultAsync(
                x => x.QrString == normalizedQrString,
                cancellationToken)
            ?? throw new PaymentApiException(
                StatusCodes.Status404NotFound,
                "QA_QRIS_NOT_FOUND",
                "QRIS Sandbox ini tidak dikenali untuk tenant yang sedang login.");

        if (payment.Status == PaymentConstants.StatusPaid)
        {
            return MapStatus(payment);
        }

        if (payment.Status != PaymentConstants.StatusPending)
        {
            throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "QA_QRIS_NOT_PENDING",
                "QRIS Sandbox ini sudah tidak menunggu pembayaran.");
        }

        if (string.IsNullOrWhiteSpace(payment.ProviderPaymentRequestId))
        {
            throw new PaymentApiException(
                StatusCodes.Status409Conflict,
                "QA_QRIS_NOT_READY",
                "Payment request QRIS Sandbox belum siap disimulasikan.");
        }

        await sandboxSimulator.SimulatePaymentAsync(
            payment.ProviderPaymentRequestId,
            payment.Amount,
            cancellationToken);

        return MapStatus(payment);
    }

    private static PaymentStatusDto MapStatus(
        NeverfadePos.Api.Entities.Payment payment) => new()
    {
        Id = payment.Id,
        TransactionId = payment.TransactionId,
        Status = payment.Status,
        Amount = payment.Amount,
        Currency = payment.Currency,
        ProviderPaymentRequestId =
            payment.ProviderPaymentRequestId ?? string.Empty,
        ProviderReferenceId = payment.ProviderReferenceId,
        QrString = payment.QrString,
        ExpiresAt = payment.ExpiresAt,
        FailureCode = payment.FailureCode,
        UpdatedAt = payment.UpdatedAt
    };
}

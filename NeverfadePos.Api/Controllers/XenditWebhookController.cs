using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.Services.Payment;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks/xendit")]
public sealed class XenditWebhookController(IPaymentService paymentService)
    : ControllerBase
{
    [HttpPost("payments")]
    public async Task<IActionResult> Payment(
        [FromHeader(Name = "x-callback-token")] string? callbackToken,
        XenditPaymentWebhookDto webhook,
        CancellationToken cancellationToken)
    {
        try
        {
            await paymentService.ProcessXenditWebhookAsync(
                callbackToken,
                webhook,
                cancellationToken);
        }
        catch (PaymentApiException ex) when (
            ex.StatusCode == StatusCodes.Status400BadRequest &&
            ex.Code == "XENDIT_WEBHOOK_INVALID" &&
            IsDashboardLegacySample(webhook))
        {
            // Xendit Dashboard "Test and save" currently sends a legacy
            // sample payload. The payment service verifies the callback token
            // before strict v3 validation, so reaching this catch means the
            // token already passed and no payment mutation has occurred.
            return Ok();
        }

        return Ok();
    }

    private static bool IsDashboardLegacySample(
        XenditPaymentWebhookDto webhook)
    {
        var legacyEvent =
            string.Equals(
                webhook.Event,
                "payment.succeeded",
                StringComparison.Ordinal) ||
            string.Equals(
                webhook.Event,
                "payment.failed",
                StringComparison.Ordinal);

        return legacyEvent &&
            webhook.Data is not null &&
            string.IsNullOrWhiteSpace(webhook.Data.PaymentId) &&
            string.IsNullOrWhiteSpace(webhook.Data.PaymentRequestId) &&
            !string.IsNullOrWhiteSpace(webhook.Data.LegacyId) &&
            webhook.Data.LegacyAmount is > 0m;
    }
}

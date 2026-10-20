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
            // Xendit Dashboard "Test and save" can send a fixed legacy/sample
            // payload. The payment service verifies the callback token before
            // strict v3 validation, so reaching this catch means the token
            // already passed and no payment mutation has occurred.
            return Ok();
        }
        catch (PaymentApiException ex) when (
            ex.StatusCode == StatusCodes.Status404NotFound &&
            ex.Code == "PAYMENT_ROUTE_NOT_FOUND" &&
            IsDashboardV3PaymentStatusSample(webhook))
        {
            // Payment Requests v3 -> Payment Status "Test and save" sends a
            // structurally valid payment.capture fixture whose payment request
            // intentionally does not exist in NeverFade. Token verification and
            // strict v3 shape validation have already passed, while route lookup
            // happens before tenant scope or any payment/stock/ledger mutation.
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
            string.Equals(
                webhook.BusinessId,
                "sample_business_id",
                StringComparison.Ordinal) &&
            webhook.Data is not null &&
            !string.IsNullOrWhiteSpace(webhook.Data.LegacyId) &&
            webhook.Data.LegacyAmount is > 0m;
    }

    private static bool IsDashboardV3PaymentStatusSample(
        XenditPaymentWebhookDto webhook) =>
        string.Equals(
            webhook.Event,
            "payment.capture",
            StringComparison.Ordinal) &&
        string.Equals(
            webhook.BusinessId,
            "62440e322008e87fb29c1fd0",
            StringComparison.Ordinal) &&
        webhook.Data is not null &&
        string.Equals(
            webhook.Data.PaymentId,
            "py-97716cc2-2840-4ead-949b-db60e9aeb55e",
            StringComparison.Ordinal) &&
        string.Equals(
            webhook.Data.PaymentRequestId,
            "pr-ced7965b-d588-49f1-ba41-d499277e5395",
            StringComparison.Ordinal) &&
        string.Equals(
            webhook.Data.ReferenceId,
            "90392f42-d98a-49ef-a7f3-90392f42d98a",
            StringComparison.Ordinal) &&
        webhook.Data.RequestAmount == 10000m &&
        string.Equals(
            webhook.Data.Status,
            "SUCCEEDED",
            StringComparison.Ordinal) &&
        string.Equals(
            webhook.Data.ChannelCode,
            "CARDS",
            StringComparison.Ordinal) &&
        string.Equals(
            webhook.Data.Currency,
            "IDR",
            StringComparison.Ordinal);
}

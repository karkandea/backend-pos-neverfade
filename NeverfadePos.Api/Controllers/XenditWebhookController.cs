using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        await paymentService.ProcessXenditWebhookAsync(
            callbackToken,
            webhook,
            cancellationToken);

        return Ok();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Payment;
using NeverfadePos.Api.DTOs.Transaction;
using NeverfadePos.Api.Services.Payment;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments")]
public sealed class PaymentController(IPaymentService paymentService)
    : ControllerBase
{
    [HttpPost("qris")]
    public async Task<ActionResult<QrisPaymentDto>> CreateQris(
        CreateTransactionDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await paymentService.CreateQrisAsync(
            request,
            cancellationToken));
    }
}

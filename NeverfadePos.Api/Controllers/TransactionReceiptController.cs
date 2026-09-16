using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Services.WhatsApp;

namespace NeverfadePos.Api.Controllers;

public sealed class SendWhatsAppReceiptRequest
{
    [Required]
    [MaxLength(50)]
    public string Phone { get; set; } = string.Empty;
}

[ApiController]
[Authorize]
[Route("api/transactions/{transactionId:guid}/receipt")]
public sealed class TransactionReceiptController(
    IWhatsAppReceiptService whatsAppReceiptService)
    : ControllerBase
{
    [HttpPost("whatsapp")]
    public async Task<ActionResult<WhatsAppReceiptResult>> SendWhatsApp(
        Guid transactionId,
        SendWhatsAppReceiptRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await whatsAppReceiptService.SendReceiptAsync(
            transactionId,
            request.Phone,
            cancellationToken));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Services.WhatsApp;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/whatsapp")]
public sealed class WhatsAppController(
    IWhatsAppReceiptService whatsAppReceiptService)
    : ControllerBase
{
    [Authorize(Roles = "owner,admin")]
    [HttpGet("status")]
    public async Task<ActionResult<WhatsAppConnectionStatus>> GetStatus(
        [FromQuery] Guid? outletId,
        CancellationToken cancellationToken)
    {
        return Ok(await whatsAppReceiptService.GetStatusAsync(
            outletId,
            cancellationToken));
    }

    [Authorize(Roles = "owner,admin")]
    [HttpPost("connect")]
    public async Task<ActionResult<WhatsAppConnectionStatus>> Connect(
        [FromQuery] Guid? outletId,
        CancellationToken cancellationToken)
    {
        return Ok(await whatsAppReceiptService.ConnectAsync(
            outletId,
            cancellationToken));
    }

    [Authorize(Roles = "owner,admin")]
    [HttpGet("qr")]
    public async Task<IActionResult> GetQr(
        [FromQuery] Guid? outletId,
        CancellationToken cancellationToken)
    {
        var qr = await whatsAppReceiptService.GetQrAsync(
            outletId,
            cancellationToken);

        return Ok(new
        {
            mimeType = qr.MimeType,
            data = qr.Data
        });
    }

    [Authorize(Roles = "owner,admin")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromQuery] Guid? outletId,
        CancellationToken cancellationToken)
    {
        await whatsAppReceiptService.LogoutAsync(
            outletId,
            cancellationToken);

        return Ok(new { ok = true });
    }
}

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
        CancellationToken cancellationToken)
    {
        return Ok(await whatsAppReceiptService.GetStatusAsync(
            cancellationToken));
    }

    [Authorize(Roles = "owner,admin")]
    [HttpPost("connect")]
    public async Task<ActionResult<WhatsAppConnectionStatus>> Connect(
        CancellationToken cancellationToken)
    {
        return Ok(await whatsAppReceiptService.ConnectAsync(
            cancellationToken));
    }

    [Authorize(Roles = "owner,admin")]
    [HttpGet("qr")]
    public async Task<IActionResult> GetQr(
        CancellationToken cancellationToken)
    {
        var qr = await whatsAppReceiptService.GetQrAsync(
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
        CancellationToken cancellationToken)
    {
        await whatsAppReceiptService.LogoutAsync(
            cancellationToken);

        return Ok(new { ok = true });
    }
}

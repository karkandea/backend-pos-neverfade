using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Settings;
using NeverfadePos.Api.Services.Settings;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/settings")]
public sealed class SettingsController(
    ISettingsService settingsService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SettingsDto>> Get(
        CancellationToken cancellationToken)
    {
        return Ok(await settingsService.GetAsync(
            cancellationToken));
    }

    [Authorize(Roles = "owner,admin")]
    [HttpPut]
    public async Task<IActionResult> Update(
        UpdateSettingsDto request,
        CancellationToken cancellationToken)
    {
        await settingsService.UpdateAsync(
            request,
            cancellationToken);

        return Ok(new { ok = true });
    }
}

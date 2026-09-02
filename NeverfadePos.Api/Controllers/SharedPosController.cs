using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.SharedPos;
using NeverfadePos.Api.Services.SharedPos;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Route("api/shared-pos")]
public sealed class SharedPosController(ISharedPosService sharedPosService)
    : ControllerBase
{
    private const string DeviceTokenHeader = "X-NF-Device-Token";
    private const string SessionTokenHeader = "X-NF-Session-Token";

    [HttpGet("devices")]
    [Authorize(Roles = "owner,admin")]
    [RequireCapability(TenantCapabilities.Attendance)]
    [RequireRecentSharedDeviceReauth]
    public async Task<ActionResult<List<SharedPosDeviceDto>>> GetDevices(CancellationToken cancellationToken)
    {
        return Ok(await sharedPosService.GetDevicesAsync(cancellationToken));
    }

    [HttpPost("devices")]
    [Authorize(Roles = "owner,admin")]
    [RequireCapability(TenantCapabilities.Attendance)]
    [RequireRecentSharedDeviceReauth]
    public async Task<ActionResult<RegisteredSharedPosDeviceDto>> RegisterDevice(
        RegisterSharedPosDeviceRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await sharedPosService.RegisterDeviceAsync(request, cancellationToken));
    }

    [HttpPost("devices/{deviceId:guid}/deactivate")]
    [Authorize(Roles = "owner,admin")]
    [RequireCapability(TenantCapabilities.Attendance)]
    [RequireRecentSharedDeviceReauth]
    public async Task<IActionResult> DeactivateDevice(Guid deviceId, CancellationToken cancellationToken)
    {
        await sharedPosService.DeactivateDeviceAsync(deviceId, cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpPost("unlock")]
    [AllowAnonymous]
    public async Task<ActionResult<SharedPosUnlockResponseDto>> Unlock(
        SharedPosUnlockRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await sharedPosService.UnlockAsync(
            Request.Headers[DeviceTokenHeader].FirstOrDefault() ?? string.Empty,
            request,
            cancellationToken));
    }

    [HttpGet("session")]
    [AllowAnonymous]
    public async Task<ActionResult<SharedPosSessionDto>> GetSession(CancellationToken cancellationToken)
    {
        return Ok(await sharedPosService.GetSessionAsync(
            Request.Headers[SessionTokenHeader].FirstOrDefault() ?? string.Empty,
            cancellationToken));
    }

    [HttpPost("lock")]
    [AllowAnonymous]
    public async Task<IActionResult> Lock(CancellationToken cancellationToken)
    {
        await sharedPosService.LockAsync(
            Request.Headers[SessionTokenHeader].FirstOrDefault() ?? string.Empty,
            cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpPost("attendance/checkin")]
    [AllowAnonymous]
    public async Task<ActionResult<SharedAttendanceResultDto>> CheckIn(CancellationToken cancellationToken)
    {
        return Ok(await sharedPosService.CheckInAsync(
            Request.Headers[SessionTokenHeader].FirstOrDefault() ?? string.Empty,
            cancellationToken));
    }

    [HttpPost("attendance/checkout")]
    [AllowAnonymous]
    public async Task<ActionResult<SharedAttendanceResultDto>> CheckOut(CancellationToken cancellationToken)
    {
        return Ok(await sharedPosService.CheckOutAsync(
            Request.Headers[SessionTokenHeader].FirstOrDefault() ?? string.Empty,
            cancellationToken));
    }
}

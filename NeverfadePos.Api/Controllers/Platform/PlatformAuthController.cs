using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.DTOs.PlatformAuth;
using NeverfadePos.Api.Services.PlatformAuth;

namespace NeverfadePos.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/auth")]
public sealed class PlatformAuthController(
    IPlatformAuthService authService)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<
        PlatformLoginResponseDto>> Login(
        PlatformLoginRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await authService.LoginAsync(
            request,
            cancellationToken));
    }

    [Authorize(
        AuthenticationSchemes =
            PlatformAuthConstants.AuthenticationScheme,
        Policy =
            PlatformAuthConstants.AuthorizationPolicy)]
    [HttpGet("me")]
    public async Task<ActionResult<PlatformUserDto>> Me(
        CancellationToken cancellationToken)
    {
        return Ok(await authService.MeAsync(
            cancellationToken));
    }
}

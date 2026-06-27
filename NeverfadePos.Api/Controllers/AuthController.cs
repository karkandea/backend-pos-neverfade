using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.Services.Auth;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(
        LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponseDto>> Me(
        CancellationToken cancellationToken)
    {
        var result = await authService.MeAsync(
            cancellationToken);

        return Ok(result);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Auth;
using NeverfadePos.Api.Services.Auth;
using NeverfadePos.Api.Services.Mfa;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, MfaService mfaService) : ControllerBase
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
    [HttpGet("mfa/status")]
    public async Task<ActionResult<MfaStatus>> MfaStatus(CancellationToken ct) =>
        Ok(await mfaService.StatusAsync(ct));

    [Authorize]
    [HttpPost("mfa/enroll")]
    public async Task<ActionResult<MfaEnrollment>> EnrollMfa(
        MfaEnrollRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await mfaService.EnrollAsync(request.Password, ct));
    }

    [Authorize]
    [HttpPost("mfa/confirm")]
    public async Task<ActionResult<MfaConfirmation>> ConfirmMfa(
        MfaConfirmRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await mfaService.ConfirmAsync(request.TotpCode, ct));
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

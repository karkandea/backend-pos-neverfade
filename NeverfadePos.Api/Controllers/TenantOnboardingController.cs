using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Onboarding;
using NeverfadePos.Api.Services.Onboarding;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "owner,admin")]
[Route("api/tenant/onboarding")]
public sealed class TenantOnboardingController(ITenantOnboardingService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TenantOnboardingDto>> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.GetAsync(cancellationToken));
    }
}

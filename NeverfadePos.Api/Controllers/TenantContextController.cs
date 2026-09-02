using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Tenant;
using NeverfadePos.Api.Services.Tenant;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tenant/context")]
public sealed class TenantContextController(
    ITenantContextService tenantContextService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TenantContextDto>> Get(
        CancellationToken cancellationToken) =>
        Ok(await tenantContextService.GetAsync(cancellationToken));
}

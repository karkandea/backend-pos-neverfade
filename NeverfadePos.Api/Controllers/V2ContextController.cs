using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.DTOs.Tenant;
using NeverfadePos.Api.Services.Tenant;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v2/context")]
public sealed class V2ContextController(ITenantContextService tenantContextService) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResponseEnvelope<TenantContextDto>>> Get(CancellationToken cancellationToken)
    {
        var data = await tenantContextService.GetAsync(cancellationToken);
        Response.Headers["X-Correlation-Id"] = HttpContext.TraceIdentifier;
        return Ok(new ApiResponseEnvelope<TenantContextDto>
        {
            Data = data,
            Meta = new ApiResponseMeta
            {
                CorrelationId = HttpContext.TraceIdentifier,
                AsOf = DateTime.UtcNow
            }
        });
    }
}

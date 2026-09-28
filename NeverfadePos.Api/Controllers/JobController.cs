using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.DTOs.Job;
using NeverfadePos.Api.Services.Job;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v2/jobs")]
public sealed class JobController(IJobService jobService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResponseEnvelope<JobDto>>> Get(
        Guid id,
        [FromQuery] Guid? outletId,
        CancellationToken cancellationToken)
    {
        var data = await jobService.GetAsync(
            id,
            outletId,
            cancellationToken);
        var correlationId = HttpContext.TraceIdentifier;
        Response.Headers["X-Correlation-Id"] = correlationId;

        return Ok(new ApiResponseEnvelope<JobDto>
        {
            Data = data,
            Meta = new ApiResponseMeta
            {
                CorrelationId = correlationId,
                AsOf = DateTime.UtcNow
            }
        });
    }
}

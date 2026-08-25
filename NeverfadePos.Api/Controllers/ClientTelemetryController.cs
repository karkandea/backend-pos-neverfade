using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Telemetry;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Route("api/client-telemetry")]
public sealed class ClientTelemetryController(
    ILogger<ClientTelemetryController> logger)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login-failure")]
    [RequestSizeLimit(4096)]
    public IActionResult LoginFailure(LoginFailureTelemetryDto request)
    {
        var eventId = request.ClientEventId == Guid.Empty
            ? Guid.NewGuid()
            : request.ClientEventId;
        var occurredAt = request.OccurredAt == default
            ? DateTimeOffset.UtcNow
            : request.OccurredAt;

        logger.LogWarning(
            "CLIENT_LOGIN_FAILURE EventId={EventId} OccurredAt={OccurredAt} ErrorCode={ErrorCode} ErrorName={ErrorName} ErrorMessage={ErrorMessage} HttpStatus={HttpStatus} Online={Online} DurationMs={DurationMs} TargetOrigin={TargetOrigin} Visibility={Visibility} EffectiveConnectionType={EffectiveConnectionType} RttMs={RttMs} DownlinkMbps={DownlinkMbps}",
            eventId,
            occurredAt,
            request.ErrorCode,
            request.ErrorName,
            request.ErrorMessage,
            request.HttpStatus,
            request.Online,
            request.DurationMs,
            request.TargetOrigin,
            request.VisibilityState,
            request.EffectiveConnectionType,
            request.RttMs,
            request.DownlinkMbps);

        return NoContent();
    }
}

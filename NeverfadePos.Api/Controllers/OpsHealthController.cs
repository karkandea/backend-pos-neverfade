using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Common;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Controllers;

// EX02 CORE-15 read-only diagnostics. Does not simulate an outbox or worker.
[ApiController]
[Authorize(Roles = "owner")]
[Route("api/v2/ops")]
public sealed class OpsHealthController(AppDbContext db) : ControllerBase
{
    [HttpGet("health")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResponseEnvelope<OpsHealthDto>>> GetHealth(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fiveMinutesAgo = now.AddMinutes(-5);
        var stalePayments = await db.Payments.AsNoTracking().CountAsync(
            x => (x.Status == PaymentConstants.StatusCreating && x.CreatedAt < fiveMinutesAgo) ||
                 (x.Status == PaymentConstants.StatusPending && x.ExpiresAt < now),
            cancellationToken);
        var unprocessedWebhooks = await db.PaymentWebhookEvents.AsNoTracking().CountAsync(
            x => x.ProcessingStatus != "processed", cancellationToken);
        var staleJobs = await db.Jobs.AsNoTracking().CountAsync(
            x => (x.State == "queued" || x.State == "running") &&
                 x.UpdatedAt < fiveMinutesAgo, cancellationToken);
        var data = new OpsHealthDto
        {
            StalledPayments = stalePayments,
            UnprocessedWebhooks = unprocessedWebhooks,
            StalledJobs = staleJobs,
            NeedsAttention = stalePayments > 0 || unprocessedWebhooks > 0 || staleJobs > 0
        };
        return Ok(Envelope(data, now));
    }

    [HttpGet("alerts")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResponseEnvelope<List<OpsAlertDto>>>> GetAlerts(
        [FromQuery] int limit = 30, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
            throw new TenantApiException(400, "INVALID_ALERT_LIMIT",
                "Batas alert harus antara 1 sampai 100.");
        var records = await db.OpsAlerts.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new OpsAlertDto
            {
                Id = x.Id, SourceKind = x.SourceKind, SourceId = x.SourceId,
                CorrelationId = x.CorrelationId, State = x.State, CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return Ok(Envelope(records, DateTime.UtcNow));
    }

    [HttpGet("audit")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ApiResponseEnvelope<List<OpsAuditItemDto>>>> GetAudit(
        [FromQuery] int limit = 30, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100)
            throw new TenantApiException(400, "INVALID_AUDIT_LIMIT",
                "Batas audit harus antara 1 sampai 100.");
        var events = await db.TenantAuditEvents.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(limit)
            .Select(x => new OpsAuditItemDto
            {
                Id = x.Id,
                EventType = x.EventType,
                ActorUserId = x.ActorUserId,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return Ok(Envelope(events, DateTime.UtcNow));
    }

    private ApiResponseEnvelope<T> Envelope<T>(T data, DateTime asOf)
    {
        var correlationId = HttpContext.TraceIdentifier;
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Correlation-Id"] = correlationId;
        return new ApiResponseEnvelope<T>
        {
            Data = data,
            Meta = new ApiResponseMeta { CorrelationId = correlationId, AsOf = asOf }
        };
    }
}

public sealed class OpsHealthDto
{
    public int StalledPayments { get; set; }
    public int UnprocessedWebhooks { get; set; }
    public int StalledJobs { get; set; }
    public bool NeedsAttention { get; set; }
    // Explicitly unimplemented until CORE-26 outbox/worker development.
    public bool OutboxMetricsAvailable => false;
    public bool WorkerDispatchAvailable => false;
}

public sealed class OpsAlertDto
{
    public Guid Id { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class OpsAuditItemDto
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

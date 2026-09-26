using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.DemoMode;

public sealed class DemoEventRequest
{
    public Guid SessionId { get; set; }
    [Required, MaxLength(40)] public string BusinessType { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string EventName { get; set; } = string.Empty;
    [MaxLength(12)] public string? Mode { get; set; }
    [MaxLength(48)] public string? Step { get; set; }
}

[ApiController]
[Route("api/demo/events")]
public sealed class DemoConversionController(
    AppDbContext db,
    IConfiguration configuration)
    : ControllerBase
{
    private static readonly IReadOnlySet<string> Events = new HashSet<string>(StringComparer.Ordinal)
    {
        "category_selected", "demo_mode_selected", "demo_started",
        "scenario_step_completed", "scenario_completed", "conversion_cta_clicked"
    };

    private static readonly IReadOnlySet<string> Steps = new HashSet<string>(StringComparer.Ordinal)
    {
        "order_opened", "item_added", "sent_to_kitchen", "kitchen_preparing", "kitchen_ready",
        "payment_completed", "order_closed", "work_order_created", "laundry_processing",
        "laundry_ready", "laundry_completed", "product_selected", "sale_completed", "result_viewed",
        "pricing", "contact", "merchant_login"
    };

    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting("demo-events")]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Record(
        DemoEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("DemoMode:Enabled"))
            return NotFound();

        if (request.SessionId == Guid.Empty ||
            !DemoModeDefaults.Profiles.Any(x => x.Key == request.BusinessType) ||
            !Events.Contains(request.EventName) ||
            request.Mode is not (null or "guided" or "free") ||
            (request.Step is not null && !Steps.Contains(request.Step)))
        {
            return BadRequest(new { code = "DEMO_EVENT_INVALID" });
        }

        // Pure event semantics: no arbitrary payload, URL, IP address or PII.
        if ((request.EventName == "scenario_step_completed" && request.Step is null) ||
            (request.EventName == "conversion_cta_clicked" &&
             request.Step is not ("pricing" or "contact" or "merchant_login")))
            return BadRequest(new { code = "DEMO_EVENT_INVALID" });

        db.Set<DemoConversionEvent>().Add(new DemoConversionEvent
        {
            SessionId = request.SessionId,
            BusinessType = request.BusinessType,
            EventName = request.EventName,
            Mode = request.Mode,
            Step = request.Step
        });

        await db.SaveChangesAsync(cancellationToken);
        return Accepted();
    }
}

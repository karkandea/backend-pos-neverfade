using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Telemetry;

public sealed class LoginFailureTelemetryDto
{
    public Guid ClientEventId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    [MaxLength(64)]
    public string ErrorCode { get; set; } = string.Empty;

    [MaxLength(64)]
    public string ErrorName { get; set; } = string.Empty;

    [MaxLength(180)]
    public string ErrorMessage { get; set; } = string.Empty;

    [Range(100, 599)]
    public int? HttpStatus { get; set; }

    public bool Online { get; set; }

    [Range(0, 120000)]
    public int DurationMs { get; set; }

    [MaxLength(256)]
    public string TargetOrigin { get; set; } = string.Empty;

    [MaxLength(32)]
    public string VisibilityState { get; set; } = string.Empty;

    [MaxLength(32)]
    public string EffectiveConnectionType { get; set; } = string.Empty;

    [Range(0, 100000)]
    public int? RttMs { get; set; }

    [Range(0, 10000)]
    public double? DownlinkMbps { get; set; }
}

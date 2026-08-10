namespace NeverfadePos.Api.Entities;

public sealed class PlatformAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ActorPlatformUserId { get; set; }

    public Guid TenantId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? Metadata { get; set; }

    public PlatformUser? ActorPlatformUser { get; set; }

    public Tenant? Tenant { get; set; }
}

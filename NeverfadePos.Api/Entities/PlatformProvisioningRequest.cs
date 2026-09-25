namespace NeverfadePos.Api.Entities;

// Platform-scoped idempotency receipt. Never stores the request body or owner password.
public sealed class PlatformProvisioningRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorPlatformUserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

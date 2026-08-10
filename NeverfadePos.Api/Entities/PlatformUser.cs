namespace NeverfadePos.Api.Entities;

public sealed class PlatformUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Nama { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "superadmin";

    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlatformAuditEvent> AuditEvents { get; set; } =
        new List<PlatformAuditEvent>();
}

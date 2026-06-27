namespace NeverfadePos.Api.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
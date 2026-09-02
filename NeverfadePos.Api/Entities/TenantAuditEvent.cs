using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class TenantAuditEvent : BaseEntity
{
    public Guid? ActorUserId { get; set; }

    public Guid? ActorKaryawanId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? Metadata { get; set; }

    public Tenant? Tenant { get; set; }

    public User? ActorUser { get; set; }

    public Karyawan? ActorKaryawan { get; set; }
}

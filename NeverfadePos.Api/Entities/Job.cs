using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class Job : BaseEntity
{
    public Guid? OutletId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string State { get; set; } = "queued";
    public Guid? ResultReference { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

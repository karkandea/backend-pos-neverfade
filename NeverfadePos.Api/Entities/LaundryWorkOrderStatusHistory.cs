using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class LaundryWorkOrderStatusHistory : BaseEntity
{
    public Guid LaundryWorkOrderId { get; set; }
    public Guid ActorUserId { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }
    public LaundryWorkOrder? LaundryWorkOrder { get; set; }
    public User? ActorUser { get; set; }
}

using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class AttendancePolicy : BaseEntity
{
    public int GraceMinutes { get; set; } = 10;

    public int AbsenceThresholdMinutes { get; set; } = 120;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }
}

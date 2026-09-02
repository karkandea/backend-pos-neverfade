using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class EmployeeScheduleException : BaseEntity
{
    public Guid KaryawanId { get; set; }

    public DateOnly Date { get; set; }

    public string Type { get; set; } = "off";

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? Note { get; set; }

    public Tenant? Tenant { get; set; }

    public Karyawan? Karyawan { get; set; }
}

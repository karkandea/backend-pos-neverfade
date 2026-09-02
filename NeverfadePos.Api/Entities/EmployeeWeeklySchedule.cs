using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class EmployeeWeeklySchedule : BaseEntity
{
    public Guid KaryawanId { get; set; }

    public int DayOfWeek { get; set; }

    public bool IsWorkingDay { get; set; } = true;

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public Tenant? Tenant { get; set; }

    public Karyawan? Karyawan { get; set; }
}

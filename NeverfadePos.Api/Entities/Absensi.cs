using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public class Absensi : BaseEntity
{
    public Guid KaryawanId { get; set; }

    public DateOnly Tanggal { get; set; }

    // Legacy contract fields retained for backward-compatible API projection.
    public TimeOnly? CheckIn { get; set; }

    public TimeOnly? CheckOut { get; set; }

    public DateTime? CheckInAtUtc { get; set; }

    public DateTime? CheckOutAtUtc { get; set; }

    public bool OutsideSchedule { get; set; }

    public Tenant? Tenant { get; set; }

    public Karyawan? Karyawan { get; set; }

    public ICollection<AttendanceCorrection> Corrections { get; set; } = new List<AttendanceCorrection>();
}

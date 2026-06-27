using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public class Absensi : BaseEntity
{
    public Guid KaryawanId { get; set; }

    public DateOnly Tanggal { get; set; }

    public TimeOnly? CheckIn { get; set; }

    public TimeOnly? CheckOut { get; set; }

    public Tenant? Tenant { get; set; }

    public Karyawan? Karyawan { get; set; }
}

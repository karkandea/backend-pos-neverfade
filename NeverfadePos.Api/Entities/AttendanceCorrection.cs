using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class AttendanceCorrection : BaseEntity
{
    public Guid AbsensiId { get; set; }

    public Guid CorrectedByUserId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string BeforeData { get; set; } = string.Empty;

    public string AfterData { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }

    public Absensi? Absensi { get; set; }

    public User? CorrectedByUser { get; set; }
}

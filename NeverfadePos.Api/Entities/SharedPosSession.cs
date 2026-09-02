using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class SharedPosSession : BaseEntity
{
    public Guid DeviceId { get; set; }

    public Guid KaryawanId { get; set; }

    public Guid? UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Tenant? Tenant { get; set; }

    public SharedPosDevice? Device { get; set; }

    public Karyawan? Karyawan { get; set; }

    public User? User { get; set; }
}

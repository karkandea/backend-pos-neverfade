using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class SharedPosDevice : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public Guid? CreatedByUserId { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public int FailedUnlockCount { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    public Tenant? Tenant { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<SharedPosSession> Sessions { get; set; } = new List<SharedPosSession>();
}

using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

/// <summary>Explicit tenant-scoped user assignment to one active outlet.</summary>
public sealed class UserOutletAssignment : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid OutletId { get; set; }
    public User? User { get; set; }
    public Outlet? Outlet { get; set; }
}

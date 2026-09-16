using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class WhatsAppSender : BaseEntity
{
    public Guid OutletId { get; set; }

    public string Provider { get; set; } = "waha";

    public string SessionName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string LastStatus { get; set; } = "NOT_CONFIGURED";

    public bool IsDefault { get; set; } = true;

    public bool Active { get; set; } = true;

    public DateTime? LastConnectedAt { get; set; }

    public Tenant? Tenant { get; set; }

    public Outlet? Outlet { get; set; }
}

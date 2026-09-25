using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public sealed class Outlet : BaseEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public bool Active { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ICollection<UserOutletAssignment> UserAssignments { get; set; } = new List<UserOutletAssignment>();


    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public ICollection<WhatsAppSender> WhatsAppSenders { get; set; } = new List<WhatsAppSender>();
}

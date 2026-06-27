using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public class User : BaseEntity
{
    public string Nama { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool Active { get; set; } = true;

    public Tenant? Tenant { get; set; }
}

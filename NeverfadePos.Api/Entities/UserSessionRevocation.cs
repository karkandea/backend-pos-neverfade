using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

/// <summary>Durable replay ledger; never reuse a key for another user.</summary>
public sealed class UserSessionRevocation : BaseEntity
{
    public Guid UserId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

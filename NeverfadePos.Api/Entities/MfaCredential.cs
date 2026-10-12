using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

/// <summary>Per-user MFA state. The authenticator secret is never stored in plaintext.</summary>
public sealed class MfaCredential : BaseEntity
{
    public Guid UserId { get; set; }
    public string ProtectedSecret { get; set; } = string.Empty;
    public DateTime EnrollmentExpiresAtUtc { get; set; }
    public DateTime? EnabledAtUtc { get; set; }
    public long LastUsedStep { get; set; } = -1;
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public string RecoveryCodeHashesJson { get; set; } = "[]";
}

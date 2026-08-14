namespace NeverfadePos.Api.Entities;

public sealed class WithdrawalRoute
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public Guid WithdrawalRequestId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Tenant? Tenant { get; set; }

    public WithdrawalRequest? WithdrawalRequest { get; set; }
}

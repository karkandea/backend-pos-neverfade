namespace NeverfadePos.Api.DTOs.Finance;

public sealed class PlatformWithdrawalDto : WithdrawalDto
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public Guid RequestedByUserId { get; set; }

    public string RequestedByName { get; set; } = string.Empty;

    public string RequestedByUsername { get; set; } = string.Empty;
}

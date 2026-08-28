namespace NeverfadePos.Api.Payments;

public sealed class PaymentModeOptions
{
    public string Mode { get; set; } = "Disabled";

    public string SandboxAllowedTenantIds { get; set; } = string.Empty;

    public bool LiveEnabled { get; set; }

    public string LiveAllowedTenantIds { get; set; } = string.Empty;
}

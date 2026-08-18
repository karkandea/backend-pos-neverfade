namespace NeverfadePos.Api.DTOs.Payment;

public sealed class PaymentCapabilitiesDto
{
    public bool QrisEnabled { get; set; }

    public string Mode { get; set; } = string.Empty;

    public bool IsSandbox { get; set; }
}

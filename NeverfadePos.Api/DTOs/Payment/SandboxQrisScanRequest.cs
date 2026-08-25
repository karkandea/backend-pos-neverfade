using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Payment;

public sealed class SandboxQrisScanRequest
{
    [Required]
    [MaxLength(4096)]
    public string QrString { get; set; } = string.Empty;
}

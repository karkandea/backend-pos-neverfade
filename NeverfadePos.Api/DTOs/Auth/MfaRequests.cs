using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Auth;

public sealed class MfaEnrollRequest
{
    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class MfaConfirmRequest
{
    [Required]
    [RegularExpression(@"^[0-9]{6}$")]
    public string TotpCode { get; set; } = string.Empty;
}

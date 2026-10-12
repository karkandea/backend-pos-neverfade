using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.Auth;

public sealed class LoginRequestDto
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(6)]
    public string? TotpCode { get; set; }

    [MaxLength(20)]
    public string? RecoveryCode { get; set; }
}

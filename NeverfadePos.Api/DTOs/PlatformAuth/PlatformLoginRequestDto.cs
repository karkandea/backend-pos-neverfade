using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.PlatformAuth;

public sealed class PlatformLoginRequestDto
{
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}

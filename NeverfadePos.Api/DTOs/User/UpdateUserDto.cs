using System.ComponentModel.DataAnnotations;

namespace NeverfadePos.Api.DTOs.User;

public sealed class UpdateUserDto
{
    [Required]
    [MaxLength(200)]
    public string Nama { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    public bool Active { get; set; }

    public string? Password { get; set; }
}

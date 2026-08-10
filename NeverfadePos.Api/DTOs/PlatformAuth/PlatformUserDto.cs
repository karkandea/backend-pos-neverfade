namespace NeverfadePos.Api.DTOs.PlatformAuth;

public sealed class PlatformUserDto
{
    public Guid Id { get; set; }

    public string Nama { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}

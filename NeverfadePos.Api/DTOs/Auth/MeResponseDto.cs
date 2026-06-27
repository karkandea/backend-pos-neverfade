namespace NeverfadePos.Api.DTOs.Auth;

public sealed class MeResponseDto
{
    public Guid Id { get; set; }

    public string Nama { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}

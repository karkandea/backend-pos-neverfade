namespace NeverfadePos.Api.DTOs.Auth;

public sealed class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;

    public LoginUserDto User { get; set; } = new();
}

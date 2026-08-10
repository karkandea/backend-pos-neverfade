namespace NeverfadePos.Api.DTOs.PlatformAuth;

public sealed class PlatformLoginResponseDto
{
    public string Token { get; set; } = string.Empty;

    public PlatformUserDto User { get; set; } = new();
}

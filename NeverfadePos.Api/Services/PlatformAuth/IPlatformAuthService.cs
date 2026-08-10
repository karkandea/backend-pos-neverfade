using NeverfadePos.Api.DTOs.PlatformAuth;

namespace NeverfadePos.Api.Services.PlatformAuth;

public interface IPlatformAuthService
{
    Task<PlatformLoginResponseDto> LoginAsync(
        PlatformLoginRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PlatformUserDto> MeAsync(
        CancellationToken cancellationToken = default);
}

using NeverfadePos.Api.DTOs.Auth;

namespace NeverfadePos.Api.Services.Auth;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default);

    Task<MeResponseDto> MeAsync(
        CancellationToken cancellationToken = default);
}

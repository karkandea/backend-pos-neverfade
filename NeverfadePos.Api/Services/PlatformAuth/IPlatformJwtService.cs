using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.PlatformAuth;

public interface IPlatformJwtService
{
    string GenerateToken(PlatformUser user);
}

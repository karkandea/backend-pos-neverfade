using NeverfadePos.Api.Entities;

namespace NeverfadePos.Api.Services.Auth;

public interface IJwtService
{
    string GenerateToken(User user);
}

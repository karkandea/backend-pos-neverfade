using System.Security.Claims;

namespace NeverfadePos.Api.Auth;

public sealed class PlatformCurrentUser(
    IHttpContextAccessor httpContextAccessor)
{
    public Guid? UserId =>
        GetGuid(ClaimTypes.NameIdentifier) ??
        GetGuid("sub");

    public string? Scope =>
        GetString(PlatformAuthConstants.ScopeClaim);

    public string? Role =>
        GetString(ClaimTypes.Role) ??
        GetString("role");

    private string? GetString(string claimType)
    {
        return httpContextAccessor
            .HttpContext?
            .User?
            .FindFirst(claimType)?
            .Value;
    }

    private Guid? GetGuid(string claimType)
    {
        return Guid.TryParse(
            GetString(claimType),
            out var value)
            ? value
            : null;
    }
}

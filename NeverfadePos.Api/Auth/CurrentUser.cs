using System.Security.Claims;

namespace NeverfadePos.Api.Auth;

public class CurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId =>
        GetGuid(ClaimTypes.NameIdentifier) ??
        GetGuid("sub");

    public Guid? TenantId =>
        GetGuid("tenant_id");

    public string? Username =>
        GetString("username");

    public string? Nama =>
        GetString("nama");

    public string? Role =>
        GetString(ClaimTypes.Role) ??
        GetString("role");

    private string? GetString(string claimType)
    {
        return _httpContextAccessor
            .HttpContext?
            .User?
            .FindFirst(claimType)?
            .Value;
    }

    private Guid? GetGuid(string claimType)
    {
        var value = GetString(claimType);

        return Guid.TryParse(value, out var guid)
            ? guid
            : null;
    }
}
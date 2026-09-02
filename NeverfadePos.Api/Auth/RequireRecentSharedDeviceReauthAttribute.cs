using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NeverfadePos.Api.Auth;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRecentSharedDeviceReauthAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var sessionKind = user.FindFirst("session_kind")?.Value;

        if (!string.Equals(sessionKind, "shared_pos", StringComparison.Ordinal))
        {
            return;
        }

        var raw = user.FindFirst("shared_reauth_until")?.Value;
        if (long.TryParse(raw, out var unix) &&
            DateTimeOffset.FromUnixTimeSeconds(unix) > DateTimeOffset.UtcNow)
        {
            return;
        }

        context.Result = new ObjectResult(new
        {
            code = "SHARED_DEVICE_REAUTH_REQUIRED",
            message = "Masukkan PIN owner/admin lagi untuk melanjutkan tindakan sensitif ini."
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}

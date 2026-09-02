using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.Middleware;

public sealed class SharedPosSessionMiddleware(RequestDelegate next)
{
    private static readonly string[] SensitivePathPrefixes =
    [
        "/api/finance",
        "/api/users",
        "/api/settings",
        "/api/karyawan",
        "/api/attendance",
        "/api/shared-pos/devices"
    ];

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (!string.Equals(
                context.User.FindFirst("session_kind")?.Value,
                "shared_pos",
                StringComparison.Ordinal))
        {
            await next(context);
            return;
        }

        var sessionRaw = context.User.FindFirst("shared_session_id")?.Value;
        var employeeRaw = context.User.FindFirst("employee_id")?.Value;
        var userRaw = context.User.FindFirst("sub")?.Value ??
                      context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(sessionRaw, out var sessionId) ||
            !Guid.TryParse(employeeRaw, out var employeeId) ||
            !Guid.TryParse(userRaw, out var userId))
        {
            await RejectAsync(context);
            return;
        }

        var now = DateTime.UtcNow;
        var valid = await db.SharedPosSessions
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == sessionId &&
                x.KaryawanId == employeeId &&
                x.UserId == userId &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > now &&
                x.Device != null && x.Device.Active &&
                x.Karyawan != null && x.Karyawan.Status == "aktif" &&
                x.User != null && x.User.Active,
                context.RequestAborted);

        if (!valid)
        {
            await RejectAsync(context);
            return;
        }

        if (RequiresRecentReauth(context.Request.Path) &&
            !HasRecentReauth(context.User, now))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                code = "SHARED_DEVICE_REAUTH_REQUIRED",
                message = "Masukkan PIN owner/admin lagi untuk melanjutkan tindakan sensitif ini."
            });
            return;
        }

        await next(context);
    }

    private static bool RequiresRecentReauth(PathString path)
    {
        return SensitivePathPrefixes.Any(prefix =>
            path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasRecentReauth(System.Security.Claims.ClaimsPrincipal user, DateTime now)
    {
        var raw = user.FindFirst("shared_reauth_until")?.Value;
        return long.TryParse(raw, out var unix) &&
               DateTimeOffset.FromUnixTimeSeconds(unix) > new DateTimeOffset(now);
    }

    private static async Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            code = "SHARED_SESSION_INVALID",
            message = "Sesi shared POS sudah tidak aktif. Masukkan PIN lagi."
        });
    }
}

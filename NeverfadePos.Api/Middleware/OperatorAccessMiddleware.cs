using NeverfadePos.Api.Auth;

namespace NeverfadePos.Api.Middleware;

/// <summary>
/// Fail-closed route boundary for restricted staff JWTs. Endpoint capability,
/// tenant and outlet checks still apply after this early role gate.
/// </summary>
public sealed class OperatorAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var role = context.RequestServices.GetRequiredService<CurrentUser>().Role;
        if (context.User.Identity?.IsAuthenticated != true ||
            (role != "dapur" && role != "laundry_operator") ||
            (role == "dapur" && IsKitchenRoute(context.Request)) ||
            (role == "laundry_operator" && IsLaundryRoute(context.Request)))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            code = "OPERATOR_SCOPE_FORBIDDEN",
            message = "Akun operator hanya dapat mengakses antrean pekerjaan dan outlet tugasnya."
        });
    }

    private static bool IsLaundryRoute(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (HttpMethods.IsGet(request.Method))
            return path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/tenant/context", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/outlets", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/laundry/operator", StringComparison.OrdinalIgnoreCase) ||
                (path.StartsWith("/api/laundry/operator/", StringComparison.OrdinalIgnoreCase) &&
                 Guid.TryParse(path["/api/laundry/operator/".Length..], out _));

        if (!HttpMethods.IsPost(request.Method)) return false;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 5 &&
            segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("laundry", StringComparison.OrdinalIgnoreCase) &&
            segments[2].Equals("operator", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(segments[3], out _) &&
            segments[4].Equals("status", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsKitchenRoute(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;
        if (HttpMethods.IsGet(request.Method))
            return path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/tenant/context", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/outlets", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/api/restaurant/kitchen/operator", StringComparison.OrdinalIgnoreCase);

        if (!HttpMethods.IsPost(request.Method)) return false;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 7 &&
            segments[0].Equals("api", StringComparison.OrdinalIgnoreCase) &&
            segments[1].Equals("restaurant", StringComparison.OrdinalIgnoreCase) &&
            segments[2].Equals("kitchen", StringComparison.OrdinalIgnoreCase) &&
            segments[3].Equals("operator", StringComparison.OrdinalIgnoreCase) &&
            segments[4].Equals("items", StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(segments[5], out _) &&
            segments[6].Equals("status", StringComparison.OrdinalIgnoreCase);
    }
}

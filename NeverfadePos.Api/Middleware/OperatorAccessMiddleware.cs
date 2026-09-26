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
        if (context.User.Identity?.IsAuthenticated != true ||
            context.RequestServices.GetRequiredService<CurrentUser>().Role != "dapur" ||
            IsKitchenRoute(context.Request))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            code = "OPERATOR_SCOPE_FORBIDDEN",
            message = "Akun dapur hanya dapat mengakses antrean dapur dan outlet tugasnya."
        });
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

using NeverfadePos.Api.DemoMode;

namespace NeverfadePos.Api.Middleware;

public sealed class DemoSafetyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public DemoSafetyMiddleware(
        RequestDelegate next,
        IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_configuration.GetValue<bool>("DemoMode:Enabled") ||
            context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(
                context.User.FindFirst("tenant_id")?.Value,
                out var tenantId) ||
            !DemoModeDefaults.IsDemoTenant(tenantId))
        {
            await _next(context);
            return;
        }

        if (IsReadRequest(context.Request) ||
            IsAllowedDemoMutation(context.Request))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            code = "DEMO_READ_ONLY",
            message = "Data master pada mode demo hanya dapat dilihat. Silakan coba alur transaksi bisnis yang tersedia."
        });
    }

    private static bool IsReadRequest(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) ||
        HttpMethods.IsHead(request.Method) ||
        HttpMethods.IsOptions(request.Method);

    private static bool IsAllowedDemoMutation(HttpRequest request)
    {
        if (HttpMethods.IsPost(request.Method) &&
            request.Path.Equals(
                "/api/transactions",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Path.StartsWithSegments(
                "/api/restaurant/orders",
                StringComparison.OrdinalIgnoreCase) ||
            request.Path.StartsWithSegments(
                "/api/restaurant/kitchen",
                StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsPost(request.Method) ||
                   HttpMethods.IsPut(request.Method) ||
                   HttpMethods.IsDelete(request.Method);
        }

        if (request.Path.StartsWithSegments(
                "/api/laundry/work-orders",
                StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsPost(request.Method);
        }

        return false;
    }
}

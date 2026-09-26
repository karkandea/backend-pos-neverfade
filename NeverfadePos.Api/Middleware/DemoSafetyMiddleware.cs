using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
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

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (!_configuration.GetValue<bool>("DemoMode:Enabled"))
        {
            await _next(context);
            return;
        }

        // Public session renewal and allowlisted telemetry must work even if an
        // old browser sends an expired or pre-isolation bearer token.
        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.Equals("/api/demo/session", StringComparison.OrdinalIgnoreCase))
        {
            // This public endpoint establishes a new isolated tenant, even when an
            // existing browser sends its old bearer token. Treat it as anonymous.
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            await _next(context);
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method) &&
            context.Request.Path.Equals("/api/demo/events", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var tenantId) ||
            tenantId == Guid.Empty)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // Reserved shared demo tenants are never allowed to serve public traffic.
        // Every accepted user token must belong to an unexpired visitor tenant.
        var active = !DemoModeDefaults.IsDemoTenant(tenantId) &&
            await db.DemoVisitorSessions.AsNoTracking().AnyAsync(x =>
                x.TenantId == tenantId && x.ExpiresAt > DateTime.UtcNow,
                context.RequestAborted);

        if (!active)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                code = "DEMO_SESSION_EXPIRED",
                message = "Sesi demo berakhir. Pilih kembali jenis bisnis."
            });
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
                "/api/demo/events",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (HttpMethods.IsPost(request.Method) &&
            request.Path.Equals(
                "/api/demo/session",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

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

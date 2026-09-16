using NeverfadePos.Api.DemoMode;

namespace NeverfadePos.Api.Middleware;

public sealed class DemoSafetyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public DemoSafetyMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime applicationLifetime,
        ILoggerFactory loggerFactory)
    {
        _next = next;
        _configuration = configuration;

        DemoResetScheduler.Start(
            scopeFactory,
            configuration,
            applicationLifetime,
            loggerFactory);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_configuration.GetValue<bool>("DemoMode:Enabled") ||
            context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(
                context.User.FindFirst("tenant_id")?.Value,
                out var tenantId) ||
            tenantId != DemoModeDefaults.TenantId)
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
            message = "Data master pada mode demo hanya dapat dilihat. Silakan coba transaksi melalui Kasir."
        });
    }

    private static bool IsReadRequest(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) ||
        HttpMethods.IsHead(request.Method) ||
        HttpMethods.IsOptions(request.Method);

    private static bool IsAllowedDemoMutation(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) &&
        request.Path.Equals(
            "/api/transactions",
            StringComparison.OrdinalIgnoreCase);
}

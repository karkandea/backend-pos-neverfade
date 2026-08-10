using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;

namespace NeverfadePos.Api.Middleware;

public sealed class TenantStatusMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.HasClaim("scope", "tenant") &&
            TryGetTenantId(context.User, out var tenantId))
        {
            var status = await db.Tenants
                .AsNoTracking()
                .Where(x => x.Id == tenantId)
                .Select(x => x.Status)
                .FirstOrDefaultAsync(context.RequestAborted);

            if (status == "suspended")
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "TENANT_SUSPENDED",
                    message = "Tenant sedang ditangguhkan."
                });
                return;
            }

            if (status is null)
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await next(context);
    }

    private static bool TryGetTenantId(
        ClaimsPrincipal principal,
        out Guid tenantId) =>
        Guid.TryParse(
            principal.FindFirst("tenant_id")?.Value,
            out tenantId) &&
        tenantId != Guid.Empty;
}

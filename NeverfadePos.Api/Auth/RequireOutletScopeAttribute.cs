using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NeverfadePos.Api.Services.Outlet;

namespace NeverfadePos.Api.Auth;

/// <summary>Resolve the requested outlet server-side before entering operational endpoints.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireOutletScopeAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var raw = context.HttpContext.Request.Headers["X-Outlet-Id"].ToString();
        Guid? outletId = null;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (!Guid.TryParse(raw, out var parsed) || parsed == Guid.Empty)
            {
                context.Result = new BadRequestObjectResult(new
                {
                    code = "INVALID_OUTLET_ID", message = "Outlet yang dipilih tidak valid."
                });
                return;
            }
            outletId = parsed;
        }

        var outletService = context.HttpContext.RequestServices.GetRequiredService<IOutletService>();
        var outlet = await outletService.ResolveAsync(outletId, context.HttpContext.RequestAborted);
        var scope = context.HttpContext.RequestServices.GetRequiredService<IOutletExecutionScope>();
        using (scope.Begin(outlet.Id))
            await next();
    }
}

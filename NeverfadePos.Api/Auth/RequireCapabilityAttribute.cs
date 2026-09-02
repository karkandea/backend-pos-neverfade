using Microsoft.AspNetCore.Mvc.Filters;
using NeverfadePos.Api.Services.Tenant;

namespace NeverfadePos.Api.Auth;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class RequireCapabilityAttribute(string capability)
    : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(
        AuthorizationFilterContext context)
    {
        var service = context.HttpContext.RequestServices
            .GetRequiredService<ITenantCapabilityService>();

        await service.RequireAsync(
            capability,
            context.HttpContext.RequestAborted);
    }
}

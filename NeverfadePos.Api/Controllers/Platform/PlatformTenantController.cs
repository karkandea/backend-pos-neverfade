using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.DTOs.PlatformTenant;
using NeverfadePos.Api.Services.PlatformTenant;

namespace NeverfadePos.Api.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants")]
[Authorize(
    AuthenticationSchemes = PlatformAuthConstants.AuthenticationScheme,
    Policy = PlatformAuthConstants.AuthorizationPolicy)]
public sealed class PlatformTenantController(
    IPlatformTenantService tenantService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlatformTenantDto>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await tenantService.GetAllAsync(cancellationToken));

    [HttpGet("{tenantId:guid}")]
    public async Task<ActionResult<PlatformTenantDto>> GetById(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        Ok(await tenantService.GetByIdAsync(
            tenantId,
            cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PlatformTenantDto>> Create(
        CreatePlatformTenantRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await tenantService.CreateAsync(
            request,
            cancellationToken));

    [HttpPut("{tenantId:guid}/business-profile")]
    public async Task<ActionResult<PlatformTenantDto>> UpdateBusinessProfile(
        Guid tenantId,
        UpdateTenantBusinessProfileRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await tenantService.UpdateBusinessProfileAsync(
            tenantId,
            request,
            cancellationToken));

    [HttpPost("{tenantId:guid}/activate")]
    public async Task<ActionResult<PlatformTenantDto>> Activate(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        Ok(await tenantService.ActivateAsync(
            tenantId,
            cancellationToken));

    [HttpPost("{tenantId:guid}/suspend")]
    public async Task<ActionResult<PlatformTenantDto>> Suspend(
        Guid tenantId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)]
        SuspendPlatformTenantRequestDto? request,
        CancellationToken cancellationToken) =>
        Ok(await tenantService.SuspendAsync(
            tenantId,
            request,
            cancellationToken));
}

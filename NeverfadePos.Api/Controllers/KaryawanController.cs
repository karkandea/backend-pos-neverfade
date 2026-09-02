using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.DTOs.Karyawan;
using NeverfadePos.Api.DTOs.SharedPos;
using NeverfadePos.Api.Services.Karyawan;
using NeverfadePos.Api.Services.SharedPos;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "owner,admin")]
[RequireRecentSharedDeviceReauth]
[Route("api/karyawan")]
public sealed class KaryawanController(
    IKaryawanService karyawanService,
    IEmployeeSharedAccessService employeeSharedAccessService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<KaryawanDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        return Ok(await karyawanService.GetAllAsync(search, status, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KaryawanDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await karyawanService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<KaryawanDto>> Create(CreateKaryawanDto request, CancellationToken cancellationToken)
    {
        return Ok(await karyawanService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KaryawanDto>> Update(Guid id, UpdateKaryawanDto request, CancellationToken cancellationToken)
    {
        return Ok(await karyawanService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await karyawanService.DeleteAsync(id, cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpGet("{id:guid}/shared-access")]
    public async Task<ActionResult<EmployeeSharedAccessDto>> GetSharedAccess(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await employeeSharedAccessService.GetAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}/shared-access")]
    public async Task<ActionResult<EmployeeSharedAccessDto>> UpdateSharedAccess(
        Guid id,
        UpdateEmployeeSharedAccessRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeSharedAccessService.UpdateAsync(id, request, cancellationToken));
    }
}

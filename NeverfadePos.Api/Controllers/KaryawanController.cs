using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Karyawan;
using NeverfadePos.Api.Services.Karyawan;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "owner,admin")]
[Route("api/karyawan")]
public sealed class KaryawanController(
    IKaryawanService karyawanService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<KaryawanDto>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        return Ok(await karyawanService.GetAllAsync(
            search,
            status,
            cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KaryawanDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await karyawanService.GetByIdAsync(
            id,
            cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<KaryawanDto>> Create(
        CreateKaryawanDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await karyawanService.CreateAsync(
            request,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KaryawanDto>> Update(
        Guid id,
        UpdateKaryawanDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await karyawanService.UpdateAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await karyawanService.DeleteAsync(
            id,
            cancellationToken);

        return Ok(new { ok = true });
    }
}

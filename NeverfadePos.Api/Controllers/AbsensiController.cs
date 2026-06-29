using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Absensi;
using NeverfadePos.Api.Services.Absensi;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "owner,admin")]
[Route("api/absensi")]
public sealed class AbsensiController(
    IAbsensiService absensiService)
    : ControllerBase
{
    [HttpPost("checkin")]
    public async Task<ActionResult<AbsensiResultDto>> CheckIn(
        CreateAbsensiDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await absensiService.CheckInAsync(
            request,
            cancellationToken));
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<AbsensiResultDto>> CheckOut(
        CreateAbsensiDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await absensiService.CheckOutAsync(
            request,
            cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<List<AbsensiDto>>> GetAll(
        [FromQuery] Guid? karyawanId,
        [FromQuery] DateOnly? tanggal,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        return Ok(await absensiService.GetAllAsync(
            karyawanId,
            tanggal,
            startDate,
            endDate,
            cancellationToken));
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.DTOs.Laporan;
using NeverfadePos.Api.Services.Laporan;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/laporan")]
public sealed class LaporanController(
    ILaporanService laporanService)
    : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<LaporanSummaryDto>> Summary(
        [FromQuery] string period = "harian",
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateDateRange(startDate, endDate);
        if (error is not null) return BadRequest(new { message = error });
        return Ok(await laporanService.GetSummaryAsync(
            period,
            cancellationToken,
            startDate,
            endDate));
    }

    [HttpGet("chart")]
    public async Task<ActionResult<List<LaporanChartDto>>> Chart(
        [FromQuery] string period = "mingguan",
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateDateRange(startDate, endDate);
        if (error is not null) return BadRequest(new { message = error });
        return Ok(await laporanService.GetChartAsync(
            period,
            cancellationToken,
            startDate,
            endDate));
    }

    [HttpGet("top-products")]
    public async Task<ActionResult<List<TopProductDto>>> TopProducts(
        [FromQuery] string period = "harian",
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateDateRange(startDate, endDate);
        if (error is not null) return BadRequest(new { message = error });
        return Ok(await laporanService.GetTopProductsAsync(
            period,
            cancellationToken,
            startDate,
            endDate));
    }

    private static string? ValidateDateRange(DateOnly? startDate, DateOnly? endDate)
    {
        if (startDate.HasValue != endDate.HasValue)
            return "Isi tanggal mulai dan tanggal selesai.";
        if (!startDate.HasValue) return null;
        if (startDate.Value > endDate!.Value)
            return "Tanggal mulai tidak boleh setelah tanggal selesai.";
        if (endDate.Value.DayNumber - startDate.Value.DayNumber > 365)
            return "Rentang laporan maksimal 366 hari.";
        return null;
    }
}

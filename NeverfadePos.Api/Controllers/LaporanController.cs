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
        CancellationToken cancellationToken = default)
    {
        return Ok(await laporanService.GetSummaryAsync(
            period,
            cancellationToken));
    }

    [HttpGet("chart")]
    public async Task<ActionResult<List<LaporanChartDto>>> Chart(
        CancellationToken cancellationToken = default)
    {
        return Ok(await laporanService.GetChartAsync(
            cancellationToken));
    }

    [HttpGet("top-products")]
    public async Task<ActionResult<List<TopProductDto>>> TopProducts(
        [FromQuery] string period = "harian",
        CancellationToken cancellationToken = default)
    {
        return Ok(await laporanService.GetTopProductsAsync(
            period,
            cancellationToken));
    }
}

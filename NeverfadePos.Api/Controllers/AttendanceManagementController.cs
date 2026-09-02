using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NeverfadePos.Api.Auth;
using NeverfadePos.Api.BusinessModes;
using NeverfadePos.Api.DTOs.Attendance;
using NeverfadePos.Api.Services.Attendance;

namespace NeverfadePos.Api.Controllers;

[ApiController]
[Authorize(Roles = "owner,admin")]
[RequireCapability(TenantCapabilities.Attendance)]
[RequireRecentSharedDeviceReauth]
[Route("api/attendance")]
public sealed class AttendanceManagementController(
    IAttendanceManagementService attendanceManagementService)
    : ControllerBase
{
    private static readonly TimeZoneInfo Wib = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Jakarta");

    [HttpGet("policy")]
    public async Task<ActionResult<AttendancePolicyDto>> GetPolicy(CancellationToken cancellationToken)
    {
        return Ok(await attendanceManagementService.GetPolicyAsync(cancellationToken));
    }

    [HttpPut("policy")]
    public async Task<ActionResult<AttendancePolicyDto>> UpdatePolicy(
        UpdateAttendancePolicyRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await attendanceManagementService.UpdatePolicyAsync(request, cancellationToken));
    }

    [HttpGet("employees/{karyawanId:guid}/schedule")]
    public async Task<ActionResult<List<WeeklyScheduleDayDto>>> GetWeeklySchedule(
        Guid karyawanId,
        CancellationToken cancellationToken)
    {
        return Ok(await attendanceManagementService.GetWeeklyScheduleAsync(karyawanId, cancellationToken));
    }

    [HttpPut("employees/{karyawanId:guid}/schedule")]
    public async Task<ActionResult<List<WeeklyScheduleDayDto>>> ReplaceWeeklySchedule(
        Guid karyawanId,
        ReplaceWeeklyScheduleRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await attendanceManagementService.ReplaceWeeklyScheduleAsync(karyawanId, request, cancellationToken));
    }

    [HttpGet("exceptions")]
    public async Task<ActionResult<List<ScheduleExceptionDto>>> GetExceptions(
        [FromQuery] Guid? karyawanId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        return Ok(await attendanceManagementService.GetExceptionsAsync(karyawanId, from, to, cancellationToken));
    }

    [HttpPut("exceptions")]
    public async Task<ActionResult<ScheduleExceptionDto>> UpsertException(
        UpsertScheduleExceptionRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await attendanceManagementService.UpsertExceptionAsync(request, cancellationToken));
    }

    [HttpDelete("exceptions/{id:guid}")]
    public async Task<IActionResult> DeleteException(Guid id, CancellationToken cancellationToken)
    {
        await attendanceManagementService.DeleteExceptionAsync(id, cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AttendanceDashboardDto>> GetDashboard(
        [FromQuery] DateOnly? date,
        [FromQuery] Guid? karyawanId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Wib));
        return Ok(await attendanceManagementService.GetDashboardAsync(
            selectedDate,
            karyawanId,
            status,
            cancellationToken));
    }

    [HttpPost("corrections")]
    public async Task<ActionResult<AttendanceCorrectionDto>> Correct(
        CorrectAttendanceRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await attendanceManagementService.CorrectAsync(request, cancellationToken));
    }
}

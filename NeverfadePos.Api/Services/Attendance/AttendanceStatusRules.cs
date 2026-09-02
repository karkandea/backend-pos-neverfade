using NeverfadePos.Api.DTOs.SharedPos;
using NeverfadePos.Api.Entities;
using AbsensiEntity = NeverfadePos.Api.Entities.Absensi;

namespace NeverfadePos.Api.Services.Attendance;

internal sealed record EffectiveSchedule(
    bool IsScheduled,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    string? ExceptionType);

internal static class AttendanceStatusRules
{
    public static EffectiveSchedule ResolveSchedule(
        EmployeeWeeklySchedule? weekly,
        EmployeeScheduleException? exception)
    {
        if (exception is not null)
        {
            if (exception.Type is "leave" or "holiday" or "off")
            {
                return new EffectiveSchedule(false, null, null, exception.Type);
            }

            if (exception.Type == "changed_shift")
            {
                return new EffectiveSchedule(true, exception.StartTime, exception.EndTime, exception.Type);
            }
        }

        if (weekly is null || !weekly.IsWorkingDay)
        {
            return new EffectiveSchedule(false, null, null, exception?.Type);
        }

        return new EffectiveSchedule(true, weekly.StartTime, weekly.EndTime, exception?.Type);
    }

    public static SharedAttendanceStateDto BuildState(
        DateOnly date,
        AbsensiEntity? attendance,
        EffectiveSchedule schedule,
        int graceMinutes,
        int absenceThresholdMinutes,
        DateTime nowWib)
    {
        var status = ResolveStatus(
            date,
            attendance,
            schedule,
            graceMinutes,
            absenceThresholdMinutes,
            nowWib);

        return new SharedAttendanceStateDto
        {
            Date = date.ToString("yyyy-MM-dd"),
            Status = status,
            CheckIn = attendance?.CheckIn?.ToString("HH:mm"),
            CheckOut = attendance?.CheckOut?.ToString("HH:mm"),
            ScheduleStart = schedule.StartTime?.ToString("HH:mm"),
            ScheduleEnd = schedule.EndTime?.ToString("HH:mm"),
            ExceptionType = schedule.ExceptionType,
            OutsideSchedule = attendance?.OutsideSchedule == true ||
                              (attendance?.CheckIn is not null && !schedule.IsScheduled),
            NextAction = attendance?.CheckIn is null
                ? "checkin"
                : attendance.CheckOut is null
                    ? "checkout"
                    : null
        };
    }

    public static string ResolveStatus(
        DateOnly date,
        AbsensiEntity? attendance,
        EffectiveSchedule schedule,
        int graceMinutes,
        int absenceThresholdMinutes,
        DateTime nowWib)
    {
        var currentDate = DateOnly.FromDateTime(nowWib);

        if (attendance?.CheckIn is not null)
        {
            if (attendance.CheckOut is null)
            {
                if (currentDate > date)
                {
                    return "missing_checkout";
                }

                if (schedule.IsScheduled &&
                    schedule.EndTime.HasValue &&
                    currentDate == date &&
                    TimeOnly.FromDateTime(nowWib) > schedule.EndTime.Value)
                {
                    return "missing_checkout";
                }

                return "working";
            }

            if (schedule.IsScheduled && schedule.StartTime.HasValue)
            {
                var lateAfter = schedule.StartTime.Value.AddMinutes(graceMinutes);
                if (attendance.CheckIn.Value > lateAfter)
                {
                    return "late";
                }
            }

            return "present";
        }

        if (!schedule.IsScheduled)
        {
            return "off";
        }

        if (currentDate < date)
        {
            return "scheduled";
        }

        if (currentDate > date)
        {
            return "absent";
        }

        if (!schedule.StartTime.HasValue)
        {
            return "scheduled";
        }

        var absentAfter = schedule.StartTime.Value.AddMinutes(absenceThresholdMinutes);
        return TimeOnly.FromDateTime(nowWib) >= absentAfter
            ? "absent"
            : "scheduled";
    }
}
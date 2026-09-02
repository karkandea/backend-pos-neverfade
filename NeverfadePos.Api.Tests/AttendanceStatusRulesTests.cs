using NeverfadePos.Api.Entities;
using NeverfadePos.Api.Services.Attendance;
using Xunit;

namespace NeverfadePos.Api.Tests;

public sealed class AttendanceStatusRulesTests
{
    private static readonly DateOnly Today = new(2026, 9, 2);
    private static readonly EffectiveSchedule StandardShift =
        new(true, new TimeOnly(9, 0), new TimeOnly(17, 0), null);

    [Fact]
    public void ResolveSchedule_OffExceptionOverridesWeeklyShift()
    {
        var weekly = new EmployeeWeeklySchedule
        {
            IsWorkingDay = true,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0)
        };
        var exception = new EmployeeScheduleException
        {
            Type = "leave"
        };

        var result = AttendanceStatusRules.ResolveSchedule(weekly, exception);

        Assert.False(result.IsScheduled);
        Assert.Null(result.StartTime);
        Assert.Null(result.EndTime);
        Assert.Equal("leave", result.ExceptionType);
    }

    [Fact]
    public void ResolveSchedule_ChangedShiftOverridesWeeklyHours()
    {
        var weekly = new EmployeeWeeklySchedule
        {
            IsWorkingDay = true,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(17, 0)
        };
        var exception = new EmployeeScheduleException
        {
            Type = "changed_shift",
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(20, 0)
        };

        var result = AttendanceStatusRules.ResolveSchedule(weekly, exception);

        Assert.True(result.IsScheduled);
        Assert.Equal(new TimeOnly(12, 0), result.StartTime);
        Assert.Equal(new TimeOnly(20, 0), result.EndTime);
        Assert.Equal("changed_shift", result.ExceptionType);
    }

    [Fact]
    public void ResolveStatus_ScheduledBeforeAbsenceThreshold()
    {
        var status = AttendanceStatusRules.ResolveStatus(
            Today,
            null,
            StandardShift,
            graceMinutes: 10,
            absenceThresholdMinutes: 120,
            new DateTime(2026, 9, 2, 10, 30, 0));

        Assert.Equal("scheduled", status);
    }

    [Fact]
    public void ResolveStatus_AbsentAfterThresholdWithoutAttendanceRow()
    {
        var status = AttendanceStatusRules.ResolveStatus(
            Today,
            null,
            StandardShift,
            graceMinutes: 10,
            absenceThresholdMinutes: 120,
            new DateTime(2026, 9, 2, 11, 0, 0));

        Assert.Equal("absent", status);
    }

    [Fact]
    public void ResolveStatus_LateAfterGracePeriod()
    {
        var attendance = new Absensi
        {
            Tanggal = Today,
            CheckIn = new TimeOnly(9, 11),
            CheckOut = new TimeOnly(17, 1)
        };

        var status = AttendanceStatusRules.ResolveStatus(
            Today,
            attendance,
            StandardShift,
            graceMinutes: 10,
            absenceThresholdMinutes: 120,
            new DateTime(2026, 9, 2, 17, 2, 0));

        Assert.Equal("late", status);
    }

    [Fact]
    public void ResolveStatus_PastOpenAttendanceIsMissingCheckoutRegardlessOfCurrentClock()
    {
        var yesterday = Today.AddDays(-1);
        var attendance = new Absensi
        {
            Tanggal = yesterday,
            CheckIn = new TimeOnly(9, 0)
        };

        var status = AttendanceStatusRules.ResolveStatus(
            yesterday,
            attendance,
            StandardShift,
            graceMinutes: 10,
            absenceThresholdMinutes: 120,
            new DateTime(2026, 9, 2, 8, 0, 0));

        Assert.Equal("missing_checkout", status);
    }

    [Fact]
    public void BuildState_OutsideSchedulePunchIsVisible()
    {
        var attendance = new Absensi
        {
            Tanggal = Today,
            CheckIn = new TimeOnly(8, 30),
            OutsideSchedule = true
        };
        var off = new EffectiveSchedule(false, null, null, "off");

        var state = AttendanceStatusRules.BuildState(
            Today,
            attendance,
            off,
            graceMinutes: 10,
            absenceThresholdMinutes: 120,
            new DateTime(2026, 9, 2, 8, 31, 0));

        Assert.Equal("working", state.Status);
        Assert.True(state.OutsideSchedule);
        Assert.Equal("checkout", state.NextAction);
    }
}